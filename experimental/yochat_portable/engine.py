from dataclasses import replace
import json
from pathlib import Path
import queue
import threading
import time

from .bridge import Bridge
from .core import Transcript, WakeGate, parse_hotkey, transcript_path


class Engine:
    def __init__(self, settings):
        self.lock = threading.Lock()
        self.settings = settings
        self.stopped = threading.Event()
        self.paused = False
        self.active = None
        self.observed_at = 0.0
        self.pill = None
        self.rows = []
        self.native_status = 'Connecting to ChatGPT…'
        self.speech_status = 'Starting local wake listener…'
        self.text_status = 'Waiting for transcript…'
        self.wakes = queue.Queue(maxsize=1)
        self.gate = WakeGate()
        self.reader = Transcript()
        self.bridge = Bridge()
        self.generation = 0
        self.threads = []

    def config(self):
        with self.lock:
            return replace(self.settings)

    def configure(self, settings):
        with self.lock:
            self.settings = settings
            self.generation += 1
            self.rows = []

    def state(self):
        with self.lock:
            return dict(pill=self.pill, rows=list(self.rows), observed_at=self.observed_at,
                        native=self.native_status, speech=self.speech_status, transcript=self.text_status)

    def listening_allowed(self):
        with self.lock:
            return (not self.paused and self.active is False and
                    time.monotonic() - self.observed_at < 1.5 and
                    time.monotonic() >= self.gate.pending_until)

    def start(self):
        for target in (self.native_loop, self.transcript_loop, self.speech_loop):
            thread = threading.Thread(target=target, daemon=True)
            self.threads.append(thread); thread.start()

    def close(self):
        self.stopped.set()
        for thread in self.threads:
            thread.join(timeout=3)

    def native_loop(self):
        try:
            while not self.stopped.is_set():
                try:
                    result = self.bridge.request()
                    with self.lock:
                        self.active = result.get('active') if not result.get('error') else None
                        self.pill = result.get('pill') if not result.get('error') else None
                        self.observed_at = time.monotonic()
                        self.native_status = result.get('error', 'Voice active' if self.active else 'Ready for voice')
                    try:
                        text, confidence, captured = self.wakes.get_nowait()
                    except queue.Empty:
                        text = None
                    if text is not None:
                        config = self.config()
                        with self.lock:
                            accepted = (not self.paused and time.monotonic() - captured < 1.5 and
                                        self.gate.accept(text, confidence, config.phrase, config.confidence,
                                                         config.cooldown, time.monotonic(), self.active, self.observed_at))
                            if accepted:
                                self.generation += 1
                                self.rows = []
                        if accepted:
                            response = self.bridge.request('hotkey', keys=parse_hotkey(config.hotkey))
                            if response.get('error'):
                                with self.lock:
                                    self.native_status = response['error']
                except Exception as error:
                    with self.lock:
                        self.active = None; self.pill = None
                        self.native_status = f'Native connection unavailable: {error}'
                    self.stopped.wait(1)
                self.stopped.wait(.05)
        finally:
            self.bridge.close()

    def transcript_loop(self):
        generation = -1
        hidden_since = None
        cleared = False
        while not self.stopped.is_set():
            config = self.config()
            with self.lock:
                current = self.generation
                active = self.active
            if generation != current:
                self.reader.reset(); generation = current
            if active is False:
                if hidden_since is None:
                    hidden_since = time.monotonic()
                if not cleared and time.monotonic() - hidden_since > 1.5:
                    self.reader.reset(); cleared = True
            elif active is True:
                hidden_since = None; cleared = False
            try:
                self.reader.update(transcript_path(config.transcript).read_text(encoding='utf-8-sig'))
                with self.lock:
                    if generation == self.generation:
                        self.rows = list(self.reader.rows)
                        self.text_status = 'Direct transcript connected' if self.rows else 'Waiting for a new speech segment'
            except FileNotFoundError:
                with self.lock:
                    self.rows = []
                    self.text_status = 'Transcript file missing; this ChatGPT build may not expose it'
            except (ValueError, OSError):
                with self.lock:
                    self.text_status = 'Waiting for a readable, complete transcript update'
            self.stopped.wait(.1)

    def speech_loop(self):
        model = None
        model_path = None
        while not self.stopped.is_set():
            try:
                import sounddevice as sd
                import vosk
                vosk.SetLogLevel(-1)
                config = self.config()
                requested = config.model or str(Path(__file__).resolve().parents[1] / 'models/vosk-model-small-en-us-0.15')
                if requested != model_path:
                    if not Path(requested).is_dir():
                        raise RuntimeError('Wake model missing. Run setup-model.py or select a model in Settings.')
                    model = vosk.Model(requested); model_path = requested
                if not self.listening_allowed():
                    with self.lock:
                        self.speech_status = 'Wake listening paused while voice is active or unavailable'
                    self.stopped.wait(.2)
                    continue
                audio = queue.Queue(maxsize=20)

                def callback(data, frames, timing, status):
                    if not self.listening_allowed():
                        return
                    try:
                        audio.put_nowait(bytes(data))
                    except queue.Full:
                        pass

                rate = int(sd.query_devices(kind='input')['default_samplerate'])
                recognizer = vosk.KaldiRecognizer(model, rate, json.dumps([config.phrase, '[unk]']))
                recognizer.SetWords(True)
                with sd.RawInputStream(samplerate=rate, blocksize=max(800, rate // 10), dtype='int16',
                                       channels=1, callback=callback):
                    with self.lock:
                        self.speech_status = f'Listening for “{config.phrase}”'
                    while not self.stopped.is_set() and self.listening_allowed() and self.config() == config:
                        try:
                            chunk = audio.get(timeout=.2)
                        except queue.Empty:
                            continue
                        if recognizer.AcceptWaveform(chunk):
                            result = json.loads(recognizer.Result())
                            words = result.get('result', [])
                            confidence = min((word.get('conf', 0) for word in words), default=0)
                            try:
                                self.wakes.put_nowait((result.get('text', ''), confidence, time.monotonic()))
                            except queue.Full:
                                pass
            except Exception as error:
                with self.lock:
                    self.speech_status = f'Wake listener unavailable: {error}'
                self.stopped.wait(2)

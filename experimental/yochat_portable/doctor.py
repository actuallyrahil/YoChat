"""Read-only diagnostics; never print transcript text or send a shortcut."""
import json
from pathlib import Path
import platform
import sys

from .bridge import Bridge
from .core import Settings, Transcript, data_dir, transcript_path


def main():
    report = {'platform': platform.system(), 'architecture': platform.machine(), 'experimental': True}
    try:
        config = Settings.load(data_dir() / 'settings.json')
        source = transcript_path(config.transcript)
        report['transcript_exists'] = source.is_file()
        if source.is_file():
            Transcript().update(source.read_text(encoding='utf-8-sig'))
            report['transcript_format_supported'] = True
        model = Path(config.model) if config.model else Path(__file__).resolve().parents[1] / 'models/vosk-model-small-en-us-0.15'
        report['wake_model_exists'] = (model / 'am/final.mdl').is_file()
        import sounddevice as sd
        report['input_device_available'] = sd.query_devices(kind='input')['max_input_channels'] > 0
    except Exception as error:
        report['setup_error'] = type(error).__name__
    bridge = Bridge()
    try:
        native = bridge.request()
        report['native_error'] = native.get('error')
        report['voice_active'] = native.get('active')
        report['pill_found'] = bool(native.get('pill'))
    except Exception as error:
        report['native_error'] = str(error)
    finally:
        bridge.close()
    print(json.dumps(report, indent=2))
    ready = all(report.get(key) for key in ('transcript_format_supported', 'wake_model_exists', 'input_device_available'))
    return 0 if ready and not report.get('native_error') and not report.get('setup_error') else 1


if __name__ == '__main__':
    sys.exit(main())

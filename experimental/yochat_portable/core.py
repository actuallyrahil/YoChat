"""Pure models: no microphone, native UI, or network access."""
import json
import math
import os
import sys
from dataclasses import asdict, dataclass
from pathlib import Path


def data_dir():
    if sys.platform == 'darwin':
        return Path.home() / 'Library/Application Support/YoChatExperimental'
    return Path(os.environ.get('XDG_CONFIG_HOME', Path.home() / '.config')) / 'yochat-experimental'


def transcript_path(configured=''):
    if configured.strip():
        return Path(os.path.expandvars(configured)).expanduser()
    return Path(os.environ.get('CODEX_HOME', Path.home() / '.codex')) / 'realtime-voice-continuity.json'


def parse_hotkey(value):
    aliases = {'control': 'ctrl', 'command': 'cmd', 'option': 'alt', 'return': 'enter'}
    parts = [aliases.get(p.strip().lower(), p.strip().lower()) for p in value.split('+')]
    modifiers = {'ctrl', 'alt', 'shift', 'cmd'}
    if len(parts) < 2 or len(set(parts)) != len(parts) or any(p not in modifiers for p in parts[:-1]):
        raise ValueError('Use modifiers and one key, such as Ctrl+Alt+Space.')
    if parts[-1] not in {'space', 'enter'} and not (len(parts[-1]) == 1 and parts[-1] in 'abcdefghijklmnopqrstuvwxyz0123456789'):
        raise ValueError('The final key must be Space, Enter, a letter, or a digit.')
    return parts


@dataclass
class Settings:
    phrase: str = 'yo chat'
    hotkey: str = 'Ctrl+Alt+Space'
    confidence: float = .78
    cooldown: float = 4.0
    gap: int = 28
    bubbles: bool = True
    transcript: str = ''
    model: str = ''

    def validate(self):
        for name in ('phrase', 'hotkey', 'transcript', 'model'):
            if not isinstance(getattr(self, name), str):
                raise ValueError(f'{name} must be text.')
        self.phrase = ' '.join(self.phrase.lower().split())
        if not self.phrase or len(self.phrase) > 100:
            raise ValueError('Choose a wake phrase of 1–100 characters.')
        parse_hotkey(self.hotkey)
        for name, low, high in [('confidence', .5, 1), ('cooldown', 1, 60), ('gap', 0, 300)]:
            value = getattr(self, name)
            if not isinstance(value, (int, float)) or not math.isfinite(value) or not low <= value <= high:
                raise ValueError(f'{name} must be between {low} and {high}.')
        if not isinstance(self.bubbles, bool):
            raise ValueError('bubbles must be true or false.')
        return self

    @classmethod
    def load(cls, path):
        if not path.exists():
            return cls()
        data = json.loads(path.read_text(encoding='utf-8'))
        if not isinstance(data, dict):
            raise ValueError('Settings must be a JSON object.')
        return cls(**{k: v for k, v in data.items() if k in cls.__dataclass_fields__}).validate()

    def save(self, path):
        self.validate()
        path.parent.mkdir(parents=True, exist_ok=True)
        temporary = path.with_suffix('.tmp')
        temporary.write_text(json.dumps(asdict(self), indent=2), encoding='utf-8')
        temporary.replace(path)


class Transcript:
    def __init__(self):
        self.reset()

    def reset(self):
        self.previous = None
        self.rows = []
        self.thread = None

    def update(self, content):
        data = json.loads(content)
        if not isinstance(data, dict) or data.get('version') != 1 or not isinstance(data.get('threads'), dict):
            raise ValueError('Unsupported transcript format.')
        next_threads = {}
        for key, thread in data['threads'].items():
            if not isinstance(thread, dict) or not isinstance(thread.get('items'), list):
                raise ValueError('Unsupported transcript items.')
            rows = []
            for item in thread['items']:
                if not isinstance(item, dict) or not isinstance(item.get('text'), str):
                    raise ValueError('Unsupported transcript entry.')
                role, text = item.get('role'), item['text']
                row = (role, text)
                if role in ('user', 'assistant') and text.strip() and (not rows or rows[-1] != row):
                    rows.append(row)
            next_threads[key] = rows[-5:]
        if self.previous is not None:
            changed = [key for key in next_threads if self.previous.get(key) != next_threads[key]]
            if changed:
                # Prefer the connected call when several threads change together.
                self.thread = self.thread if self.thread in changed else changed[-1]
                self.rows = next_threads[self.thread]
        self.previous = next_threads


class WakeGate:
    def __init__(self):
        self.last = -math.inf
        self.pending_until = 0

    def accept(self, text, confidence, phrase, threshold, cooldown, now, active, observed_at):
        if active is not False or now - observed_at > 1.5:
            return False
        if now < self.pending_until or now - self.last < cooldown:
            return False
        if ' '.join(text.lower().split()) != phrase.lower() or confidence < threshold:
            return False
        self.last = now
        self.pending_until = now + max(10, cooldown)
        return True


def placement(center_x, top, width, height, gap, screen):
    x, y, sw, sh = screen
    bottom = min(top - gap, y + sh)
    if bottom - height < y:
        return None
    return (round(max(x, min(center_x - width / 2, x + sw - width))), round(bottom - height))

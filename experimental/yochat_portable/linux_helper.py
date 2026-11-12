"""Isolated AT-SPI reader and X11 shortcut sender; stdout is JSON protocol only."""
import json
import os
import sys


def snapshot():
    if os.environ.get('XDG_SESSION_TYPE') == 'wayland' or os.environ.get('WAYLAND_DISPLAY'):
        return {'error': 'Experimental Linux support requires an X11 login session, not Wayland.'}
    if not os.environ.get('DISPLAY'):
        return {'error': 'No X11 display. Launch from your desktop session.'}
    import pyatspi as a
    desktop = a.Registry.getDesktop(0)
    active = False
    recognized = False
    found = False
    incomplete = False
    pill = None
    for app in desktop:
        if (app.name or '').lower() not in ('chatgpt', 'codex'):
            continue
        found = True
        for window in app:
            queue = [window]
            index = 0
            mute = False
            stop = None
            obstacles = []
            while index < len(queue) and index < 2500:
                node = queue[index]; index += 1
                name = node.name or ''
                if name in ('Start voice chat', 'Start new voice chat', 'Mute microphone', 'Mute voice chat', 'Unmute voice chat'):
                    recognized = True
                if name in ('Mute voice chat', 'Unmute voice chat'):
                    active = True
                    mute = True
                if name == 'Stop voice chat':
                    active = recognized = True
                if node.getState().contains(a.STATE_SHOWING):
                    try:
                        r = node.queryComponent().getExtents(a.DESKTOP_COORDS)
                        x, y, w, h = r.x, r.y, r.width, r.height
                        if w > 0 and h > 0 and name == 'Stop voice chat':
                            stop = (x + w / 2, y)
                        if 80 <= w <= 650 and 28 <= h <= 240:
                            obstacles.append((x, y, w, h))
                    except NotImplementedError:
                        pass
                queue.extend(list(node))
            if index < len(queue):
                incomplete = True
            if mute and stop and window.getState().contains(a.STATE_SHOWING):
                cx, top = stop
                for x, y, w, h in obstacles:
                    if top - 240 < y + h <= stop[1] and x < cx + 160 and x + w > cx - 160:
                        top = min(top, y)
                pill = {'x': cx, 'top': top}
    if not found:
        return {'error': 'ChatGPT is not in the accessibility tree. Start it with accessibility enabled.'}
    if not active and (not recognized or incomplete):
        return {'error': 'Voice controls unavailable. Check English UI and AT-SPI access.'}
    return {'active': active, 'pill': pill}


def hotkey(keys):
    from .core import parse_hotkey
    keys = parse_hotkey('+'.join(keys))
    state = snapshot()
    if state.get('error') or state.get('active') is not False:
        return {'error': 'Voice is active or its state is unavailable; shortcut was not sent.'}
    from pynput.keyboard import Controller, Key
    mapping = {'ctrl': Key.ctrl, 'alt': Key.alt, 'shift': Key.shift, 'cmd': Key.cmd, 'space': Key.space, 'enter': Key.enter}
    controller = Controller()
    pressed = []
    try:
        for key in keys:
            key = mapping.get(key, key)
            controller.press(key); pressed.append(key)
    finally:
        for key in reversed(pressed):
            controller.release(key)
    return {'ok': True}


def main():
    for line in sys.stdin:
        try:
            request = json.loads(line)
            result = hotkey(request['keys']) if request.get('command') == 'hotkey' else snapshot()
        except Exception as error:
            # No transcript text is ever read by this helper.
            result = {'error': f'Accessibility unavailable ({type(error).__name__}). Check the platform setup guide.'}
        print(json.dumps(result), flush=True)


if __name__ == '__main__':
    main()

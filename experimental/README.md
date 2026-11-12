# YoChat for macOS and Linux — Experimental

This is a separate port of YoChat, with a Qt menu-bar/tray app, configurable
local wake recognition, live transcript bubbles, and native pill tracking.
The existing Windows app, installer and release workflow are unchanged.

**Experimental means the native voice flow still needs testing on real machines.**
A successful build or preview does not prove that your ChatGPT build exposes
the required transcript and accessibility controls. No API key is needed.

## Requirements

| Platform | Initial target | Native connection |
| --- | --- | --- |
| macOS | Apple Silicon or Intel with Python 3.11–3.13 and Apple Command Line Tools | Swift helper using Apple Accessibility and native key events |
| Linux | Ubuntu 24.04/Debian 13, X11 desktop session, distro Python 3.11–3.13 | AT-SPI for controls, X11 for the configured shortcut |
| Wayland | Not supported in this experimental release | The launcher stops with an explanation |

Keep a compatible ChatGPT desktop app running and signed in. Set a voice
shortcut in ChatGPT and match it in YoChat Settings; the initial value is
`Ctrl+Alt+Space` on both platforms, not an assertion about ChatGPT's default.
Check that the shortcut opens the floating pill without restoring a full chat.

The internal file `~/.codex/realtime-voice-continuity.json` must update during
voice. `CODEX_HOME` and a custom transcript path are supported. This source has
only been confirmed in the Windows setup used for development; its availability
on your Mac/Linux build is a **feasibility check**, not a guaranteed feature.
If unavailable, YoChat reports it rather than opening chat windows or adding
another transcription service. Completed segments update; word-by-word captions
are not guaranteed. Exact same-speaker adjacent duplicates are shown once.

The pill must expose English accessibility labels such as `Mute voice chat`
and `Stop voice chat`. An inaccessible or unknown call state blocks activation
so YoChat does not accidentally toggle off a call.

## Install on Mac

Install Python 3.11–3.13 from [python.org](https://www.python.org/downloads/macos/)
or your package manager. Install Apple's compiler tools once:

```sh
xcode-select --install
```

Clone the repository, or extract the experimental source bundle:

```sh
git clone https://github.com/actuallyrahil/YoChat.git
cd YoChat/experimental
bash install-macos.sh
bash run.sh
```

If `python3` is a different version, select an installed interpreter:

```sh
YOCHAT_PYTHON=python3.12 bash install-macos.sh
```

The installer creates a local `.venv`, installs the pinned Python dependencies,
compiles `native/MacBridge`, downloads the English wake model, and runs the tests.
It does not modify your existing Windows setup or launch a voice call.

Allow **Microphone** for the terminal/Python running YoChat. In **System Settings
→ Privacy & Security → Accessibility**, allow the terminal and/or `MacBridge`
as required by macOS. Restart YoChat after changing permissions. The helper
is locally compiled and unsigned; this release is a source installation, not
a notarized `.app` or drag-and-drop installer. No screen recording is used.

## Install on Linux

Use an **X11 login session**. XWayland inside a Wayland session is not enough
for this initial port. On Ubuntu/Debian install the system prerequisites:

```sh
sudo apt install git python3-venv python3-pyatspi libportaudio2 libxcb-cursor0 libxkbcommon-x11-0 libegl1 libopengl0 libgl1
git clone https://github.com/actuallyrahil/YoChat.git
cd YoChat/experimental
bash install-linux.sh
bash run.sh
```

The installer uses `/usr/bin/python3` and a local virtual environment with
system packages visible, because AT-SPI bindings come from your distribution.
It does not use an unrelated `pyatspi` package from PyPI. If you choose another
interpreter using `YOCHAT_PYTHON`, it must be able to import the distro bindings.

ChatGPT must expose its accessibility tree. Enable desktop accessibility if
needed; an Electron build may require launching ChatGPT yourself with
`--force-renderer-accessibility`. YoChat never relaunches or navigates ChatGPT.
GNOME may need a tray/AppIndicator extension. The launcher disables Qt's extra
scale conversion to match X11 coordinates; mixed-DPI desktops need live testing.

## Use and configure

Run `bash run.sh`, then use the blue **Y** tray/menu-bar icon:

- **Settings:** wake phrase, hotkey, confidence, cooldown, bubble spacing,
  bubble visibility, transcript file and Vosk model directory.
- **Pause listening:** releases the wake microphone stream; the active
  conversation can continue updating its bubbles.
- **Connection status:** separate native-control, wake-listener and transcript
  status messages. Nothing in this dialog includes transcript contents.
- **Exit YoChat:** stops the workers and closes the overlay.

Say **“Yo Chat”** while another application is in front. The listener releases
its microphone stream during a detected call and suppresses additional wake
commands. Bubbles follow the accessible stop control in the pill, leave space
above nearby prompt controls, and hide when the pill disappears. They also hide
when native position information becomes stale. Oversized replies can be opened
by clicking **Read full message…**.

On the first run, validate the configured native hotkey and permissions before
expecting wake activation. The experimental app deliberately refuses to send
shortcuts if it cannot determine ChatGPT's voice state.

Settings are separate from Windows:

- macOS: `~/Library/Application Support/YoChatExperimental/settings.json`
- Linux: `${XDG_CONFIG_HOME:-~/.config}/yochat-experimental/settings.json`

No login item or autostart entry is installed. Start manually until the flow is
verified on your machine. To update, exit YoChat, pull changes, and rerun the
installer. To uninstall, exit and delete this experimental directory; optionally
remove its settings folder. Do not delete ChatGPT's continuity file.

## Preview and diagnostics

```sh
bash run.sh --preview     # Sample bubbles; no microphone or ChatGPT access
bash doctor.sh            # Read-only checks, no transcript text or shortcut
```

The diagnostic checks source format, model presence, default input device, and
native access. It does not open the microphone or establish that speech works.
With voice active, `pill_found` should be true when the floating pill is visible.

If setup stops:

| Symptom | Next check |
| --- | --- |
| No transcript file | Confirm your ChatGPT build writes the supported source; choosing an arbitrary file cannot enable the integration |
| Native access unavailable | Grant Mac Accessibility access, or check AT-SPI/English control names on Linux |
| Microphone unavailable | Grant permissions, check default input, then restart YoChat |
| No wake response | Check tray status, exact phrase, hotkey, model directory and recognition confidence |
| No Linux tray icon | Enable system tray/AppIndicator support in your desktop |
| Unsupported Wayland | Log into X11; compositor-specific Wayland integration is not implemented |

Do not include private transcripts in bug reports. The doctor output can be
shared after reviewing it; it contains status only, not file contents.

## First live test checklist

1. With voice closed, run the doctor and confirm native access is available.
2. From another app, say the wake phrase. Confirm only the pill appears.
3. Speak and wait for the reply. Confirm both sides appear once.
4. Move the pill horizontally and vertically; check spacing and multiple monitors.
5. Close voice; confirm bubbles hide. Repeat for a second call.
6. Switch chats in the main app; confirm the voice transcript stays connected.
7. Check mute, pause, missing permissions, and recovery after ChatGPT restarts.

## Development and validation

```sh
.venv/bin/python -m unittest discover -s tests -v
QT_QPA_PLATFORM=offscreen .venv/bin/python -m yochat_portable.ui --smoke-test
```

The separate **Experimental Mac and Linux** GitHub workflow runs model tests,
dependency imports and UI smoke checks on both systems, and compiles the Swift
helper on Mac. Its artifacts are **source bundles requiring installation**,
not signed native installers. Hosted runners do not have your ChatGPT account,
microphone, or accessibility permissions, so they cannot validate live calls.

## Privacy and third-party components

Wake recognition runs locally through [Vosk](https://alphacephei.com/vosk/),
using [sounddevice](https://python-sounddevice.readthedocs.io/) for microphone
input. The UI uses [Qt for Python/PySide6](https://doc.qt.io/qtforpython-6/).
The app does not upload audio, save recordings, or write transcript logs.
Dependency/model downloads happen only during installation. ChatGPT's own
service and privacy behavior are separate.

YoChat source is MIT; dependency licenses remain their own. Qt/PySide6 includes
LGPL/GPL/commercial licensing terms; Vosk and its model have their own licenses.
Source installs use unmodified external packages. Preserve the dependencies'
license notices and applicable obligations if distributing bundled binaries.
The downloaded model is `vosk-model-small-en-us-0.15`, from the
[publisher's model list](https://alphacephei.com/vosk/models).

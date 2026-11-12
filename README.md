# YoChat — ChatGPT voice overlay for Windows, macOS and Linux

**Windows: working version · macOS and Linux: experimental source ports**

**Say “Yo Chat.” Talk to ChatGPT. Read the conversation without leaving your app.**

YoChat is an open-source tray/menu-bar app that adds a configurable **wake word**
and **floating live transcript bubbles** to compatible ChatGPT desktop voice
sessions. Your messages and ChatGPT’s replies appear above the native voice
pill, follow it as you move it, and disappear when you close voice.

Keep your editor, browser, or document in front. No separate transcript window
needs to stay on your screen.

[Windows setup](#windows) · [Mac setup](#macos-experimental) · [Linux setup](#linux-experimental) ·
[Compatibility](#compatibility) ·
[Settings](#settings) · [Troubleshooting](#troubleshooting) · [MIT license](LICENSE)

<p align="center">
  <img src="docs/images/conversation-preview.png" width="320" alt="YoChat floating transcript preview: blue user messages and dark ChatGPT replies in rounded, left-aligned chat bubbles">
</p>
<p align="center"><em>Windows bubble renderer with sample messages. This is a layout preview, not a recording of a live call; the native ChatGPT pill is not shown.</em></p>

> **Compatibility matters:** live bubbles currently depend on an internal local
> voice-transcript file exposed by the desktop build used during development.
> They do **not** work with every ChatGPT installation. Check
> [compatibility](#compatibility) before installing.

## What you get

- **Hands-free voice activation.** “Yo Chat” is the default wake phrase; choose
  your own phrase, recognition confidence, and cooldown.
- **Both sides of the conversation.** Read your speech and ChatGPT’s responses
  in a compact, always-on-top chat overlay. Updates arrive at completed speech
  segments, rather than guaranteed word-by-word captions.
- **Bubbles that stay with the pill.** Move the native voice control and the text
  follows. It leaves room for prompt cards when their bounds are available.
- **A small desktop footprint.** Up to five complete messages, with a full-message
  reader for oversized replies. Closing voice hides the bubbles.
- **Local wake recognition.** Windows uses Windows Speech; the experimental
  Mac/Linux versions use Vosk. No API key or separate cloud transcription service.
- **Tray/menu-bar controls.** Pause listening, change settings, check the
  transcript connection, or exit. Windows also offers optional start on login.

Useful when you want a ChatGPT voice assistant beside your work, a readable
voice-chat overlay, or a wake-word launcher without keeping the full chat open.

## Platform status

| Platform | Status | Install | Live voice verification |
| --- | --- | --- | --- |
| Windows x64 | Working version; unchanged by the ports | Portable build or .NET source installer | Direct-reader flow and duplicate fix confirmed; see [verification notes](VERIFICATION.md) |
| macOS | **Experimental** | Python source install; Swift helper compiled locally | Native helper build, model tests and UI checks passed; live calls pending |
| Linux X11 | **Experimental** | Python source install; Ubuntu/Debian prerequisites | Model tests, dependency imports and UI checks passed; live calls pending |
| Linux Wayland | Not supported | — | No compositor integration implemented |

Mac and Linux are separate implementations under [`experimental/`](experimental/).
Passing automated checks does not prove that your ChatGPT build exposes the
needed controls or transcript. See [experimental verification](docs/experimental-verification.md)
and the [live test checklist](experimental/README.md#first-live-test-checklist).

## Compatibility

| Requirement | Current support |
| --- | --- |
| Operating system | Choose a version from the platform table above. Windows ARM64 is untested. |
| ChatGPT | A signed-in desktop build with the native floating voice pill and the transcript source below |
| ChatGPT UI language | English control names are used to locate the pill |
| Microphone | OS microphone permission; Windows Speech on Windows, a local Vosk model on Mac/Linux |
| Browser version | Not supported |
| API key | Not required |

The Windows desktop build tested during development exposes local Work/voice
conversations and updates this internal source:

| Platform | Default transcript path |
| --- | --- |
| Windows | `%USERPROFILE%\.codex\realtime-voice-continuity.json` |
| macOS / Linux | `~/.codex/realtime-voice-continuity.json` — availability still needs checking on your build |

**This file is an internal format, not a stable public API.** A normal ChatGPT
installation may not produce it. During an active voice call, the file must
exist and update for live bubbles to work. YoChat reports a missing source in
its tray status; it does not open a full chat window to work around it.

Adjacent entries with exactly the same speaker and text are shown once, because
the source can publish duplicate segments. Without message IDs, identical
back-to-back utterances from one speaker are indistinguishable; repeats across
another speaker's turn and different wording are preserved.

YoChat respects `CODEX_HOME` if set. You can also choose a transcript file in
Settings. The supported format has `version: 1` and per-thread `items` containing
`role` and `text`. Future desktop updates may change this integration.

The pill must expose the English accessibility controls `Mute voice chat` and
`Stop voice chat`. The native voice shortcut must already work while ChatGPT is
running. YoChat does not install ChatGPT or enable account features.

## Getting started

### Before installing

Sign into the compatible ChatGPT desktop app and enable its voice shortcut.
The default used by YoChat is **Ctrl+Alt+Space**. Try that shortcut from another
app: it should open the floating voice pill. Keep ChatGPT running; its full
windows can stay minimized.

Allow microphone access in your OS settings. Each version uses your default
input device. Mac also needs Accessibility permission; Linux needs AT-SPI access.

### Windows

**From source:** install [Git](https://git-scm.com/downloads/win) and the
[.NET 10 SDK](https://learn.microsoft.com/dotnet/core/install/windows), then run:

```powershell
# Install the SDK once, then open a new PowerShell terminal.
winget install --exact --id Microsoft.DotNet.SDK.10

# Download, build, install, and start YoChat.
git clone https://github.com/actuallyrahil/YoChat.git
cd YoChat
.\install.ps1
```

The installer runs the tests, builds the app with .NET included, installs it
under `%LOCALAPPDATA%\Programs\YoChat`, creates a Start Menu shortcut, and starts
listening. Building requires internet access to restore dependencies. YoChat
itself does not require administrator access.

**Portable build:** when a release is available, download `YoChat-win-x64.zip`
from [Releases](https://github.com/actuallyrahil/YoChat/releases), extract the
**whole folder**, and run `YoChat.exe`. No separate .NET installation is needed.
If no release is listed yet, use the source instructions above.

If you used OkayCodex or an earlier bubble prototype, exit those utilities first
so only one wake listener is running.

### macOS (experimental)

Requires Python 3.11–3.13 and Apple Command Line Tools. Read the
[Mac prerequisites and permissions](experimental/README.md#install-on-mac) first.

```sh
xcode-select --install  # Once; finish installation before continuing.
git clone https://github.com/actuallyrahil/YoChat.git
cd YoChat/experimental
bash install-macos.sh
bash run.sh
```

The installer creates a local Python environment, compiles the Swift helper,
and downloads the English wake model. This is a source install, not a signed
or notarized `.app`. Grant the microphone and Accessibility permissions described
in the guide. Use the menu-bar icon for settings and connection status.

### Linux (experimental)

Initially targets Ubuntu 24.04/Debian 13 with distro Python 3.11–3.13 and an
**X11 login session**. Follow the [Linux setup guide](experimental/README.md#install-on-linux)
for accessibility and tray support.

```sh
sudo apt install git python3-venv python3-pyatspi libportaudio2 libxcb-cursor0 libxkbcommon-x11-0 libegl1 libopengl0 libgl1
git clone https://github.com/actuallyrahil/YoChat.git
cd YoChat/experimental
bash install-linux.sh
bash run.sh
```

The installer creates a local Python environment with access to your distro's
AT-SPI bindings and downloads the English wake model. Native Wayland and XWayland
inside a Wayland session are not supported by this initial port.

For either experimental version, from `YoChat/experimental`:

```sh
bash run.sh --preview   # Sample bubbles; no microphone or ChatGPT access.
bash doctor.sh          # Read-only checks; no transcript text in the report.
```

### Start a conversation

With another app in front, say **“Yo Chat”**, then speak normally. Bubbles appear
when the next transcript segment arrives. Drag the pill to reposition the
conversation; close voice when you are finished.

Use YoChat’s tray/menu-bar icon to open **Settings**, **Pause listening**, check
connection status, or **Exit**. Pausing the wake listener does not hide an
existing call’s bubbles.

### Windows only: start on login

Exit YoChat before running the installer again:

```powershell
.\install.ps1 -StartOnLogin
```

Other installer options: `-NoStart`, `-NoShortcuts` (skip the Start Menu shortcut),
and `-InstallDirectory <path>`. Mac/Linux currently start manually; no login item
or autostart entry is installed.

## Settings

<p align="center">
  <img src="docs/images/settings.png" width="560" alt="YoChat Settings showing wake phrase, voice hotkey, recognition confidence, cooldown, live bubble toggle, and optional transcript path">
</p>
<p align="center"><em>Windows settings form, rendered with default values. The experimental ports have their own settings dialog.</em></p>

| Setting | Default | What it controls |
| --- | --- | --- |
| Wake phrase | Yo Chat | The phrase that starts voice |
| Voice hotkey | Ctrl+Alt+Space | Must match ChatGPT’s configured shortcut |
| Recognition confidence | 0.78 | How certain recognition must be before activation |
| Wake cooldown | 4 seconds | Minimum interval between accepted activations |
| Live bubbles | Enabled | Show or hide the transcript overlay |
| Transcript file | Automatic | Override the source path if needed |

Changes apply when saved. Settings are kept separately for each version:

| Version | Settings location |
| --- | --- |
| Windows | `%LOCALAPPDATA%\YoChat\settings.json` |
| macOS | `~/Library/Application Support/YoChatExperimental/settings.json` |
| Linux | `${XDG_CONFIG_HOME:-~/.config}/yochat-experimental/settings.json` |

The experimental versions also expose bubble spacing and a Vosk model folder.
Windows suppresses wake activation based on microphone capture state; Mac/Linux
use accessible voice-control state and pause their wake microphone during a
detected call. Unknown or stale state blocks activation.

## Privacy

Wake recognition runs locally through Windows Speech or Vosk. YoChat does not
save or upload microphone audio, write transcript logs, or make runtime network
requests. It reads the existing transcript file without modifying it and keeps
displayed text in memory. The experimental installers download dependencies and
a speech model once; the running app does not download them. Settings and the
installed model are stored locally, not voice recordings.

**ChatGPT’s voice service still has its own network use, privacy settings, and
local storage.** Local wake recognition does not make ChatGPT voice offline.

## Troubleshooting

| Symptom | What to check |
| --- | --- |
| “Yo Chat” does nothing | Check tray status, listening is enabled, microphone permissions, default input device, and Windows Speech language or Vosk model. Try slightly lowering confidence. |
| Wake detected, but no pill | Test the hotkey yourself. It must match ChatGPT’s shortcut, and ChatGPT must already be running. |
| Pill appears, but no messages | Check tray connection status and whether the continuity file updates during voice. Speak a new sentence; old messages are deliberately suppressed at startup. |
| Transcript updates, but bubbles stay hidden | Confirm English ChatGPT UI and supported pill controls. Run both apps at the same privilege level. |
| A full ChatGPT window appears | Test the native shortcut with YoChat exited. YoChat does not open or focus full chat windows. |
| Mac/Linux native access fails | Run `bash doctor.sh`; check Mac Accessibility permission or Linux AT-SPI, English control labels, and X11 session. |
| PowerShell blocks the Windows installer | Follow your device’s script-execution policy, or use a portable release. On managed devices, ask your administrator. |

For a bug report, include your OS and ChatGPT versions, the YoChat connection status,
and steps to reproduce. **Do not attach your continuity file or private
transcript.** Redact messages and personal details from screenshots.

## How it works

1. A local speech recognizer listens for the configured wake phrase.
2. YoChat checks voice state, clears the previous call's bubbles, and sends
   ChatGPT's configured voice shortcut.
3. The direct reader watches the continuity file for new speech segments.
4. Native accessibility APIs locate the pill and nearby prompts; the overlay
   draws the conversation above them.

| Component | Windows | Experimental Mac / Linux |
| --- | --- | --- |
| UI | Windows Forms + WPF | Python + Qt/PySide6 |
| Wake recognition | System.Speech | Vosk + sounddevice |
| Pill access | Windows UI Automation | Apple Accessibility via Swift / AT-SPI |
| Shortcut | Windows key events | Native Mac key events / X11 |

Geometry tracking, transcript reading, and rendering operate independently.
Mac/Linux isolate native accessibility in a helper process with timeouts.
YoChat does not navigate chats, create transcript windows, or automate sending
chat messages.

## Development and contributions

**Windows development** uses C#, .NET 10, Windows Forms, WPF, and System.Speech:

```powershell
.\scripts\test.ps1
dotnet run --project .\src\YoChat\YoChat.csproj
.\scripts\publish.ps1
```

The portable output is `artifacts/YoChat-win-x64/`. Run `YoChat.exe --self-test`
from that folder for packaged UI checks without accessing the microphone.
See [verification notes](VERIFICATION.md) for what was tested and what still
needs a normal desktop voice-session check.

The Windows GitHub Actions workflow tests and builds pushes and pull requests.
Maintainers can push a `v*` tag to build and publish a portable release.

**Experimental development** uses the separate Python environment:

```sh
cd experimental
.venv/bin/python -m unittest discover -s tests -v
QT_QPA_PLATFORM=offscreen .venv/bin/python -m yochat_portable.ui --smoke-test
```

The **Experimental Mac and Linux** workflow runs platform checks and creates
source artifacts. These are not signed installers or verified live-call releases.
See the [experimental guide](experimental/README.md) for native setup and testing.

Issues and pull requests are welcome, especially reproducible compatibility
reports. Keep generated builds, personal settings, and transcript data out of
commits. Images in this README use sample text rendered by the app, not private
voice recordings.

## Uninstall

**Windows:** exit YoChat, delete its installation folder and Start Menu shortcut, and remove
`YoChat.lnk` from `shell:startup` if enabled. Optionally delete
`%LOCALAPPDATA%\YoChat` to remove settings.

**Mac/Linux:** exit YoChat, delete the experimental installation directory, and
optionally remove its settings folder listed above. No autostart entry is
installed. On every platform, leave ChatGPT’s files alone.

## License and attribution

[MIT](LICENSE). Built from the author’s OkayCodex wake utility and the YoChat
direct-transcript bubble prototype, with separate experimental Mac/Linux ports.
See [NOTICE.md](NOTICE.md). Dependencies retain their own licenses, including
[Qt, Vosk and model licensing](experimental/README.md#privacy-and-third-party-components)
for the experimental versions.

YoChat is an independent, experimental companion. It is not affiliated with or
endorsed by OpenAI.

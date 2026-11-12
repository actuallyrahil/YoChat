# YoChat

Say **“Yo Chat”** to start ChatGPT voice and see floating conversation bubbles
above its voice pill. Keep working in your current app: YoChat does not open,
select, restore or focus a full ChatGPT chat window.

Windows • local wake recognition • direct transcript • one tray app • MIT

## Install and run

### Portable download

Download `YoChat-win-x64.zip` from this repository's Releases, extract the whole
folder, and run **YoChat.exe**. The portable build includes .NET. It starts
listening and enables bubbles immediately. Right-click its tray icon for
**Settings**, **Pause listening**, connection status and **Exit**.

Releases become available when the repository owner pushes a `v*` tag; this
source package does not claim that a GitHub release already exists.

### Build and install from source

On Windows, install the [.NET 10 SDK](https://learn.microsoft.com/dotnet/core/install/windows) once:

```powershell
winget install --exact --id Microsoft.DotNet.SDK.10
```

Open a new PowerShell terminal in this repository's root, then:

```powershell
.\install.ps1
```

This runs the tests, builds a self-contained Windows x64 application, installs
it under `%LOCALAPPDATA%\Programs\YoChat`, creates a Start Menu shortcut, and
starts it. No administrator access is required for YoChat itself. Building
requires internet access to restore official .NET/NuGet dependencies.

To start YoChat automatically after signing in:

```powershell
.\install.ps1 -StartOnLogin
```

Use `-NoStart` to install without launching, `-NoShortcuts` to skip the Start Menu shortcut, or `-InstallDirectory <path>` to
choose another folder. Exit an existing YoChat tray instance before upgrading.
If migrating from OkayCodex/the prototype, exit its tray utility and bubble
reader first so two listeners do not send the same shortcut.

## One-time ChatGPT and Windows setup

1. Install and sign into a compatible ChatGPT Windows desktop app. Enable its
   voice shortcut and set it to **Ctrl+Alt+Space**, or enter your chosen shortcut
   in YoChat Settings. Confirm the shortcut opens the floating voice pill.
2. Allow microphone access for desktop apps in Windows Settings. Install a
   Windows Speech recognizer for your language if Windows Speech is unavailable.
   YoChat uses the default Windows microphone and recognizer.
3. Keep ChatGPT running; its full windows can remain minimized. Say **Yo Chat**,
   then speak normally. The bubbles appear after the first new transcript segment.

YoChat does not install ChatGPT, sign in for you, change microphone permissions,
or change ChatGPT's shortcut. Those remain user-controlled prerequisites.

## Compatibility — read this before installing

The direct transcript integration was verified with a ChatGPT desktop build
that exposes local Work/voice conversations and writes:

```text
%USERPROFILE%\.codex\realtime-voice-continuity.json
```

This is an **internal app format, not a public stable API**. Not every ChatGPT
desktop build writes this file; browser-only ChatGPT is not supported. Starting
a voice call must create/update that file for bubbles to work. A generic
ChatGPT installation is not sufficient if it lacks this source. The tray menu
reports a missing source rather than opening a chat window as a workaround.

YoChat respects `CODEX_HOME` when set. You can override the transcript file in
Settings. The current format must have `version: 1` and per-thread `items` with
`role` and `text`. Updates appear at **completed speech segments**, not guaranteed
word-by-word streaming. No API key or second transcription service is needed.

The Windows accessibility names used for the floating pill are currently English
(`Mute voice chat`, `Stop voice chat`). Other ChatGPT UI languages may prevent
pill detection. Windows x64 is the packaged target; ARM64 is not tested.

## Settings and behavior

| Setting | Default |
| --- | --- |
| Wake phrase | Yo Chat |
| Voice hotkey | Ctrl+Alt+Space |
| Recognition confidence | 0.78 |
| Wake cooldown | 4 seconds |
| Live bubbles | Enabled |
| Transcript file | Automatic |

Settings are saved in `%LOCALAPPDATA%\YoChat\settings.json`. Changes take effect
when saved. Wake listening starts with the application; Pause listening affects
the wake detector, while the bubbles can continue showing an existing call.

- Up to five complete messages, with blue user bubbles and dark assistant bubbles.
- Left-aligned text, balanced padding and rounded corners.
- Oversized messages open a full-message reader when you choose it.
- Bubbles follow the pill and move above prompt cards when ChatGPT exposes their bounds.
- Closing the pill hides the bubbles. Each accepted wake resets the old transcript.
- Pill windows are discovered dynamically, including after ChatGPT restarts; no
  machine-specific process IDs, window IDs, or installation paths are shipped.
- A wake is ignored while ChatGPT is capturing microphone audio, or when Windows
  cannot determine microphone state, to avoid toggling an existing call off.

## Privacy

Wake recognition runs locally through Windows Speech. YoChat does not save or
upload microphone audio and makes no runtime network requests. It reads
ChatGPT's existing continuity file without modifying it. Transcript text stays
in memory; YoChat does not write transcript logs. ChatGPT independently owns its
voice service, privacy settings and source file. Only the user-selected settings
are persisted by YoChat.

## Development

```powershell
.\scripts\test.ps1
dotnet run --project .\src\YoChat\YoChat.csproj
.\scripts\publish.ps1
```

The publish output is `artifacts/YoChat-win-x64/`. The app combines the local
wake detector and WPF bubble renderer in one process. UI Automation reads only
the existing pill's controls and geometry; it never drives ChatGPT navigation.
The renderer, geometry sampler and direct-transcript reader run independently.

GitHub Actions tests and builds on Windows. Push a tag such as `v0.1.0` to create
a portable release automatically. Source control should contain the repository
files only, not `bin`, `obj`, `artifacts`, personal settings or transcript data.

## Troubleshooting

- **No wake detection:** check the tray status, Windows microphone permissions,
  default input device and installed Speech language. Try lowering confidence
  slightly in Settings. Make sure listening is not paused.
- **Wake detected, no pill:** press the configured shortcut yourself. It must
  match ChatGPT's voice shortcut, and ChatGPT must already be running.
- **Pill but no text:** check the bubble status in the tray. Verify that the
  continuity file updates during voice, use a compatible build, and speak a new
  sentence after starting YoChat. Old messages are deliberately not flashed at startup.
- **Text source exists, no bubbles:** check the ChatGPT UI language and that its
  pill exposes the supported accessibility controls. Avoid running either app
  as administrator while the other runs normally.
- **ChatGPT opens a full window:** confirm its native shortcut behavior with
  YoChat exited. YoChat has no code path that opens or focuses a chat window.

## Uninstall

Exit YoChat from the tray, delete its installed folder and Start Menu shortcut,
and remove `YoChat.lnk` from `shell:startup` if enabled. Optionally delete
`%LOCALAPPDATA%\YoChat` to remove its settings. ChatGPT files are never removed.

## License

[MIT](LICENSE). Built from the author's OkayCodex wake utility and the YoChat
direct-transcript bubble prototype; see [NOTICE.md](NOTICE.md). Independent of
OpenAI. This is an experimental companion, not an official ChatGPT integration.

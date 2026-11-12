# Experimental platform verification

12 November 2026. This covers the separate `experimental/` source port, not a
claim that native ChatGPT voice has been verified on Mac or Linux.

## Passed

- [GitHub platform checks](https://github.com/actuallyrahil/YoChat/actions/runs/43136563888)
  on macOS 15 and Ubuntu 24.04: ten model tests on each platform, Python syntax,
  shell syntax, pinned dependency imports, and the offline Qt UI smoke check.
- The Swift Accessibility helper compiled successfully on macOS.
- Ubuntu's system Python imported the distro AT-SPI bindings successfully.
- The new bubble and settings widgets rendered locally using synthetic text.
- The model installer downloaded and extracted the publisher's English model
  into an isolated local folder. Vosk loaded it, found both default wake words
  in its vocabulary, and processed synthetic silence without microphone access.
- A source diff confirms no changes to Windows application code, tests,
  installer, publish scripts, or Windows build workflow. The existing Windows
  workflow also passed after the experimental port was added.

## Still requires real-machine checks

- ChatGPT actually supplies the supported continuity file on the target build.
- Accessibility labels, coordinates, prompt clearance, hidden/minimized windows,
  permission recovery, and multiple monitors behave as expected.
- Real microphone wake detection, pause/resume, native shortcut activation,
  focus preservation, and repeated calls work together.
- Intel Mac behavior, mixed-DPI Linux desktops and distro variations.

Wayland integration is not implemented. CI runs without a ChatGPT account or
live audio, so its success cannot establish these behaviors. Follow the
[live checklist](../experimental/README.md#first-live-test-checklist) before
describing either port as supported.

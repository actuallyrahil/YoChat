# Experimental macOS and Linux implementation plan

Scope: a separate Python/Qt app for macOS and Linux X11; leave Windows source,
installer, binaries and workflow untouched. Experimental until tested on hardware.

1. Add pure transcript, placement, configuration and wake-gating models and
   regression tests before implementation. Exact-neighbor duplicate handling
   matches the verified Windows behavior; no personal transcript fixtures.
2. Add native geometry/shortcut helpers: Swift Accessibility on macOS and
   AT-SPI plus X11 key events on Linux. Probe only ChatGPT/Codex, never navigate
   or foreground windows. Unknown/stale state blocks wake activation.
3. Add a Qt tray/settings/bubbles frontend and local Vosk microphone worker.
   Keep accessibility in a subprocess with timeouts, speech off the UI thread,
   and transcript reads atomic with partial-write recovery. No transcript logs.
4. Add repeatable shell installers, model setup, diagnostics and preview mode.
   Linux targets X11; Wayland gets a clear unsupported message. Permissions and
   source-file availability remain user/environment prerequisites.
5. Test pure models and UI smoke locally; compile mac helper and run Linux/mac
   tests in a separate GitHub workflow. Package experimental source bundles.
   Record native live checks as pending, even when build checks pass.

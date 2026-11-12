# Release verification

Version 0.1.1, Windows x64, 12 November 2026.

- All 17 automated tests passed: wake matching, confidence, cooldown, hotkeys,
  microphone handoff, settings persistence, transcript updates and repeated-call
  reset, partial-write recovery, placement and prompt clearance.
- Self-contained publish succeeded. The published executable's `--self-test`
  exited with code 0, including bubble layout and settings-window initialization.
- The source installer completed in an isolated folder with `-NoStart
  -NoShortcuts`; this did not replace the user's running prototype.
- The preceding direct-reader prototype was confirmed by the user on two
  successive calls with only the pill and live bubbles visible.

The newly combined tray application still needs a full microphone/voice session
check on a normal desktop. Automated checks do not establish compatibility with
every ChatGPT build. GitHub Actions has been supplied but has not run remotely.
See README for the internal transcript-format and English UI requirements.


Duplicate-source regression: verified exact-neighbor collapse, source cleanup,
repeated turns, different wording, and the five-message limit with synthetic data.
The user confirmed the duplicate-transcript fix in a live call on 12 November 2026.

Wake recognition fix: the old one-phrase Windows grammar reproduced a false
activation on synthetic unrelated speech. The replacement Vosk decoder offers
an explicit unknown-speech alternative and requires an exact complete phrase.
Nine synthetic audio fixtures pass with only Yo Chat activating. The user confirmed in a live microphone test that ordinary speech did not
activate voice and only Yo Chat did. Synthetic and live spot checks are not a
guarantee against every real-world false activation.

#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
if [[ "$(uname -s)" != Darwin ]]; then echo 'This installer is for macOS.' >&2; exit 1; fi
PYTHON="${YOCHAT_PYTHON:-python3}"
"$PYTHON" -c 'import sys; assert (3,11) <= sys.version_info[:2] <= (3,13), "Use Python 3.11–3.13; set YOCHAT_PYTHON to its executable."'
if ! xcrun --find swiftc >/dev/null 2>&1; then
  echo 'Install Apple Command Line Tools with xcode-select --install, then retry.' >&2
  exit 1
fi
"$PYTHON" -m venv .venv
.venv/bin/python -m pip install -r requirements.txt
xcrun swiftc native/MacBridge.swift -o native/MacBridge -framework AppKit -framework ApplicationServices
.venv/bin/python setup-model.py
.venv/bin/python -m unittest discover -s tests -v
echo 'Installed YoChat Experimental. Run: bash run.sh'
echo 'Allow Microphone for your terminal/Python and Accessibility for the terminal/MacBridge when requested.'

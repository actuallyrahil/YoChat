#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
if [[ "$(uname -s)" != Linux ]]; then echo 'This installer is for Linux.' >&2; exit 1; fi
PYTHON="${YOCHAT_PYTHON:-/usr/bin/python3}"
"$PYTHON" -c 'import sys; assert (3,11) <= sys.version_info[:2] <= (3,13), "Use distro Python 3.11–3.13 with python3-pyatspi installed."'
if ! "$PYTHON" -c 'import pyatspi; import ctypes; ctypes.CDLL("libportaudio.so.2")'; then
  echo 'Install OS dependencies first. Ubuntu/Debian: sudo apt install python3-venv python3-pyatspi libportaudio2 libxcb-cursor0 libxkbcommon-x11-0' >&2
  exit 1
fi
# AT-SPI is supplied by the distro, not an unrelated package from PyPI.
"$PYTHON" -m venv --system-site-packages .venv
.venv/bin/python -m pip install -r requirements.txt
.venv/bin/python setup-model.py
.venv/bin/python -m unittest discover -s tests -v
echo 'Installed YoChat Experimental. In an X11 desktop session run: bash run.sh'

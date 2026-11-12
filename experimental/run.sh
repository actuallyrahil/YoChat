#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
if [[ ! -x .venv/bin/python ]]; then echo 'Run the installer for your platform first.' >&2; exit 1; fi
if [[ "$(uname -s)" == Linux ]]; then
  if [[ "${XDG_SESSION_TYPE:-}" == wayland || -n "${WAYLAND_DISPLAY:-}" ]]; then
    echo 'YoChat Experimental currently requires an X11 login session. Native Wayland is not supported.' >&2
    exit 1
  fi
  # AT-SPI screen coordinates are X11 pixels. Avoid implicit Qt DPI conversion.
  export QT_QPA_PLATFORM=xcb QT_SCALE_FACTOR=1 QT_AUTO_SCREEN_SCALE_FACTOR=0 QT_ENABLE_HIGHDPI_SCALING=0
fi
exec .venv/bin/python -m yochat_portable.ui "$@"

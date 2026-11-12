import json
from pathlib import Path
import selectors
import subprocess
import sys


class Bridge:
    """A hung accessibility call cannot freeze the GUI or permit a stale wake."""
    def __init__(self):
        self.process = None

    def request(self, command='snapshot', **parameters):
        if self.process is None or self.process.poll() is not None:
            if sys.platform == 'darwin':
                executable = Path(__file__).resolve().parents[1] / 'native/MacBridge'
                if not executable.exists():
                    raise RuntimeError('Mac helper missing. Run bash install-macos.sh first.')
                argv = [str(executable)]
            elif sys.platform.startswith('linux'):
                argv = [sys.executable, '-m', 'yochat_portable.linux_helper']
            else:
                raise RuntimeError('Use the existing Windows version on Windows.')
            self.process = subprocess.Popen(argv, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                            stderr=subprocess.DEVNULL, text=True, bufsize=1)
        try:
            self.process.stdin.write(json.dumps(dict(command=command, **parameters)) + '\n')
            self.process.stdin.flush()
            with selectors.DefaultSelector() as selector:
                selector.register(self.process.stdout, selectors.EVENT_READ)
                if not selector.select(2):
                    raise TimeoutError('Accessibility check timed out; wake activation is blocked.')
            line = self.process.stdout.readline()
            if not line:
                raise RuntimeError('Accessibility helper exited.')
            result = json.loads(line)
            if not isinstance(result, dict):
                raise ValueError('Invalid helper response.')
            return result
        except Exception:
            self.close()
            raise

    def close(self):
        if self.process:
            if self.process.poll() is None:
                self.process.terminate()
                try:
                    self.process.wait(timeout=1)
                except subprocess.TimeoutExpired:
                    self.process.kill(); self.process.wait(timeout=1)
            self.process.stdin.close(); self.process.stdout.close()
            self.process = None

"""One-time download from the model publisher. The running app never downloads."""
from pathlib import Path
import shutil
import tempfile
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parent
NAME = 'vosk-model-small-en-us-0.15'
URL = f'https://alphacephei.com/vosk/models/{NAME}.zip'


def safe_members(archive):
    for item in archive.infolist():
        parts = Path(item.filename).parts
        if (not parts or parts[0] != NAME or '..' in parts or '\\' in item.filename or
                item.filename.startswith('/') or ':' in item.filename or
                (item.external_attr >> 16) & 0o170000 == 0o120000):
            raise ValueError('Unsafe model archive member.')
        yield item


def main():
    models = ROOT / 'models'
    models.mkdir(exist_ok=True)
    target = models / NAME
    if (target / 'am/final.mdl').is_file():
        print('English wake model already installed.'); return
    if target.exists():
        raise RuntimeError(f'Incomplete model directory: {target}. Move it aside and retry.')
    print(f'Downloading the English wake model from {URL}')
    with tempfile.TemporaryDirectory(dir=models) as temporary:
        folder = Path(temporary)
        archive_path = folder / 'model.zip'
        with urllib.request.urlopen(URL, timeout=60) as response, archive_path.open('wb') as output:
            total = 0
            while chunk := response.read(1024 * 1024):
                total += len(chunk)
                if total > 100 * 1024 * 1024:
                    raise ValueError('Unexpected model download size.')
                output.write(chunk)
        with zipfile.ZipFile(archive_path) as archive:
            members = list(safe_members(archive))
            if sum(m.file_size for m in members) > 200 * 1024 * 1024:
                raise ValueError('Unexpected expanded model size.')
            archive.extractall(folder, members=members)
        if not (folder / NAME / 'am/final.mdl').is_file():
            raise ValueError('Downloaded archive is not the expected Vosk model.')
        shutil.move(str(folder / NAME), str(target))
    print('Wake model installed. Its license is provided by the model publisher.')


if __name__ == '__main__':
    main()

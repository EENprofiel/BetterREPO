"""Package a built release. Standard-library-only, excludes game/third-party DLLs."""
from pathlib import Path
import hashlib
import json
import shutil
import struct
import zipfile

ROOT = Path(__file__).resolve().parent
META = json.loads((ROOT / 'manifest.json').read_text())
VERSION = META['version_number']
DLL = ROOT / 'bin/Release/netstandard2.1/TruckEnergyDisplay.dll'
if not DLL.is_file():
    raise SystemExit('Build Release before packaging.')
if f'PluginVersion = "{VERSION}"' not in (ROOT / 'Plugin.cs').read_text():
    raise SystemExit('Plugin/manifest version mismatch.')
icon = (ROOT / 'icon.png').read_bytes()
if icon[:8] != b'\x89PNG\r\n\x1a\n' or struct.unpack('>II', icon[16:24]) != (256, 256):
    raise SystemExit('Thunderstore icon must be a 256x256 PNG.')
if any(p.stat().st_mtime > DLL.stat().st_mtime for p in ROOT.glob('*.cs')):
    raise SystemExit('C# source changed since the last release build.')
DIST = ROOT / 'dist'
DIST.mkdir(exist_ok=True)
shutil.copy2(DLL, DIST / 'TruckEnergyDisplay.dll')

def archive(path, entries):
    with zipfile.ZipFile(path, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        for local, name in sorted(entries, key=lambda e: e[1]):
            info = zipfile.ZipInfo(name, (2026, 9, 19, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o100644 << 16
            z.writestr(info, local.read_bytes())
    with zipfile.ZipFile(path) as z:
        assert z.testzip() is None

release = DIST / f'TruckEnergyDisplay-{VERSION}-Thunderstore.zip'
entries = [(ROOT / n, n) for n in ('manifest.json', 'README.md', 'TECHNICAL.md', 'CHANGELOG.md', 'LICENSE', 'icon.png')]
entries.append((DLL, 'BepInEx/plugins/TruckEnergyDisplay/TruckEnergyDisplay.dll'))
archive(release, entries)
source = DIST / f'TruckEnergyDisplay-{VERSION}-Source.zip'
allowed = {'.cs', '.csproj', '.md', '.json', '.png', '.svg', '.py'}
entries = []
for p in ROOT.rglob('*'):
    rel = p.relative_to(ROOT)
    if not p.is_file() or any(x in {'bin', 'obj', 'dist'} for x in rel.parts):
        continue
    if p.suffix in allowed or p.name == 'LICENSE':
        entries.append((p, 'TruckEnergyDisplay/' + rel.as_posix()))
archive(source, entries)
with zipfile.ZipFile(release) as z:
    assert z.read('BepInEx/plugins/TruckEnergyDisplay/TruckEnergyDisplay.dll') == DLL.read_bytes()
for p in (release, source, DIST / 'TruckEnergyDisplay.dll'):
    print(p.name, p.stat().st_size, hashlib.sha256(p.read_bytes()).hexdigest())

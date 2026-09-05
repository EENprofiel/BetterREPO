#!/usr/bin/env python3
import json,subprocess,sys,zipfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]; PACKAGE=ROOT/'package'; DIST=ROOT/'dist'
subprocess.run([sys.executable,str(ROOT/'scripts/validate.py')],check=True)
v=json.loads((PACKAGE/'manifest.json').read_text())['version_number']; DIST.mkdir(exist_ok=True)
out=DIST/f'BetterREPO-{v}.zip'
if out.exists(): out.unlink()
with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
 for p in sorted(PACKAGE.rglob('*')):
  if p.is_file(): z.write(p,p.relative_to(PACKAGE).as_posix())
print(out)

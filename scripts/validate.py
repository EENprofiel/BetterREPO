#!/usr/bin/env python3
import argparse,json,re,struct,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]; PACKAGE=ROOT/'package'; MANIFEST=PACKAGE/'manifest.json'
def fail(m): print('ERROR:',m,file=sys.stderr); raise SystemExit(1)
def png_size(p):
 d=p.read_bytes()
 if len(d)<24 or d[:8]!=b'\x89PNG\r\n\x1a\n' or d[12:16]!=b'IHDR': fail('icon.png is not a valid PNG')
 return struct.unpack('>II',d[16:24])
def main():
 ap=argparse.ArgumentParser(); ap.add_argument('--tag'); a=ap.parse_args()
 for f in ('manifest.json','README.md','icon.png'):
  if not (PACKAGE/f).is_file(): fail(f'Missing package/{f}')
 m=json.loads(MANIFEST.read_text())
 if m.get('name')!='BetterREPO': fail('manifest name must be BetterREPO')
 v=m.get('version_number','')
 if not re.fullmatch(r'\d+\.\d+\.\d+',v): fail('invalid version_number')
 if a.tag and a.tag!=f'v{v}': fail(f'tag {a.tag} does not match v{v}')
 deps=m.get('dependencies')
 if not isinstance(deps,list) or not deps: fail('dependencies must be non-empty')
 if len(deps)!=len(set(deps)): fail('duplicate dependencies found')
 if png_size(PACKAGE/'icon.png')!=(256,256): fail('icon.png must be 256x256')
 ch=PACKAGE/'CHANGELOG.md'
 if ch.is_file() and v not in ch.read_text(): fail(f'CHANGELOG.md does not mention {v}')
 print(f'BetterREPO {v} validated successfully: {len(deps)} dependencies')
if __name__=='__main__': main()

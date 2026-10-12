#!/usr/bin/env python3
import argparse,json,re,struct,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]; PACKAGE=ROOT/'package'; MANIFEST=PACKAGE/'manifest.json'
def fail(m): print('ERROR:',m,file=sys.stderr); raise SystemExit(1)
def png_size(p):
 d=p.read_bytes()
 if len(d)<24 or d[:8]!=b'\x89PNG\r\n\x1a\n' or d[12:16]!=b'IHDR': fail('icon.png is not a valid PNG')
 return struct.unpack('>II',d[16:24])
def norm(s): return re.sub(r'[^a-z0-9]','',s.lower())
def check_readme(deps):
 # The package README is the Thunderstore page: it must list exactly what the manifest installs.
 text=(PACKAGE/'README.md').read_text(encoding='utf-8')
 listed={}
 for name,ver in re.findall(r'^- \*\*(.+?)\*\* `([^`]+)`',text,re.M): listed[norm(name)]=(name,ver)
 want={}
 for d in deps:
  parts=d.rsplit('-',2)
  if len(parts)!=3: fail(f'dependency {d} is not Author-Name-Version')
  want[norm(parts[1])]=(parts[1],parts[2])
 errors=[f'README lists {listed[k][0]} but the manifest does not install it' for k in listed if k not in want]
 errors+=[f'README is missing {want[k][0]} {want[k][1]}' for k in want if k not in listed]
 errors+=[f'README says {listed[k][0]} {listed[k][1]} but the manifest pins {want[k][1]}' for k in want if k in listed and listed[k][1]!=want[k][1]]
 m=re.search(r'\*\*(\d+) pinned Thunderstore dependencies\*\*',text)
 if not m or int(m.group(1))!=len(deps): errors.append(f'README dependency count is {m.group(1) if m else "missing"}, manifest has {len(deps)}')
 if errors: fail('package/README.md does not match manifest.json:\n  '+'\n  '.join(errors))
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
 check_readme(deps)
 ch=PACKAGE/'CHANGELOG.md'
 if ch.is_file() and v not in ch.read_text(): fail(f'CHANGELOG.md does not mention {v}')
 print(f'BetterREPO {v} validated successfully: {len(deps)} dependencies')
if __name__=='__main__': main()

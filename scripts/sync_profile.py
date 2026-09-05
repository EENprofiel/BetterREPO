#!/usr/bin/env python3
import argparse,json,re,shutil,tempfile,zipfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]; PACKAGE=ROOT/'package'; MANIFEST=PACKAGE/'manifest.json'
def parse(text):
 mods=[]; cur=None
 for line in text.splitlines():
  m=re.match(r'^\s*- name:\s*(\S+)\s*$',line)
  if m:
   if cur: mods.append(cur)
   cur={'name':m.group(1),'version':{}}; continue
  if not cur: continue
  m=re.match(r'^\s*(major|minor|patch):\s*(\d+)\s*$',line)
  if m: cur['version'][m.group(1)]=int(m.group(2)); continue
  m=re.match(r'^\s*enabled:\s*(true|false)\s*$',line,re.I)
  if m: cur['enabled']=m.group(1).lower()=='true'
 if cur: mods.append(cur)
 return [f"{x['name']}-{x['version']['major']}.{x['version']['minor']}.{x['version']['patch']}" for x in mods if x.get('enabled')]
def main():
 ap=argparse.ArgumentParser(); ap.add_argument('profile',type=Path); a=ap.parse_args()
 with tempfile.TemporaryDirectory() as td:
  t=Path(td)
  with zipfile.ZipFile(a.profile) as z: z.extractall(t)
  deps=parse((t/'export.r2x').read_text())
  m=json.loads(MANIFEST.read_text()); m['dependencies']=deps; MANIFEST.write_text(json.dumps(m,indent=2)+'\n')
  tracked=PACKAGE/'config'; exported=t/'config'; updated=[]
  if tracked.is_dir() and exported.is_dir():
   for p in tracked.iterdir():
    src=exported/p.name
    if p.is_file() and src.is_file(): shutil.copyfile(src,p); updated.append(p.name)
  ignored=sorted({p.name for p in exported.iterdir() if p.is_file()}-{p.name for p in tracked.iterdir() if p.is_file()}) if exported.is_dir() else []
 print(f'Dependencies synced: {len(deps)}; tracked configs refreshed: {len(updated)}; ignored configs: {len(ignored)}')
 print('Review git diff before committing.')
if __name__=='__main__': main()

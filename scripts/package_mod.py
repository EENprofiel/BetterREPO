#!/usr/bin/env python3
"""Build the Thunderstore ZIP for one mod from the common mod layout.

Usage: python scripts/package_mod.py <ModName> [--dll path/to/Mod.dll]

Build the mod in Release first. The DLL is found under mods/<ModName>/bin/
unless --dll is given. The ZIP is written to dist/<ModName>-<version>.zip.
"""
import argparse, json, sys, zipfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import check_mods  # noqa: E402

ROOT = check_mods.ROOT
FILES = ("manifest.json", "README.md", "CHANGELOG.md", "LICENSE", "icon.png")


def find_dll(folder, name):
    found = [p for p in folder.glob(f"bin/**/{name}.dll") if "Release" in p.parts]
    if not found:
        raise SystemExit(f"No Release build of {name}.dll under {folder / 'bin'}. Run 'dotnet build -c Release' first.")
    return max(found, key=lambda p: p.stat().st_mtime)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("mod")
    ap.add_argument("--dll")
    args = ap.parse_args()
    errors = check_mods.check(args.mod) if (check_mods.MODS / args.mod).is_dir() else [f"no folder mods/{args.mod}"]
    if errors:
        raise SystemExit("\n".join(f"ERROR: {args.mod}: {e}" for e in errors))
    folder = check_mods.MODS / args.mod
    version = json.loads((folder / "manifest.json").read_text(encoding="utf-8"))["version_number"]
    dll = Path(args.dll) if args.dll else find_dll(folder, args.mod)
    if not dll.is_file():
        raise SystemExit(f"DLL not found: {dll}")
    out = ROOT / "dist"
    out.mkdir(exist_ok=True)
    target = out / f"{args.mod}-{version}.zip"
    entries = [(folder / f, f) for f in FILES] + [(dll, f"BepInEx/plugins/{args.mod}/{args.mod}.dll")]
    with zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED) as z:
        for local, arc in sorted(entries, key=lambda e: e[1]):
            info = zipfile.ZipInfo(arc, (2026, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o100644 << 16
            z.writestr(info, local.read_bytes())
    print(target)


if __name__ == "__main__":
    main()

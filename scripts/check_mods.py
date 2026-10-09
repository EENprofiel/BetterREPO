#!/usr/bin/env python3
"""Check that every mod folder follows the common layout and has consistent versions."""
import argparse, json, re, struct, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MODS = ROOT / "mods"
REQUIRED = ("README.md", "CHANGELOG.md", "LICENSE", "manifest.json", "icon.png")
SEMVER = re.compile(r"\d+\.\d+\.\d+")


def mod_names():
    return sorted(p.name for p in MODS.iterdir() if (p / "manifest.json").is_file() or any(p.glob("*.csproj")))


def check(name):
    """Return a list of problems for one mod."""
    folder = MODS / name
    errors = []
    for f in REQUIRED:
        if not (folder / f).is_file():
            errors.append(f"missing {f}")
    if not (folder / f"{name}.csproj").is_file():
        errors.append(f"missing {name}.csproj")
    if errors:
        return errors
    manifest = json.loads((folder / "manifest.json").read_text(encoding="utf-8"))
    version = manifest.get("version_number", "")
    if manifest.get("name") != name:
        errors.append(f"manifest name {manifest.get('name')!r} must be {name!r}")
    if not SEMVER.fullmatch(version):
        errors.append(f"invalid version_number {version!r}")
    if len(manifest.get("description", "")) > 250:
        errors.append("manifest description is longer than 250 characters")
    if not manifest.get("dependencies"):
        errors.append("manifest dependencies must not be empty")
    icon = (folder / "icon.png").read_bytes()
    if icon[:8] != b"\x89PNG\r\n\x1a\n" or struct.unpack(">II", icon[16:24]) != (256, 256):
        errors.append("icon.png must be a 256x256 PNG")
    if not re.search(rf"^##\s+\[?{re.escape(version)}\]?\b", (folder / "CHANGELOG.md").read_text(encoding="utf-8"), re.M):
        errors.append(f"CHANGELOG.md has no '## {version}' heading")
    versions = {
        m.group(1)
        for p in folder.rglob("*.cs")
        if not {"bin", "obj", "dist", "tests"} & set(p.relative_to(folder).parts)
        for m in re.finditer(r'PluginVersion\s*=\s*"([^"]+)"', p.read_text(encoding="utf-8-sig"))
    }
    if versions != {version}:
        errors.append(f"PluginVersion in code {sorted(versions)} does not match manifest {version}")
    csproj = (folder / f"{name}.csproj").read_text(encoding="utf-8-sig")
    m = re.search(r"<Version>([^<]+)</Version>", csproj)
    if m and m.group(1) != version:
        errors.append(f"csproj Version {m.group(1)} does not match manifest {version}")
    return errors


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--tag", help="release tag such as VanillaOrModded-v1.0.0; checks only that mod")
    args = ap.parse_args()
    names = mod_names()
    if args.tag:
        m = re.fullmatch(r"([A-Za-z0-9]+)-v(\d+\.\d+\.\d+)", args.tag)
        if not m or m.group(1) not in names:
            print(f"ERROR: tag {args.tag!r} must look like <ModName>-vX.Y.Z for a mod in mods/", file=sys.stderr)
            return 1
        names = [m.group(1)]
    failed = False
    for name in names:
        errors = check(name)
        if args.tag and not errors:
            version = json.loads((MODS / name / "manifest.json").read_text(encoding="utf-8"))["version_number"]
            if args.tag != f"{name}-v{version}":
                errors.append(f"tag {args.tag} does not match manifest version {version}")
        for e in errors:
            print(f"ERROR: {name}: {e}", file=sys.stderr)
        failed |= bool(errors)
        if not errors:
            print(f"{name}: ok")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())

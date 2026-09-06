#!/usr/bin/env python3
import argparse
import json
import re
import tempfile
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PACKAGE = ROOT / "package"
MANIFEST = PACKAGE / "manifest.json"
THUNDERSTORE_ID = re.compile(r"^[A-Za-z0-9_]+-[A-Za-z0-9_]+$")


def parse_export(text: str):
    mods = []
    current = None

    for line in text.splitlines():
        match = re.match(r"^\s*- name:\s*(\S+)\s*$", line)
        if match:
            if current:
                mods.append(current)
            current = {"name": match.group(1), "version": {}}
            continue

        if not current:
            continue

        match = re.match(r"^\s*(major|minor|patch):\s*(\d+)\s*$", line)
        if match:
            current["version"][match.group(1)] = int(match.group(2))
            continue

        match = re.match(r"^\s*enabled:\s*(true|false)\s*$", line, re.I)
        if match:
            current["enabled"] = match.group(1).lower() == "true"

    if current:
        mods.append(current)

    return mods


def dependency_string(mod):
    version = mod["version"]
    return (
        f"{mod['name']}-"
        f"{version['major']}.{version['minor']}.{version['patch']}"
    )


def is_public_dependency(mod_name: str, package_name: str):
    if not THUNDERSTORE_ID.fullmatch(mod_name):
        return False

    _, mod_slug = mod_name.split("-", 1)
    return mod_slug.lower() != package_name.lower()


def effective_settings(text: str):
    section = ""
    settings = []

    for raw_line in text.splitlines():
        line = raw_line.strip()

        if not line or line.startswith("#") or line.startswith(";"):
            continue

        if line.startswith("[") and line.endswith("]"):
            section = line[1:-1].strip()
            continue

        if "=" in line:
            key, value = line.split("=", 1)
            settings.append((section, key.strip(), value.strip()))

    return settings


def write_normalized(path: Path, text: str):
    path.write_text(
        text.replace("\r\n", "\n"),
        encoding="utf-8",
        newline="\n",
    )


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("profile", type=Path)
    args = parser.parse_args()

    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    package_name = manifest["name"]

    with tempfile.TemporaryDirectory() as temp_dir:
        extracted = Path(temp_dir)

        with zipfile.ZipFile(args.profile) as archive:
            archive.extractall(extracted)

        exported_mods = parse_export(
            (extracted / "export.r2x").read_text(encoding="utf-8")
        )
        enabled_mods = [mod for mod in exported_mods if mod.get("enabled")]
        public_mods = [
            mod
            for mod in enabled_mods
            if is_public_dependency(mod["name"], package_name)
        ]

        dependencies = [dependency_string(mod) for mod in public_mods]
        manifest["dependencies"] = dependencies
        MANIFEST.write_text(
            json.dumps(manifest, indent=2) + "\n",
            encoding="utf-8",
            newline="\n",
        )

        tracked_dir = PACKAGE / "config"
        exported_dir = extracted / "config"
        updated = []
        unchanged = []

        if tracked_dir.is_dir() and exported_dir.is_dir():
            for tracked in sorted(tracked_dir.iterdir()):
                source = exported_dir / tracked.name

                if not tracked.is_file() or not source.is_file():
                    continue

                tracked_text = tracked.read_text(encoding="utf-8-sig")
                source_text = source.read_text(encoding="utf-8-sig")

                if effective_settings(tracked_text) != effective_settings(source_text):
                    write_normalized(tracked, source_text)
                    updated.append(tracked.name)
                else:
                    unchanged.append(tracked.name)

        ignored_configs = 0
        if exported_dir.is_dir():
            tracked_names = {
                path.name for path in tracked_dir.iterdir() if path.is_file()
            }
            ignored_configs = len(
                [
                    path
                    for path in exported_dir.iterdir()
                    if path.is_file() and path.name not in tracked_names
                ]
            )

    excluded_entries = len(enabled_mods) - len(public_mods)
    print(
        f"Dependencies synced: {len(dependencies)}; "
        f"excluded local/self entries: {excluded_entries}"
    )
    print(
        f"Tracked configs changed: {len(updated)}; "
        f"unchanged: {len(unchanged)}; "
        f"unknown configs ignored: {ignored_configs}"
    )
    print("Review git diff before committing.")


if __name__ == "__main__":
    main()

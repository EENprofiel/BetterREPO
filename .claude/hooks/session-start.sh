#!/bin/bash
# SessionStart hook for Claude Code on the web: installs the .NET 8 SDK and,
# when the network allows, fetches the build-only DLLs for VanillaOrModded.
set -uo pipefail

if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

ROOT="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "$0")/../.." && pwd)}"

# 1. .NET 8 SDK from the Ubuntu archive (api.nuget.org is reachable, dot.net is not).
if ! command -v dotnet >/dev/null 2>&1; then
  export DEBIAN_FRONTEND=noninteractive
  apt-get update -qq >/dev/null 2>&1
  apt-get install -y -qq dotnet-sdk-8.0 >/dev/null 2>&1 || echo "session-start: could not install dotnet-sdk-8.0" >&2
fi
if command -v dotnet >/dev/null 2>&1 && [ -n "${CLAUDE_ENV_FILE:-}" ]; then
  echo 'export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1' >> "$CLAUDE_ENV_FILE"
fi

# 2. Build-only dependencies, same versions as .github/workflows/mods.yml.
#    thunderstore.io must be in the environment's allowed domains.
LIB="$ROOT/mods/VanillaOrModded/lib"
fetch() { # name author version
  local name="$1" author="$2" ver="$3" tmp f
  [ -f "$LIB/$name.dll" ] && return 0
  tmp="$(mktemp -d)"
  if curl -fsSL -m 60 -o "$tmp/p.zip" "https://thunderstore.io/package/download/$author/$name/$ver/" \
     && unzip -q -o "$tmp/p.zip" -d "$tmp/x"; then
    f="$(find "$tmp/x" -name "$name.dll" | head -1)"
    [ -n "$f" ] && mkdir -p "$LIB" && cp "$f" "$LIB/$name.dll"
  fi
  [ -f "$LIB/$name.dll" ] || echo "session-start: $name.dll not fetched (thunderstore.io blocked by network policy?)" >&2
  rm -rf "$tmp"
}
fetch MenuLib nickklmao 2.5.4
fetch REPOLib Zehs 4.2.0
exit 0

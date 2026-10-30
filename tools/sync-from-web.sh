#!/usr/bin/env bash
# Copies the source-of-truth data from the web repo (docs/UNITY_PORT_FAZ1.md, D-094): levels and golden replays.
# Usage: tools/sync-from-web.sh [/path/to/minik-usta]   (default: ../minik-usta next to this repo)
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WEB="${1:-$HERE/../minik-usta}"
if [ ! -d "$WEB/levels" ] || [ ! -d "$WEB/tests/golden" ]; then
  echo "sync-from-web: web repo not found at $WEB (expected levels/ and tests/golden/)" >&2
  exit 1
fi
LEVELS="$HERE/Assets/StreamingAssets/MinikUsta/levels"
GOLDEN="$HERE/Assets/MinikUsta/Core.Tests/Golden"
mkdir -p "$LEVELS" "$GOLDEN"
cp "$WEB"/levels/*.json "$LEVELS/"
cp "$WEB"/tests/golden/*.json "$GOLDEN/"
echo "levels: $(ls "$LEVELS"/*.json | wc -l) files -> $LEVELS"
echo "golden: $(ls "$GOLDEN"/*.json | wc -l) files -> $GOLDEN"
echo "web commit: $(git -C "$WEB" rev-parse --short HEAD 2>/dev/null || echo '?')"

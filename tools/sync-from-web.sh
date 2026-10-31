#!/usr/bin/env bash
# Copies the source-of-truth data from the web repo (docs/UNITY_PORT_FAZ1.md, D-094): levels, golden replays, the K-45
# validator fixtures (tests/level/fixtures/{valid,invalid}) and config/economy.json (L-17 booster unlock levels).
# Usage: tools/sync-from-web.sh [/path/to/minik-usta]   (default: ../minik-usta next to this repo)
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WEB="${1:-$HERE/../minik-usta}"
if [ ! -d "$WEB/levels" ] || [ ! -d "$WEB/tests/golden" ]; then
  echo "sync-from-web: web repo not found at $WEB (expected levels/ and tests/golden/)" >&2
  exit 1
fi
LEVELS="$HERE/Assets/StreamingAssets/MinikUsta/levels"
CONFIG="$HERE/Assets/StreamingAssets/MinikUsta/config"
GOLDEN="$HERE/Assets/MinikUsta/Core.Tests/Golden"
FIXTURES="$HERE/Assets/MinikUsta/Core.Tests/Fixtures/Levels"
mkdir -p "$LEVELS" "$CONFIG" "$GOLDEN" "$FIXTURES/valid" "$FIXTURES/invalid"
cp "$WEB"/levels/*.json "$LEVELS/"
cp "$WEB"/config/economy.json "$CONFIG/economy.json"
cp "$WEB"/tests/golden/*.json "$GOLDEN/"
rm -f "$FIXTURES"/valid/*.json "$FIXTURES"/invalid/*.json
cp "$WEB"/tests/level/fixtures/valid/*.json "$FIXTURES/valid/"
cp "$WEB"/tests/level/fixtures/invalid/*.json "$FIXTURES/invalid/"
echo "levels: $(ls "$LEVELS"/*.json | wc -l) files -> $LEVELS"
echo "config: economy.json -> $CONFIG"
echo "golden: $(ls "$GOLDEN"/*.json | wc -l) files -> $GOLDEN"
echo "fixtures: $(ls "$FIXTURES"/valid/*.json | wc -l) valid, $(ls "$FIXTURES"/invalid/*.json | wc -l) invalid -> $FIXTURES"
echo "web commit: $(git -C "$WEB" rev-parse --short HEAD 2>/dev/null || echo '?')"

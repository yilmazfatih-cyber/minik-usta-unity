#!/usr/bin/env bash
# Copies the source-of-truth data from the web repo (docs/UNITY_PORT_FAZ1.md, D-094): levels, golden replays, the K-45
# validator fixtures (tests/level/fixtures/{valid,invalid}), config/economy.json (L-17 booster unlock levels) and
# src/theme/tokens.json (design-lead's tokens; FeelSettingsTests check the Presentation.Core defaults against it).
# Faz 3 Q1 (D-096): the game reads its data through Resources.Load<TextAsset> (works in WebGL without a web request), so
# levels, economy.json, src/i18n/{tr,en}.json and the canonical solver logs (dev "play the solution") are ALSO copied to
# Assets/MinikUsta/Resources/{levels,config,i18n,solutions}; the
# StreamingAssets copies stay for the core tests (Core.Tests TestPaths, dotnet Core.Tests csproj).
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
PGOLDEN="$HERE/Assets/MinikUsta/Presentation.Core.Tests/Golden"
RES="$HERE/Assets/MinikUsta/Resources"
mkdir -p "$LEVELS" "$CONFIG" "$GOLDEN" "$FIXTURES/valid" "$FIXTURES/invalid" "$PGOLDEN" "$RES/levels" "$RES/config" "$RES/i18n"
cp "$WEB"/levels/*.json "$LEVELS/"
cp "$WEB"/config/economy.json "$CONFIG/economy.json"
cp "$WEB"/tests/golden/*.json "$GOLDEN/"
rm -f "$FIXTURES"/valid/*.json "$FIXTURES"/invalid/*.json
cp "$WEB"/tests/level/fixtures/valid/*.json "$FIXTURES/valid/"
cp "$WEB"/tests/level/fixtures/invalid/*.json "$FIXTURES/invalid/"
cp "$WEB"/src/theme/tokens.json "$PGOLDEN/tokens.json"
rm -f "$RES"/levels/*.json
cp "$WEB"/levels/*.json "$RES/levels/"
cp "$WEB"/config/economy.json "$RES/config/economy.json"
cp "$WEB"/src/i18n/tr.json "$WEB"/src/i18n/en.json "$RES/i18n/"
# dev builds: "play the solver's solution" (principle 8) reads the canonical logs (~9 KB)
mkdir -p "$RES/solutions"
rm -f "$RES"/solutions/*.json
cp "$WEB"/tests/golden/level_*.solver.json "$RES/solutions/"
echo "levels: $(ls "$LEVELS"/*.json | wc -l) files -> $LEVELS"
echo "config: economy.json -> $CONFIG"
echo "golden: $(ls "$GOLDEN"/*.json | wc -l) files -> $GOLDEN"
echo "fixtures: $(ls "$FIXTURES"/valid/*.json | wc -l) valid, $(ls "$FIXTURES"/invalid/*.json | wc -l) invalid -> $FIXTURES"
echo "tokens: tokens.json -> $PGOLDEN"
echo "resources: $(ls "$RES"/levels/*.json | wc -l) levels, $(ls "$RES"/solutions/*.json | wc -l) solutions, economy.json, tr.json + en.json -> $RES"
echo "web commit: $(git -C "$WEB" rev-parse --short HEAD 2>/dev/null || echo '?')"

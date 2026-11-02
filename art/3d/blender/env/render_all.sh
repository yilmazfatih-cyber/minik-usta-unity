#!/usr/bin/env bash
# B1 full pipeline (art/3d/blender/env/README.md): Blender renders -> post -> atlas + fx packing -> previews.
# Usage: bash art/3d/blender/env/render_all.sh [family ...]   (families: blocks board ui icons bg fx town; default all)
#   WEB=/path/to/minik-usta (Playwright + Chromium from the web repo, symbol masks, SVG raster of keep2d frames)
set -euo pipefail
cd "$(dirname "$0")/../../../.."
WEB="${WEB:-../minik-usta}"
RAW=art/3d/out/env/raw
FAMILIES=("$@")
[ ${#FAMILIES[@]} -eq 0 ] && FAMILIES=(blocks board ui icons bg fx town)
mkdir -p "$RAW"
[ -f art/3d/out/env/sym/sym_w.png ] || node tools/art/symmasks.mjs --web "$WEB"
for f in "${FAMILIES[@]}"; do
  echo "== $f"
  blender -b --factory-startup -noaudio --python "art/3d/blender/env/$f.py" -- --out "$RAW" > "art/3d/out/env/$f.log" 2>&1
  grep -E "DONE|Error|Traceback" "art/3d/out/env/$f.log" | tail -3
done
python3 art/3d/blender/env/post_env.py
node tools/art/rasterize.mjs --web "$WEB"
node tools/art/packfx.mjs

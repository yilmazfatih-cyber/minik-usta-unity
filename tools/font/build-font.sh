#!/usr/bin/env bash
# Rebuilds Assets/MinikUsta/Resources/fonts/Baloo2-ExtraBold.ttf (paket V1; D-096: uGUI Text needs a static TTF, no TMP).
#
# Source: Baloo 2 (OFL 1.1, no Reserved Font Name) — google/fonts ofl/baloo2/Baloo2[wght].ttf (variable, wght 400…800).
# Unity's dynamic font renders a variable TTF at its default instance (400), so the ExtraBold (800) instance is cut with
# fontTools varLib.instancer and subset to Latin + Latin Extended-A/B + punctuation, currency, arrows, maths and geometric
# shapes (Turkish ş ğ ı İ ö ü ç and every character of the i18n tables; FontCoverageTests checks the cmap).
# Needs: curl, python3 with fontTools (no other package). Output is deterministic for the same input and fontTools version.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
OUT="$ROOT/Assets/MinikUsta/Resources/fonts"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
BASE="https://raw.githubusercontent.com/google/fonts/main/ofl/baloo2"
curl -sSfL -o "$WORK/Baloo2-wght.ttf" "$BASE/Baloo2%5Bwght%5D.ttf"
curl -sSfL -o "$WORK/OFL.txt" "$BASE/OFL.txt"
python3 -I -m fontTools.varLib.instancer "$WORK/Baloo2-wght.ttf" wght=800 --update-name-table -o "$WORK/Baloo2-ExtraBold-full.ttf"
python3 -I -m fontTools.subset "$WORK/Baloo2-ExtraBold-full.ttf" \
  --unicodes="U+0000-024F,U+0300-036F,U+2000-206F,U+20A0-20CF,U+2100-214F,U+2190-21FF,U+2200-22FF,U+25A0-25FF" \
  --layout-features='*' --name-IDs='*' --name-languages='*' --glyph-names --notdef-outline \
  --output-file="$OUT/Baloo2-ExtraBold.ttf"
cp "$WORK/OFL.txt" "$OUT/OFL.txt"
sha256sum "$WORK/Baloo2-wght.ttf" "$OUT/Baloo2-ExtraBold.ttf"

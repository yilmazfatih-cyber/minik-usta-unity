#!/usr/bin/env bash
# Brings the CI screenshots of the `ci-shots` branch into a local folder (paket C1; docs/CI.md section 7).
#   bash tools/ci-shots/fetch.sh [dest] [short-sha|latest]
# Default: dest = artifacts/ci-shots, folder = latest. Prints the manifest summary; then open the PNGs with any image viewer
# (<dest>/390x844/home.png, level_start.png, tutorial.png, drag_mid.png, landed.png, win.png, settings.png and the same under 360x800/).
# Needs only git and read access to the repository (the branch is a single orphan commit that CI force-pushes, so this fetches it
# fresh each time and does not keep history).
set -euo pipefail
dest="${1:-artifacts/ci-shots}"
which="${2:-latest}"
remote="${CI_SHOTS_REMOTE:-origin}"
branch="ci-shots"
git fetch --quiet --depth 1 "$remote" "+refs/heads/$branch:refs/remotes/$remote/$branch"
rm -rf "$dest"
mkdir -p "$dest"
git archive "$remote/$branch" "$which" | tar -x -C "$dest" --strip-components=1
python3 -I - "$dest/manifest.json" <<'PY'
import json, sys
d = json.load(open(sys.argv[1]))
print("commit %s  generated %s  ok=%s  dpr=%s" % (d["shortSha"], d["generatedAt"], d["ok"], d["dpr"]))
print("run:", d.get("run"))
for name, v in d["viewports"].items():
    print("  %s: %d/%d screens, errors=%s" % (name, len(v["screens"]), len(d["shots"]), v["errors"]))
for w in d.get("warnings", []):
    print("  warning:", w)
PY
echo "files in $dest"

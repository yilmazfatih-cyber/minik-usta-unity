#!/usr/bin/env bash
# Publishes the CI screenshots to the `ci-shots` branch (paket C1; docs/CI.md "Ekran goruntuleri").
#
# Layout of the branch (always ONE orphan commit, force-pushed, so the repository never grows by more than the kept runs):
#   latest/                 the newest run: <viewport>/<shot>.png, console-<viewport>.log, manifest.json
#   <short-sha>/            the same files, one folder per run (the newest KEEP runs are kept, older ones are dropped)
#   index.json              [{ "shortSha", "commit", "generatedAt", "ok", "run" }, ...] newest first
#   README.md               how to read the branch
# Anyone can read it with:  git fetch origin ci-shots && git show origin/ci-shots:latest/390x844/win.png > win.png
#
# Inputs (environment): SHOTS_DIR (output of shoot.mjs, must hold manifest.json), SHA (full commit sha), GITHUB_TOKEN (push access;
# optional for a file:// remote), REPO_URL (default https://github.com/$GITHUB_REPOSITORY.git), KEEP (default 8), BRANCH (default ci-shots).
# NEVER fails the workflow: every problem is a ::warning:: and the exit code is 0.
set -uo pipefail

SHOTS_DIR="${SHOTS_DIR:?SHOTS_DIR is required}"
SHA="${SHA:?SHA is required}"
BRANCH="${BRANCH:-ci-shots}"
KEEP="${KEEP:-8}"
REPO_URL="${REPO_URL:-https://github.com/${GITHUB_REPOSITORY:-unknown/unknown}.git}"
SHORT="${SHA:0:7}"

warn() { echo "::warning title=CI screenshots publish::$*"; }

if [ ! -f "$SHOTS_DIR/manifest.json" ]; then
  warn "no manifest.json in $SHOTS_DIR; nothing to publish"
  exit 0
fi

auth=()
if [ -n "${GITHUB_TOKEN:-}" ]; then
  basic="$(printf 'x-access-token:%s' "$GITHUB_TOKEN" | base64 -w0)"
  echo "::add-mask::$basic"
  auth=(-c "http.extraheader=AUTHORIZATION: basic $basic")
fi
GIT_ID=(-c user.name="github-actions[bot]" -c user.email="41898282+github-actions[bot]@users.noreply.github.com")

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
old="$work/old"
new="$work/new"
mkdir -p "$new"

# 1. what the branch holds now (a missing branch is the first run)
if git "${auth[@]}" ls-remote --exit-code --heads "$REPO_URL" "$BRANCH" >/dev/null 2>&1; then
  if ! git "${auth[@]}" clone --quiet --depth 1 --branch "$BRANCH" "$REPO_URL" "$old" 2>"$work/clone.err"; then
    warn "cannot clone $BRANCH: $(tr '\n' ' ' <"$work/clone.err" | head -c 300); starting a fresh branch"
    rm -rf "$old"
  fi
fi

# 2. the new tree: this run + latest + the newest KEEP-1 earlier runs (not this sha)
mkdir -p "$new/$SHORT" "$new/latest"
cp -R "$SHOTS_DIR"/. "$new/$SHORT/"
cp -R "$SHOTS_DIR"/. "$new/latest/"
if [ -d "$old" ] && [ -f "$old/index.json" ]; then
  python3 -I - "$old" "$new" "$SHORT" "$KEEP" <<'PY'
import json, os, shutil, sys
old, new, short, keep = sys.argv[1], sys.argv[2], sys.argv[3], int(sys.argv[4])
try:
    index = json.load(open(os.path.join(old, "index.json")))
except Exception:
    index = []
kept = 0
for e in index:
    s = e.get("shortSha")
    if not s or s == short or not os.path.isdir(os.path.join(old, s)):
        continue
    if kept >= keep - 1:
        break
    shutil.copytree(os.path.join(old, s), os.path.join(new, s))
    kept += 1
print("kept %d earlier run folder(s)" % kept)
PY
fi

# 3. index.json (newest first) and README.md
python3 -I - "$new" "$SHORT" <<'PY'
import json, os, sys
new, short = sys.argv[1], sys.argv[2]
rows = []
for name in os.listdir(new):
    m = os.path.join(new, name, "manifest.json")
    if name == "latest" or not os.path.isfile(m):
        continue
    try:
        d = json.load(open(m))
    except Exception:
        continue
    rows.append({"shortSha": d.get("shortSha", name), "commit": d.get("commit"), "generatedAt": d.get("generatedAt"), "ok": d.get("ok"), "run": d.get("run")})
rows.sort(key=lambda r: r.get("generatedAt") or "", reverse=True)
json.dump(rows, open(os.path.join(new, "index.json"), "w"), indent=2)
open(os.path.join(new, "index.json"), "a").write("\n")
PY
cat >"$new/README.md" <<'EOF'
# ci-shots

Screenshots of the Unity WebGL build, taken by CI on every push to `main` (workflow `Unity WebGL -> Pages`, job `CI screenshots`).
Software GL (SwiftShader) in headless Chromium, mobile user agent, device scale factor in `manifest.json`.
This branch is ONE orphan commit that is force-pushed on every run; do not build on it.

- `latest/` - the newest run. `latest/manifest.json` has the source commit, the time, the screen list and any errors.
- `<short-sha>/` - the same files for one run (the newest 8 runs are kept). `index.json` lists them, newest first.
- Files: `<viewport>/<shot>.png` for viewports `390x844` and `360x800` and shots `home`, `settings`, `level_start`, `tutorial`,
  `drag_mid`, `landed`, `win`; `console-<viewport>.log` (browser console tail); `<viewport>/_failure.png` when a viewport broke.
  `<viewport>/seq_king_wave_strip.png` and `seq_king_big_cheer_strip.png` (P4B-08): 24 pictures of the home king's arrival wave and of the
  win window's big cheer, taken in slow motion and cropped to where anything moved; `manifest.json` `viewports.<name>.motion` has the
  numbers (pictures, unique pictures, changed pairs, changes per game second) and `art3d` the 3D page decode times (P4B-05).

Read one: `git fetch origin ci-shots && git show origin/ci-shots:latest/390x844/win.png > win.png`
EOF

# 4. one orphan commit, force-pushed (the branch moves forward only by content, never by history)
git -C "$new" init --quiet -b "$BRANCH" || { warn "git init failed"; exit 0; }
git -C "$new" add -A
git -C "$new" "${GIT_ID[@]}" commit --quiet -m "ci shots $SHORT" -m "Source commit: $SHA" || { warn "git commit failed"; exit 0; }
git -C "$new" remote add origin "$REPO_URL"
for attempt in 1 2 3; do
  if git -C "$new" "${auth[@]}" push --quiet --force origin "HEAD:refs/heads/$BRANCH" 2>"$work/push.err"; then
    echo "published ci-shots/$SHORT and ci-shots/latest ($(find "$new/$SHORT" -name '*.png' | wc -l) png) to $BRANCH"
    exit 0
  fi
  echo "push attempt $attempt failed: $(tr '\n' ' ' <"$work/push.err" | head -c 300)"
  sleep $((attempt * 4))
done
warn "could not push $BRANCH after 3 attempts (check that the job has contents: write)"
exit 0

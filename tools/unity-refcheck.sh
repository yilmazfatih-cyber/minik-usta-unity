#!/usr/bin/env bash
# Reference-assembly check of the Unity layer (paket V1 item 8; REVIEW_PHASE3 Q5 open question g, approved by the orchestrator).
#
# Idea: besides the signature stubs (dotnet/UnityStubs), compile Assets/MinikUsta/Unity against Unity's REAL reference
# assemblies of the pinned editor line (6000.0.x), taken from the NuGet package UnityEngine.Modules, downloaded to artifacts/
# (git-ignored), never a project dependency.
#
# State (2026-11-01): nuget.org lists exactly one UnityEngine.Modules version, 2021.3.33 — no 6000.0.x. A 2021.3 reference set
# would check a different API surface (and uGUI is not in it at all: it would have to be compiled from the needle-mirror
# com.unity.ugui sources), so the check is SKIPPED and NOT part of the gate. Since run 41350760117 (docs/CI.md) the real
# Unity 6000.0.85f1 compile happens in GameCI on every push to main, which is the stronger check.
#
# Usage: tools/unity-refcheck.sh            exit 0 with "SKIPPED …" while no 6000.0.x package exists;
#                                           exit 2 with the version to wire up when one appears (then extend this script).
set -euo pipefail
PREFIX="${UNITY_REF_PREFIX:-6000.0.}"
INDEX="https://api.nuget.org/v3-flatcontainer/unityengine.modules/index.json"
json="$(curl -sSf "$INDEX")" || { echo "unity-refcheck: SKIPPED — cannot reach $INDEX"; exit 0; }
versions="$(printf '%s' "$json" | python3 -I -c 'import json,sys; print(" ".join(json.load(sys.stdin)["versions"]))')"
match="$(for v in $versions; do case "$v" in "$PREFIX"*) echo "$v";; esac; done | sort -V | tail -1)"
if [ -z "$match" ]; then
  echo "unity-refcheck: SKIPPED — nuget.org UnityEngine.Modules has [$versions], no ${PREFIX}x; the real 6000.0.85f1 compile is the GameCI run (docs/CI.md §6)"
  exit 0
fi
echo "unity-refcheck: UnityEngine.Modules $match is now on nuget.org — wire the compile step here (download to artifacts/unity-ref/$match, compile Assets/MinikUsta/Unity + uGUI sources against it) and add it to the gate"
exit 2

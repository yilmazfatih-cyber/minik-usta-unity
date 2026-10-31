#!/usr/bin/env bash
# Self-test of the Unity stub gate (REVIEW_PHASE3 P3-02): proves that the stubs reject a platform-limited type used without a guard.
#
# `Handheld` exists in Unity's player only for mobile targets (Android, iOS). The default dotnet build defines no UNITY_* symbol (the
# situation of the WebGL player), so an unguarded `Handheld` must fail there and pass only in the Android variant. If someone removes
# the `#if UNITY_ANDROID || UNITY_IOS` around the stub's Handheld, case 1 stops failing and this script exits non-zero.
#
# Usage: tools/stub-gate-selftest.sh        (needs only the dotnet SDK; takes ~30 s; leaves nothing behind)
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

UNGUARDED='class Probe { static void M() { UnityEngine.Handheld.Vibrate(); } }'
GUARDED='class Probe {
  static void M() {
#if UNITY_ANDROID || UNITY_IOS
    UnityEngine.Handheld.Vibrate();
#endif
  }
}'

# build_probe <name> <stub project> <extra define or empty> <source>; prints ok, fail (compile error CSxxxx) or broken (anything else)
build_probe() {
  local dir="$WORK/$1"
  mkdir -p "$dir"
  cat > "$dir/Probe.csproj" <<XML
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>disable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <DefineConstants>\$(DefineConstants);$3</DefineConstants>
  </PropertyGroup>
  <ItemGroup><ProjectReference Include="$ROOT/dotnet/$2" /></ItemGroup>
</Project>
XML
  printf '%s\n' "$4" > "$dir/Probe.cs"
  if dotnet build "$dir/Probe.csproj" -nologo -v q > "$dir/build.log" 2>&1; then echo ok
  elif grep -q 'error CS' "$dir/build.log"; then echo fail   # a real compile error, not a restore or tooling problem
  else echo broken; fi
}

failures=0
expect() { # <case> <expected> <actual>
  if [ "$2" = "$3" ]; then echo "PASS  $1 (build $3)"; else echo "FAIL  $1: expected build $2, got $3"; failures=$((failures + 1)); fi
}

expect "1 unguarded Handheld, default stubs (WebGL situation) must NOT compile" fail "$(build_probe c1 UnityStubs/UnityStubs.csproj '' "$UNGUARDED")"
expect "2 unguarded Handheld, Android stubs compiles"                        ok   "$(build_probe c2 UnityStubs.Android/UnityStubs.Android.csproj UNITY_ANDROID "$UNGUARDED")"
expect "3 guarded Handheld, default stubs compiles (branch skipped)"          ok   "$(build_probe c3 UnityStubs/UnityStubs.csproj '' "$GUARDED")"
expect "4 guarded Handheld, Android stubs compiles (branch taken)"            ok   "$(build_probe c4 UnityStubs.Android/UnityStubs.Android.csproj UNITY_ANDROID "$GUARDED")"

if [ "$failures" -ne 0 ]; then echo "stub gate self-test: $failures case(s) failed"; exit 1; fi
echo "stub gate self-test: all 4 cases as expected"

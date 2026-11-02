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

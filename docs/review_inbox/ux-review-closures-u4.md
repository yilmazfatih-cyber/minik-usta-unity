# Closures of the ux-review findings (paket U4, code-lead)

Source: `docs/review_inbox/ux-review.md` (R-01 .. R-07, measured on U2 CI shots). Tests: `UxU4Tests.cs` (names `U4_Rnn_...`).
Visual proof (ci-shots after the CI run) is recorded in the section "CI check" at the end.

| Id | Status | What changed | Test |
| --- | --- | --- | --- |
| R-01 | closed in code | `ScreenLayout.SceneBand` (bottom edge 24 px over the crane area, height min(gap - 48, 420), 0 at H <= 1920 and under 96 px, board width); `SceneBand` builds a warm radial glow (`fx_glow`) plus the level's structure as dark silhouette cells that turn into coloured blocks as placements fill them (placeholder until the scene is drawn); `LevelHud` syncs it with the HUD state | `U4_R01_*` (4) |
| R-02 | closed | locked tab names full opacity, white fill, `#2A1D8B` stroke; only the icon stays at 55 % (white on `#6858F8` = 4.8:1) | `U4_R02_*` |
| R-03 | closed | the offer counter is built only from the 2nd offer (`offer.N >= 2`), cream `#FFF6D2` + `#2A1D8B` stroke; the window is shorter on the 1st offer | `U4_R03_*` (2), `K29_*` updated |
| R-04 | closed in code | `UiKit.PlateTint` (`#9180FF`, multiplicative) on every `panel_plate` (settings rows, language row, level start plate): `#A090F8` x tint ~ `#5B48F7`, white 5.0:1. The AC-4 rule for stroked titles >= 52 px stays with design-lead | `U4_R04_*` (2) |
| R-05 | closed | pre-level slots follow K3 (`BoosterSlotRules.Visible`, 2 levels before the unlock); no slot: title and row hidden, panel 240 px shorter, picture and text block centred in the 320 px plate; slot 150 px, lock icon centred, label 34 px at `slot.Bottom + 8` (white + `#2A1D8B`, readable on the darker plate) | `U4_R05_*` (2) |
| R-06 | closed | HUD row 1 right padding 24 -> 40 px (`HudRow1RightPadPx`); trowel icon 36 px and badge 10 px inside the streak pill (overflow 0); gold chip >= 24 px from the moves plate and the pause tool (`GoldChipGapPx`). EN streak label shortened to "Streak" (`unity_en.json hud.streak`) so it stays at 34 px in the narrower capsule | `U4_R06_*` (2), `U2_P0_4_AC5_*` |
| R-07 | closed | "Cikis" (exit.leave) is `btn_secondary` like the quit window; pause tool 144 x 144 (`PauseToolPx`, right edge unchanged at 1032, centred on the moves plate) | `U4_R07_*`, `U2_P0_5_*` updated |

Notes
- `LayoutSettings.PauseToolPx` is not a tokens.json field (`ToolPx` stays 136 for the other tools); design-lead may add `kit.v3.screen.hud.pauseToolPx = 144`.
- The scene band is a placeholder (silhouette cells); the drawn scene (ART_UNITY) replaces `SceneBand.Cells` only.

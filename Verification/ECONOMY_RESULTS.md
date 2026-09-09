# Resource economy verification — 8 September 2026

Standalone tests passed: 15 economy assertions, 91 encirclement/pricing assertions, and 201,383 general simulation assertions. The command exited successfully. Test failures are caught and printed to the terminal without Windows unhandled-exception dialogs.

Economy checks verify 220 manpower / 40 gold / 20 industry per second at 80 tiles, higher/lower income after territory changes, zero income without land, exact fractional accumulation for a one-tile faction, independent faction balances, invalid-input rejection, and production at 524,288 cells. Captured territory contributes to the next tick. Wilderness costs one troop on plains while enemy plains still cost 12 per action.

Existing finite-force and encirclement scenarios were rebalanced for the new wilderness prices while retaining their conservation, exhaustion, discount, obstacle, and combat assertions. Enemy mountain combat still costs 180 normally and 90 when encircled.

Scripts compiled against the project's Unity references. The isolated Unity 6000.5.4f1 scene test passed (`SCENE_SMOKE_PASS` in `economy-smoke.log`), verifying live gold/industry production, HUD resource totals and income rates, numeric territory labels, input, rendering, camera controls, refunds, and automatic pocket capture.

Visually inspected `economy-advance.png`: the four-column resource bar is readable, separated from the terrain-cost readout, and does not overlap the map controls. Additional screenshots: `economy-overview.png`, `economy-pocket-before.png`, and `economy-pocket-after.png`. The test adds extra manpower and accelerates simulation time; screenshot totals are test balances, not starting balances.

No gameplay errors were logged. Unity's existing fresh-project SearchDatabase indexing exception remains an editor-only exception, as documented in earlier verification reports. Temporary isolated Unity caches were removed after the run. The actual game's package manifest and scene were unchanged.

Backup before this update: `../Backups/before-resource-economy.zip`. Tuning and current formulas: `../My project/README.md` and `../My project/Assets/Scripts/PlayerData.cs`.

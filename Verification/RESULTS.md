# Verification - 14 September 2026 (deterministic real-time simulation)

- Replaced the active fourteen-step WeGo driver with `GameClock` and `GameSimulation`. The clock starts unpaused at 1x, supports pause plus 1x/2x/4x, and converts unscaled wall time into fixed 0.12-second ticks without discarding backlog. Commands remain available while paused or running.
- `GameSimulation` now coordinates the existing economy, encirclement, division movement/combat, national defender losses, and fog-dirty reports. Movement/combat and pocket reduction retain the base cadence, enclosure/fog checks use four-tick periods, and economy accrues once per simulated second.
- Added 22 clock/simulation assertions. They pin continuous advancement without End Turn, pause/resume continuity, proportional wall-time speed scaling, the slower economy cadence, and identical full authoritative state after sixty simulated seconds at 1x/2x/4x under different render-delta patterns.
- Updated former WeGo tests and the Unity scene harness for continuous time, pause, speed controls, commands in both pause states, and per-simulated-second resource labels. Existing division combat, defensive frontage/entrenchment, transport, encirclement, scenario, and map suites remain in place.
- Standalone .NET 8 suites passed: 29 economy, 22 clock, 133 division, 31,934 duel-map, 142 encirclement, and 202,045 general simulation assertions. Unity 6000.5.4f1 compiler response files compiled the runtime cleanly and compiled the editor harness with only its existing Unity API deprecation warnings. A full scene execution could not start because the local Unity Licensing Client repeatedly disconnected while the main project was already open; this was an environment/licensing failure before the harness entered play mode.

# Verification - 14 September 2026 (script organization and responsibility split)

- Reorganized gameplay scripts into `Core`, `Economy`, `Units`, `Combat`, `Orders`, `Scenarios`, `Rendering`, `UI`, and `Input`. Every existing script moved with its Unity `.meta`, preserving serialized GUIDs; `MapController` remains the root scene composition component.
- Split scenario/editor vocabulary from `ScenarioMap` and moved the historical catalogue to `ScenarioTemplates`. Split designer pointer painting and infrastructure preview from `GameSetupUI`.
- Split planning input from `MapController`, routing and unit combat from `DivisionSystem`, and formation/resource presentation from `MapHUD`. These remain partials of their original Unity types, so scene serialization and public callers are unchanged.
- Added `ARCHITECTURE.md` with the dependency flow and extension rules for the next military-command interface work. Updated the standalone test project to compile the reorganized paths and extracted simulation files.
- Standalone .NET 8 suites passed unchanged: 29 economy, 158 division, 31,934 duel-map, 142 encirclement, and 202,045 general simulation assertions. Unity 6000.5.4f1 compiled the reorganized project and both `SCENE_SMOKE_PASS` and `DESIGNER_SMOKE_PASS` passed.

# Verification - 13 September 2026 (formation destruction, functional roads, smooth axes)

- Division-on-division combat now applies simultaneous casualties from both formations' pre-combat strengths. Terrain and entrenchment protect the holder, Reserve is more vulnerable to disruption, and the same pair cannot exchange fire twice because of roster iteration in one resolution step.
- Zero-strength formations are removed in that step, reported through `LastDestroyed`, and release their held frontage and its entrenchment. Per-faction formation losses feed an end-of-turn combat report rather than disappearing without feedback.
- Scenario road and railway waypoints now resolve into connected, walkable cell networks. They route around water and difficult terrain, lower pathfinding cost enough to attract attack and redeployment orders, and grant one extra movement cell while a formation is travelling on them. The designer and game render the same network the simulation uses.
- Roads and railways use a rounded terrain-following display path. Axes of advance receive three smoothing passes and additional corner geometry while preserving their real cell route and exact objective.
- Standalone .NET 8 suites passed: 29 economy, 158 division, 31,934 duel-map, 142 encirclement, and 202,045 general simulation assertions. New cases pin mutual losses, destruction reporting, released fieldworks, obstacle-aware road construction, road route preference, and attack/redeployment speed bonuses.
- Unity 6000.5.4f1 compiled the gameplay and editor harness successfully. `SCENE_SMOKE_PASS` additionally checked that Quick Play builds at least six transport corridors, the in-game road is a resolved multi-point line, and an axis-of-advance shaft has the smoothed point and corner geometry. `DESIGNER_SMOKE_PASS` covered template switching, painting/reset, and launching the edited map with rebuilt infrastructure. The refreshed `symbology.png` and Kursk designer render were inspected visually; the command axes round through turns and the road/rail lines curve through their terrain corridors.

# Verification — 13 September 2026 (defensive deployment and rigid front lines)

- Assigning a held frontage now gives the division a terrain-weighted deployment route to the nearest assigned cell. The route is restricted to friendly, walkable ground; during turn resolution the unit moves one cell per step, never spends combat strength, and only entrenches the full line after arriving. A lost or blocked corridor stops the deployment instead of turning a Defend order into an attack.
- Defensive overlays now use rigid rectangular teeth with square corners. They are thicker and sort above movement arrows: friendly lines are blue, configured allied lines are green, and observed enemy lines are red. Enemy movement intentions remain hidden and enemy defensive positions still respect fog of war.
- The supplied [NATO Military Map Symbols gallery](https://commons.wikimedia.org/wiki/NATO_Military_Map_Symbols#Friendly_Force_Unit_Symbols) is recorded in the project guide and renderer as the reference vocabulary for future unit frames, echelon marks, and role insignia.
- Standalone .NET 8 suites passed: 29 economy, 140 division, 31,934 duel-map, 142 encirclement, and 202,045 general simulation assertions. Unity 6000.5.4f1 compiled cleanly and `SCENE_SMOKE_PASS` covered the square geometry plus friendly, allied, and observed-enemy relationship colours. `defence-lines.png` was inspected at close zoom.

# Verification — 13 September 2026 (whole-half starts, 30-division armies, fog of war)

- Each nation now begins holding its **entire half** of the duel island — 31,784 cells apiece — rather than a small blob. Verified that the two nations together hold every walkable cell and that every walkable cell of the western half belongs to red.
- The opening order of battle is **thirty divisions a side**: fifteen posted along the frontier, each given a stretch of it to hold, and fifteen garrisoning the towns and facilities in depth. Both sides are placed from one mirrored list, so neither gets better ground. Frontier posts search nearby rows because the top and bottom of the island are open sea.
- The economy was rescaled for nations of this size. The old per-cell rates were tuned for an 80-cell opening and would have paid for twenty-two divisions a turn at 31,700 cells. Gold is now 0.06, civilians 0.08 and industry 0.03 per cell per step, which a test pins to between one and six divisions a turn at full territory; the observed figure is 26,699 gold a turn, about 2.7 divisions.
- `FogOfWar` gives each nation sight of the ground it holds, six cells past its own border, and a fourteen-cell circle around each division. Enemy formations standing in the dark are not drawn at all. `MapFogRenderer` paints the mask as one cell-resolution overlay texture, rebuilt at turn boundaries rather than by re-tinting terrain chunks. **F9** lifts the fog for debugging and a banner stays up while it is lifted.
- Symbology was reworked toward a real operations map: axis-of-advance arrows now taper toward the objective and finish in a **solid** arrowhead (a two-point line whose far width is zero renders as a filled triangle), and held lines are drawn as a **scalloped forward-line-of-troops trace** whose lobes bulge toward the enemy. Line weight and lobe depth both fall away as the division holding the line is stretched. The first pass was far too heavy and tangled at close zoom; widths were cut roughly threefold after inspecting the render.
- Standalone .NET 8 suites passed: 29 economy, 136 division, 31,934 duel map, 142 encirclement, and 202,045 simulation assertions. The economy suite now derives its expectations from the tuning constants rather than literals, so retuning cannot silently invalidate the rules.
- `SCENE_SMOKE_PASS` and `DESIGNER_SMOKE_PASS` in Unity 6000.5.4f1, including in-scene mirror and ocean checks, whole-half ownership, the thirty-division order of battle with its held lines, fog hiding enemy formations, and the F9 reveal round-tripping.

## Two harness defects found along the way

- `Capture` restored the canvas to overlay mode without re-laying it out, so the next screen-space hit test ran against stale camera-space corners. `BlocksMapInput` then reported a bottom-left click as being over the command panel, and selection silently failed. Diagnosed by logging the press-time inputs rather than the values at assertion time, which differed.
- `ApplyInput` queued a button release but never let the controller see it, so selection-on-release depended on frame timing. It now drives the controller after the release too.

# Verification — 13 September 2026 (mirrored duel map, military overlay, held lines)

- Quick Play is now the Mirror Duel: a 320 x 320 square island of 102,400 cells ringed by ocean and split down the middle. `TerrainGenerator.GenerateDuel` generates only the left half and mirrors it, re-mirroring after river carving so the drainage search cannot introduce a difference. Verified at 0 asymmetric cells, 0 land on the border, and a land split of exactly 31,734 per side.
- Rivers are carved two cells wide and there are far more of them (3,702 river and 322 ford cells against 466 and 42 before). River carving had a real bug: widening wrote into the next cell downstream, which terminated the walk after one step and left the map with 10 river cells. The course is now traced first and widened afterwards.
- Rivers are shaded flat instead of with relief, which was washing narrow channels into the ground they cut through, and given a darker blue. Bridges are drawn as a pale deck with dark rails, laid across the current rather than along it.
- `MapOrderRenderer` draws the plan as a military map would: an attack arrow per division with an open arrowhead, and a toothed defence trace per held line, teeth pointing away from home. It rebuilds only when the plan's signature changes, not per frame.
- Divisions can be assigned a stretch of front by right-dragging along owned ground. A group splits the line evenly. The entrenchment bonus is divided across the assigned width, so the standard 6-cell frontage gives the full +60 per cell, 12 cells gives +30 and 24 gives +15. Marching off releases the line.
- Starting armies rose to 155,000 military each, fifteen divisions plus a small reserve, identical on both sides.
- Production is quoted per turn in the HUD rather than per second, which had been left over from the real-time model.
- Standalone .NET 8 suites passed: 32 economy, 136 division, 33 duel map, 142 encirclement, and 202,045 simulation assertions. The duel suite checks squareness, the 100,000-cell floor, exact terrain and elevation mirroring, the ocean ring, biome variety, river and ford density, surviving elevation for relief, equal and mirrored starts, paired settlements/sites/routes, settlements on walkable ground, and a second seed.
- `SCENE_SMOKE_PASS` in Unity 6000.5.4f1, including in-scene mirror and ocean checks, equal starting ground and armies, the hold-line assignment weakening as it widens, and the defence trace and its teeth being drawn.

# Verification — 13 September 2026 (WeGo turns, stances, selection)

- The game no longer runs in real time. `TurnState` adds a WeGo turn: orders are given while the map stands still, and ending the turn resolves every unit's orders simultaneously over a fixed budget of 14 steps. Each step advances all divisions, squeezes every pocket, and accrues one period of production, so a turn is worth a fixed income however long the player thinks. Move orders, stance changes, and raising divisions are all refused while a turn resolves.
- Every real-time driver was removed from `MapController`: the per-second recruitment tick and the encirclement, pocket, and division interval timers are gone, along with their serialized intervals. The compile is warning-free.
- Divisions gained four stances. Attack advances and fights; Defend holds and raises the ground's defence by 60; Reserve holds, does not fight, and absorbs up to 500 replacements per step charged to the national military population; Redeploy covers two cells per step but refuses contested ground and stops short of it. Ordering a dug-in or reserve division somewhere puts it back on the attack, so stance and movement are never two separate chores.
- Selection now supports Shift to add (and to remove an already-selected division) and Ctrl+1-9 / 1-9 control groups. The commitment slider's old number-key shortcuts gave way to the groups; the slider itself remains.
- Standalone .NET 8 suites passed: 32 economy, 109 division (up from 63), 142 encirclement, and 202,045 simulation assertions. The new division cases cover each stance's behaviour, entrenchment arithmetic, refit stopping at full strength, refit against a bankrupt nation, redeploy speed and its refusal to fight, re-ordering a dug-in division, group stance changes, null-safety, and the full turn state machine including double-execute and double-complete.
- `SCENE_SMOKE_PASS` in Unity 6000.5.4f1. The scene harness ends the opening turn before checking production, asserts the turn rolls over, and while a turn resolves confirms the banner reports RESOLVING and that move orders, division raising, and stance changes are all refused.
- Fixed a one-frame lag: the turn banner was only refreshed from `Update`, so immediately after `EndTurn` it still claimed to be planning. The phase change now updates the HUD directly.

# Verification — 11 September 2026 (drag selection)

- Left-drag now draws a selection box and picks up every division inside it. Because left-drag was previously bound to camera panning, that binding was removed: panning is middle-drag, WASD, and the arrow keys. A press only becomes a box after travelling seven pixels, so a click and a drag are never confused.
- Selection became a set. `MapController.Selection` holds the whole group, `SelectedDivision` still reports the first for existing callers, and destroyed divisions drop out of the set each tick.
- Sending several divisions to one tile no longer stacks them. `DivisionSystem.OrderGroup` walks outward from the clicked cell and gives each division the nearest free walkable cell no other member of the group has taken.
- `DivisionSystem.FindInBox` normalises a rectangle dragged in any direction and never crosses factions. Both it and `OrderGroup` are pure C#, so drag selection is covered headlessly rather than only in the scene.
- Standalone .NET 8 suites passed: 32 economy, 63 division (up from 38), 142 encirclement, and 202,045 simulation assertions. The new division cases cover full-map boxes, reversed drags, a tight box around one unit, an empty box, cross-faction exclusion, group orders reaching every member with distinct destinations near the click, and empty/null groups reporting no work.
- `SCENE_SMOKE_PASS` and `DESIGNER_SMOKE_PASS` in Unity 6000.5.4f1. The scene harness box-selects the whole map, checks the selection is genuinely multi-unit, drives `ShowSelectionBox`/`HideSelectionBox` and asserts the rectangle and its four edges appear and then go away, and confirms a group order gives every member a distinct destination.

# Verification — 11 September 2026 (infantry divisions replace the border attack)

- Attacking is now unit-based. `Division` and `DivisionSystem` add infantry divisions that cost 10,000 military population each, are numbered per faction as they are raised, take terrain-weighted routes, claim the wilderness they march through plus a two-cell frontage, and pay for contested ground out of their own strength.
- Left-clicking the map no longer launches a border attack. Left-click selects a division, left-drag pans, right-click sends the selected division, B raises one, and Escape clears the selection. Pressing open ground deliberately leaves the selection alone so panning never costs you the unit you were about to send.
- The whole national-advance path was removed from `MapController`: `IssueOrder`, `CancelOrder`, `FinishOrder`, the per-frame advance loop, and the advance-tuning fields are gone. `ExpansionOrder` itself is retained because it still carries most of the simulation suite's coverage, but nothing in the game reaches it now.
- Starting military was raised so both sides take the field with a real order of battle: Quick Play 45,000 for the player (four divisions) and 32,000 per rival (three). Scenario templates already started between 36,000 and 52,000.
- Standalone .NET 8 suites passed: 32 economy, 38 division, 142 encirclement, and 202,045 simulation assertions. The division suite covers cost, per-faction numbering, proximity selection, routing, water refusal, frontage claiming, combat cost, a too-weak division holding instead of evaporating, and removal from the roster when destroyed.
- `SCENE_SMOKE_PASS` in Unity 6000.5.4f1. The scene harness now selects a division by clicking it, raises one and checks the 10,000 charge, sends it and steps the roster directly to confirm it marches and claims ground, and asserts that territory does **not** grow while nothing is ordered — the check that would fail if a border attack were still running.
- Inspected the rendered HUD: the counter draws as a faction-coloured disc carrying the NATO infantry symbol (box with crossed diagonals) above its unit number, and MILITARY reads 5,000 after four divisions are deducted from 45,000.
- Divisions now muster at least nine cells apart. The first render had all four stacked on the territory centre, hiding every counter but the last and burying the territory's own population label.

# Verification — 11 September 2026 (populations and ring assault)

- Pocket reduction now attacks from every direction at once. `PocketAssault.Step` took a single cheapest perimeter tile per tick; it now snapshots the frontier and gives every perimeter tile bordering the enclosing faction its own action in the same tick, so a pocket collapses a whole layer at a time. Defender reserve is charged once per attacking tile.
- Population split into civilians and military. Civilians grow at 30 + 0.5 x L per second; military never grows and is recruited at one civilian plus one coin per soldier, all-or-nothing. The HUD gained CIVILIANS and a RECRUIT control (also bound to R); territory labels on the map still show military only.
- Standalone .NET 8 suites passed: 32 economy assertions (civilian growth, coin-priced recruitment, fractional balances, free wilderness), 142 encirclement assertions, and 202,045 simulation assertions.
- New encirclement coverage asserts the ring behaviour directly: a 7x7 pocket loses exactly its 24-tile perimeter on the first step and its 16-tile perimeter on the second, and the shrunken core stays encircled.
- Two existing encirclement cases had premises that only held for one-tile-per-tick reduction and were restructured, not weakened. A breach of the outer wall can no longer free a partly reduced pocket, because the survivors are enclosed by the newly captured tiles; that case now breaches before the wave takes anything. The manual-order case moved to a wider pocket so a hand-issued capture still races the automatic wave.
- `SceneSmoke.Begin` and `DesignerSmoke.Begin` both passed. The scene harness now drives the RECRUIT button and asserts all three ledgers move together, that civilians grow, and that no territory label shows civilians.
- Compiled all 20 gameplay scripts against Unity 6000.5.4f1: no errors or warnings. Note that `compile.rsp` covers gameplay scripts only — an error in `Verification/Unity/Assets/Editor/SceneSmoke.cs` is caught by the Unity run, not by that fast check.

# Verification — 11 September 2026

Covers the scenario/infrastructure, paused-recruitment, and free-wilderness work.

- Compiled all 20 gameplay scripts against the installed Unity 6000.5.4f1 assemblies: no C# errors or warnings. `compile.rsp` had been missing `GameSetupUI`, `MapInfrastructureRenderer`, `ScenarioInfrastructure`, and `ScenarioMap`, so the fast compile check was not covering the new code; those four are now listed.
- Standalone .NET 8 suites passed: 16 economy assertions (paused recruitment, land-scaled gold/industry, fractional balances, free wilderness), 135 encirclement/terrain-pricing/conservation assertions, and 202,045 assertions covering scenario templates, map painting, generation, broad advances, obstacles, fords, combat, troop commitment, label centers, and 524,288-tile scaling.
- `SceneSmoke.Begin` and `DesignerSmoke.Begin` both passed in Unity 6000.5.4f1 batch mode against `Verification/Unity`.
- Fixed the designer preview layout defect: infrastructure route geometry was anchored to the preview's vertical midpoint, so roads and rails escaped the map frame and crossed the page subtitle. Routes now anchor at the overlay origin with absolute positioning, and a `RectMask2D` on the preview clips anything reaching the frame edge.
- Re-rendered and visually inspected all five designer previews plus `start-screen.png`. Typhoon, Stalingrad, Kursk, and Bagration each keep every road, railway, and site marker inside the map frame, with routes terminating on their settlement markers. Sandbox is correctly bare: no settlements, no infrastructure, no legend, and the paint-both-sides prompt.

## The `-nographics` trap

A batch run started with `-nographics` completes, reports `SCENE_SMOKE_PASS`, and writes PNGs — but every capture is a single flat colour, because there is no graphics device to render through. Every existing assertion checks GameObject presence, not pixels, so a blank render was indistinguishable from a good one. `SceneSmoke.Capture` now samples the captured image and fails the run when it has no colour variation. Run the harness with graphics enabled, as documented below.

# Verification — 7 September 2026

- Compiled all gameplay scripts against the installed Unity 6000.5.4f1 assemblies: no C# errors or warnings.
- Standalone .NET 8 simulation tests passed: broad directional fronts, terrain-weighted routes, water barriers, unreachable destinations, fords, combat costs and defense, resource pauses/resumption, invalid actions, neutral orders avoiding uninvolved factions, and territory-center placement.
- Generated and tested 256 × 160 maps for seeds 1847, 42, and 2026. Each had all ten terrain categories, four 80-cell starts, and a reachable rival attack destination.
- An isolated Unity 6000.5.4f1 scene test passed startup, terrain texture generation, screen/grid conversion, numeric labels, left-click orders, timed expansion, wheel zoom, middle-drag panning, right-click cancellation, and invalid destinations. Batch-mode input was driven through explicit Input System updates and the actual component handlers.
- Inspected `overview.png` and `advance.png`, rendered by Unity. The advance screenshot uses additional test manpower to finish the order quickly; the actual player starts with 1,600.

The isolated editor logged a UnityEditor.Search.SearchDatabase indexing exception on startup. It was excluded from game-runtime error tracking; there were no gameplay exceptions. The successful run is recorded in `unity-smoke.log` with `SCENE_SMOKE_PASS`.

The temporary Unity Library/Temp/Logs caches were removed after verification. The test project's Assets, Packages, ProjectSettings, and editor harness remain for reproduction. Before repeating that scene test after gameplay changes, refresh its copied scripts and scene from `../My project`.

Run simulation tests with a .NET 8 SDK:

```powershell
dotnet run --project Verification/SimulationTests.csproj
```

The isolated scene harness is `Unity/Assets/Editor/SceneSmoke.cs`. It can be run with Unity's `-batchmode -force-d3d11 -projectPath <absolute Verification/Unity path> -executeMethod SceneSmoke.Begin -logFile <output log>` arguments. It needs access to the local Unity licensing service.

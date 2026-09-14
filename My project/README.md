# Open War learning prototype

Open `Assets/Scenes/game.unity` and press Play. The title screen offers the original procedural Quick Play map or the Map Designer. The red territory is yours.

## Combat and transport

Enemy formations are now real combatants. When an advance meets an opposing counter or its active defensive frontage, both formations take casualties from their pre-combat strengths in the same resolution step. Terrain and fieldworks protect the holder, while reserves caught in combat are more vulnerable to disruption. A formation reduced to zero strength is removed immediately, releases the defensive line and entrenchment it maintained, and appears in the end-of-turn combat report. A surviving attacker must still reduce the ground garrison before it can occupy the cell. Battered divisions recover only in Reserve, using replacements from the national military pool.

Roads and railways are functional transport networks rather than straight decorative links between cities. Scenario waypoints are resolved into connected walkable cell paths that bend around water and difficult country. Attack, defensive deployment, and redeployment pathfinding prefer the lower movement cost of built transport; an attacking formation can cover up to two road cells per step, while redeployment can cover up to three. The designer and game render this same cell network as a rounded route, and movement arrows use a separately smoothed staff-work curve while retaining their exact underlying order.

## Starting screen and map designer

- **Quick Play** launches the **Mirror Duel**: a 320 × 320 square island of 102,400 cells, ringed by ocean and split down the middle. **Each nation begins holding its entire half** — about 31,700 cells — with **thirty divisions**: fifteen dug in along the frontier, each holding a stretch of it, and fifteen garrisoning the towns and facilities behind. Terrain, rivers, coastline, settlements, infrastructure, territory and order of battle are all mirrored, so the only difference between the sides is what each player does.
- **Map Designer** opens an editable 160 × 96 battlefield. Choose a scenario template, paint terrain or red/blue/neutral control directly on the preview, adjust the brush size, reset the current template, and select **Play Map** to launch it.
- Included historical templates are Operation Typhoon (1941), Stalingrad (1942), Kursk (1943), and Operation Bagration (1944). Each includes the major cities and operational towns in its campaign area plus roads, railways, airfields, rail hubs, supply depots, bridges, ports, and industrial centers. Geographic points use projected latitude/longitude and appear in both the designer and gameplay. Terrain, front shapes, and the polylines connecting infrastructure points remain gameplay-scaled.
- **Sandbox** is a completely neutral, all-plains 160 × 96 canvas with no pre-placed settlements, infrastructure, or front line. Paint at least one walkable red tile and one walkable blue tile before launching it.
- Terrain brushes include plains, forest, hills, mountains, water, river, ford, and desert. Water and river painting clears control because those cells block land movement.

Historical settlement and infrastructure coverage follows the campaign maps in the [West Point Digital History Center atlas](https://dhc.westpoint.edu/atlases/) and U.S. Army studies of [the 1940–1942 eastern campaign](https://history.army.mil/portals/143/Images/Publications/catalog/104-21.pdf), [Moscow to Stalingrad](https://history.army.mil/portals/143/Images/Publications/catalog/30-12.pdf), [Kursk](https://www.armyupress.army.mil/Portals/7/combat-studies-institute/csi-books/glantz2.pdf), and [Operation Bagration](https://www.armyupress.army.mil/Portals/7/Research-and-Books/Archives/2018/PDF/Sep-2018-WeavingTheTangledWeb.pdf). Coordinates are based on the [GeoNames geographical database](https://www.geonames.org/), with specific airfield locations cross-checked against published aerodrome records. Labels use wartime English spellings where appropriate. Route endpoints are geolocated, but their connecting polylines are intentionally schematic rather than a turn-by-turn reconstruction of every 1940s road or track segment.

## Controls

- **Left-click a division** to select it. Clicking open ground leaves the selection alone, so a stray click never costs you the unit you were about to send. Escape clears the selection.
- **Left-drag to draw a selection box** and pick up every one of your divisions inside it. A press only becomes a box once it has travelled a few pixels, so a click and a drag never get confused.
- **Hold Shift** while clicking or dragging to add to the current selection instead of replacing it. Shift-clicking a division that is already selected removes it.
- **Ctrl+1–9** stores the selection as a control group; **1–9** recalls it. Divisions destroyed since the group was stored simply drop out of it.
- **Right-click** sends the whole selection. Several divisions sent to one tile spread over the nearest free cells around it rather than stacking on the same square.
- **Right-drag along your own border** to assign the selection a stretch of front to hold. The line is split between the divisions selected, and holding it puts them on the defensive.
- **F1–F4** set the selection's stance: attack, defend, reserve, redeploy.
- **F9** lifts the fog for debugging. A banner stays on screen while it is lifted.
- **Space** or the END TURN button resolves the turn.
- **Middle-drag, WASD or the arrow keys** pan the map; left-drag is reserved for selection.
- **B** raises another infantry division for 10,000 military population; **R** recruits military population from civilians and coins.
- The commitment slider governs the share of the reserve spent on automatic pocket reduction. Its old number-key shortcuts gave way to control groups.
- Middle-drag pans at 3.2 times the previous speed.
- WASD or arrow keys pan; hold Shift for a further speed boost.
- Mouse wheel zooms toward the cursor. F returns to the map overview.
- Hover over a tile to see its terrain, occupation cost per action, and encirclement discount. The bottom-left pocket readout shows enclosed cells and troops assigned to automatic assaults.

The number over each territory is its military population. Labels follow an owned cell nearest the territory's average position. Infrastructure uses tan road lines, double-track railway lines, and coded point markers: `A` airfield, `H` rail hub, `D` depot, `B` bridge, `P` port, and `I` industrial center.

The top resource bar shows your military population, civilian population, gold (coin icon), industry (wrench icon), and owned land. Military never grows on its own — press R or use the RECRUIT button to buy soldiers with civilians and coins. Civilians, gold, and industry show income per second, start at zero, and accumulate as stockpiles. Production and spending buildings will come later.

## The turn: plan, then resolve

The game does not run in real time. It runs **WeGo**: you plan a turn with everything standing still, and when you end it every unit on the map — yours and the rivals' — carries out its orders *simultaneously*.

**Planning.** Nothing moves. Select divisions, send them, set their stances, raise new ones, recruit. The turn banner at the top reads `TURN n · PLANNING`.

**Resolving.** Press Space or END TURN and the turn plays out over a fixed budget of simultaneous steps. Each step advances every division at once, squeezes every pocket, and accrues one period of production. Orders are locked for the duration: move orders, stance changes, and raising divisions are all refused until the turn finishes. The banner counts the turn out as `RESOLVING n%`.

When the last step resolves, the turn number advances and planning opens again. Because production accrues per step rather than per second, a turn is worth a fixed amount of income no matter how long you spend thinking about it.

## Holding a line

Right-drag along ground you already hold and the selected divisions take responsibility for that stretch of front. The drag is traced cell by cell, only ground you own counts, and a group splits the line evenly between its members rather than piling onto the same cells.

Assigning a line also creates a defensive deployment route. During resolution, each division moves through friendly territory toward the nearest cell in its assigned frontage. It begins entrenching the full line after it arrives; if the route or destination is lost, it stops rather than turning the defensive order into an attack.

**A division covers a standard frontage of 6 cells at full strength.** Stretch it wider and the same division is spread over more ground: the entrenchment bonus is divided across the line, so twice the width is half the defence per cell. Assigning 6 cells gives the full +60 per cell, 12 cells gives +30, 24 cells gives +15. Deciding how thin to spread is the whole trade.

Held lines are drawn as a high-contrast, rigid crenellated trace with square corners and rectangular teeth facing away from the holding faction's territory. Friendly lines are blue, allied lines are green, and observed enemy lines are red. The line becomes somewhat thinner and shallower when its division is spread over a wider frontage, so an overextended front still looks overextended.

Ordering a division to move anywhere releases the line it was holding — it cannot march and hold at once. Ground lost since the line was drawn is simply skipped when the division digs in.

## Fog of war

A nation sees the ground it holds, **six cells beyond its own border**, and a **fourteen-cell circle around each of its divisions**. Everything else is dark, and enemy formations standing in the dark are not drawn at all — no counter, no strength, nothing.

Sight passes over water as readily as over land, so a coastline is not blind. The mask is recomputed at each turn boundary rather than every frame, which is all a WeGo game needs, and it is drawn as a single cell-resolution overlay texture rather than by re-tinting the terrain chunks.

**Press F9 to lift the fog** and watch what the enemy is actually doing. The fog is still computed underneath, so switching back restores the true picture immediately, and a banner stays on screen the whole time it is lifted so a debug run can never be mistaken for a real one.

## The military map

The plan is drawn the way a map overlay would show it, in map space so it pans and zooms with the terrain:

- **Axis-of-advance arrows** trace the route each of your divisions will take. The shaft *broadens* toward the objective and ends in a **solid arrowhead**, as on an operations map, rather than a thin line with an open V.
- **Forward-line-of-troops traces** run along each held line as a rigid square-toothed boundary. Blue means friendly, green means allied, and red means enemy. Enemy traces respect fog of war; seeing a defensive position does not reveal the enemy's movement orders.

Only friendly axes of advance are drawn, so rival intentions remain hidden. Observed held lines can be shown because they represent established defensive positions. The overlay is rebuilt only when the visible plan actually changes, not every frame.

Future unit frames, echelon marks, and role icons should use the [NATO Military Map Symbols reference gallery](https://commons.wikimedia.org/wiki/NATO_Military_Map_Symbols#Friendly_Force_Unit_Symbols) as their visual vocabulary. The current crossed rectangle is the infantry symbol, and the relationship colours above are the game's readability layer around that symbol set.

Rivers are carved two cells wide and shaded flat rather than with relief, so a channel stays readable where it cuts across open ground. Bridges are drawn as a pale deck with dark rails, laid across the current rather than along it.

## Stances

A stance is an order about *intent*, and it decides what a division does when the turn resolves.

| Stance | What it does |
| --- | --- |
| **Attack** | Advances on its destination, claiming wilderness and fighting for contested ground. Spends strength wherever it meets resistance. |
| **Defend** | Moves through friendly ground to the nearest cell of an assigned defensive line, then holds and digs in. With no assigned line, it entrenches its current position. |
| **Reserve** | Held out of the line. Does not move or fight, and absorbs up to 500 replacements per step from the national military population to rebuild strength. |
| **Redeploy** | Repositions without fighting. Covers two cells per step instead of one, but refuses to enter contested ground and stops short of it. |

Setting a division to reserve drops its move order. Setting it to defend drops any offensive route, but retains or creates the friendly-territory deployment route needed to reach an assigned line. Conversely, giving a dug-in or reserve division an ordinary destination puts it back on the attack, so you never have to change stance and issue a move as two separate steps.

Reserve is the only route back to full strength: a division never recovers on its own, and replacements are charged to the same military population that raising a new division draws on. Rebuilding a battered division is therefore a direct trade against fielding another one.

## Infantry divisions

Territory changes hands only where a division goes. There is no national border attack any more: clicking the map never launches one, and a nation grows exactly as far as it can march.

An **infantry division** costs **10,000 military population** and is raised on your own ground with **B** or the RAISE DIVISION button. Divisions are numbered per faction as they are raised — your 1st, 2nd, 3rd — and each is drawn on the map as a faction-coloured disc carrying the NATO infantry symbol (a box with crossed diagonals) above its unit number. The selected division's disc is ringed in gold.

Starting military is set so both sides take the field with a real order of battle: Quick Play gives you 45,000 (four divisions, with change) and each rival 32,000 (three). The scenario templates already start between 36,000 and 52,000, so they field three to five divisions each.

Select one or more divisions, then right-click where they should go; they move when the turn resolves. It takes a terrain-weighted route, so it prefers open ground over hills and mountains, and water and deep rivers block it entirely — fords still cross. Order a division into open country and it simply marches; order it onto enemy ground and it fights its way in.

As a division advances it claims the wilderness it stands on plus a two-cell frontage either side. Without that frontage a division would paint a one-cell thread nobody could see. Wilderness costs a division nothing, as it costs nothing for anyone else.

Contested ground is different. Entering an enemy tile is paid for out of the division's own strength at the usual terrain price, and the defender loses reserve for every attack. A division that cannot afford the next attack holds its position and drops its orders rather than evaporating; a division reduced to nothing leaves the roster on the next step. Strength is never replenished automatically — raising a fresh division is the only way to restore lost weight.

## Expansion and combat

Divisions take terrain-weighted routes to their destination. Water and deep rivers block land movement, while striped fords allow river crossings. The `ExpansionOrder` corridor search that used to drive border attacks is retained as a tested component, but nothing in the game reaches it any more.

The manual front takes one action every 0.035 seconds. Every walkable wilderness tile costs zero manpower, while terrain still influences route selection. Enemy occupation retains its terrain prices. Each paid enemy action deals 12 damage to the tile's garrison and deducts up to 12 from the defender's reserve. The three rivals defend and reduce their own encircled pockets, but do not yet issue strategic offensive orders.

| Terrain | Wilderness | Enemy cost per action |
| --- | ---: | ---: |
| Plains (`Land`) | 0 | 12 |
| Coast | 0 | 14 |
| Forest | 0 | 18 |
| Desert | 0 | 20 |
| Ford | 0 | 22 |
| Hills | 0 | 24 |
| Snow | 0 | 30 |
| Mountains | 0 | 36 |

Water and deep rivers are impassable. Enemy plains take one action; forests take two; hills and fords three; snow four; mountains five. Thus a fresh enemy mountain costs 180 troops normally. The hover readout quotes **per action**, not the total required to remove its remaining garrison.

## Two populations

The population is split in two, and only one of them grows on its own.

- **Civilians** grow every simulation second with owned land. They are the pool recruits are drawn from, never a combat resource.
- **Military** is the only pool combat can spend. It never grows passively. Every soldier must be recruited.

Recruiting converts civilians into soldiers and charges coins for them: **one soldier costs one civilian and one coin.** Press **R** or use the **RECRUIT** button under the commitment slider to enlist every soldier you can currently afford. The button shows that number, and recruitment is all-or-nothing, so a batch can never leave the two ledgers disagreeing about how many soldiers exist.

The rates are chosen so neither number is ever decorative. Coins accrue at half a cell per second while civilians accrue at half a cell plus a flat base, so a small nation is limited by its treasury and a large one is squeezed by both at once.

| Resource | Income per second | At the starting 80 tiles |
| --- | --- | ---: |
| Civilians | 30 + 0.5 × L | 70 |
| Military | 0 (recruited only) | 0 |
| Gold | 0.5 × L | 40 |
| Industry | 0.25 × L | 20 |

Both populations appear in the top bar, alongside gold, industry, and owned land. **The number drawn inside a territory on the map is the military population only** — the civilian count is a national figure and is not painted on the ground.

Civilians, gold, and industry use decimal balances, so even one tile produces 0.5 gold and 0.25 industry each second without losing fractions. Captures and land losses change production at the next economy tick, and existing balances remain intact. Starting military is 1,600 for you and 900 for rivals, each beginning with 1,500 civilians and 80 cells on Quick Play; scenario templates carry their own starting reserves. Industry still has no spending mechanic. All four factions use these rules.

`CiviliansPerCell`, `CivilianBase`, `GoldPerCell`, and `IndustryPerCell` tune production; `CiviliansPerRecruit` and `GoldPerRecruit` set the conversion price. `TickEconomy` applies one second of production using the indexed territory count, and `AffordableRecruits` reports whichever of the two inputs runs out first. `TerrainRules.MovementCost` weights route selection, `ExpansionCost` keeps walkable wilderness free, and `AttackCost` tunes enemy occupation separately.

## Encirclement prototype

A pocket is non-owned land enclosed by a closed ring of **one faction's territory**, using the same four-neighbor connectivity as movement. Wilderness and enemy land both qualify. Detection floods outward-connected space through water as well as land, so an ordinary coast, river bank, separate island, or an opening onto the map edge does not count as a closed ring. Other factions cannot complete your ring for you.

All **wilderness costs zero manpower** on every walkable terrain. Encircled wilderness is likewise captured automatically with an empty reserve and without temporarily committing troops. The hover readout marks every wilderness tile `FREE` and still identifies an encircled free capture.

Encircled **enemy land costs 50% of normal**, rounded up with a minimum of one troop per action. A fresh enemy mountain costs 18 per action, or 90 total. Manual orders and automatic assaults use the same cost calculation, and enemy garrisons still resist. Detection identifies pockets; capture continues outward from connected owned land on the existing simulation ticks.

A surrounded pocket is attacked **from every direction at once**. Each tick, every tile of the pocket's perimeter that borders the enclosing faction takes its own action, so the pocket collapses inward a whole layer at a time instead of being eaten from whichever corner is cheapest. A 7 × 7 pocket loses its 24-tile perimeter in one tick, then its 16-tile perimeter in the next. Defenders lose reserve once per attacking tile, so a pocket squeezed from all sides bleeds far faster than the old single-point assault.

One consequence is worth knowing: once a ring attack has taken a layer, the survivors are enclosed by the newly captured tiles, so breaching the original outer wall no longer frees them.

The enclosing faction reduces accessible pocket cells from their edges on this schedule, one ring per 0.1 seconds. It handles multiple pockets without another click. Free wilderness waves use no troops. When they reach enemy land, a paid wave forms using the slider's current percentage of the **home reserve** (rivals use 50%), provided it can afford combat. That force is reserved up front. A paid wave's commitment stays fixed; subsequent waves use the latest slider setting. With passive recruitment paused, no new manpower enters the reserve. Unspent troops return when a paid wave completes, cannot afford another action, or loses its encirclement. Free waves never generate refunds. Right-click recalls your manual advance; pocket reduction remains automatic.

Losing territory immediately invalidates that faction's pocket discounts. Detection checks changed enclosures every 0.75 seconds and restores any still-valid pockets. Captures inside a known pocket update its membership without another flood. Detection examines only the enclosing faction's padded bounding rectangle and skips unchanged factions. It still runs on the main thread, so very large changing enclosures can cause a short pause.

Ground access is still required: an enclosed island separated from all your land by unforded water receives the discount but cannot be occupied until a ground connection exists. Transport, supplies, divisions/brigades, and combat planning are future systems. `EncirclementSystem` and its internal `PocketAssault` keep this temporary territorial rule separate from manual orders and rendering so those systems can replace it later.

## Terrain and tuning

The map is 1,024 × 512 cells: **524,288 tiles**, including water, with a deterministic seed of 1847. That is 12.8 times the previous 256 × 160 map. Elevation and moisture create sea, coasts, plains, forests, hills, mountains, desert, and snow. Rivers drain toward the ocean and have periodic fords. Four display pixels per cell add terrain detail without changing the simulation grid. Territory colors preserve terrain shading and use visible border lines.

The renderer divides the world into 32 sections of 128 × 128 cells. Captures upload only changed sections, including neighboring sections when a border crosses a seam. Territory counts and coordinate sums update when ownership changes; labels search only their faction's cells and ignore changes to other factions. This keeps economy ticks and ordinary captures from scanning or uploading the whole map. Initial map generation and a long-distance route search still run on the main thread.

Select Map to tune Width, Height, Terrain Seed, Advance Radius (front width), Advance Interval (seconds/action), Front Spread (how much longer than the best approach a route may be and still join), Max Fronts, Encirclement Check Interval, and Pocket Assault Interval. Terrain costs and the encirclement multiplier live in `TerrainRules.cs`. Front Spread also sets how far the route search explores, so lowering it shortens the pause when you click a distant target. All starts are placed on the largest connected land region. Select Main Camera to tune Scroll Zoom Strength, Drag Speed, Keyboard Speed, and zoom limits. The scene saves existing settings explicitly; changing a field's code default alone does not replace saved Inspector values.

The commitment panel is built in code against the existing canvas, so no scene edits are needed to see it.

## Code map

Gameplay scripts are organized by responsibility under `Assets/Scripts`: `Core`, `Economy`, `Units`, `Combat`, `Orders`, `Scenarios`, `Rendering`, `UI`, and `Input`. `MapController.cs` stays at the root as the scene composition root. Unity `.meta` files moved with their scripts, so serialized scene references retain their original GUIDs.

The main extension path is now `MapControllerInput` -> `DivisionRouting` -> turn resolution in `MapController` -> `DivisionCombat` -> the rendering and HUD layers. Large Unity components use partial classes only to preserve their existing serialized identity while separating source responsibilities; no gameplay object was replaced or renamed.

- `MapData`: cells, indexed ownership/counts, claim/combat validation, and your territory-center algorithm restricted to owned cells.
- `TerrainGenerator` / `TerrainRules`: seeded landscape generation and terrain gameplay effects.
- `ITroopSource`: the contract for anything that pays for an advance, so `MapData` can charge a nation or a detached force without knowing which it holds.
- `PlayerData`: military reserve, civilian population, decimal gold/industry balances, income rates, coin-priced recruitment, and economy ticks. `CommittedForce`: the finite pool one order carries, and the recall that returns survivors.
- `Division` / `DivisionStance` / `DivisionSystem`: formation state and roster lifecycle. `DivisionRouting` owns individual, group, and defensive pathfinding; `DivisionCombat` owns opposing-unit detection, simultaneous casualties, and loss reporting.
- `TurnState`: the WeGo turn — planning versus resolving, the per-turn step budget, and the roll-over into the next turn.
- `DuelMap`: the mirrored Quick Play island — square ocean-ringed terrain, mirrored starting territory, and paired settlements and infrastructure.
- `MapOrderRenderer`: tapered friendly axes of advance plus relationship-coloured, square-toothed defensive lines, rebuilt only when the visible plan changes.
- `FogOfWar` / `MapFogRenderer`: what a nation can see, and the cell-resolution mask that dims the rest.
- `ExpansionOrder` / `GridHeap`: the former border-attack corridor search, still covered by tests but no longer wired to input.
- `EncirclementSystem` / `PocketAssault`: changed-enclosure flood detection, pocket membership, paid automatic reduction, and refunds.
- `StartingTerritories`: connected, separated starting territories.
- `ScenarioMap` / `ScenarioTypes`: editable battlefield state and editor vocabulary. `ScenarioTemplates` contains the four Eastern Front layouts and blank Sandbox; `ScenarioInfrastructure` and `MapTransportNetwork` contain logistics metadata and resolved transport paths.
- `GameSetupUI`: title and designer page composition. `MapDesignerCanvas` translates pointer input into paint coordinates, while `MapDesignerInfrastructureView` renders the functional road and logistics preview.
- `MapController`: scene startup and turn resolution. `MapControllerInput` owns selection, control groups, hold-line authoring, orders, and stances. Nothing on the map advances outside a resolving turn.
- `MapRenderer` / `MapInfrastructureRenderer`: terrain colors, relief, ownership borders, affected-section updates, and semantic road/rail overlays.
- `MapHUD`: HUD composition, map labels, turn controls, and the commitment panel. `MapHUDFormations` owns division counters and the selection box; `MapHUDResources` owns the economy bar, procedural coin/wrench icons, recruitment, and resource refresh.
- `CameraController`: navigation, middle-drag panning, and cursor-centered zoom.

Standalone simulation checks live in `../Verification/SimulationTests.csproj`. Run them with a .NET 8 SDK. The existing scripts and scene before these changes are backed up in `../Backups/before-directional-expansion.zip`.

The staged unit-deployment design is documented in `UNIT_SPAWN_PLAN.md`. Infrastructure sites already declare land, air, and naval spawn eligibility, but the current build intentionally creates no units.

The version immediately before increasing the map to 524,288 tiles is in `../Backups/before-large-map.zip`. After updating, reopen `game.unity` outside Play mode to load its saved dimensions. F fits the larger world automatically; zoom in to inspect the small starting territories.

The version before encirclement and directional continuation is in `../Backups/before-encirclement.zip`. Encirclement regression scenarios are in `../Verification/EncirclementTests.cs`; the isolated Unity scene harness is in `../Verification/Unity/Assets/Editor/SceneSmoke.cs`.

The version before the faster economy, cheaper wilderness, gold, and industry is in `../Backups/before-resource-economy.zip`. Economy regression scenarios are in `../Verification/EconomyTests.cs`.

# Open War learning prototype

Open `Assets/Scenes/game.unity` and press Play. The red territory is yours.

## Controls

- The commitment slider in the bottom-right sets the share of your available troops that the next advance takes with it. Redirecting includes survivors recalled from your current manual order. Number keys 1–9 select 10%–90% and 0 selects 100%.
- Left-click reachable wilderness or an enemy tile to issue an advance. Every stretch of your border with a comparable approach to that tile joins in, so one click can open several prongs at once. A new valid click replaces the previous order and recalls its survivors first.
- Right-click recalls the current advance and returns its surviving troops. Invalid destinations leave the current order running.
- Middle-drag pans at 3.2 times the previous speed.
- WASD or arrow keys pan; hold Shift for a further speed boost.
- Mouse wheel zooms toward the cursor. F returns to the map overview.
- Hover over a tile to see its terrain, occupation cost per action, and encirclement discount. The bottom-left pocket readout shows enclosed cells and troops assigned to automatic assaults.

The number over each territory is its available manpower. Labels follow an owned cell nearest the territory's average position.

The top resource bar shows your manpower, gold, industry, and owned land, including income per second. Gold and industry start at zero and accumulate as stockpiles; production and spending buildings will come later.

## Troop commitment

An advance is not funded by the nation as it goes. Issuing an order detaches a fixed force from your reserve, and that order spends only what it carries. The reserve drops by the committed amount the moment you click, and the status line reports how much of the force is still in the field.

The click sets a direction. After reaching it, the advance continues along the heading from your territory's center through the clicked cell, until its corridor has no eligible, reachable land left or its force cannot pay for the next action. Survivors return to the reserve in either case, including a right-click recall. Even a remainder too small for another tile is returned. A map edge or an impassable barrier ends the advance; taking the clicked tile does not.

Recruits go to the reserve, never to a force already in the field, so a spent advance stays spent until you commit again. This makes the slider the main tactical decision: a large commitment takes more ground in one push, while a small one leaves troops at home for the next opportunity.

## Attacking on several fronts

An order is not a single arm. The route search runs backwards from the destination across neutral and enemy ground, and every stretch of your border it reaches is a front already facing that tile. The cheapest approach sets the standard: any other stretch whose route costs no more than Front Spread times that best approach opens a prong of its own, up to Max Fronts of them. Prongs must be at least two corridor widths apart, so neighbouring border tiles do not produce duplicates of the same push.

Because the tolerance is a ratio, the shape of an attack follows its range. A destination just past your border admits only the stretches right beside it and you get a tight shove. A destination across the map admits far more of your border and you get a wide pincer that wraps around lakes and mountains on both sides.

Each prong measures its progress from its own origin, so they all step off together and advance in step rather than one finishing before the next begins. Prongs that converge share a tail once they meet. The primary corridor continues past the clicked tile in the selected direction.

Spreading a fixed force over more prongs does not buy more ground, only a broader face. The corridor is several times larger, so a commitment that would have carried one prong to a distant target will run out partway with six. Widening the attack and paying for it are two separate decisions.

## Expansion and combat

An order finds terrain-weighted routes from your existing territory to the clicked tile and advances through a corridor around each of them, then continues beyond it. Captures always share an edge with your territory. Water and deep rivers block land movement, while striped fords allow river crossings. Orders toward neutral land do not attack unrelated factions along the way.

The manual front takes one action every 0.035 seconds. Wilderness is captured in one paid action and now costs approximately 75–83% less than before. Enemy occupation has its own terrain prices, so the wilderness reduction does not reduce garrison combat costs. Each paid enemy action deals 12 damage to the tile's garrison. Combat also deducts up to 12 from the defender's reserve per action. The three rivals defend and reduce their own encircled pockets, but do not yet issue strategic offensive orders.

| Terrain | Wilderness | Enemy cost per action |
| --- | ---: | ---: |
| Plains (`Land`) | 1 | 12 |
| Coast | 2 | 14 |
| Forest | 2 | 18 |
| Desert | 3 | 20 |
| Ford | 3 | 22 |
| Hills | 4 | 24 |
| Snow | 5 | 30 |
| Mountains | 6 | 36 |

Water and deep rivers are impassable. Enemy plains take one action; forests take two; hills and fords three; snow four; mountains five. Thus a fresh enemy mountain costs 180 troops normally. The hover readout quotes **per action**, not the total required to remove its remaining garrison.

## Economy

Every simulation second, each faction earns income from its current owned-cell count, L:

| Resource | Income per second | At the starting 80 tiles |
| --- | --- | ---: |
| Manpower | 140 + L | 220 |
| Gold | 0.5 × L | 40 |
| Industry | 0.25 × L | 20 |

Recruitment is approximately four times its previous rate of 35 + floor(L / 4). Gold and industry increase directly in proportion to owned land. Captures and land losses change production at the next economy tick. A faction with no land produces nothing, including no base recruitment. Existing balances remain intact.

Gold and industry use decimal balances, so even one tile produces 0.5 gold and 0.25 industry each second without losing fractions. Their stockpiles currently have no spending mechanic. All four factions use these rules. Starting manpower remains 1,600 for you and 900 for rivals, with 80 starting cells each.

Tune `BaseRecruitment`, `RecruitmentPerCell`, `GoldPerCell`, and `IndustryPerCell` in `PlayerData.cs`. `TickEconomy` applies one second of production; the controller calls it using the indexed territory count. `TerrainRules.ExpansionCost` tunes wilderness and `AttackCost` tunes enemy occupation separately.

## Encirclement prototype

A pocket is non-owned land enclosed by a closed ring of **one faction's territory**, using the same four-neighbor connectivity as movement. Wilderness and enemy land both qualify. Detection floods outward-connected space through water as well as land, so an ordinary coast, river bank, separate island, or an opening onto the map edge does not count as a closed ring. Other factions cannot complete your ring for you.

Encircled **wilderness costs zero manpower** on every terrain for the enclosing faction. It is captured automatically even with an empty reserve, without temporarily committing troops. Wilderness outside an encirclement keeps its normal terrain cost. The hover readout marks surrounded wilderness as `FREE CAPTURE`.

Encircled **enemy land costs 50% of normal**, rounded up with a minimum of one troop per action. A fresh enemy mountain costs 18 per action, or 90 total. Manual orders and automatic assaults use the same cost calculation, and enemy garrisons still resist. Detection identifies pockets; capture continues outward from connected owned land on the existing simulation ticks.

The enclosing faction automatically reduces accessible pocket cells from their edges, at one action per 0.1 seconds. It handles multiple pockets without another click. Free wilderness waves use no troops. When they reach enemy land, a paid wave forms using the slider's current percentage of the **home reserve** (rivals use 50%), provided it can afford combat. That force is reserved up front. A paid wave's commitment stays fixed; subsequent waves use the latest slider setting. Recruits continue entering the home reserve. Unspent troops return when a paid wave completes, cannot afford another action, or loses its encirclement. Free waves never generate refunds. Right-click recalls your manual advance; pocket reduction remains automatic.

Losing territory immediately invalidates that faction's pocket discounts. Detection checks changed enclosures every 0.75 seconds and restores any still-valid pockets. Captures inside a known pocket update its membership without another flood. Detection examines only the enclosing faction's padded bounding rectangle and skips unchanged factions. It still runs on the main thread, so very large changing enclosures can cause a short pause.

Ground access is still required: an enclosed island separated from all your land by unforded water receives the discount but cannot be occupied until a ground connection exists. Transport, supplies, divisions/brigades, and combat planning are future systems. `EncirclementSystem` and its internal `PocketAssault` keep this temporary territorial rule separate from manual orders and rendering so those systems can replace it later.

## Terrain and tuning

The map is 1,024 × 512 cells: **524,288 tiles**, including water, with a deterministic seed of 1847. That is 12.8 times the previous 256 × 160 map. Elevation and moisture create sea, coasts, plains, forests, hills, mountains, desert, and snow. Rivers drain toward the ocean and have periodic fords. Four display pixels per cell add terrain detail without changing the simulation grid. Territory colors preserve terrain shading and use visible border lines.

The renderer divides the world into 32 sections of 128 × 128 cells. Captures upload only changed sections, including neighboring sections when a border crosses a seam. Territory counts and coordinate sums update when ownership changes; labels search only their faction's cells and ignore changes to other factions. This keeps recruitment and ordinary captures from scanning or uploading the whole map. Initial map generation and a long-distance route search still run on the main thread.

Select Map to tune Width, Height, Terrain Seed, Advance Radius (front width), Advance Interval (seconds/action), Front Spread (how much longer than the best approach a route may be and still join), Max Fronts, Encirclement Check Interval, and Pocket Assault Interval. Terrain costs and the encirclement multiplier live in `TerrainRules.cs`. Front Spread also sets how far the route search explores, so lowering it shortens the pause when you click a distant target. All starts are placed on the largest connected land region. Select Main Camera to tune Scroll Zoom Strength, Drag Speed, Keyboard Speed, and zoom limits. The scene saves existing settings explicitly; changing a field's code default alone does not replace saved Inspector values.

The commitment panel is built in code against the existing canvas, so no scene edits are needed to see it.

## Code map

- `MapData`: cells, indexed ownership/counts, claim/combat validation, and your territory-center algorithm restricted to owned cells.
- `TerrainGenerator` / `TerrainRules`: seeded landscape generation and terrain gameplay effects.
- `ITroopSource`: the contract for anything that pays for an advance, so `MapData` can charge a nation or a detached force without knowing which it holds.
- `PlayerData`: manpower reserve, decimal gold/industry balances, income rates, and economy ticks. `CommittedForce`: the finite pool one order carries, and the recall that returns survivors.
- `ExpansionOrder` / `GridHeap`: one search that harvests every qualifying front, a route and corridor per prong, the advancing front, and the committed force it spends.
- `EncirclementSystem` / `PocketAssault`: changed-enclosure flood detection, pocket membership, paid automatic reduction, and refunds.
- `StartingTerritories`: connected, separated starting territories.
- `MapController`: input, recruitment, timed actions, commitment math, and destination graphics including one route line per prong.
- `MapRenderer`: terrain colors, relief, ownership borders, and updates to affected render sections.
- `MapHUD`: numeric territory labels, resource totals and production rates, order feedback, terrain cost/pocket readouts, and the commitment slider.
- `CameraController`: navigation and cursor-centered zoom.

Standalone simulation checks live in `../Verification/SimulationTests.csproj`. Run them with a .NET 8 SDK. The existing scripts and scene before these changes are backed up in `../Backups/before-directional-expansion.zip`.

The version immediately before increasing the map to 524,288 tiles is in `../Backups/before-large-map.zip`. After updating, reopen `game.unity` outside Play mode to load its saved dimensions. F fits the larger world automatically; zoom in to inspect the small starting territories.

The version before encirclement and directional continuation is in `../Backups/before-encirclement.zip`. Encirclement regression scenarios are in `../Verification/EncirclementTests.cs`; the isolated Unity scene harness is in `../Verification/Unity/Assets/Editor/SceneSmoke.cs`.

The version before the faster economy, cheaper wilderness, gold, and industry is in `../Backups/before-resource-economy.zip`. Economy regression scenarios are in `../Verification/EconomyTests.cs`.

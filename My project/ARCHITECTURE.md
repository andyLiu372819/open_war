# Open War architecture

The runtime is organized around one directional flow:

```text
Unity Update / input (wall time and player commands)
  -> GameClock (pause, speed, fixed ticks)
  -> GameSimulation (deterministic cadence coordination)
  -> fixed simulation ticks
  -> movement, combat, territory, and economy state
  -> map renderers and HUD presentation
```

## Script folders

- `Core`: map cells, terrain generation/rules, ownership data, and shared grid utilities.
- `Economy`: national resources and troop-spending contracts.
- `Units`: division state, roster lifecycle, routing, stances, and formation combat.
- `Combat`: encirclement and the retained legacy territorial advance simulation.
- `Orders`: legacy order-related compatibility code; `TurnState` is deprecated and inactive.
- `Simulation`: the authoritative fixed-timestep clock and gameplay-system coordinator.
- `Scenarios`: editable scenario state, templates, infrastructure metadata, and functional transport networks.
- `Rendering`: terrain, fog, transport, and military-order overlays.
- `UI`: starting screen, map designer, HUD, formation counters, and resource presentation.
- `Input`: camera navigation and order input, available while paused or running.

`MapController.cs` remains at `Assets/Scripts` as the Unity scene composition root. Its serialized identity is unchanged.

`GameClock` alone owns simulation time. Unity supplies unscaled frame deltas; pause and 1x/2x/4x speed determine how those deltas fill its accumulator. `GameSimulation` advances only when a fixed tick is consumed. Movement/combat and pocket reduction run every base tick, encirclement/fog use four-tick periodic checks (fog also refreshes on dirty events), and economy runs once per simulated second. A per-frame consumption cap may defer backlog but never discards it.

## Extension rules

- New command-interface code should create or edit orders through `MapControllerInput` and the public `DivisionSystem` API; it should not mutate renderer objects.
- Movement/pathfinding changes belong in `DivisionRouting`; casualty doctrine belongs in `DivisionCombat`.
- Scenario definitions belong in `ScenarioTemplates`; mutable scenario state stays in `ScenarioMap`.
- Simulation code must not depend on Unity UI or renderer classes. The standalone verification project compiles this layer directly.
- Add future subsystem cadences to `GameSimulation` in simulation time; never schedule authoritative gameplay from render frames.
- Move every Unity script together with its `.meta` file. Folder organization alone must not change a GUID.

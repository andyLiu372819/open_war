# Open War architecture

The runtime is organized around one directional flow:

```text
player input
  -> formation orders and defensive assignments
  -> WeGo turn resolution
  -> movement, combat, territory, and economy state
  -> map renderers and HUD presentation
```

## Script folders

- `Core`: map cells, terrain generation/rules, ownership data, and shared grid utilities.
- `Economy`: national resources and troop-spending contracts.
- `Units`: division state, roster lifecycle, routing, stances, and formation combat.
- `Combat`: encirclement and the retained legacy territorial advance simulation.
- `Orders`: the WeGo turn state machine.
- `Scenarios`: editable scenario state, templates, infrastructure metadata, and functional transport networks.
- `Rendering`: terrain, fog, transport, and military-order overlays.
- `UI`: starting screen, map designer, HUD, formation counters, and resource presentation.
- `Input`: camera navigation and planning-order input.

`MapController.cs` remains at `Assets/Scripts` as the Unity scene composition root. Its serialized identity is unchanged.

## Extension rules

- New command-interface code should create or edit orders through `MapControllerInput` and the public `DivisionSystem` API; it should not mutate renderer objects.
- Movement/pathfinding changes belong in `DivisionRouting`; casualty doctrine belongs in `DivisionCombat`.
- Scenario definitions belong in `ScenarioTemplates`; mutable scenario state stays in `ScenarioMap`.
- Simulation code must not depend on Unity UI or renderer classes. The standalone verification project compiles this layer directly.
- Move every Unity script together with its `.meta` file. Folder organization alone must not change a GUID.

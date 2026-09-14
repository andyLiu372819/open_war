# Unit spawning integration plan

The scenario format is now ready to identify valid deployment infrastructure without creating units yet. `ScenarioInfrastructureSite.SpawnCapability` records whether a site can support land, air, or naval units, and `ScenarioMap.EligibleSpawnSites` filters those sites by current territorial control and walkable ground.

## Phase 1: unit definitions

- Add data-only unit templates for faction, domain, strength, equipment, movement, and supply demand.
- Give every spawned unit a stable ID and a grid position separate from territory ownership.
- Version scenario/save data before unit state is serialized.

## Phase 2: deployment rules

- Land units deploy at controlled rail hubs, depots, ports, and industrial centers.
- Air units deploy at controlled airfields; naval units deploy at controlled ports.
- Require a walkable owned tile, free capacity, and an unbroken supply connection.
- Charge a defined manpower/equipment pool at spawn time. Passive manpower growth remains paused, so reinforcements must come from explicit scenario reserves or later mobilization systems.

## Phase 3: designer tools

- Add infrastructure route drawing and site placement/editing to the Sandbox.
- Add faction-specific initial deployment zones and unit order-of-battle panels.
- Validate orphaned routes, duplicate site names, blocked sites, and maps with no eligible deployment point before launch.

## Phase 4: runtime and AI

- Introduce a unit factory that consumes a validated spawn request and emits a unit without coupling UI code to unit prefabs.
- Add site capacity, cooldowns, damage/repair, rail throughput, and supply tracing.
- Teach AI factions to select safe, connected spawn sites and protect critical transport hubs.

No unit is spawned automatically in the current build. This keeps the existing territorial prototype stable while establishing the data and validation seam the deployment system will use.

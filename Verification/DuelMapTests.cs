using System;
using System.Collections.Generic;

static class DuelMapTests
{
    static int checks;
    static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception("Duel map: " + message);
    }

    public static void Run()
    {
        const int size = DuelMap.DefaultSize;
        ScenarioMap scenario = DuelMap.Create(size, 1847, out MapData map);
        Check(map.Width == size && map.Height == size, "The duel map is not square");
        Check(size * size > 100000, "The duel map is smaller than 100,000 cells");

        // Every cell mirrors about the centre line: terrain, elevation, owner.
        int mismatched = 0, borderLand = 0;
        var biomes = new Dictionary<TerrainType, int>();
        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            MapCell cell = map.GetCell(x, y);
            MapCell mirror = map.GetCell(size - 1 - x, y);
            if (cell.Terrain != mirror.Terrain || cell.Elevation != mirror.Elevation) mismatched++;
            biomes.TryGetValue(cell.Terrain, out int count);
            biomes[cell.Terrain] = count + 1;
            if ((x == 0 || y == 0 || x == size - 1 || y == size - 1) && cell.Terrain != TerrainType.Water)
                borderLand++;
        }
        Check(mismatched == 0, "The two halves are not mirror images: " + mismatched + " cells differ");
        Check(borderLand == 0, "The map is not ringed by ocean");
        Check(biomes.Count >= 9, "Insufficient biome variety on the duel map");
        Check(biomes[TerrainType.River] > 500, "Rivers are too sparse to read on the map");
        Check(biomes[TerrainType.Ford] > 20, "Too few bridges across the rivers");

        // Elevation survives generation, so relief shading still works.
        bool varied = false;
        for (int x = 1; x < size && !varied; x++)
            if (Math.Abs(map.GetCell(x, size / 2).Elevation - map.GetCell(x - 1, size / 2).Elevation) > 0.001f)
                varied = true;
        Check(varied, "Elevation was flattened, so the map would render without relief");

        // Each nation starts holding its whole half of the island.
        int red = map.CountTerritory(0), blue = map.CountTerritory(1);
        Check(red > 0 && red == blue, "Starting territory is not equal: " + red + " vs " + blue);
        int walkable = 0;
        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
            if (TerrainRules.IsWalkable(map.GetCell(x, y).Terrain)) walkable++;
        Check(red + blue == walkable, "The two nations do not hold every walkable cell between them");
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size / 2; x++)
            if (TerrainRules.IsWalkable(map.GetCell(x, y).Terrain))
                Check(map.IsOwnedBy(x, y, 0), "A cell of the western half is unclaimed");
        Check(scenario.PlayerManpower == scenario.EnemyManpower &&
            scenario.PlayerManpower == DuelMap.StartingMilitary, "Starting armies are not equal");
        Check(DuelMap.StartingMilitary / Division.Cost >= DuelMap.StartingDivisions,
            "Starting military cannot pay for the full order of battle");

        // The scripted order of battle: half on the frontier, half in depth.
        var redPlayer = new PlayerData(0, DuelMap.StartingMilitary);
        var bluePlayer = new PlayerData(1, DuelMap.StartingMilitary);
        var roster = new DivisionSystem(map, new[] { redPlayer, bluePlayer });
        int deployed = DuelMap.Deploy(map, scenario, roster, new[] { redPlayer, bluePlayer });
        Check(deployed == DuelMap.StartingDivisions * 2,
            "Deployment did not field thirty divisions a side: " + deployed);
        Check(roster.CountFor(0) == DuelMap.StartingDivisions &&
            roster.CountFor(1) == DuelMap.StartingDivisions, "The two orders of battle differ");
        int redFront = 0, redHolding = 0, blueFront = 0;
        foreach (Division division in roster.Divisions)
        {
            Check(division.Stance == DivisionStance.Defend, "A starting division is not on the defensive");
            Check(map.IsOwnedBy(division.X, division.Y, division.OwnerId),
                "A division was deployed off its own territory");
            bool nearFrontier = System.Math.Abs(division.X - size / 2) < size / 8;
            if (division.OwnerId == 0)
            {
                if (nearFrontier) redFront++;
                if (division.HeldLine.Count > 0) redHolding++;
            }
            else if (nearFrontier) blueFront++;
        }
        Check(redFront >= DuelMap.BorderDivisions && blueFront >= DuelMap.BorderDivisions,
            "Not enough divisions were posted on the frontier");
        Check(redHolding == DuelMap.BorderDivisions,
            "Frontier divisions were not given a stretch of front to hold");
        Check(redPlayer.Manpower == bluePlayer.Manpower,
            "Deployment charged the two sides differently");

        // Fog of war: a nation sees its own ground and a march beyond it.
        var redFog = new FogOfWar(map, 0);
        redFog.Refresh(roster.Divisions);
        map.TryGetTerritoryCenter(0, out float ownX, out float ownY);
        Check(redFog.IsVisible((int)ownX, (int)ownY), "A nation cannot see its own territory");
        map.TryGetTerritoryCenter(1, out float foeX, out float foeY);
        Check(!redFog.IsVisible((int)foeX, (int)foeY), "The heart of the enemy half is not hidden");
        Check(redFog.VisibleCells > red && redFog.VisibleCells < size * size,
            "Fog reveals either nothing extra or the whole map");
        // The enemy nearest the frontier and the one furthest into their half.
        Division enemyDeep = null, enemyNear = null;
        foreach (Division division in roster.Divisions)
        {
            if (division.OwnerId != 1) continue;
            if (enemyNear == null || division.X < enemyNear.X) enemyNear = division;
            if (enemyDeep == null || division.X > enemyDeep.X) enemyDeep = division;
        }
        Check(enemyDeep != null && !redFog.CanSee(enemyDeep), "A deep enemy division is not hidden");
        Check(enemyNear != null && redFog.CanSee(enemyNear), "An enemy on the frontier is not observed");
        foreach (Division division in roster.Divisions)
            if (division.OwnerId == 0) Check(redFog.CanSee(division), "A nation cannot see its own division");
        redFog.Revealed = true;
        Check(redFog.CanSee(enemyDeep) && redFog.IsVisible(size - 3, size / 2),
            "Debug reveal did not lift the fog");
        redFog.Revealed = false;
        Check(!redFog.CanSee(enemyDeep), "Turning debug reveal off did not restore the fog");
        for (int id = 2; id < 4; id++) Check(map.CountTerritory(id) == 0, "A third faction owns ground");

        // The two starts are on opposite sides and both reachable from the middle.
        map.TryGetTerritoryCenter(0, out float redX, out float redY);
        map.TryGetTerritoryCenter(1, out float blueX, out float blueY);
        Check(redX < size / 2 && blueX > size / 2, "The two starts are not on opposite halves");
        Check(Math.Abs((size - 1 - redX) - blueX) <= 1f && Math.Abs(redY - blueY) <= 1f,
            "The starting positions are not mirror images");

        // Named features are mirrored too, in both count and position.
        Check(scenario.Settlements.Count >= 8 && scenario.Settlements.Count % 2 == 0,
            "Settlements are missing or unpaired");
        Check(scenario.InfrastructureSites.Count >= 10 && scenario.InfrastructureSites.Count % 2 == 0,
            "Infrastructure sites are missing or unpaired");
        Check(scenario.InfrastructureRoutes.Count >= 4 && scenario.InfrastructureRoutes.Count % 2 == 0,
            "Routes are missing or unpaired");
        int westSites = 0, eastSites = 0;
        foreach (ScenarioInfrastructureSite site in scenario.InfrastructureSites)
        {
            if (site.X < size / 2) westSites++; else eastSites++;
        }
        Check(westSites == eastSites, "Infrastructure is not evenly split between the halves");
        foreach (ScenarioSettlement settlement in scenario.Settlements)
            Check(TerrainRules.IsWalkable(map.GetCell(settlement.X, settlement.Y).Terrain),
                settlement.Name + " was placed on water");

        // A different seed still satisfies every structural rule.
        ScenarioMap other = DuelMap.Create(size, 99, out MapData otherMap);
        int otherMismatch = 0;
        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
            if (otherMap.GetCell(x, y).Terrain != otherMap.GetCell(size - 1 - x, y).Terrain) otherMismatch++;
        Check(otherMismatch == 0, "A second seed broke the mirror");
        Check(otherMap.CountTerritory(0) == otherMap.CountTerritory(1),
            "A second seed produced unequal starts");
        Check(other.Name == scenario.Name, "Scenario identity changed with the seed");

        Console.WriteLine("PASS: " + checks +
            " duel map assertions (square 102,400 cells, ocean ring, exact mirroring, equal starts, paired features).");
    }
}

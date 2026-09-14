using System.Collections.Generic;

// The Quick Play battlefield: a square landmass ringed by ocean, split down the
// middle between two players. Terrain, rivers, settlements, infrastructure,
// starting territory and army size are all mirrored about the centre line, so
// neither side has ground the other does not.
public static class DuelMap
{
    public const int DefaultSize = 320;
    // Thirty divisions apiece: fifteen holding the frontier and fifteen
    // garrisoning strategic ground behind it, plus a small working reserve.
    public const int BorderDivisions = 15;
    public const int InteriorDivisions = 15;
    public const int StartingDivisions = BorderDivisions + InteriorDivisions;
    public const int StartingMilitary = StartingDivisions * Division.Cost + 20000;

    private static readonly int[] NeighbourX = { -1, 1, 0, 0 };
    private static readonly int[] NeighbourY = { 0, 0, -1, 1 };

    // Builds the playable map and the scenario that carries its named features.
    // The map is handed back separately because it keeps per-cell elevation,
    // which the scenario's terrain-only grid cannot express.
    public static ScenarioMap Create(int size, int seed, out MapData map)
    {
        MapCell[,] cells = TerrainGenerator.GenerateDuel(size, seed);
        map = new MapData(cells);
        var scenario = new ScenarioMap("Mirror Duel", "Skirmish",
            "A square island split down the middle. Both sides face identical terrain, " +
            "rivers, infrastructure and armies, so the only difference is what you do with them.",
            size, size, StartingMilitary, StartingMilitary, 0d, size, 0d, size);
        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
            scenario.SetTerrain(x, y, cells[x, y].Terrain);

        SeedTerritories(map, scenario, size);
        PlaceFeatures(map, scenario, size);
        map.AttachTransportNetwork(MapTransportNetwork.Build(scenario, map));
        return scenario;
    }

    // Each nation starts holding its whole half of the island. The halves are
    // mirror images, so the two territories are identical in size and shape.
    private static void SeedTerritories(MapData map, ScenarioMap scenario, int size)
    {
        int half = size / 2;
        for (int x = 0; x < half; x++)
        for (int y = 0; y < size; y++)
        {
            if (!map.IsWalkable(x, y)) continue;
            int mirroredX = size - 1 - x;
            map.TryClaimCell(x, y, 0);
            map.TryClaimCell(mirroredX, y, 1);
            scenario.SetOwner(x, y, 0);
            scenario.SetOwner(mirroredX, y, 1);
        }
    }

    // Spirals outward until it finds walkable ground, so a chosen spot that
    // happens to land in a lake still produces a usable position.
    private static bool TrySnapToLand(MapData map, ref int x, ref int y, int size)
    {
        for (int radius = 0; radius < size / 2; radius++)
        for (int dx = -radius; dx <= radius; dx++)
        for (int dy = -radius; dy <= radius; dy++)
        {
            if (System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)) != radius) continue;
            int cx = x + dx, cy = y + dy;
            // Stay in the left half: the right half is a mirror of it.
            if (cx < 1 || cx >= size / 2 || cy < 1 || cy >= size - 1) continue;
            if (!map.IsWalkable(cx, cy)) continue;
            x = cx; y = cy;
            return true;
        }
        return false;
    }

    // Named towns and transport, placed on the left and mirrored. West and East
    // names make it obvious at a glance which half of the island you are on.
    private static void PlaceFeatures(MapData map, ScenarioMap scenario, int size)
    {
        var towns = new List<(string west, string east, float x, float y, bool major)>
        {
            ("Westhold", "Easthold", 0.20f, 0.50f, true),
            ("Westmarch", "Eastmarch", 0.33f, 0.30f, false),
            ("Westford", "Eastford", 0.35f, 0.70f, false),
            ("Westbay", "Eastbay", 0.11f, 0.22f, false),
            ("Westwatch", "Eastwatch", 0.42f, 0.50f, false),
        };
        var placed = new Dictionary<string, (int x, int y)>();
        foreach ((string west, string east, float fx, float fy, bool major) in towns)
        {
            int x = Mathf(fx, size), y = Mathf(fy, size);
            if (!TrySnapToLand(map, ref x, ref y, size)) continue;
            int mirroredX = size - 1 - x;
            scenario.AddGridSettlement(west, x, y, major);
            scenario.AddGridSettlement(east, mirroredX, y, major);
            placed[west] = (x, y);
            placed[east] = (mirroredX, y);
        }
        if (!placed.ContainsKey("Westhold")) return;

        AddSite(scenario, placed, "Westhold", "Easthold", " Airfield", InfrastructureSiteType.Airfield, size);
        AddSite(scenario, placed, "Westhold", "Easthold", " Rail Hub", InfrastructureSiteType.RailHub, size);
        AddSite(scenario, placed, "Westmarch", "Eastmarch", " Depot", InfrastructureSiteType.SupplyDepot, size);
        AddSite(scenario, placed, "Westbay", "Eastbay", " Port", InfrastructureSiteType.Port, size);
        AddSite(scenario, placed, "Westwatch", "Eastwatch", " Works", InfrastructureSiteType.IndustrialCenter, size);
        AddSite(scenario, placed, "Westford", "Eastford", " Bridge", InfrastructureSiteType.Bridge, size);

        AddRoute(scenario, placed, "Westhold", "Westmarch", "Westwatch",
            "West Trunk Railway", "East Trunk Railway", InfrastructureRouteType.Railway);
        AddRoute(scenario, placed, "Westhold", "Westford", "Westwatch",
            "West Southern Road", "East Southern Road", InfrastructureRouteType.Road);
        AddRoute(scenario, placed, "Westbay", "Westhold", null,
            "West Coast Road", "East Coast Road", InfrastructureRouteType.Road);
    }

    private static void AddSite(ScenarioMap scenario, Dictionary<string, (int x, int y)> placed,
        string west, string east, string suffix, InfrastructureSiteType type, int size)
    {
        if (!placed.TryGetValue(west, out (int x, int y) w) ||
            !placed.TryGetValue(east, out (int x, int y) e)) return;
        scenario.AddGridInfrastructure(west + suffix, type, w.x, w.y);
        scenario.AddGridInfrastructure(east + suffix, type, e.x, e.y);
    }

    private static void AddRoute(ScenarioMap scenario, Dictionary<string, (int x, int y)> placed,
        string first, string second, string third, string westName, string eastName,
        InfrastructureRouteType type)
    {
        var west = new List<int>();
        var east = new List<int>();
        foreach (string town in new[] { first, second, third })
        {
            if (town == null || !placed.TryGetValue(town, out (int x, int y) point)) continue;
            west.Add(point.x); west.Add(point.y);
            east.Add(scenario.Width - 1 - point.x); east.Add(point.y);
        }
        if (west.Count < 4) return;
        scenario.AddGridRoute(westName, type, west.ToArray());
        scenario.AddGridRoute(eastName, type, east.ToArray());
    }

    // The opening order of battle: half the army holding the frontier, half
    // garrisoning the towns and facilities behind it. Both sides are placed
    // from the same list of positions, mirrored, so neither has better ground.
    public static int Deploy(MapData map, ScenarioMap scenario, DivisionSystem divisions,
        IReadOnlyList<PlayerData> factions)
    {
        if (map == null || divisions == null || factions == null) return 0;
        int size = map.Width;
        int placed = 0;
        var frontier = FrontierPositions(map, size, BorderDivisions);
        var garrisons = GarrisonPositions(map, scenario, size, InteriorDivisions);
        foreach (PlayerData faction in factions)
        {
            if (faction.Id > 1) continue;
            placed += DeploySide(map, divisions, faction, frontier, size, true);
            placed += DeploySide(map, divisions, faction, garrisons, size, false);
        }
        return placed;
    }

    private static int DeploySide(MapData map, DivisionSystem divisions, PlayerData faction,
        List<int> positions, int size, bool onFrontier)
    {
        int placed = 0;
        foreach (int id in positions)
        {
            int x = id % size, y = id / size;
            // Player one holds the mirrored half, so every position flips.
            if (faction.Id == 1) x = size - 1 - x;
            if (!TrySnapToOwned(map, ref x, ref y, faction.Id, size)) continue;
            if (!divisions.TryPlace(faction, x, y, out Division division)) continue;
            placed++;
            // Frontier formations dig in on the border; garrisons hold their
            // town or facility. Both start on the defensive.
            divisions.SetStance(division, DivisionStance.Defend);
            if (!onFrontier) continue;
            var stretch = new List<int>();
            for (int step = -2; step <= 2; step++)
            {
                int cy = y + step;
                if (map.IsOwnedBy(x, cy, faction.Id)) stretch.Add(cy * size + x);
            }
            divisions.AssignFrontage(division, stretch);
        }
        return placed;
    }

    // Evenly spaced posts down the inside edge of the left half, which is the
    // frontier between the two nations. The top and bottom of the island are
    // open sea, so each post searches nearby rows rather than giving up.
    private static List<int> FrontierPositions(MapData map, int size, int count)
    {
        var positions = new List<int>();
        int half = size / 2;
        for (int i = 0; i < count; i++)
        {
            int wanted = (int)((i + 0.5f) / count * size);
            for (int shift = 0; shift < size / 2 && positions.Count == i; shift++)
            for (int sign = 0; sign < 2 && positions.Count == i; sign++)
            {
                int y = wanted + (sign == 0 ? shift : -shift);
                if (y < 1 || y >= size - 1) continue;
                int x = half - 3;
                while (x > 4 && !map.IsWalkable(x, y)) x--;
                if (!map.IsWalkable(x, y)) continue;
                int id = y * size + x;
                // Do not stack two posts on the same cell when rows are scarce.
                if (positions.Contains(id)) continue;
                positions.Add(id);
            }
        }
        return positions;
    }

    // Towns and facilities first, then a spread of interior points to make up
    // the rest, so the garrison covers the depth of the territory.
    private static List<int> GarrisonPositions(MapData map, ScenarioMap scenario, int size, int count)
    {
        var positions = new List<int>();
        var taken = new List<int>();
        void Offer(int x, int y)
        {
            if (positions.Count >= count) return;
            if (x < 1 || x >= size / 2 - 4 || y < 1 || y >= size - 1) return;
            if (!map.IsWalkable(x, y)) return;
            foreach (int other in taken)
            {
                int dx = other % size - x, dy = other / size - y;
                if (dx * dx + dy * dy < 100) return;
            }
            taken.Add(y * size + x);
            positions.Add(y * size + x);
        }
        if (scenario != null)
        {
            foreach (ScenarioSettlement settlement in scenario.Settlements)
                if (settlement.X < size / 2) Offer(settlement.X, settlement.Y);
            foreach (ScenarioInfrastructureSite site in scenario.InfrastructureSites)
                if (site.X < size / 2) Offer(site.X, site.Y);
        }
        // Fill out with a grid across the depth of the half.
        for (int gy = 1; gy <= 8 && positions.Count < count; gy++)
        for (int gx = 1; gx <= 6 && positions.Count < count; gx++)
            Offer(gx * (size / 2) / 7, gy * size / 9);
        // Still short on a cramped map: take any owned ground that is free.
        for (int x = 2; x < size / 2 - 4 && positions.Count < count; x += 3)
        for (int y = 2; y < size - 2 && positions.Count < count; y += 3)
            Offer(x, y);
        return positions;
    }

    private static bool TrySnapToOwned(MapData map, ref int x, ref int y, int ownerId, int size)
    {
        for (int radius = 0; radius < 24; radius++)
        for (int dx = -radius; dx <= radius; dx++)
        for (int dy = -radius; dy <= radius; dy++)
        {
            if (System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)) != radius) continue;
            int cx = x + dx, cy = y + dy;
            if (!map.IsOwnedBy(cx, cy, ownerId) || !map.IsWalkable(cx, cy)) continue;
            x = cx; y = cy;
            return true;
        }
        return false;
    }

    private static int Mathf(float fraction, int size)
    {
        int value = (int)(fraction * size);
        return value < 1 ? 1 : value > size - 2 ? size - 2 : value;
    }
}

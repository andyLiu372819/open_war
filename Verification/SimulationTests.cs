using System;
using System.Collections.Generic;

static class SimulationTests
{
    static int checks;
    static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }

    // Enough troops that commitment is never the limiting factor in a test that
    // is checking something else.
    const int Ample = 10000000;

    static TerrainType[,] Plains(int width, int height)
    {
        var terrain = new TerrainType[width, height];
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++) terrain[x, y] = TerrainType.Land;
        return terrain;
    }

    static void RunToTarget(MapData map, int ownerId, ExpansionOrder order)
    {
        int steps = 0;
        while (!order.IsComplete && steps++ < 900000)
        {
            int before = order.RemainingTroops;
            AdvanceResult result = order.Step(out int x, out int y);
            Check(result != AdvanceResult.OutOfTroops, "Unexpected resource starvation");
            if (result == AdvanceResult.Captured)
            {
                Check(map.HasOwnedNeighbor(x, y, ownerId), "Disconnected capture");
                Check(map.IsWalkable(x, y), "Captured water");
                if (order.LastDefenderId < 0)
                    Check(order.RemainingTroops == before, "Wilderness capture consumed manpower");
                else
                    Check(order.RemainingTroops < before, "Enemy capture did not consume manpower");
            }
        }
        Check(map.IsOwnedBy(order.TargetX, order.TargetY, ownerId), "Failed to reach destination");
    }

    static int Main()
    {
        try
        {
            RunTests();
            return 0;
        }
        catch (Exception exception)
        {
            // Failed checks should fail the command, without opening a Windows
            // unhandled-exception dialog on the user's desktop.
            Console.Error.WriteLine("SIMULATION_TESTS_FAILED: " + exception);
            return 1;
        }
    }

    static void RunTests()
    {
        EconomyTests.Run();
        DivisionTests.Run();
        DuelMapTests.Run();
        EncirclementTests.Run();
        foreach (EasternFrontTemplate template in ScenarioTemplates.All)
        {
            ScenarioMap scenario = ScenarioTemplates.Create(template);
            Check(scenario.Width == 160 && scenario.Height == 96, "Scenario template dimensions are incorrect");
            bool sandbox = template == EasternFrontTemplate.Sandbox;
            if (sandbox)
            {
                Check(!scenario.IsPlayable(out _), "Blank sandbox should require starting sides");
                Check(scenario.CountOwned(0) == 0 && scenario.CountOwned(1) == 0,
                    "Blank sandbox unexpectedly has a front line");
                Check(scenario.Settlements.Count == 0, "Blank sandbox unexpectedly has settlements");
                Check(scenario.InfrastructureRoutes.Count == 0 && scenario.InfrastructureSites.Count == 0,
                    "Blank sandbox unexpectedly has infrastructure");
                Check(scenario.TerrainAt(0, 0) == TerrainType.Land &&
                    scenario.TerrainAt(scenario.Width - 1, scenario.Height - 1) == TerrainType.Land,
                    "Sandbox is not entirely plains");
            }
            else
            {
                Check(scenario.IsPlayable(out _), "Scenario template is not playable: " + template);
                Check(scenario.Settlements.Count >= 20, "Historical template is missing settlements: " + template);
                Check(scenario.InfrastructureRoutes.Count >= 8 && scenario.InfrastructureSites.Count >= 10,
                    "Historical template is missing infrastructure: " + template);
                var names = new HashSet<string>();
                foreach (ScenarioSettlement settlement in scenario.Settlements)
                {
                    Check(names.Add(settlement.Name), "Duplicate settlement: " + settlement.Name);
                    Check(settlement.X >= 0 && settlement.X < scenario.Width &&
                        settlement.Y >= 0 && settlement.Y < scenario.Height,
                        "Settlement projected outside the map: " + settlement.Name);
                    int expectedX = (int)Math.Round((settlement.Longitude - scenario.WestLongitude) /
                        (scenario.EastLongitude - scenario.WestLongitude) * (scenario.Width - 1));
                    int expectedY = (int)Math.Round((settlement.Latitude - scenario.SouthLatitude) /
                        (scenario.NorthLatitude - scenario.SouthLatitude) * (scenario.Height - 1));
                    Check(settlement.X == expectedX && settlement.Y == expectedY,
                        "Settlement projection drifted: " + settlement.Name);
                }
                var routeNames = new HashSet<string>();
                foreach (ScenarioInfrastructureRoute route in scenario.InfrastructureRoutes)
                {
                    Check(routeNames.Add(route.Name) && route.Points.Count >= 2,
                        "Invalid or duplicate infrastructure route: " + route.Name);
                    foreach (ScenarioRoutePoint point in route.Points)
                        Check(point.X >= 0 && point.X < scenario.Width && point.Y >= 0 && point.Y < scenario.Height,
                            "Infrastructure route projected outside the map: " + route.Name);
                }
                var siteNames = new HashSet<string>();
                foreach (ScenarioInfrastructureSite site in scenario.InfrastructureSites)
                {
                    Check(siteNames.Add(site.Name), "Duplicate infrastructure site: " + site.Name);
                    Check(site.X >= 0 && site.X < scenario.Width && site.Y >= 0 && site.Y < scenario.Height,
                        "Infrastructure site projected outside the map: " + site.Name);
                }
                int eligible = 0;
                foreach (int ownerId in new[] { 0, 1 })
                    foreach (ScenarioInfrastructureSite site in scenario.EligibleSpawnSites(ownerId,
                        UnitSpawnCapability.Land | UnitSpawnCapability.Air | UnitSpawnCapability.Naval))
                    {
                        Check(scenario.OwnerAt(site.X, site.Y) == ownerId,
                            "Future spawn-site ownership filter leaked");
                        eligible++;
                    }
                Check(eligible > 0, "Historical template has no controlled future spawn site");
            }
            MapData scenarioData = scenario.CreateMapData();
            Check(scenarioData.CountTerritory(0) == scenario.CountOwned(0) &&
                scenarioData.CountTerritory(1) == scenario.CountOwned(1), "Scenario ownership changed during conversion");

            ScenarioMap painted = scenario.Copy();
            painted.Paint(10, 10, DesignerBrush.Water, 2);
            Check(painted.TerrainAt(10, 10) == TerrainType.Water && painted.OwnerAt(10, 10) == -1,
                "Impassable terrain brush did not clear control");
            painted.Paint(10, 10, DesignerBrush.RedSide, 0);
            Check(painted.OwnerAt(10, 10) == -1, "Control brush claimed impassable terrain");
            painted.Paint(10, 10, DesignerBrush.Plains, 0);
            painted.Paint(10, 10, DesignerBrush.RedSide, 0);
            Check(painted.OwnerAt(10, 10) == 0, "Control brush did not claim restored land");
            if (sandbox)
            {
                painted.Paint(140, 80, DesignerBrush.BlueSide, 0);
                Check(painted.IsPlayable(out _), "Painted sandbox did not become playable");
            }
            Check(scenario.OwnerAt(10, 10) != painted.OwnerAt(10, 10) ||
                scenario.TerrainAt(10, 10) != painted.TerrainAt(10, 10), "Scenario copy mutated its template");
            Check(scenario.Settlements.Count == painted.Settlements.Count,
                "Scenario copy lost settlement metadata");
            Check(scenario.InfrastructureRoutes.Count == painted.InfrastructureRoutes.Count &&
                scenario.InfrastructureSites.Count == painted.InfrastructureSites.Count,
                "Scenario copy lost infrastructure metadata");
        }
        var map = new MapData(Plains(32, 20));
        map.TryClaimCell(3, 10, 0);
        Check(ExpansionOrder.TryCreate(map, 0, 26, 10, 4, Ample, out var order), "Cannot issue neutral order");
        RunToTarget(map, 0, order);
        Check(map.CountTerritory(0) > 100, "Advance is a single-tile snake, not a broad front");
        Check(map.GetCell(0, 0).OwnerId == -1, "Order spread across unrelated map");
        Check(!ExpansionOrder.TryCreate(map, 0, -1, 10, 4, Ample, out _), "Out of bounds order accepted");
        Check(!ExpansionOrder.TryCreate(map, 0, 26, 10, 4, Ample, out _), "Own target accepted");
        map = new MapData(Plains(24, 16));
        map.TryClaimCell(2, 8, 0);
        Check(ExpansionOrder.TryCreate(map, 0, 20, 8, 3, 0, out order),
            "Zero-manpower wilderness order was rejected");
        int zeroBefore = map.CountTerritory(0);
        Check(order.Step(out _, out _) == AdvanceResult.Captured && order.SpentTroops == 0 &&
            map.CountTerritory(0) > zeroBefore, "Zero-manpower wilderness order did not capture for free");
        Check(!ExpansionOrder.TryCreate(map, 0, 20, 8, 3, -50, out _), "Order with negative commitment accepted");

        var terrain = Plains(28, 22);
        for (int y = 0; y < 20; y++) terrain[12, y] = TerrainType.Water;
        map = new MapData(terrain);
        map.TryClaimCell(5, 8, 0);
        Check(ExpansionOrder.TryCreate(map, 0, 20, 8, 3, Ample, out order), "Could not route around water");
        RunToTarget(map, 0, order);
        for (int y = 0; y < 20; y++) Check(map.GetCell(12, y).OwnerId == -1, "Water wall was captured");
        terrain[12, 20] = terrain[12, 21] = TerrainType.Water;
        map = new MapData(terrain);
        map.TryClaimCell(5, 8, 0);
        Check(!ExpansionOrder.TryCreate(map, 0, 20, 8, 3, Ample, out _), "Unreachable island accepted");

        terrain = Plains(20, 12);
        for (int y = 0; y < 12; y++) terrain[10, y] = TerrainType.River;
        terrain[10, 6] = TerrainType.Ford;
        map = new MapData(terrain);
        map.TryClaimCell(3, 6, 0);
        Check(ExpansionOrder.TryCreate(map, 0, 16, 6, 3, Ample, out order), "Could not route through ford");
        RunToTarget(map, 0, order);
        Check(map.GetCell(10, 6).OwnerId == 0, "Did not use river crossing");

        // A committed force is finite: it stops when spent instead of waiting
        // for the nation to recruit more, and its survivors come home.
        terrain = Plains(10, 10);
        for (int x = 0; x < 10; x++)
        for (int y = 0; y < 10; y++) terrain[x, y] = TerrainType.Mountains;
        map = new MapData(terrain);
        map.TryClaimCell(3, 3, 0);
        for (int x = 0; x < map.Width; x++)
        for (int y = 0; y < map.Height; y++)
            if (x != 3 || y != 3) map.TryClaimCell(x, y, 1);
        Check(ExpansionOrder.TryCreate(map, 0, 6, 3, 2, 5, out order), "Undersized commitment rejected");
        int version = map.OwnershipVersion;
        Check(order.Step(out _, out _) == AdvanceResult.OutOfTroops, "Spent force kept advancing");
        Check(order.IsComplete, "Spent order stayed active");
        Check(map.OwnershipVersion == version, "Spent order mutated the map");
        Check(order.Recall() == 5 && order.RemainingTroops == 0, "Survivors were not recalled");
        Check(order.Recall() == 0, "Recall paid out twice");

        // A force large enough for a few tiles takes exactly those tiles.
        map = new MapData(Plains(20, 12));
        map.TryClaimCell(2, 6, 0);
        for (int x = 0; x < map.Width; x++)
        for (int y = 0; y < map.Height; y++)
            if (x != 2 || y != 6) map.TryClaimCell(x, y, 1);
        Check(ExpansionOrder.TryCreate(map, 0, 17, 6, 2, 33, out order), "Bounded order rejected");
        while (!order.IsComplete) order.Step(out _, out _);
        int taken = map.CountTerritory(0) - 1;
        Check(taken > 0, "Bounded order captured nothing");
        Check(!map.IsOwnedBy(17, 6, 0), "A 33-troop force crossed the whole map");
        Check(order.SpentTroops + order.RemainingTroops == order.CommittedTroops, "Committed troops leaked");
        Check(order.SpentTroops == taken * TerrainRules.AttackCost(TerrainType.Land),
            "Bounded order spent an unexpected amount");
        Check(order.RemainingTroops < TerrainRules.AttackCost(TerrainType.Land),
            "Order stopped while it could still afford a tile");

        // Doubling the commitment on identical ground doubles the ground taken.
        map = new MapData(Plains(40, 24));
        map.TryClaimCell(2, 12, 0);
        for (int x = 0; x < map.Width; x++)
        for (int y = 0; y < map.Height; y++)
            if (x != 2 || y != 12) map.TryClaimCell(x, y, 1);
        ExpansionOrder.TryCreate(map, 0, 37, 12, 3, 120, out order);
        while (!order.IsComplete) order.Step(out _, out _);
        int small = map.CountTerritory(0);
        map = new MapData(Plains(40, 24));
        map.TryClaimCell(2, 12, 0);
        for (int x = 0; x < map.Width; x++)
        for (int y = 0; y < map.Height; y++)
            if (x != 2 || y != 12) map.TryClaimCell(x, y, 1);
        ExpansionOrder.TryCreate(map, 0, 37, 12, 3, 240, out order);
        while (!order.IsComplete) order.Step(out _, out _);
        Check(map.CountTerritory(0) > small, "A larger commitment did not take more ground");
        Check(map.CountTerritory(0) - 1 == (small - 1) * 2, "Commitment does not scale ground linearly");

        // Every stretch of border with a comparable approach joins the attack,
        // and no two prongs open on the same stretch.
        map = new MapData(Plains(40, 30));
        for (int y = 6; y < 24; y++) map.TryClaimCell(2, y, 0);
        Check(ExpansionOrder.TryCreate(map, 0, 37, 15, 3, Ample, out order, 1.6f, 6),
            "Wide border could not reach the destination");
        Check(order.FrontCount == 3, "A wide border did not open the expected fronts");
        Check(order.Routes.Count == order.FrontCount, "Front count disagrees with routes");
        Check(ReferenceEquals(order.Route, order.Routes[0]), "Primary route is not the first front");
        Check(order.Routes[0][order.Routes[0].Count - 1] == 15 * map.Width + 39,
            "Primary front does not continue past the destination");
        for (int i = 0; i < order.Routes.Count; i++)
        {
            var front = order.Routes[i];
            Check(front.Count >= 2, "Front route is too short to leave the border");
            Check(map.IsOwnedBy(front[0] % map.Width, front[0] / map.Width, 0),
                "Front did not start on owned land");
            Check(!map.IsOwnedBy(front[1] % map.Width, front[1] / map.Width, 0),
                "Front route ran through our own territory");
            Check(map.HasOwnedNeighbor(front[1] % map.Width, front[1] / map.Width, 0),
                "Front did not step straight off the border");
            for (int j = 0; j < i; j++)
            {
                int a = order.Routes[i][0], b = order.Routes[j][0];
                int dx = a % map.Width - b % map.Width, dy = a / map.Width - b / map.Width;
                Check(dx * dx + dy * dy >= 36, "Two fronts opened on the same stretch of border");
            }
        }

        // A distant stretch of border joins only when the tolerance pays for the
        // longer approach, and the cap limits how many prongs open at once.
        map = new MapData(Plains(40, 26));
        map.TryClaimCell(2, 13, 0);
        map.TryClaimCell(2, 1, 0);
        Check(ExpansionOrder.TryCreate(map, 0, 37, 13, 3, Ample, out var tight, 1.0f, 6),
            "Tight tolerance rejected the order");
        Check(ExpansionOrder.TryCreate(map, 0, 37, 13, 3, Ample, out var wide, 2.0f, 6),
            "Wide tolerance rejected the order");
        Check(tight.FrontCount == 1, "Tight tolerance recruited a distant front");
        Check(wide.FrontCount == 2, "Wide tolerance ignored a reachable front");
        Check(ExpansionOrder.TryCreate(map, 0, 37, 13, 3, Ample, out order, 2.0f, 1),
            "Capped order rejected");
        Check(order.FrontCount == 1, "Front cap ignored");

        // The same commitment spread over several prongs arrives on a broader
        // face than a single prong does.
        int SpreadOf(int fronts)
        {
            var wall = new MapData(Plains(40, 30));
            for (int y = 6; y < 24; y++) wall.TryClaimCell(2, y, 0);
            ExpansionOrder.TryCreate(wall, 0, 37, 15, 3, 500, out var spread, 1.6f, fronts);
            while (!spread.IsComplete) spread.Step(out _, out _);
            int lowest = int.MaxValue, highest = int.MinValue;
            for (int x = 3; x < 40; x++)
            for (int y = 0; y < 30; y++)
                if (wall.IsOwnedBy(x, y, 0))
                {
                    lowest = Math.Min(lowest, y);
                    highest = Math.Max(highest, y);
                }
            Check(highest >= lowest, "A bounded advance captured nothing");
            return highest - lowest;
        }
        Check(SpreadOf(6) > SpreadOf(1), "Extra fronts did not widen the attack");

        terrain = Plains(8, 8);
        terrain[4, 3] = TerrainType.Mountains;
        map = new MapData(terrain);
        map.TryClaimCell(3, 3, 0);
        map.TryClaimCell(4, 3, 1);
        int defense = map.GetCell(4, 3).Defense;
        var player = new PlayerData(0, 1000);
        Check(map.TryAdvanceCell(4, 3, player, out bool captured) && !captured, "Mountain captured instantly");
        Check(map.GetCell(4, 3).OwnerId == 1 && map.GetCell(4, 3).Defense < defense, "Defense did not resist attack");
        for (int i = 0; i < 4; i++) map.TryAdvanceCell(4, 3, player, out captured);
        Check(captured && map.GetCell(4, 3).OwnerId == 0, "Combat did not capture tile");
        Check(map.CountTerritory(0) == 2 && map.CountTerritory(1) == 0, "Territory index failed on ownership transfer");
        Check(!map.TryGetTerritoryCenter(1, out _, out _), "Defeated faction retained a label center");
        Check(player.Manpower == 820, "Combat charged unexpected cost");
        Check(!map.TryAdvanceCell(7, 7, player, out _), "Nonadjacent combat accepted");
        Check(player.Manpower == 820, "Invalid combat spent troops");

        // The same combat charged against a committed force, not the reserve.
        terrain = Plains(8, 8);
        terrain[4, 3] = TerrainType.Mountains;
        map = new MapData(terrain);
        map.TryClaimCell(3, 3, 0);
        map.TryClaimCell(4, 3, 1);
        var force = new CommittedForce(0, 1000);
        for (int i = 0; i < 5; i++) map.TryAdvanceCell(4, 3, force, out captured);
        Check(captured && map.GetCell(4, 3).OwnerId == 0, "Committed force could not take a mountain");
        Check(force.Manpower == 820 && force.Spent == 180, "Committed force charged unexpected cost");
        Check(!force.TrySpendManpower(821) && force.Manpower == 820, "Force overspent its commitment");
        Check(!force.TrySpendManpower(0) && !force.TrySpendManpower(-5), "Force accepted a non-positive spend");

        // A foreign faction must not be attacked while ordering neutral expansion.
        map = new MapData(Plains(12, 8));
        map.TryClaimCell(1, 4, 0);
        for (int y = 0; y < 8; y++) map.TryClaimCell(6, y, 2);
        Check(!ExpansionOrder.TryCreate(map, 0, 10, 4, 3, Ample, out _), "Neutral order crossed uninvolved faction");
        Check(ExpansionOrder.TryCreate(map, 0, 6, 4, 3, Ample, out order), "Enemy attack rejected");
        RunToTarget(map, 0, order);

        // The user's centroid method must still return a cell inside a curved territory.
        map = new MapData(Plains(10, 10));
        for (int x = 1; x < 8; x++) map.TryClaimCell(x, 1, 0);
        for (int y = 2; y < 8; y++) map.TryClaimCell(1, y, 0);
        Check(map.TryGetTerritoryCenter(0, out float cx, out float cy) &&
            map.IsOwnedBy((int)cx, (int)cy, 0), "Territory label outside owned land");
        Check(!map.TryGetTerritoryCenter(9, out _, out _), "Empty territory had a center");

        foreach (int seed in new[] { 1847, 42, 2026 })
        {
            map = new MapData(256, 160, seed);
            var copy = new MapData(256, 160, seed);
            var counts = new Dictionary<TerrainType, int>();
            for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
            {
                var cell = map.GetCell(x, y);
                Check(cell.Terrain == copy.GetCell(x, y).Terrain, "Generation is not deterministic");
                counts.TryGetValue(cell.Terrain, out int count);
                counts[cell.Terrain] = count + 1;
            }
            Console.WriteLine("Seed " + seed + ": " + string.Join(", ", counts));
            Check(counts.Count >= 9, "Insufficient biome variety");
            StartingTerritories.Create(map, 4);
            for (int id = 0; id < 4; id++) Check(map.CountTerritory(id) == 80, "Starting territory incomplete");
            map.TryGetTerritoryCenter(1, out cx, out cy);
            Check(ExpansionOrder.TryCreate(map, 0, (int)cx, (int)cy, 4, Ample, out order), "Rival is unreachable");
            RunToTarget(map, 0, order);
        }
        var timer = System.Diagnostics.Stopwatch.StartNew();
        map = new MapData(1024, 512, 1847);
        Console.WriteLine("Large map generation: " + timer.ElapsedMilliseconds + " ms, " + map.Width * map.Height + " tiles");
        timer.Restart();
        StartingTerritories.Create(map, 4);
        Console.WriteLine("Large map starts: " + timer.ElapsedMilliseconds + " ms");
        map.TryGetTerritoryCenter(1, out float targetX, out float targetY);
        timer.Restart();
        Check(ExpansionOrder.TryCreate(map, 0, (int)targetX, (int)targetY, 4, Ample, out order), "Large map rival unreachable");
        Console.WriteLine("Large map long-distance order: " + timer.ElapsedMilliseconds + " ms, route " +
            order.Route.Count + " cells, " + order.FrontCount + " fronts");
        int unaffectedVersion = map.GetTerritoryVersion(2);
        timer.Restart();
        RunToTarget(map, 0, order);
        Console.WriteLine("Large map complete advance: " + timer.ElapsedMilliseconds + " ms, " +
            order.SpentTroops.ToString("N0") + " troops spent");
        Check(map.GetTerritoryVersion(2) == unaffectedVersion, "Unrelated territory invalidated");
        var actualCounts = new int[4];
        for (int x = 0; x < map.Width; x++)
        for (int y = 0; y < map.Height; y++)
        {
            int owner = map.GetCell(x, y).OwnerId;
            if (owner >= 0) actualCounts[owner]++;
        }
        for (int id = 0; id < 4; id++)
            Check(actualCounts[id] == map.CountTerritory(id), "Cached manpower land count disagrees with cells");
        timer.Restart();
        for (int repeat = 0; repeat < 1000; repeat++)
        for (int id = 0; id < 4; id++)
        {
            Check(map.CountTerritory(id) > 0, "Large map count failed");
            Check(map.TryGetTerritoryCenter(id, out float x, out float y) && map.IsOwnedBy((int)x, (int)y, id),
                "Large map label escaped territory");
        }
        Console.WriteLine("4,000 large-map territory count/center queries: " + timer.ElapsedMilliseconds + " ms");
        Console.WriteLine("PASS: " + checks + " assertions covering scenario templates, map painting, generation, broad advances, obstacles, fords, combat, troop commitment, label centers, and 524,288-tile scaling.");
    }
}

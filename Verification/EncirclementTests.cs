using System;
using System.Diagnostics;

static class EncirclementTests
{
    static int checks;
    static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception("Encirclement: " + message);
    }

    static TerrainType[,] Plains(int width = 12, int height = 12)
    {
        var terrain = new TerrainType[width, height];
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++) terrain[x, y] = TerrainType.Land;
        return terrain;
    }

    static void Ring(MapData map, int left, int bottom, int right, int top, int owner = 0, bool gap = false)
    {
        for (int x = left; x <= right; x++)
        {
            if (!gap || x != left + 1) map.TryClaimCell(x, bottom, owner);
            map.TryClaimCell(x, top, owner);
        }
        for (int y = bottom + 1; y < top; y++)
        {
            map.TryClaimCell(left, y, owner);
            map.TryClaimCell(right, y, owner);
        }
    }

    static void Drain(EncirclementSystem system, int owner = 0, float share = 1f)
    {
        for (int i = 0; i < 20000; i++)
        {
            AdvanceResult result = system.Step(owner, share, out _, out _);
            if (result == AdvanceResult.Complete || result == AdvanceResult.OutOfTroops) return;
        }
        throw new Exception("Pocket assault did not terminate");
    }

    public static void Run()
    {
        // Every movement/capture call uses the same pricing rule, not just the UI.
        int[] neutral = { 1, 2, 2, 3, 3, 4, 5, 6 };
        int[] enemy = { 12, 14, 18, 20, 22, 24, 30, 36 };
        TerrainType[] biomes = { TerrainType.Land, TerrainType.Coast, TerrainType.Forest,
            TerrainType.Desert, TerrainType.Ford, TerrainType.Hills, TerrainType.Snow, TerrainType.Mountains };
        for (int i = 0; i < biomes.Length; i++)
        {
            var ground = Plains(); ground[4, 4] = biomes[i];
            var terrainMap = new MapData(ground);
            terrainMap.TryClaimCell(3, 4, 0);
            Check(terrainMap.GetAdvanceCost(4, 4, 0) == neutral[i], "Wrong wilderness terrain cost");
            var troops = new CommittedForce(0, 500);
            Check(terrainMap.TryAdvanceCell(4, 4, troops, out bool taken) && taken, "Neutral action failed");
            Check(troops.Spent == neutral[i], "Actual neutral spending disagrees with quote");
            terrainMap = new MapData(ground);
            terrainMap.TryClaimCell(3, 4, 0); terrainMap.TryClaimCell(4, 4, 1);
            Check(terrainMap.GetAdvanceCost(4, 4, 0) == enemy[i], "Wrong enemy action cost");
            troops = new CommittedForce(0, enemy[i] - 1);
            int defense = terrainMap.GetCell(4, 4).Defense;
            Check(!terrainMap.TryAdvanceCell(4, 4, troops, out _), "Unaffordable attack accepted");
            Check(troops.Spent == 0 && terrainMap.GetCell(4, 4).Defense == defense, "Rejected action changed combat");

            terrainMap = new MapData(ground);
            Ring(terrainMap, 3, 3, 5, 5);
            var emptyReserve = new PlayerData(0, 0);
            var freeSystem = new EncirclementSystem(terrainMap, new[] { emptyReserve });
            freeSystem.Refresh();
            Check(terrainMap.GetAdvanceCost(4, 4, 0) == 0, "Encircled wilderness must be free on every terrain");
            Check(terrainMap.GetAdvanceCost(4, 4, 2) == neutral[i], "Free capture leaked to a different faction");
            Check(freeSystem.Step(0, .05f, out _, out _) == AdvanceResult.Captured,
                "Empty reserve could not automatically capture encircled wilderness");
            Check(emptyReserve.Manpower == 0 && freeSystem.RemainingTroops(0) == 0,
                "Free capture reserved or created troops");
            freeSystem.Step(0, .05f, out _, out _);
            Check(freeSystem.LastRefund == 0, "Free wave created a refund");
        }

        var map = new MapData(Plains());
        Ring(map, 3, 3, 7, 7, gap: true);
        var player = new PlayerData(0, 1000);
        var rival = new PlayerData(1, 500);
        var system = new EncirclementSystem(map, new[] { player, rival });
        system.Refresh();
        Check(map.EncircledCellCount(0) == 0, "Open ring counted as a pocket");
        map.TryClaimCell(4, 3, 0);
        map.TryClaimCell(4, 4, 1);
        int before = map.CountTerritory(0);
        system.Refresh();
        Check(map.EncircledCellCount(0) == 9, "Closed ring did not detect all nine cells");
        Check(map.CountTerritory(0) == before && player.Manpower == 1000, "Detection captured or charged instantly");
        Check(map.GetAdvanceCost(4, 4, 0) == 6 && map.GetAdvanceCost(5, 5, 0) == 0, "Pocket discount wrong");
        Check(map.GetAdvanceCost(4, 4, 2) == 12 && !map.IsEncircledBy(4, 4, 1), "Discount leaked to another faction");
        int enclosureVersion = map.GetEnclosureVersion(0);
        Check(system.Step(0, .5f, out _, out _) == AdvanceResult.Captured, "Pocket did not attack automatically");
        Check(player.Manpower == 1000 && system.RemainingTroops(0) == 0, "Free wave withdrew reserve");
        Check(map.GetEnclosureVersion(0) == enclosureVersion, "An internal capture scheduled an unnecessary flood");
        Drain(system);
        Check(player.Manpower == 1000 && map.IsOwnedBy(4, 4, 1), "Free wave spent troops or captured enemy for free");
        Drain(system);
        Check(map.EncircledCellCount(0) == 0 && map.CountTerritory(0) == before + 9, "Auto assault left accessible pocket cells");
        Check(player.Manpower == 994 && system.RemainingTroops(0) == 0, "Incorrect net cost/refund (8 free neutral + 1 enemy plains)");
        Check(map.GetCell(2, 4).OwnerId == -1 && map.CountTerritory(1) == 0, "Assault escaped pocket or retained enemy");
        system.Step(0, 1, out _, out _);
        Check(player.Manpower == 994 && system.LastRefund == 0, "Refund paid twice");

        // An enemy mountain still resists five actions, but each costs half.
        var mountain = Plains(); mountain[4, 4] = TerrainType.Mountains;
        map = new MapData(mountain); Ring(map, 3, 3, 5, 5); map.TryClaimCell(4, 4, 1);
        player = new PlayerData(0, 100);
        system = new EncirclementSystem(map, new[] { player }); system.Refresh();
        for (int i = 0; i < 5; i++)
        {
            var result = system.Step(0, 1, out _, out _);
            Check(result == (i < 4 ? AdvanceResult.Fighting : AdvanceResult.Captured), "Mountain defense bypassed");
        }
        Drain(system);
        Check(player.Manpower == 10 && map.IsOwnedBy(4, 4, 0), "Discounted mountain should cost 90 in total");

        // Losing any wall invalidates the old mask before the next flood tick.
        var mixed = Plains(); mixed[5, 5] = TerrainType.Mountains;
        map = new MapData(mixed); Ring(map, 3, 3, 7, 7); map.TryClaimCell(6, 2, 1);
        player = new PlayerData(0, 100);
        system = new EncirclementSystem(map, new[] { player }); system.Refresh();
        system.Step(0, 1, out _, out _);
        Check(map.TryAdvanceCell(6, 3, new CommittedForce(1, 100), out bool breached) && breached, "Could not breach ring");
        Check(!map.IsEncircledBy(5, 5, 0) && map.GetAdvanceCost(5, 5, 0) == 6, "Broken ring kept discount");
        Check(system.Step(0, 1, out _, out _) == AdvanceResult.Complete, "Broken-pocket assault kept going");
        Check(system.LastRefund == 0 && player.Manpower == 100, "Broken free wave changed reserve");
        system.Refresh();
        Check(map.EncircledCellCount(0) == 0, "Breach failed to connect pocket to exterior");

        // Exhaustion, later recruitment, and concurrent manual captures conserve troops.
        var rugged = Plains();
        for (int x = 0; x < 12; x++)
        for (int y = 0; y < 12; y++) rugged[x, y] = TerrainType.Mountains;
        map = new MapData(rugged); Ring(map, 3, 3, 7, 7);
        for (int x = 4; x <= 6; x++)
        for (int y = 4; y <= 6; y++) map.TryClaimCell(x, y, 1);
        player = new PlayerData(0, 38); system = new EncirclementSystem(map, new[] { player }); system.Refresh();
        Drain(system);
        Check(player.Manpower == 2 && map.EncircledCellCount(0) == 9, "Exhausted wave overspent or failed to refund remainder");
        system.Step(0, .05f, out _, out _);
        Check(player.Manpower == 2 && system.RemainingTroops(0) == 0, "Unaffordable wave withdrew reserve");
        player.AddManpower(1000); Drain(system);
        Check(player.Manpower == 228 && map.EncircledCellCount(0) == 0, "Recruitment did not fund a later wave");
        map = new MapData(Plains()); Ring(map, 3, 3, 7, 7);
        player = new PlayerData(0, 100); system = new EncirclementSystem(map, new[] { player }); system.Refresh();
        system.Step(0, 1, out _, out _);
        var manual = new CommittedForce(0, 50);
        Check(map.TryAdvanceCell(6, 6, manual, out _) && manual.Spent == 0, "Manual order did not receive free wilderness capture");
        Drain(system);
        Check(player.Manpower == 100 && map.EncircledCellCount(0) == 0, "Free captures consumed manpower");

        map = new MapData(Plains()); Ring(map, 3, 3, 9, 9);
        player = new PlayerData(0, 100); system = new EncirclementSystem(map, new[] { player }); system.Refresh();
        system.Step(0, 1, out _, out _);
        manual = new CommittedForce(0, 1000);
        for (int x = 4; x <= 8; x++)
        for (int y = 4; y <= 8; y++)
            if (x == 4 || x == 8 || y == 4 || y == 8) map.TryAdvanceCell(x, y, manual, out _);
        Drain(system);
        Check(map.EncircledCellCount(0) == 0 && player.Manpower == 100 && manual.Spent == 0,
            "Automatic wave stranded behind a manually captured perimeter");

        // Separate pockets are all reduced. Other factions cannot complete our ring.
        map = new MapData(Plains(20, 12)); Ring(map, 2, 2, 4, 4); Ring(map, 10, 2, 12, 4);
        player = new PlayerData(0, 100); system = new EncirclementSystem(map, new[] { player }); system.Refresh();
        Check(map.EncircledCellCount(0) == 2, "Disconnected pockets missed"); Drain(system);
        Check(player.Manpower == 100 && map.EncircledCellCount(0) == 0, "Only one pocket was reduced or free captures cost troops");

        // A mixed pocket clears free ground even with no reserve, but enemy
        // occupation waits for enough troops and never bypasses its garrison.
        map = new MapData(Plains()); Ring(map, 3, 3, 7, 7); map.TryClaimCell(5, 5, 1);
        player = new PlayerData(0, 0); system = new EncirclementSystem(map, new[] { player }); system.Refresh();
        Drain(system);
        Check(map.EncircledCellCount(0) == 1 && map.IsOwnedBy(5, 5, 1) && player.Manpower == 0,
            "Zero-reserve mixed pocket did not separate free wilderness from enemy combat");
        Drain(system);
        Check(map.IsOwnedBy(5, 5, 1) && player.Manpower == 0, "Unfunded enemy assault captured land");
        player.AddManpower(6); Drain(system);
        Check(map.EncircledCellCount(0) == 0 && player.Manpower == 0, "Funded enemy pocket did not cost six troops");
        map = new MapData(Plains()); Ring(map, 3, 3, 7, 7, gap: true); map.TryClaimCell(4, 3, 1);
        system = new EncirclementSystem(map, new[] { new PlayerData(0, 100) }); system.Refresh();
        Check(map.EncircledCellCount(0) == 0, "Mixed-faction ring counted as ours");

        // The map edge is exterior; water cannot turn an ordinary coast into a pocket.
        map = new MapData(Plains());
        for (int x = 0; x < 8; x++) { map.TryClaimCell(x, 3, 0); map.TryClaimCell(x, 7, 0); }
        for (int y = 3; y <= 7; y++) map.TryClaimCell(7, y, 0);
        system = new EncirclementSystem(map, new[] { new PlayerData(0, 100) }); system.Refresh();
        Check(map.EncircledCellCount(0) == 0, "Open map-edge pocket falsely encircled");
        var coast = Plains(); coast[4, 3] = TerrainType.Water;
        map = new MapData(coast); Ring(map, 3, 3, 7, 7);
        system = new EncirclementSystem(map, new[] { new PlayerData(0, 100) }); system.Refresh();
        Check(map.EncircledCellCount(0) == 0, "Water completed a ring");

        // A click is now a heading. Stop at impassable ground or the map edge.
        map = new MapData(Plains(30, 12)); map.TryClaimCell(2, 6, 0);
        Check(ExpansionOrder.TryCreate(map, 0, 5, 6, 1, 10000, out var order), "Directional order rejected");
        for (int i = 0; !order.IsComplete && i < 1000; i++) order.Step(out _, out _);
        Check(order.IsComplete && map.IsOwnedBy(29, 6, 0), "Attack stopped at clicked cell instead of edge");
        int spent = order.SpentTroops, refund = order.Recall();
        Check(spent > 0 && refund > 0 && spent + refund == 10000 && order.SpentTroops == spent, "Directional refund accounting wrong");
        Check(order.Recall() == 0, "Directional recall duplicated troops");
        var wall = Plains(30, 12); for (int y = 0; y < 12; y++) wall[10, y] = TerrainType.Water;
        map = new MapData(wall); map.TryClaimCell(2, 6, 0);
        ExpansionOrder.TryCreate(map, 0, 5, 6, 1, 10000, out order);
        for (int i = 0; !order.IsComplete && i < 1000; i++) order.Step(out _, out _);
        Check(order.IsComplete && map.IsOwnedBy(9, 6, 0) && !map.IsOwnedBy(11, 6, 0), "Directional attack jumped water");
        Check(order.Recall() > 0, "Blocked attack lost its survivors");

        map = new MapData(Plains(30, 12)); map.TryClaimCell(2, 6, 0);
        ExpansionOrder.TryCreate(map, 0, 5, 6, 1, 1000, out order);
        for (int i = 0; i < 4; i++) map.TryClaimCell(2 + MapData.NeighborX[i], 6 + MapData.NeighborY[i], 0);
        Check(order.Step(out _, out _) == AdvanceResult.Captured, "Manual order stranded behind an automatically captured front");

        map = new MapData(Plains(1024, 512)); Ring(map, 250, 100, 750, 400);
        system = new EncirclementSystem(map, new[] { new PlayerData(0, 100) });
        var timer = Stopwatch.StartNew(); system.Refresh();
        Check(map.EncircledCellCount(0) == 499 * 299, "Large ring detection lost cells");
        Console.WriteLine("Large ring scan: " + timer.ElapsedMilliseconds + " ms, " + map.EncircledCellCount(0) + " pocket cells");
        int detectionVersion = map.EncirclementVersion;
        for (int i = 0; i < 100; i++) system.Refresh();
        Check(map.EncirclementVersion == detectionVersion, "Unchanged territory was flooded again");
        Console.WriteLine("PASS: " + checks + " encirclement, terrain pricing, conservation, and directional-continuation assertions.");
    }
}

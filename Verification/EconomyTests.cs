using System;

static class EconomyTests
{
    static int checks;
    static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception("Economy: " + message);
    }

    public static void Run()
    {
        var player = new PlayerData(0, 1600);
        Check(player.Gold == 0 && player.Industry == 0, "Resources should start at zero");
        player.TickEconomy(80);
        Check(player.Manpower == 1820, "Starting recruitment should be 220/s instead of 55/s");
        Check(player.Gold == 40 && player.Industry == 20, "Starting land income wrong");
        player.TickEconomy(160);
        Check(player.Manpower == 2120 && player.Gold == 120 && player.Industry == 60, "Expanded territory income wrong");
        player.TickEconomy(40);
        Check(player.Manpower == 2300 && player.Gold == 140 && player.Industry == 70, "Land loss did not reduce income");
        player.TickEconomy(0);
        Check(player.Manpower == 2300 && player.Gold == 140 && player.Industry == 70, "Landless faction generated income or lost balances");
        Check(PlayerData.RecruitmentPerSecond(0) == 0 && PlayerData.GoldPerSecond(0) == 0 &&
            PlayerData.IndustryPerSecond(0) == 0, "Landless HUD rates wrong");
        var small = new PlayerData(1, 0);
        small.TickEconomy(1);
        Check(small.Gold == .5m && small.Industry == .25m && small.Manpower == 141, "Fractional income was truncated");
        for (int i = 0; i < 3; i++) small.TickEconomy(1);
        Check(small.Gold == 2m && small.Industry == 1m, "Fractions did not accumulate exactly");
        Check(player.Gold == 140, "One faction received another's income");
        bool rejected = false;
        try { player.TickEconomy(-1); } catch (ArgumentOutOfRangeException) { rejected = true; }
        Check(rejected && player.Manpower == 2300 && player.Gold == 140, "Invalid land count mutated resources");
        var large = new PlayerData(2, 0);
        large.TickEconomy(524288);
        Check(large.Manpower == 524428 && large.Gold == 262144 && large.Industry == 131072, "Large map economy did not scale");

        var terrain = new TerrainType[4, 4];
        for (int x = 0; x < 4; x++)
        for (int y = 0; y < 4; y++) terrain[x, y] = TerrainType.Land;
        var map = new MapData(terrain);
        map.TryClaimCell(1, 1, 0);
        var force = new CommittedForce(0, 10);
        Check(map.TryAdvanceCell(2, 1, force, out bool captured) && captured && force.Spent == 1,
            "Plains wilderness did not cost one troop");
        map.TryClaimCell(3, 1, 1);
        Check(map.GetAdvanceCost(3, 1, 0) == 12 && !map.TryAdvanceCell(3, 1, force, out _),
            "Cheap wilderness accidentally made enemy combat cheap");
        player = new PlayerData(0, 0);
        player.TickEconomy(map.CountTerritory(0));
        Check(player.Gold == 1 && player.Industry == .5m, "Captured land did not contribute to production");
        Console.WriteLine("PASS: " + checks + " economy assertions (recruitment, land-scaled resources, fractions, and cheap wilderness).");
    }
}

using System;

static class EconomyTests
{
    static int checks;
    static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception("Economy: " + message);
    }

    // Expectations are derived from the tuning constants rather than written out
    // as literals, so retuning the economy does not invalidate the rules below.
    static decimal Civilians(int land) => land > 0 ? PlayerData.CivilianBase + land * PlayerData.CiviliansPerCell : 0m;
    static decimal Gold(int land) => land * PlayerData.GoldPerCell;
    static decimal Industry(int land) => land * PlayerData.IndustryPerCell;

    public static void Run()
    {
        var player = new PlayerData(0, 1600);
        Check(player.Gold == 0 && player.Industry == 0 && player.Civilians == 0,
            "Resources should start at zero");
        player.TickEconomy(80);
        Check(player.Manpower == 1600, "Military must not grow on its own");
        Check(player.Gold == Gold(80) && player.Industry == Industry(80), "Starting land income wrong");
        Check(player.Civilians == Civilians(80), "Civilian growth wrong");
        player.TickEconomy(160);
        Check(player.Gold == Gold(80) + Gold(160), "Expanded territory income wrong");
        Check(player.Civilians == Civilians(80) + Civilians(160), "Civilians did not scale with land");
        player.TickEconomy(40);
        decimal bankedGold = Gold(80) + Gold(160) + Gold(40);
        Check(player.Gold == bankedGold, "Land loss did not reduce income");
        player.TickEconomy(0);
        Check(player.Manpower == 1600 && player.Gold == bankedGold,
            "Landless faction generated income or lost balances");
        Check(PlayerData.CiviliansPerSecond(0) == 0 && PlayerData.GoldPerSecond(0) == 0 &&
            PlayerData.IndustryPerSecond(0) == 0, "Landless rates wrong");
        Check(PlayerData.GoldPerSecond(500) == Gold(500) &&
            PlayerData.CiviliansPerSecond(500) == Civilians(500), "Published rates disagree with production");

        // Income is quoted and accrued per resolution step, so a turn is worth
        // a fixed multiple of it however long the player spends planning.
        var perTurn = new PlayerData(9, 0);
        for (int step = 0; step < TurnState.StepsPerTurn; step++) perTurn.TickEconomy(1000);
        Check(perTurn.Gold == Gold(1000) * TurnState.StepsPerTurn,
            "A turn of production is not its step count times the rate");

        // Recruiting is the only route from civilians to soldiers, and it must
        // charge both ledgers for every single soldier it produces.
        var army = new PlayerData(1, 0);
        Check(army.AffordableRecruits() == 0 && !army.TryRecruit(1), "Recruited from an empty treasury");
        army.TickEconomy(0);
        army.AddManpower(0);
        for (int i = 0; i < 40; i++) army.TickEconomy(500);
        int affordable = army.AffordableRecruits();
        Check(affordable > 0, "Forty ticks of a 500-cell nation afforded nothing");
        Check(affordable == (int)Math.Min(Math.Floor(army.Civilians), Math.Floor(army.Gold)),
            "Affordability is not the scarcer of civilians and coins");
        decimal coinsBefore = army.Gold, civiliansBefore = army.Civilians;
        Check(army.TryRecruit(affordable), "Affordable batch was rejected");
        Check(army.Manpower == affordable && army.Gold == coinsBefore - affordable &&
            army.Civilians == civiliansBefore - affordable,
            "Recruitment did not charge one civilian and one coin per soldier");
        Check(army.AffordableRecruits() == 0, "A fully spent nation could still recruit");
        Check(!army.TryRecruit(0) && !army.TryRecruit(-5) && army.Manpower == affordable,
            "Non-positive recruitment changed the army");

        // Coins are the binding input: they accrue more slowly than civilians.
        Check(PlayerData.GoldPerCell < PlayerData.CiviliansPerCell,
            "Coins must be scarcer than civilians or the treasury never binds");
        var rich = new PlayerData(2, 0, 500m);
        Check(rich.Civilians == 500m && rich.Gold == 0, "Starting civilians were not honoured");
        Check(!rich.TryRecruit(1) && rich.Manpower == 0, "Recruited with civilians but no coins");

        var small = new PlayerData(3, 0);
        small.TickEconomy(1);
        Check(small.Gold == Gold(1) && small.Industry == Industry(1) && small.Civilians == Civilians(1),
            "Fractional income is wrong");
        for (int i = 0; i < 3; i++) small.TickEconomy(1);
        Check(small.Gold == Gold(1) * 4 && small.Industry == Industry(1) * 4,
            "Fractions did not accumulate exactly");
        Check(player.Gold == bankedGold, "One faction received another's income");
        bool rejected = false;
        try { player.TickEconomy(-1); } catch (ArgumentOutOfRangeException) { rejected = true; }
        Check(rejected && player.Manpower == 1600 && player.Gold == bankedGold,
            "Invalid land count mutated resources");
        rejected = false;
        try { new PlayerData(4, 0, -1m); } catch (ArgumentOutOfRangeException) { rejected = true; }
        Check(rejected, "Negative starting civilians accepted");

        // A nation holding a whole half of the duel map should be able to raise
        // a few divisions a turn, not twenty and not one every twenty turns.
        var nation = new PlayerData(5, 0);
        for (int step = 0; step < TurnState.StepsPerTurn; step++) nation.TickEconomy(31000);
        int divisionsPerTurn = (int)(nation.Gold / Division.Cost);
        Check(divisionsPerTurn >= 1 && divisionsPerTurn <= 6,
            "A full-half nation earns " + divisionsPerTurn + " divisions a turn, which is out of balance");

        var terrain = new TerrainType[4, 4];
        for (int x = 0; x < 4; x++)
        for (int y = 0; y < 4; y++) terrain[x, y] = TerrainType.Land;
        var map = new MapData(terrain);
        map.TryClaimCell(1, 1, 0);
        var force = new CommittedForce(0, 0);
        Check(map.TryAdvanceCell(2, 1, force, out bool captured) && captured && force.Spent == 0,
            "Wilderness capture consumed manpower");
        map.TryClaimCell(3, 1, 1);
        Check(map.GetAdvanceCost(3, 1, 0) == 12 && !map.TryAdvanceCell(3, 1, force, out _),
            "Free wilderness accidentally made enemy combat free");
        Console.WriteLine("PASS: " + checks +
            " economy assertions (civilian growth, coin-priced recruitment, per-turn production, land scaling, and free wilderness).");
    }
}

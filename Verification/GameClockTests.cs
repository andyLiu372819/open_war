using System;
using System.Collections.Generic;
using System.Text;

static class GameClockTests
{
    static int checks;

    static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception("GameClock: " + message);
    }

    public static void Run()
    {
        var clock = new GameClock();
        Check(!clock.Paused && clock.SpeedMultiplier == 1 && clock.TickCount == 0,
            "a new clock should run continuously at 1x");
        clock.AddRealTime(GameClock.DefaultFixedTickDuration * 3d);
        Check(Consume(clock) == 3 && clock.SimulationTime == GameClock.DefaultFixedTickDuration * 3d,
            "continuous time did not produce fixed ticks");

        clock = new GameClock();
        clock.AddRealTime(GameClock.DefaultFixedTickDuration * 0.5d);
        clock.SetPaused(true);
        clock.AddRealTime(100d);
        Check(!clock.TryConsumeTick() && clock.TickCount == 0 && clock.SimulationTime == 0d,
            "paused wall time advanced authoritative time");
        clock.SetPaused(false);
        clock.AddRealTime(GameClock.DefaultFixedTickDuration * 0.5d);
        Check(clock.TryConsumeTick() && clock.TickCount == 1,
            "resume did not continue from the preserved partial tick");

        Check(TicksFromWallTime(1, 0.48d) == 4 && TicksFromWallTime(2, 0.24d) == 4 &&
            TicksFromWallTime(4, 0.12d) == 4,
            "1x, 2x, and 4x did not reach equal simulation time at proportional wall times");
        Check(!clock.SetSpeed(3) && clock.SpeedMultiplier == 1,
            "an unsupported speed was accepted");

        // 500 x 0.12s is exactly sixty simulated seconds.
        const int comparedTicks = 500;
        string at1x = RunControlledSimulation(1, new[] { 1d / 60d }, comparedTicks);
        string at2x = RunControlledSimulation(2, new[] { 0.013d, 0.041d, 0.007d }, comparedTicks);
        string at4x = RunControlledSimulation(4, new[] { 0.09d, 0.003d, 0.025d, 0.2d }, comparedTicks);
        Check(at1x == at2x && at1x == at4x,
            "speed or render-frame deltas changed the state after identical simulation ticks");

        Check(EconomyTicksAfter(25) == 3,
            "economy did not use its one-simulated-second cadence");
        Console.WriteLine("PASS: " + checks +
            " clock assertions (continuous play, pause/resume, speed scaling, cadence, and frame-rate determinism).");
    }

    static int Consume(GameClock clock)
    {
        int ticks = 0;
        while (clock.TryConsumeTick()) ticks++;
        return ticks;
    }

    static int TicksFromWallTime(int speed, double wallTime)
    {
        var clock = new GameClock();
        Check(clock.SetSpeed(speed), "could not set " + speed + "x");
        clock.AddRealTime(wallTime);
        return Consume(clock);
    }

    static string RunControlledSimulation(int speed, double[] frameDeltas, int targetTicks)
    {
        CreateControlledSimulation(out MapData map, out List<PlayerData> players,
            out DivisionSystem divisions, out GameSimulation simulation);
        var clock = new GameClock();
        clock.SetSpeed(speed);
        int frame = 0, guard = 0;
        while (simulation.TickCount < targetTicks && guard++ < 100000)
        {
            clock.AddRealTime(frameDeltas[frame++ % frameDeltas.Length]);
            while (simulation.TickCount < targetTicks && clock.TryConsumeTick())
                simulation.Tick(clock.FixedTickDuration, 0.5f);
        }
        Check(simulation.TickCount == targetTicks && clock.TickCount == targetTicks,
            "controlled simulation did not reach its requested tick");
        return Snapshot(map, players, divisions);
    }

    static int EconomyTicksAfter(int ticks)
    {
        CreateControlledSimulation(out _, out _, out _, out GameSimulation simulation);
        int economyTicks = 0;
        for (int i = 0; i < ticks; i++)
            economyTicks += simulation.Tick(GameClock.DefaultFixedTickDuration, 0.5f).EconomyTicks;
        return economyTicks;
    }

    static void CreateControlledSimulation(out MapData map, out List<PlayerData> players,
        out DivisionSystem divisions, out GameSimulation simulation)
    {
        var terrain = new TerrainType[30, 12];
        for (int x = 0; x < 30; x++)
        for (int y = 0; y < 12; y++) terrain[x, y] = TerrainType.Land;
        map = new MapData(terrain);
        for (int x = 0; x < 12; x++)
        for (int y = 0; y < 12; y++) map.TryClaimCell(x, y, 0);
        for (int x = 18; x < 30; x++)
        for (int y = 0; y < 12; y++) map.TryClaimCell(x, y, 1);

        players = new List<PlayerData>
        {
            new PlayerData(0, Division.Cost * 3, 1000m),
            new PlayerData(1, Division.Cost * 3, 1000m)
        };
        divisions = new DivisionSystem(map, players);
        bool placedRed = divisions.TryPlace(players[0], 8, 6, out Division red);
        bool placedBlue = divisions.TryPlace(players[1], 21, 6, out Division blue);
        Check(placedRed && placedBlue, "could not place controlled formations");
        Check(divisions.TryOrder(red, 22, 6) && divisions.TryOrder(blue, 7, 6),
            "could not author controlled orders");
        var encirclement = new EncirclementSystem(map, players);
        encirclement.Refresh();
        simulation = new GameSimulation(map, players, divisions, encirclement, 0);
    }

    static string Snapshot(MapData map, IReadOnlyList<PlayerData> players, DivisionSystem divisions)
    {
        var state = new StringBuilder();
        foreach (PlayerData player in players)
            state.Append('P').Append(player.Id).Append(':').Append(player.Manpower).Append(':')
                .Append(player.Civilians).Append(':').Append(player.Gold).Append(':')
                .Append(player.Industry).Append('|');
        foreach (Division division in divisions.Divisions)
            state.Append('D').Append(division.OwnerId).Append(':').Append(division.Number).Append(':')
                .Append(division.X).Append(':').Append(division.Y).Append(':')
                .Append(division.Strength).Append(':').Append((int)division.Stance).Append('|');
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            MapCell cell = map.GetCell(x, y);
            state.Append(cell.OwnerId).Append('/').Append(cell.Defense).Append(',');
        }
        return state.ToString();
    }
}

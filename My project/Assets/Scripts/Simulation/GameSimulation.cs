using System;
using System.Collections.Generic;

// A transient report consumed by presentation immediately after a simulation
// tick. The instance is reused, so callers should not retain its collections.
public sealed class SimulationTickResult
{
    private readonly List<int> capturedCells = new List<int>();
    private readonly HashSet<int> capturedSet = new HashSet<int>();

    public IReadOnlyList<int> CapturedCells => capturedCells;
    public int FriendlyLosses { get; internal set; }
    public int EnemyLosses { get; internal set; }
    public int FriendlyDestroyed { get; internal set; }
    public int EnemyDestroyed { get; internal set; }
    public int EconomyTicks { get; internal set; }
    public bool EncirclementRefreshed { get; internal set; }
    public bool FogRefreshDue { get; internal set; }

    internal void Clear()
    {
        capturedCells.Clear();
        capturedSet.Clear();
        FriendlyLosses = EnemyLosses = 0;
        FriendlyDestroyed = EnemyDestroyed = 0;
        EconomyTicks = 0;
        EncirclementRefreshed = false;
        FogRefreshDue = false;
    }

    internal void AddCaptured(IReadOnlyList<int> cells)
    {
        for (int i = 0; i < cells.Count; i++)
            if (capturedSet.Add(cells[i])) capturedCells.Add(cells[i]);
    }
}

// Coordinates existing gameplay systems on deterministic simulation time.
// Rendering and input deliberately stay outside this class.
public sealed class GameSimulation
{
    public const double EconomyInterval = 1d;
    public const int EncirclementRefreshTicks = 4;
    public const int FogRefreshTicks = 4;

    private readonly MapData map;
    private readonly IReadOnlyList<PlayerData> players;
    private readonly Dictionary<int, PlayerData> playerById = new Dictionary<int, PlayerData>();
    private readonly DivisionSystem divisions;
    private readonly EncirclementSystem encirclement;
    private readonly int localPlayerId;
    private readonly List<int> pocketCaptured = new List<int>();
    private readonly List<int> pocketDefenders = new List<int>();
    private readonly List<int> divisionCaptured = new List<int>();
    private readonly List<int> divisionDefenders = new List<int>();
    private readonly SimulationTickResult result = new SimulationTickResult();
    private double economyAccumulator;

    public long TickCount { get; private set; }

    public GameSimulation(MapData map, IReadOnlyList<PlayerData> players,
        DivisionSystem divisions, EncirclementSystem encirclement, int localPlayerId)
    {
        this.map = map ?? throw new ArgumentNullException(nameof(map));
        this.players = players ?? throw new ArgumentNullException(nameof(players));
        this.divisions = divisions ?? throw new ArgumentNullException(nameof(divisions));
        this.encirclement = encirclement ?? throw new ArgumentNullException(nameof(encirclement));
        this.localPlayerId = localPlayerId;
        foreach (PlayerData player in players) playerById[player.Id] = player;
        if (!playerById.ContainsKey(localPlayerId)) throw new ArgumentOutOfRangeException(nameof(localPlayerId));
    }

    public SimulationTickResult Tick(double fixedDeltaSeconds, float localCommitment)
    {
        if (double.IsNaN(fixedDeltaSeconds) || double.IsInfinity(fixedDeltaSeconds) ||
            fixedDeltaSeconds <= 0d)
            throw new ArgumentOutOfRangeException(nameof(fixedDeltaSeconds));

        result.Clear();
        TickCount++;

        // Economy owns a slower cadence without depending on render frames.
        economyAccumulator += fixedDeltaSeconds;
        while (economyAccumulator + 0.000000001d >= EconomyInterval)
        {
            economyAccumulator -= EconomyInterval;
            if (economyAccumulator < 0d) economyAccumulator = 0d;
            foreach (PlayerData faction in players)
                faction.TickEconomy(map.CountTerritory(faction.Id));
            result.EconomyTicks++;
        }

        if (TickCount % EncirclementRefreshTicks == 0)
        {
            encirclement.Refresh();
            result.EncirclementRefreshed = true;
        }

        foreach (PlayerData faction in players)
        {
            float commitment = faction.Id == localPlayerId ? localCommitment : 0.5f;
            encirclement.Step(faction.Id, commitment, pocketCaptured, pocketDefenders);
            result.AddCaptured(pocketCaptured);
            ApplyDefenderLosses(pocketDefenders);
        }

        divisions.Step(divisionCaptured, divisionDefenders);
        result.AddCaptured(divisionCaptured);
        ApplyDefenderLosses(divisionDefenders);

        foreach (PlayerData faction in players)
        {
            int losses = divisions.LastLossesFor(faction.Id);
            if (faction.Id == localPlayerId) result.FriendlyLosses += losses;
            else result.EnemyLosses += losses;
        }
        foreach (Division destroyed in divisions.LastDestroyed)
        {
            if (destroyed.OwnerId == localPlayerId) result.FriendlyDestroyed++;
            else result.EnemyDestroyed++;
        }

        result.FogRefreshDue = result.CapturedCells.Count > 0 ||
            result.FriendlyDestroyed > 0 || result.EnemyDestroyed > 0 ||
            TickCount % FogRefreshTicks == 0;
        return result;
    }

    private void ApplyDefenderLosses(IReadOnlyList<int> defenderIds)
    {
        foreach (int defenderId in defenderIds)
        {
            if (!playerById.TryGetValue(defenderId, out PlayerData defender)) continue;
            int loss = Math.Min(12, defender.Manpower);
            if (loss > 0) defender.TrySpendManpower(loss);
        }
    }
}

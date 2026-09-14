using System.Collections.Generic;

// A single infantry division: a physical unit standing on one cell rather than a
// share of a national reserve. It pays for its own fighting, so MapData charges
// it through the same ITroopSource contract as a nation or a committed force.
public sealed class Division : ITroopSource
{
    // Forming one converts military population into a unit on the map.
    public const int Cost = 10000;
    // Wilderness the division sweeps up either side of itself as it advances.
    // Without this a division would paint a one-cell thread nobody could see.
    public const int Frontage = 2;
    // Extra defence a dug-in division adds to the ground it holds, at the
    // frontage it is designed for. Holding a wider line divides this across it.
    public const int EntrenchBonus = 60;
    // The frontage one division covers at full strength. Assign it a wider
    // stretch and the same division is spread thinner over more ground.
    public const int StandardFrontage = 6;
    // Replacements a reserve division absorbs from the national pool per step.
    public const int ReserveRefit = 500;
    // A redeploying division covers this many cells per step instead of one.
    public const int RedeploySpeed = 2;

    // Id is the owning faction, matching how CommittedForce identifies itself.
    public int Id { get; }
    public int OwnerId => Id;
    // Counts up per faction, so a nation raises its 1st, 2nd, 3rd division.
    public int Number { get; }
    public int X { get; private set; }
    public int Y { get; private set; }
    // ITroopSource calls this Manpower; for a unit on the map it is strength.
    public int Manpower { get; private set; }
    public int Strength => Manpower;
    public bool IsDestroyed => Manpower <= 0;
    public DivisionStance Stance { get; internal set; } = DivisionStance.Attack;
    // The stretch of front this division has been told to hold, as cell ids.
    // Empty means it simply holds the ground it stands on.
    private readonly List<int> heldLine = new List<int>();
    public IReadOnlyList<int> HeldLine => heldLine;
    // Defence added to each held cell. A division covering its standard
    // frontage gives the full bonus; twice that width gives half as much.
    public int LineDefense => heldLine.Count <= StandardFrontage
        ? EntrenchBonus
        : System.Math.Max(1, EntrenchBonus * StandardFrontage / heldLine.Count);
    public int TargetX { get; private set; } = -1;
    public int TargetY { get; private set; } = -1;
    public bool HasOrders => step < route.Count;
    // The cells still to be walked, so the map can draw the advance as an arrow.
    public IReadOnlyList<int> Route => remaining;
    private readonly List<int> remaining = new List<int>();
    public int Cell => Y * width + X;

    private readonly int width;
    private readonly List<int> route = new List<int>();
    private int step;

    public Division(int number, int ownerId, int x, int y, int strength, int mapWidth)
    {
        if (number <= 0) throw new System.ArgumentOutOfRangeException(nameof(number));
        if (ownerId < 0) throw new System.ArgumentOutOfRangeException(nameof(ownerId));
        if (strength <= 0) throw new System.ArgumentOutOfRangeException(nameof(strength));
        if (mapWidth <= 0) throw new System.ArgumentOutOfRangeException(nameof(mapWidth));
        Number = number;
        Id = ownerId;
        X = x;
        Y = y;
        Manpower = strength;
        width = mapWidth;
    }

    public bool TrySpendManpower(int amount)
    {
        if (amount <= 0 || Manpower - amount < 0) return false;
        Manpower -= amount;
        return true;
    }

    // Combat losses are capped at the formation's remaining strength. This is
    // deliberately separate from TrySpendManpower: an overwhelming hit must be
    // able to destroy a weak division instead of being rejected as unaffordable.
    internal int TakeLoss(int amount)
    {
        if (amount <= 0 || Manpower <= 0) return 0;
        int taken = System.Math.Min(amount, Manpower);
        Manpower -= taken;
        if (Manpower == 0) ClearOrders();
        return taken;
    }

    public void Reinforce(int amount)
    {
        if (amount > 0) Manpower += amount;
    }

    // The route always starts at the cell after the division's current one.
    internal void SetRoute(IReadOnlyList<int> cells, int targetX, int targetY)
    {
        route.Clear();
        route.AddRange(cells);
        step = 0;
        TargetX = targetX;
        TargetY = targetY;
        RebuildRemaining();
    }

    internal void SetHeldLine(IReadOnlyList<int> cells)
    {
        heldLine.Clear();
        if (cells != null) heldLine.AddRange(cells);
    }

    internal void ClearHeldLine() => heldLine.Clear();

    internal void ClearOrders()
    {
        route.Clear();
        remaining.Clear();
        step = 0;
        TargetX = TargetY = -1;
    }

    private void RebuildRemaining()
    {
        remaining.Clear();
        for (int i = step; i < route.Count; i++) remaining.Add(route[i]);
    }

    internal bool TryPeekNext(out int x, out int y)
    {
        x = y = -1;
        if (step >= route.Count) return false;
        x = route[step] % width;
        y = route[step] / width;
        return true;
    }

    internal void AdvanceTo(int x, int y)
    {
        X = x;
        Y = y;
        step++;
        if (step >= route.Count) ClearOrders();
        else RebuildRemaining();
    }

    internal void Destroy() => Manpower = 0;
}

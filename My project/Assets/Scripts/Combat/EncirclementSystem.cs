using System;
using System.Collections.Generic;

// Temporary territorial encirclement, kept separate from rendering and the
// eventual division/supply model. Only a closed ring of ONE faction blocks
// access to the exterior. Water is traversed by this detection flood so an
// ordinary coast, river bank, or separate island is not a free encirclement.
public sealed class EncirclementSystem
{
    private readonly MapData map;
    private readonly Dictionary<int, PlayerData> players = new Dictionary<int, PlayerData>();
    private readonly Dictionary<int, int> checkedVersions = new Dictionary<int, int>();
    private readonly Dictionary<int, PocketAssault> assaults = new Dictionary<int, PocketAssault>();
    public int LastDefenderId { get; private set; } = -1;
    public int LastRefund { get; private set; }

    public EncirclementSystem(MapData map, IReadOnlyList<PlayerData> factions)
    {
        this.map = map;
        foreach (PlayerData player in factions) players.Add(player.Id, player);
    }

    // Called on a slow simulation tick, not once per tile or every frame.
    public void Refresh()
    {
        foreach (int ownerId in players.Keys)
        {
            int version = map.GetEnclosureVersion(ownerId);
            if (checkedVersions.TryGetValue(ownerId, out int previous) && previous == version) continue;
            map.SetEncircledCells(ownerId, FindPockets(ownerId));
            checkedVersions[ownerId] = version;
            if (assaults.TryGetValue(ownerId, out PocketAssault assault)) assault.SyncTargets();
        }
    }

    public int RemainingTroops(int ownerId) =>
        assaults.TryGetValue(ownerId, out PocketAssault assault) ? assault.RemainingTroops : 0;

    // Cells captured and defenders engaged by the most recent Step, so the
    // caller can repaint every tile a simultaneous ring attack changed.
    private readonly List<int> stepCaptured = new List<int>();
    private readonly List<int> stepDefenders = new List<int>();
    public IReadOnlyList<int> LastCaptured => stepCaptured;
    public IReadOnlyList<int> LastDefenders => stepDefenders;

    public AdvanceResult Step(int ownerId, float commitment, List<int> captured, List<int> defenders)
    {
        captured.Clear();
        defenders.Clear();
        LastDefenderId = -1;
        LastRefund = 0;
        if (!players.TryGetValue(ownerId, out PlayerData reserve)) return AdvanceResult.Complete;
        if (!assaults.TryGetValue(ownerId, out PocketAssault assault))
        {
            if (!PocketAssault.TryCreate(map, reserve, commitment, out assault)) return AdvanceResult.Complete;
            assaults.Add(ownerId, assault);
        }
        AdvanceResult result = assault.Step(captured, defenders);
        LastDefenderId = assault.LastDefenderId;
        if (assault.IsComplete)
        {
            LastRefund = assault.Recall();
            reserve.AddManpower(LastRefund);
            assaults.Remove(ownerId);
        }
        return result;
    }

    // Convenience for callers that only need to know something happened; x and y
    // report the last cell taken this tick, not the only one.
    public AdvanceResult Step(int ownerId, float commitment, out int x, out int y)
    {
        AdvanceResult result = Step(ownerId, commitment, stepCaptured, stepDefenders);
        int last = stepCaptured.Count > 0 ? stepCaptured[stepCaptured.Count - 1] : -1;
        x = last >= 0 ? last % map.Width : -1;
        y = last >= 0 ? last / map.Width : -1;
        return result;
    }

    private HashSet<int> FindPockets(int ownerId)
    {
        var pockets = new HashSet<int>();
        int minX = map.Width, minY = map.Height, maxX = -1, maxY = -1;
        foreach (int id in map.OwnedCells(ownerId))
        {
            int x = id % map.Width, y = id / map.Width;
            minX = Math.Min(minX, x); minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
        }
        if (maxX < 0) return pockets;
        // A hole cannot lie outside the enclosing faction's bounding box.
        minX = Math.Max(0, minX - 1); minY = Math.Max(0, minY - 1);
        maxX = Math.Min(map.Width - 1, maxX + 1); maxY = Math.Min(map.Height - 1, maxY + 1);
        int width = maxX - minX + 1, height = maxY - minY + 1;
        var exterior = new bool[width * height];
        var queue = new int[exterior.Length];
        int head = 0, tail = 0;
        void Visit(int x, int y)
        {
            if (x < minX || y < minY || x > maxX || y > maxY || map.IsOwnedBy(x, y, ownerId)) return;
            int local = (y - minY) * width + x - minX;
            if (exterior[local]) return;
            exterior[local] = true;
            queue[tail++] = local;
        }
        for (int x = minX; x <= maxX; x++) { Visit(x, minY); Visit(x, maxY); }
        for (int y = minY; y <= maxY; y++) { Visit(minX, y); Visit(maxX, y); }
        while (head < tail)
        {
            int local = queue[head++];
            int x = local % width + minX, y = local / width + minY;
            for (int i = 0; i < 4; i++) Visit(x + MapData.NeighborX[i], y + MapData.NeighborY[i]);
        }
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
            if (!exterior[(y - minY) * width + x - minX] && map.IsWalkable(x, y) && !map.IsOwnedBy(x, y, ownerId))
                pockets.Add(y * map.Width + x);
        return pockets;
    }
}

// Automatically reduces every accessible pocket from its perimeter. The force
// is finite and paid up front for enemy combat. Wilderness-only waves need no
// force; a later paid wave can form when free expansion reaches enemy land.
internal sealed class PocketAssault
{
    private readonly MapData map;
    private readonly CommittedForce force;
    private readonly HashSet<int> frontier = new HashSet<int>();
    private readonly List<int> ring = new List<int>();
    public bool IsComplete { get; private set; }
    public int LastDefenderId { get; private set; } = -1;
    public int RemainingTroops => force.Manpower;

    private PocketAssault(MapData map, int ownerId, int troops)
    {
        this.map = map;
        force = new CommittedForce(ownerId, troops);
        SyncTargets();
    }

    public static bool TryCreate(MapData map, PlayerData reserve, float commitment, out PocketAssault assault)
    {
        assault = null;
        if (float.IsNaN(commitment) || commitment < 0f) return false;
        int cheapest = int.MaxValue;
        foreach (int id in map.EncircledCells(reserve.Id))
        {
            int x = id % map.Width, y = id / map.Width;
            if (map.HasOwnedNeighbor(x, y, reserve.Id))
                cheapest = Math.Min(cheapest, map.GetAdvanceCost(x, y, reserve.Id));
        }
        if (cheapest == int.MaxValue) return false;
        if (cheapest == 0)
        {
            // Do not even reserve manpower for automatic wilderness captures.
            assault = new PocketAssault(map, reserve.Id, 0);
            return true;
        }
        int troops = (int)Math.Floor(reserve.Manpower * Math.Min(1f, commitment));
        if (troops < cheapest || !reserve.TrySpendManpower(troops)) return false;
        assault = new PocketAssault(map, reserve.Id, troops);
        return true;
    }

    public void SyncTargets()
    {
        foreach (int id in map.EncircledCells(force.Id))
        {
            int x = id % map.Width, y = id / map.Width;
            if (map.HasOwnedNeighbor(x, y, force.Id)) frontier.Add(id);
        }
    }

    // A pocket is surrounded, so every tile of its perimeter is attacked in the
    // same tick rather than the cheapest one alone. The pocket collapses inward
    // from all sides instead of being eaten from whichever corner is softest.
    public AdvanceResult Step(List<int> captured, List<int> defenders)
    {
        captured.Clear();
        defenders.Clear();
        LastDefenderId = -1;
        if (IsComplete) return AdvanceResult.Complete;
        if (!HasTarget())
        {
            // A simultaneous manual order can take our entire current front.
            // Re-seed from the remaining pocket so it cannot strand this wave.
            frontier.Clear();
            SyncTargets();
        }
        // Iterate a snapshot: capturing a tile adds its neighbours to the
        // frontier, and those belong to the next ring, not this one.
        ring.Clear();
        ring.AddRange(frontier);
        bool fought = false, starved = false;
        foreach (int id in ring)
        {
            int cx = id % map.Width, cy = id / map.Width;
            if (!map.IsEncircledBy(cx, cy, force.Id) || !map.HasOwnedNeighbor(cx, cy, force.Id))
            {
                frontier.Remove(id);
                continue;
            }
            int cost = map.GetAdvanceCost(cx, cy, force.Id);
            if (force.Manpower < cost) { starved = true; continue; }
            int defender = map.GetCell(cx, cy).OwnerId;
            if (!map.TryAdvanceCell(cx, cy, force, out bool took)) continue;
            LastDefenderId = defender;
            if (defender >= 0) defenders.Add(defender);
            if (!took) { fought = true; continue; }
            captured.Add(id);
            frontier.Remove(id);
            for (int i = 0; i < 4; i++)
            {
                int nx = cx + MapData.NeighborX[i], ny = cy + MapData.NeighborY[i];
                if (map.IsEncircledBy(nx, ny, force.Id)) frontier.Add(ny * map.Width + nx);
            }
        }
        if (captured.Count > 0) return AdvanceResult.Captured;
        if (fought) return AdvanceResult.Fighting;
        IsComplete = true;
        return starved ? AdvanceResult.OutOfTroops : AdvanceResult.Complete;
    }

    private bool HasTarget()
    {
        foreach (int id in frontier)
        {
            int cx = id % map.Width, cy = id / map.Width;
            if (map.IsEncircledBy(cx, cy, force.Id) && map.HasOwnedNeighbor(cx, cy, force.Id)) return true;
        }
        return false;
    }

    public int Recall()
    {
        IsComplete = true;
        return force.Recall();
    }
}

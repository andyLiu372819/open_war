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

    public AdvanceResult Step(int ownerId, float commitment, out int x, out int y)
    {
        x = y = -1;
        LastDefenderId = -1;
        LastRefund = 0;
        if (!players.TryGetValue(ownerId, out PlayerData reserve)) return AdvanceResult.Complete;
        if (!assaults.TryGetValue(ownerId, out PocketAssault assault))
        {
            if (!PocketAssault.TryCreate(map, reserve, commitment, out assault)) return AdvanceResult.Complete;
            assaults.Add(ownerId, assault);
        }
        AdvanceResult result = assault.Step(out x, out y);
        LastDefenderId = assault.LastDefenderId;
        if (assault.IsComplete)
        {
            LastRefund = assault.Recall();
            reserve.AddManpower(LastRefund);
            assaults.Remove(ownerId);
        }
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

    public AdvanceResult Step(out int x, out int y)
    {
        x = y = -1;
        LastDefenderId = -1;
        if (IsComplete) return AdvanceResult.Complete;
        int best = FindNextCell(out int cheapest);
        if (best < 0)
        {
            // A simultaneous manual order can take our entire current front.
            // Re-seed from the remaining pocket so it cannot strand this wave.
            frontier.Clear();
            SyncTargets();
            best = FindNextCell(out cheapest);
        }
        if (best < 0) { IsComplete = true; return AdvanceResult.Complete; }
        if (force.Manpower < cheapest) { IsComplete = true; return AdvanceResult.OutOfTroops; }
        x = best % map.Width; y = best / map.Width;
        LastDefenderId = map.GetCell(x, y).OwnerId;
        if (!map.TryAdvanceCell(x, y, force, out bool captured))
        { IsComplete = true; return AdvanceResult.Complete; }
        if (!captured) return AdvanceResult.Fighting;
        frontier.Remove(best);
        for (int i = 0; i < 4; i++)
        {
            int nx = x + MapData.NeighborX[i], ny = y + MapData.NeighborY[i];
            if (map.IsEncircledBy(nx, ny, force.Id)) frontier.Add(ny * map.Width + nx);
        }
        return AdvanceResult.Captured;
    }

    private int FindNextCell(out int cheapest)
    {
        int best = -1;
        cheapest = int.MaxValue;
        foreach (int id in frontier)
        {
            int cx = id % map.Width, cy = id / map.Width;
            if (!map.IsEncircledBy(cx, cy, force.Id) || !map.HasOwnedNeighbor(cx, cy, force.Id)) continue;
            int cost = map.GetAdvanceCost(cx, cy, force.Id);
            if (cost < cheapest || (cost == cheapest && id < best)) { best = id; cheapest = cost; }
        }
        return best;
    }

    public int Recall()
    {
        IsComplete = true;
        return force.Recall();
    }
}

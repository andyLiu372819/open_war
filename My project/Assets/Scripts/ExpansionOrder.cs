using System;
using System.Collections.Generic;

public enum AdvanceResult { Captured, Fighting, OutOfTroops, Complete }

// Multiple approaches converge on a click, then continue along its heading.
// Priority increases along a route, so every prong fills out before advancing.
// The order carries a finite force committed at creation and spends only that,
// which is what makes the size of an attack a decision rather than a formality.
public sealed class ExpansionOrder
{
    private readonly MapData map;
    private readonly int ownerId;
    private readonly int enemyId;
    private readonly CommittedForce force;
    private readonly float[] priority;
    private readonly HashSet<int> frontier = new HashSet<int>();
    public int TargetX { get; }
    public int TargetY { get; }
    public bool IsComplete { get; private set; }
    public int LastDefenderId { get; private set; } = -1;
    // One route per attacking front, cheapest first. Prongs that merge share a
    // tail, so a later route ends where it joins one already listed.
    public IReadOnlyList<IReadOnlyList<int>> Routes { get; }
    public IReadOnlyList<int> Route => Routes[0];
    public int FrontCount => Routes.Count;
    // Troops handed to this order, still carried by it, and already spent.
    public int CommittedTroops => force.Committed;
    public int RemainingTroops => force.Manpower;
    public int SpentTroops => force.Spent;

    private ExpansionOrder(MapData map, int ownerId, int tx, int ty,
        int[] next, List<int> origins, int radius, int commitment)
    {
        this.map = map;
        this.ownerId = ownerId;
        force = new CommittedForce(ownerId, commitment);
        enemyId = map.GetCell(tx, ty).OwnerId;
        TargetX = tx; TargetY = ty;
        priority = new float[map.Width * map.Height];
        for (int i = 0; i < priority.Length; i++) priority[i] = float.PositiveInfinity;
        var corridor = new List<int>();
        var routed = new HashSet<int>();
        var routes = new List<IReadOnlyList<int>>();
        foreach (int origin in origins)
        {
            var route = new List<int>();
            for (int id = origin; id >= 0; id = next[id])
            {
                route.Add(id);
                // Prongs converge on the destination and share a tail once they
                // meet. Stamping that tail again would repeat the work without
                // lowering any priority already recorded there.
                if (!routed.Add(id)) break;
                Stamp(id, route.Count - 1, radius, corridor);
            }
            routes.Add(route);
        }
        // The click sets a heading, not an end condition. Keep the existing
        // multi-front approach, then carry its corridor past the clicked tile.
        ExtendDirection(routes, radius, corridor);
        Routes = routes;
        foreach (int id in corridor)
            AddFrontier(id % map.Width, id / map.Width);
    }

    private void ExtendDirection(List<IReadOnlyList<int>> routes, int radius, List<int> corridor)
    {
        if (!map.TryGetTerritoryCenter(ownerId, out float centerX, out float centerY)) return;
        double dx = TargetX + 0.5 - centerX, dy = TargetY + 0.5 - centerY;
        double scale = Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (scale < 0.001) return;
        dx /= scale; dy /= scale;
        var primary = (List<int>)routes[0];
        int startStep = primary.Count - 1;
        int previous = TargetY * map.Width + TargetX;
        for (int step = 1; step <= map.Width + map.Height; step++)
        {
            int x = (int)Math.Round(TargetX + dx * step);
            int y = (int)Math.Round(TargetY + dy * step);
            if (!map.InsideBorder(x, y)) break;
            int id = y * map.Width + x;
            if (id == previous) continue;
            primary.Add(id);
            Stamp(id, startStep + step, radius, corridor);
            previous = id;
        }
    }

    // Priority is the step count from this prong's own origin, so every prong
    // starts at zero and they advance together instead of one finishing first.
    private void Stamp(int cell, int step, int radius, List<int> corridor)
    {
        int px = cell % map.Width, py = cell / map.Width;
        for (int dx = -radius; dx <= radius; dx++)
        for (int dy = -radius; dy <= radius; dy++)
        {
            int x = px + dx, y = py + dy;
            if (dx * dx + dy * dy > radius * radius || !CanEnter(x, y)) continue;
            int id = y * map.Width + x;
            if (float.IsPositiveInfinity(priority[id])) corridor.Add(id);
            float score = step + (float)Math.Sqrt(dx * dx + dy * dy) * 1.3f;
            priority[id] = Math.Min(priority[id], score);
        }
    }

    // frontSpread is how much longer than the best approach a front's route may
    // be and still join the attack; maxFronts caps how many prongs are opened.
    public static bool TryCreate(MapData map, int ownerId, int tx, int ty,
        int radius, int commitment, out ExpansionOrder order,
        float frontSpread = 1.5f, int maxFronts = 6)
    {
        order = null;
        if (ownerId < 0 || commitment <= 0 || !map.IsWalkable(tx, ty) ||
            map.IsOwnedBy(tx, ty, ownerId)) return false;
        radius = Math.Max(1, radius);
        maxFronts = Math.Max(1, maxFronts);
        int enemy = map.GetCell(tx, ty).OwnerId;
        int length = map.Width * map.Height;
        var distance = new float[length];
        var next = new int[length];
        for (int i = 0; i < length; i++) { distance[i] = float.PositiveInfinity; next[i] = -1; }
        int target = ty * map.Width + tx;
        var heap = new GridHeap();
        distance[target] = 0f;
        heap.Push(target, 0f);
        var origins = new List<int>();
        float limit = float.PositiveInfinity;
        // Search back from the destination across neutral and enemy ground. The
        // search never continues through our own land, so every owned tile it
        // reaches is a border tile already facing the destination.
        while (heap.Count > 0)
        {
            int id = heap.Pop(out float cost);
            if (cost > distance[id]) continue;
            if (cost > limit) break;
            int x = id % map.Width, y = id / map.Width;
            if (map.IsOwnedBy(x, y, ownerId))
            {
                // The cheapest approach sets the standard the others are judged
                // against. The radius term keeps a close-quarters attack at
                // least as wide as the corridor it advances through.
                if (origins.Count == 0)
                    limit = cost * Math.Max(1f, frontSpread) +
                        TerrainRules.ExpansionCost(TerrainType.Land) * radius;
                // Neighbouring border tiles would only produce the same prong.
                if (Separated(map, origins, x, y, radius * 2)) origins.Add(id);
                if (origins.Count >= maxFronts) break;
                continue;
            }
            for (int i = 0; i < 4; i++)
            {
                int nx = x + MapData.NeighborX[i], ny = y + MapData.NeighborY[i];
                if (!map.IsWalkable(nx, ny)) continue;
                int occupant = map.GetCell(nx, ny).OwnerId;
                if (occupant >= 0 && occupant != ownerId && occupant != enemy) continue;
                int neighbor = ny * map.Width + nx;
                float candidate = cost + map.GetAdvanceCost(x, y, ownerId);
                if (candidate >= distance[neighbor]) continue;
                distance[neighbor] = candidate;
                next[neighbor] = id;
                heap.Push(neighbor, candidate);
            }
        }
        if (origins.Count == 0) return false;
        order = new ExpansionOrder(map, ownerId, tx, ty, next, origins, radius, commitment);
        return true;
    }

    private static bool Separated(MapData map, List<int> origins, int x, int y, int separation)
    {
        int squared = separation * separation;
        foreach (int id in origins)
        {
            int dx = id % map.Width - x, dy = id / map.Width - y;
            if (dx * dx + dy * dy < squared) return false;
        }
        return true;
    }

    private bool CanEnter(int x, int y)
    {
        if (!map.IsWalkable(x, y)) return false;
        int occupant = map.GetCell(x, y).OwnerId;
        return occupant < 0 || occupant == ownerId || occupant == enemyId;
    }

    private void AddFrontier(int x, int y)
    {
        if (!CanEnter(x, y) || map.IsOwnedBy(x, y, ownerId)) return;
        int id = y * map.Width + x;
        if (!float.IsPositiveInfinity(priority[id]) && map.HasOwnedNeighbor(x, y, ownerId))
            frontier.Add(id);
    }

    public AdvanceResult Step(out int x, out int y)
    {
        x = y = -1;
        LastDefenderId = -1;
        if (IsComplete)
        { IsComplete = true; return AdvanceResult.Complete; }
        int best = FindNextCell();
        if (best < 0)
        {
            // Automatic pocket assaults may have taken this order's frontier.
            // Check the remaining corridor before declaring that it is blocked.
            frontier.Clear();
            foreach (int id in map.OwnedCells(ownerId))
                for (int i = 0; i < 4; i++)
                    AddFrontier(id % map.Width + MapData.NeighborX[i], id / map.Width + MapData.NeighborY[i]);
            best = FindNextCell();
        }
        if (best < 0) { IsComplete = true; return AdvanceResult.Complete; }
        x = best % map.Width; y = best / map.Width;
        // The committed force is finite. An advance that cannot pay for its next
        // action is spent, not paused: recruits go to the reserve, not the front.
        if (force.Manpower < map.GetAdvanceCost(x, y, ownerId))
        { IsComplete = true; return AdvanceResult.OutOfTroops; }
        LastDefenderId = map.GetCell(x, y).OwnerId;
        if (!map.TryAdvanceCell(x, y, force, out bool captured))
        { IsComplete = true; return AdvanceResult.Complete; }
        if (!captured) return AdvanceResult.Fighting;
        frontier.Remove(best);
        for (int i = 0; i < 4; i++) AddFrontier(x + MapData.NeighborX[i], y + MapData.NeighborY[i]);
        return AdvanceResult.Captured;
    }

    private int FindNextCell()
    {
        int best = -1;
        float score = float.PositiveInfinity;
        foreach (int id in frontier)
        {
            int x = id % map.Width, y = id / map.Width;
            if (!CanEnter(x, y) || map.IsOwnedBy(x, y, ownerId) || !map.HasOwnedNeighbor(x, y, ownerId)) continue;
            if (priority[id] < score || (priority[id] == score && id < best))
            { best = id; score = priority[id]; }
        }
        return best;
    }

    // Ends the order and hands the surviving troops back for the reserve.
    public int Recall()
    {
        IsComplete = true;
        return force.Recall();
    }
}

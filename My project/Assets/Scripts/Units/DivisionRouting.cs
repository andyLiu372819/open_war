using System;
using System.Collections.Generic;

// Route authoring for individual formations, groups, and defensive deployments.
// Kept in DivisionSystem's partial API so callers do not depend on pathfinding.
public sealed partial class DivisionSystem
{
    // Sends a group to one destination without piling every unit on one cell:
    // each division takes the nearest free cell to the click that no other
    // division in the group has already been given.
    public int OrderGroup(IReadOnlyList<Division> group, int targetX, int targetY)
    {
        if (group == null || group.Count == 0) return 0;
        var taken = new HashSet<int>();
        int ordered = 0;
        foreach (Division division in group)
        {
            if (division == null || division.IsDestroyed) continue;
            if (!TryFindFreeTarget(targetX, targetY, taken, out int tx, out int ty)) continue;
            if (!TryOrder(division, tx, ty)) continue;
            taken.Add(ty * map.Width + tx);
            ordered++;
        }
        return ordered;
    }

    private bool TryFindFreeTarget(int targetX, int targetY, HashSet<int> taken, out int x, out int y)
    {
        x = y = -1;
        for (int radius = 0; radius < 12; radius++)
        for (int dx = -radius; dx <= radius; dx++)
        for (int dy = -radius; dy <= radius; dy++)
        {
            // Only the cells newly added at this radius.
            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) continue;
            int cx = targetX + dx, cy = targetY + dy;
            if (!map.IsWalkable(cx, cy)) continue;
            int id = cy * map.Width + cx;
            if (taken.Contains(id)) continue;
            x = cx; y = cy;
            return true;
        }
        return false;
    }

    // Terrain-weighted route to the destination across any ground the division
    // may enter. Water and deep rivers still block, fords still cross.
    public bool TryOrder(Division division, int targetX, int targetY)
    {
        if (division == null || division.IsDestroyed) return false;
        if (!map.IsWalkable(targetX, targetY)) return false;
        if (division.X == targetX && division.Y == targetY) { division.ClearOrders(); return false; }
        int length = map.Width * map.Height;
        var distance = new float[length];
        var previous = new int[length];
        for (int i = 0; i < length; i++) { distance[i] = float.PositiveInfinity; previous[i] = -1; }
        int start = division.Y * map.Width + division.X;
        int goal = targetY * map.Width + targetX;
        var heap = new GridHeap();
        distance[start] = 0f;
        heap.Push(start, 0f);
        bool reached = false;
        while (heap.Count > 0)
        {
            int id = heap.Pop(out float cost);
            if (cost > distance[id]) continue;
            if (id == goal) { reached = true; break; }
            int x = id % map.Width, y = id / map.Width;
            for (int i = 0; i < 4; i++)
            {
                int nx = x + MapData.NeighborX[i], ny = y + MapData.NeighborY[i];
                if (!map.IsWalkable(nx, ny)) continue;
                int neighbour = ny * map.Width + nx;
                float candidate = cost + map.GetMovementCost(nx, ny);
                if (candidate >= distance[neighbour]) continue;
                distance[neighbour] = candidate;
                previous[neighbour] = id;
                heap.Push(neighbour, candidate);
            }
        }
        if (!reached) return false;
        var reversed = new List<int>();
        for (int id = goal; id >= 0 && id != start; id = previous[id]) reversed.Add(id);
        reversed.Reverse();
        if (reversed.Count == 0) return false;
        // A holding division that is told to go somewhere is going somewhere,
        // and it gives up the stretch of front it was holding.
        if (division.Stance == DivisionStance.Defend || division.Stance == DivisionStance.Reserve)
            division.Stance = DivisionStance.Attack;
        ReleaseEntrenchment(division);
        division.ClearHeldLine();
        division.SetRoute(reversed, targetX, targetY);
        return true;
    }

    // Builds a terrain-weighted route to whichever assigned line cell is
    // cheapest to reach. Defensive redeployment stays on currently friendly
    // ground: a Defend order never turns into an accidental attack.
    private bool RouteDefenderToLine(Division division)
    {
        if (division == null || division.HeldLine.Count == 0 || IsOnHeldLine(division)) return false;
        int length = map.Width * map.Height;
        var goals = new HashSet<int>();
        foreach (int id in division.HeldLine)
        {
            int x = id % map.Width, y = id / map.Width;
            if (map.IsOwnedBy(x, y, division.OwnerId) && map.IsWalkable(x, y)) goals.Add(id);
        }
        if (goals.Count == 0) return false;

        var distance = new float[length];
        var previous = new int[length];
        for (int i = 0; i < length; i++) { distance[i] = float.PositiveInfinity; previous[i] = -1; }
        int start = division.Cell, goal = -1;
        var heap = new GridHeap();
        distance[start] = 0f;
        heap.Push(start, 0f);
        while (heap.Count > 0)
        {
            int id = heap.Pop(out float cost);
            if (cost > distance[id]) continue;
            if (goals.Contains(id)) { goal = id; break; }
            int x = id % map.Width, y = id / map.Width;
            for (int i = 0; i < 4; i++)
            {
                int nx = x + MapData.NeighborX[i], ny = y + MapData.NeighborY[i];
                if (!map.IsWalkable(nx, ny) || !map.IsOwnedBy(nx, ny, division.OwnerId)) continue;
                int neighbour = ny * map.Width + nx;
                float candidate = cost + map.GetMovementCost(nx, ny);
                if (candidate >= distance[neighbour]) continue;
                distance[neighbour] = candidate;
                previous[neighbour] = id;
                heap.Push(neighbour, candidate);
            }
        }
        if (goal < 0) return false;
        var route = new List<int>();
        for (int id = goal; id >= 0 && id != start; id = previous[id]) route.Add(id);
        route.Reverse();
        if (route.Count == 0) return false;
        division.SetRoute(route, goal % map.Width, goal / map.Width);
        return true;
    }
}

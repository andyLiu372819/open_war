using System;
using System.Collections.Generic;

public static class StartingTerritories
{
    public static void Create(MapData map, int playerCount)
    {
        List<int> land = map.LargestLandRegion();
        if (land.Count < playerCount * 80)
            throw new InvalidOperationException("Not enough connected land for the starting territories.");
        var starts = new List<int>();
        for (int owner = 0; owner < playerCount; owner++)
        {
            int best = -1;
            float bestScore = float.NegativeInfinity;
            foreach (int id in land)
            {
                int x = id % map.Width, y = id / map.Width;
                if (map.GetCell(x, y).OwnerId >= 0) continue;
                float score;
                if (owner == 0)
                {
                    float dx = x - map.Width * 0.35f, dy = y - map.Height * 0.5f;
                    score = -(dx * dx + dy * dy);
                }
                else
                {
                    score = float.PositiveInfinity;
                    foreach (int start in starts)
                    {
                        float dx = x - start % map.Width, dy = y - start / map.Width;
                        score = Math.Min(score, dx * dx + dy * dy);
                    }
                }
                // Avoid placing the initial territory directly on a river or peak.
                score -= TerrainRules.ExpansionCost(map.GetCell(x, y).Terrain) * 8;
                if (score > bestScore) { bestScore = score; best = id; }
            }
            if (best < 0) throw new InvalidOperationException("Could not place a starting territory.");
            starts.Add(best);
            SeedBlob(map, best, owner, 80);
        }
    }

    private static void SeedBlob(MapData map, int start, int owner, int size)
    {
        var queue = new Queue<int>();
        var seen = new HashSet<int> { start };
        queue.Enqueue(start);
        int claimed = 0;
        while (queue.Count > 0 && claimed < size)
        {
            int id = queue.Dequeue();
            int x = id % map.Width, y = id / map.Width;
            if (!map.TryClaimCell(x, y, owner)) continue;
            claimed++;
            for (int i = 0; i < 4; i++)
            {
                int nx = x + MapData.NeighborX[i], ny = y + MapData.NeighborY[i];
                if (!map.IsWalkable(nx, ny)) continue;
                int next = ny * map.Width + nx;
                if (seen.Add(next)) queue.Enqueue(next);
            }
        }
    }
}

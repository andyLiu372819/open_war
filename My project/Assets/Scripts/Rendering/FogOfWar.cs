using System.Collections.Generic;

// What one faction can currently see. A nation observes the ground it holds,
// a march of country beyond its own border, and a circle around each of its
// divisions. Everything else is dark, including enemy formations standing in it.
public sealed class FogOfWar
{
    // How far past its own border a nation can see.
    public const int BorderWatch = 6;
    // How far a division sees around itself.
    public const int DivisionWatch = 14;

    private readonly MapData map;
    private bool[] visible;
    private readonly Queue<int> frontier = new Queue<int>();
    private int[] depth;
    public int OwnerId { get; }
    // Debug reveal: when set, everything counts as seen. The fog itself is
    // still computed, so turning it back off restores the real picture.
    public bool Revealed { get; set; }
    public int VisibleCells { get; private set; }

    public FogOfWar(MapData map, int ownerId)
    {
        this.map = map ?? throw new System.ArgumentNullException(nameof(map));
        if (ownerId < 0) throw new System.ArgumentOutOfRangeException(nameof(ownerId));
        OwnerId = ownerId;
        visible = new bool[map.Width * map.Height];
        depth = new int[visible.Length];
    }

    public bool IsVisible(int x, int y)
    {
        if (!map.InsideBorder(x, y)) return false;
        return Revealed || visible[y * map.Width + x];
    }

    public bool IsVisible(int cell) =>
        Revealed || (cell >= 0 && cell < visible.Length && visible[cell]);

    // Recomputed on periodic simulation ticks and dirty gameplay events, not
    // every render frame. Owned ground seeds a limited flood outward, and each
    // division adds a circle wherever it stands.
    public void Refresh(IReadOnlyList<Division> divisions)
    {
        System.Array.Clear(visible, 0, visible.Length);
        for (int i = 0; i < depth.Length; i++) depth[i] = int.MaxValue;
        frontier.Clear();
        foreach (int id in map.OwnedCells(OwnerId))
        {
            visible[id] = true;
            depth[id] = 0;
            frontier.Enqueue(id);
        }
        // Sight passes over water as readily as land, so a coast is not blind.
        while (frontier.Count > 0)
        {
            int id = frontier.Dequeue();
            int step = depth[id];
            if (step >= BorderWatch) continue;
            int x = id % map.Width, y = id / map.Width;
            for (int i = 0; i < 4; i++)
            {
                int nx = x + MapData.NeighborX[i], ny = y + MapData.NeighborY[i];
                if (!map.InsideBorder(nx, ny)) continue;
                int next = ny * map.Width + nx;
                if (depth[next] <= step + 1) continue;
                depth[next] = step + 1;
                visible[next] = true;
                frontier.Enqueue(next);
            }
        }
        if (divisions != null)
            foreach (Division division in divisions)
            {
                if (division.OwnerId != OwnerId) continue;
                Illuminate(division.X, division.Y, DivisionWatch);
            }
        VisibleCells = 0;
        for (int i = 0; i < visible.Length; i++) if (visible[i]) VisibleCells++;
    }

    private void Illuminate(int centreX, int centreY, int radius)
    {
        for (int dx = -radius; dx <= radius; dx++)
        for (int dy = -radius; dy <= radius; dy++)
        {
            if (dx * dx + dy * dy > radius * radius) continue;
            int x = centreX + dx, y = centreY + dy;
            if (!map.InsideBorder(x, y)) continue;
            visible[y * map.Width + x] = true;
        }
    }

    // True when this faction can see the division: its own always, an enemy
    // only while it stands in observed ground.
    public bool CanSee(Division division) =>
        division != null && (Revealed || division.OwnerId == OwnerId ||
            IsVisible(division.X, division.Y));
}

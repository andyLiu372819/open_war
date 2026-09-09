using System;
using System.Collections.Generic;

public class MapData
{
    public int Width { get; }
    public int Height { get; }
    private MapCell[,] cells;
    private sealed class Territory
    {
        public readonly HashSet<int> Cells = new HashSet<int>();
        public long SumX;
        public long SumY;
        public int Version;
        public int EnclosureVersion;
    }
    private readonly Dictionary<int, Territory> territories = new Dictionary<int, Territory>();
    private readonly Dictionary<int, HashSet<int>> encircled = new Dictionary<int, HashSet<int>>();
    public int EncirclementVersion { get; private set; }
    public int OwnershipVersion { get; private set; }
    public static readonly int[] NeighborX = { -1, 1, 0, 0 };
    public static readonly int[] NeighborY = { 0, 0, -1, 1 };

    public MapData(int width, int height, int seed = 1847)
    {
        if (width < 4 || height < 4)
            throw new ArgumentOutOfRangeException(nameof(width), "Map dimensions must be at least 4.");
        Width = width;
        Height = height;
        cells = TerrainGenerator.Generate(width, height, seed);
    }

    // Hand-authored maps are useful for scenarios and simulation tests.
    public MapData(TerrainType[,] terrain)
    {
        Width = terrain.GetLength(0);
        Height = terrain.GetLength(1);
        cells = new MapCell[Width, Height];
        for (int x = 0; x < Width; x++)
        for (int y = 0; y < Height; y++)
            cells[x, y] = new MapCell(terrain[x, y]);
    }

    public MapCell GetCell(int x, int y)
    {
        if (InsideBorder(x, y))
        {
            return cells[x, y];
        }

        throw new System.IndexOutOfRangeException(
            "Cell coordinates are outside the map."
        );
    }

    public bool TryClaimCell(int x, int y, int ownerId)
    {
        bool claimable = (InsideBorder(x, y) && ownerId >= 0);
        if (!claimable)
        {
            return false;
        }

        MapCell tempCell = GetCell(x, y);
        claimable = TerrainRules.IsWalkable(tempCell.Terrain) &&
            tempCell.OwnerId == -1;

        if (!claimable)
        {
            return false;
        }

        SetOwner(x, y, ownerId);
        return true;
    }

    public bool TryExpandCell(int x, int y, int ownerId)
    {
        return ownerId >= 0 && HasOwnedNeighbor(x, y, ownerId) &&
            TryClaimCell(x, y, ownerId);
    }

    public bool IsWalkable(int x, int y) => InsideBorder(x, y) &&
        TerrainRules.IsWalkable(cells[x, y].Terrain);

    public bool HasOwnedNeighbor(int x, int y, int ownerId)
    {
        if (!InsideBorder(x, y)) return false;
        for (int i = 0; i < 4; i++)
            if (IsOwnedBy(x + NeighborX[i], y + NeighborY[i], ownerId)) return true;
        return false;
    }

    public int GetAdvanceCost(int x, int y, int attackerId = -1)
    {
        MapCell cell = GetCell(x, y);
        if (!TerrainRules.IsWalkable(cell.Terrain)) return int.MaxValue;
        int cost = cell.OwnerId >= 0 ? TerrainRules.AttackCost(cell.Terrain) : TerrainRules.ExpansionCost(cell.Terrain);
        if (IsEncircledBy(x, y, attackerId))
        {
            if (cell.OwnerId < 0) return 0;
            cost = Math.Max(1, (int)Math.Ceiling(cost * TerrainRules.EncirclementCostMultiplier));
        }
        return cost;
    }

    // Paying and changing ownership happen together; failed actions cost nothing.
    public bool TryAdvanceCell(int x, int y, ITroopSource attacker, out bool captured)
    {
        captured = false;
        if (!IsWalkable(x, y) || IsOwnedBy(x, y, attacker.Id) ||
            !HasOwnedNeighbor(x, y, attacker.Id)) return false;
        MapCell cell = cells[x, y];
        int cost = GetAdvanceCost(x, y, attacker.Id);
        if (cost > 0 && !attacker.TrySpendManpower(cost)) return false;
        if (cell.OwnerId >= 0)
        {
            cell.Defense = Math.Max(0, cell.Defense - 12);
            if (cell.Defense > 0) return true;
        }
        SetOwner(x, y, attacker.Id);
        captured = true;
        return true;
    }

    private void SetOwner(int x, int y, int ownerId)
    {
        int oldOwner = cells[x, y].OwnerId;
        if (oldOwner == ownerId) return;
        int id = y * Width + x;
        // Losing a wall can open a pocket immediately. Adding own land cannot
        // open a pocket, so existing discounts survive captures inside it.
        if (encircled.TryGetValue(oldOwner, out HashSet<int> oldPockets) && oldPockets.Count > 0)
        {
            oldPockets.Clear();
            EncirclementVersion++;
        }
        bool capturedInsidePocket = encircled.TryGetValue(ownerId, out HashSet<int> ownPockets) && ownPockets.Remove(id);
        if (capturedInsidePocket)
            EncirclementVersion++;
        if (territories.TryGetValue(oldOwner, out Territory oldTerritory))
        {
            oldTerritory.Cells.Remove(id);
            oldTerritory.SumX -= x;
            oldTerritory.SumY -= y;
            oldTerritory.Version++;
            oldTerritory.EnclosureVersion++;
        }
        if (!territories.TryGetValue(ownerId, out Territory territory))
        {
            territory = new Territory();
            territories.Add(ownerId, territory);
        }
        territory.Cells.Add(id);
        territory.SumX += x;
        territory.SumY += y;
        territory.Version++;
        if (!capturedInsidePocket) territory.EnclosureVersion++;
        cells[x, y].OwnerId = ownerId;
        cells[x, y].Defense = TerrainRules.Defense(cells[x, y].Terrain);
        OwnershipVersion++;
    }

    public int CountTerritory(int ownerId)
    {
        return territories.TryGetValue(ownerId, out Territory territory) ? territory.Cells.Count : 0;
    }

    public int GetTerritoryVersion(int ownerId) =>
        territories.TryGetValue(ownerId, out Territory territory) ? territory.Version : 0;

    internal int GetEnclosureVersion(int ownerId) =>
        territories.TryGetValue(ownerId, out Territory territory) ? territory.EnclosureVersion : 0;

    internal IEnumerable<int> OwnedCells(int ownerId)
    {
        if (territories.TryGetValue(ownerId, out Territory territory))
            foreach (int id in territory.Cells) yield return id;
    }

    public bool IsEncircledBy(int x, int y, int ownerId) => ownerId >= 0 &&
        InsideBorder(x, y) && !IsOwnedBy(x, y, ownerId) &&
        encircled.TryGetValue(ownerId, out HashSet<int> pocket) && pocket.Contains(y * Width + x);

    internal void SetEncircledCells(int ownerId, HashSet<int> cellsInPockets)
    {
        encircled[ownerId] = cellsInPockets;
        EncirclementVersion++;
    }

    public int EncircledCellCount(int ownerId) =>
        encircled.TryGetValue(ownerId, out HashSet<int> pocket) ? pocket.Count : 0;

    internal IEnumerable<int> EncircledCells(int ownerId)
    {
        if (encircled.TryGetValue(ownerId, out HashSet<int> pocket))
            foreach (int id in pocket) yield return id;
    }

    public List<int> LargestLandRegion()
    {
        var largest = new List<int>();
        var visited = new bool[Width * Height];
        for (int start = 0; start < visited.Length; start++)
        {
            if (visited[start] || !IsWalkable(start % Width, start / Width)) continue;
            var region = new List<int> { start };
            visited[start] = true;
            for (int head = 0; head < region.Count; head++)
            {
                int x = region[head] % Width, y = region[head] / Width;
                for (int i = 0; i < 4; i++)
                {
                    int nx = x + NeighborX[i], ny = y + NeighborY[i];
                    if (!IsWalkable(nx, ny)) continue;
                    int id = ny * Width + nx;
                    if (visited[id]) continue;
                    visited[id] = true;
                    region.Add(id);
                }
            }
            if (region.Count > largest.Count) largest = region;
        }
        return largest;
    }

    public bool TryGetTerritoryCenter(int ownerId, out float centerX, out float centerY)
    {
        centerX = centerY = 0f;
        if (ownerId < 0 || !territories.TryGetValue(ownerId, out Territory territory) ||
            territory.Cells.Count == 0) return false;

        // The original average-then-nearest algorithm, restricted to owned cells.
        // Sums are maintained during captures, so no full-map first pass is needed.
        double averageX = territory.SumX / (double)territory.Cells.Count + 0.5;
        double averageY = territory.SumY / (double)territory.Cells.Count + 0.5;
        double closestDistance = double.PositiveInfinity;
        int bestOrder = int.MaxValue;
        foreach (int id in territory.Cells)
        {
            int x = id % Width, y = id / Width;
            double dx = x + 0.5 - averageX, dy = y + 0.5 - averageY;
            double distanceSquared = dx * dx + dy * dy;
            int tieOrder = x * Height + y;
            if (distanceSquared < closestDistance ||
                (distanceSquared == closestDistance && tieOrder < bestOrder))
            {
                closestDistance = distanceSquared;
                bestOrder = tieOrder;
                centerX = x + 0.5f;
                centerY = y + 0.5f;
            }
        }
        return true;
    }

    public bool InsideBorder(int x, int y)
    {
        return x >= 0 && x < Width &&
            y >= 0 && y < Height;
    }

    public bool IsOwnedBy(int x, int y, int ownerId)
    {
        return InsideBorder(x, y) &&
            cells[x, y].OwnerId == ownerId;
    }
}

using System;
using System.Collections.Generic;

// Seeded value noise, elevation-based biomes, and drainage rivers. No Unity
// dependency: the simulation can be tested separately from the scene.
public static class TerrainGenerator
{
    private static readonly int[] NeighbourX = { -1, 1, 0, 0 };
    private static readonly int[] NeighbourY = { 0, 0, -1, 1 };

    public static MapCell[,] Generate(int width, int height, int seed)
    {
        var cells = new MapCell[width, height];
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            float nx = x / (float)(width - 1) * 2f - 1f;
            float ny = y / (float)(height - 1) * 2f - 1f;
            float elevation = Clamp01(0.84f - 0.64f * (nx * nx + ny * ny)
                + (Fractal(nx * 3f + 9f, ny * 3f + 9f, seed) - 0.5f) * 0.8f);
            if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
                elevation = 0.1f;
            float moisture = Fractal(nx * 4f + 19f, ny * 4f + 19f, seed + 71);
            TerrainType terrain;
            if (elevation < 0.35f) terrain = TerrainType.Water;
            else if (elevation < 0.385f) terrain = TerrainType.Coast;
            else if (elevation > 0.85f) terrain = TerrainType.Snow;
            else if (elevation > 0.73f) terrain = TerrainType.Mountains;
            else if (elevation > 0.65f) terrain = TerrainType.Hills;
            else if (moisture < 0.44f && ny < 0.25f) terrain = TerrainType.Desert;
            else if (moisture > 0.51f) terrain = TerrainType.Forest;
            else terrain = TerrainType.Land;
            cells[x, y] = new MapCell(terrain, elevation);
        }
        CarveRivers(cells, seed);
        return cells;
    }

    // A square landmass ringed by ocean, identical on both sides of the centre
    // line. Everything is generated for the left half and mirrored, so the two
    // players face exactly the same terrain, rivers, and coastline.
    public static MapCell[,] GenerateDuel(int size, int seed)
    {
        if (size < 64) throw new ArgumentOutOfRangeException(nameof(size), "A duel map must be at least 64 cells.");
        if (size % 2 != 0) throw new ArgumentException("A duel map must be an even number of cells wide.", nameof(size));
        var cells = new MapCell[size, size];
        int half = size / 2;
        for (int x = 0; x < half; x++)
        for (int y = 0; y < size; y++)
        {
            float nx = x / (float)(size - 1) * 2f - 1f;
            float ny = y / (float)(size - 1) * 2f - 1f;
            // A Chebyshev falloff gives a square coast rather than a round island.
            float edge = Math.Max(Math.Abs(nx), Math.Abs(ny));
            float rim = Clamp01((0.90f - edge) / 0.16f);
            float noise = Fractal(nx * 3.4f + 11f, ny * 3.4f + 11f, seed);
            float elevation = Clamp01((0.66f + (noise - 0.5f) * 0.62f) * rim);
            float moisture = Fractal(nx * 4.2f + 23f, ny * 4.2f + 23f, seed + 71);
            cells[x, y] = new MapCell(Biome(elevation, moisture, ny), elevation);
        }
        MirrorHalves(cells, size);
        // Rivers are carved across the whole square and then mirrored again, so
        // the drainage search cannot introduce a difference between the sides.
        CarveRivers(cells, seed, 2, 24);
        MirrorHalves(cells, size);
        return cells;
    }

    // Copies the left half onto the right, flipped. Doing this after every stage
    // is what guarantees the two players get identical ground.
    private static void MirrorHalves(MapCell[,] cells, int size)
    {
        int half = size / 2;
        for (int x = 0; x < half; x++)
        for (int y = 0; y < size; y++)
        {
            MapCell source = cells[x, y];
            cells[size - 1 - x, y] = new MapCell(source.Terrain, source.Elevation);
        }
    }

    private static TerrainType Biome(float elevation, float moisture, float ny)
    {
        if (elevation < 0.35f) return TerrainType.Water;
        if (elevation < 0.385f) return TerrainType.Coast;
        if (elevation > 0.85f) return TerrainType.Snow;
        if (elevation > 0.73f) return TerrainType.Mountains;
        if (elevation > 0.65f) return TerrainType.Hills;
        if (moisture < 0.44f && ny < 0.25f) return TerrainType.Desert;
        if (moisture > 0.51f) return TerrainType.Forest;
        return TerrainType.Land;
    }

    private static void CarveRivers(MapCell[,] cells, int seed, int riverWidth = 1, int sourceCount = 7)
    {
        int width = cells.GetLength(0), height = cells.GetLength(1);
        riverWidth = Math.Max(1, riverWidth);
        int[] downstream = new int[width * height];
        bool[] visited = new bool[downstream.Length];
        var heap = new GridHeap();
        // Flood inland from the sea: every parent chain drains to water,
        // including basins that would trap a naive downhill river walk.
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int id = y * width + x;
            downstream[id] = -1;
            if (cells[x, y].Terrain != TerrainType.Water) continue;
            visited[id] = true;
            heap.Push(id, cells[x, y].Elevation);
        }
        int[] dx = NeighbourX, dy = NeighbourY;
        while (heap.Count > 0)
        {
            int id = heap.Pop(out float level);
            int x = id % width, y = id / width;
            for (int i = 0; i < 4; i++)
            {
                int nx = x + dx[i], ny = y + dy[i];
                if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                int next = ny * width + nx;
                if (visited[next]) continue;
                visited[next] = true;
                downstream[next] = id;
                heap.Push(next, Math.Max(level + 0.00001f, cells[nx, ny].Elevation));
            }
        }

        var random = new Random(seed);
        var sources = new List<int>();
        var channel = new List<int>();
        for (int attempt = 0; attempt < sourceCount * 600 && sources.Count < sourceCount; attempt++)
        {
            int x = random.Next(1, width - 1), y = random.Next(1, height - 1);
            if (cells[x, y].Elevation < 0.68f) continue;
            bool close = false;
            foreach (int source in sources)
            {
                int sx = x - source % width, sy = y - source / width;
                if (sx * sx + sy * sy < Math.Min(width, height) * 3) close = true;
            }
            if (close) continue;
            int id = y * width + x;
            sources.Add(id);
            // Walk the whole course first. Widening as we went would convert the
            // next cell downstream and cut the river off after a single step.
            channel.Clear();
            while (id >= 0)
            {
                x = id % width; y = id / width;
                MapCell step = cells[x, y];
                if (step.Terrain == TerrainType.Water || step.Terrain == TerrainType.River ||
                    step.Terrain == TerrainType.Ford) break;
                channel.Add(id);
                id = downstream[id];
            }
            for (int length = 0; length < channel.Count; length++)
            {
                x = channel[length] % width; y = channel[length] / width;
                // Periodic shallow crossings make both river banks reachable.
                TerrainType terrain = length % 12 == 6 ? TerrainType.Ford : TerrainType.River;
                MapCell old = cells[x, y];
                cells[x, y] = new MapCell(terrain, old.Elevation);
                // A wider channel reads far better on the map than one cell, and
                // a ford widens with it so the crossing stays usable.
                for (int side = 0; side < 4 && riverWidth > 1; side++)
                for (int spread = 1; spread < riverWidth; spread++)
                {
                    int wx = x + NeighbourX[side] * spread, wy = y + NeighbourY[side] * spread;
                    if (wx < 1 || wy < 1 || wx >= width - 1 || wy >= height - 1) continue;
                    // Never widen over the course itself, or a ford turns to river.
                    if (channel.Contains(wy * width + wx)) continue;
                    MapCell neighbour = cells[wx, wy];
                    if (neighbour.Terrain == TerrainType.Water ||
                        neighbour.Terrain == TerrainType.River ||
                        neighbour.Terrain == TerrainType.Ford) continue;
                    cells[wx, wy] = new MapCell(terrain, neighbour.Elevation);
                }
            }
        }
    }

    private static float Fractal(float x, float y, int seed)
    {
        float value = 0f, weight = 0.55f, total = 0f;
        for (int octave = 0; octave < 5; octave++)
        {
            value += Noise(x, y, seed + octave * 101) * weight;
            total += weight;
            x *= 2f; y *= 2f; weight *= 0.5f;
        }
        return value / total;
    }

    private static float Noise(float x, float y, int seed)
    {
        int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
        float tx = x - ix, ty = y - iy;
        tx = tx * tx * (3f - 2f * tx);
        ty = ty * ty * (3f - 2f * ty);
        float a = Hash(ix, iy, seed), b = Hash(ix + 1, iy, seed);
        float c = Hash(ix, iy + 1, seed), d = Hash(ix + 1, iy + 1, seed);
        return (a + (b - a) * tx) * (1f - ty) + (c + (d - c) * tx) * ty;
    }

    private static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint n = (uint)(x * 374761393 + y * 668265263 + seed * 144269);
            n = (n ^ (n >> 13)) * 1274126177;
            return ((n ^ (n >> 16)) & 0x00ffffff) / 16777215f;
        }
    }

    private static float Clamp01(float value) => Math.Max(0f, Math.Min(1f, value));
}

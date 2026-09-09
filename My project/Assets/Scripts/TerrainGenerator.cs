using System;
using System.Collections.Generic;

// Seeded value noise, elevation-based biomes, and drainage rivers. No Unity
// dependency: the simulation can be tested separately from the scene.
public static class TerrainGenerator
{
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

    private static void CarveRivers(MapCell[,] cells, int seed)
    {
        int width = cells.GetLength(0), height = cells.GetLength(1);
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
        int[] dx = { -1, 1, 0, 0 }, dy = { 0, 0, -1, 1 };
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
        for (int attempt = 0; attempt < 3000 && sources.Count < 7; attempt++)
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
            int length = 0;
            while (id >= 0)
            {
                x = id % width; y = id / width;
                MapCell old = cells[x, y];
                if (old.Terrain == TerrainType.Water || old.Terrain == TerrainType.River ||
                    old.Terrain == TerrainType.Ford) break;
                // Periodic shallow crossings make both river banks reachable.
                TerrainType terrain = length % 12 == 6 ? TerrainType.Ford : TerrainType.River;
                cells[x, y] = new MapCell(terrain, old.Elevation);
                id = downstream[id];
                length++;
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

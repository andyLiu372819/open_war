using System;
using System.Collections.Generic;

// A scenario route is only a chain of surveyed waypoints. The transport
// network resolves each leg onto actual walkable map cells, bending around
// water and expensive terrain, then exposes those built corridors to both the
// renderer and the division pathfinder.
public sealed class MapTransportRoute
{
    private readonly List<int> cells;
    public string Name { get; }
    public InfrastructureRouteType Type { get; }
    public IReadOnlyList<int> Cells => cells;

    internal MapTransportRoute(string name, InfrastructureRouteType type, List<int> routeCells)
    {
        Name = name;
        Type = type;
        cells = routeCells;
    }
}

public sealed class MapTransportNetwork
{
    private readonly bool[] roads;
    private readonly bool[] rails;
    private readonly List<MapTransportRoute> routes = new List<MapTransportRoute>();
    public IReadOnlyList<MapTransportRoute> Routes => routes;

    private MapTransportNetwork(int cellCount)
    {
        roads = new bool[cellCount];
        rails = new bool[cellCount];
    }

    public static MapTransportNetwork Build(ScenarioMap scenario, MapData map)
    {
        if (map == null) throw new ArgumentNullException(nameof(map));
        var network = new MapTransportNetwork(map.Width * map.Height);
        if (scenario == null) return network;
        foreach (ScenarioInfrastructureRoute source in scenario.InfrastructureRoutes)
        {
            var cells = new List<int>();
            for (int point = 1; point < source.Points.Count; point++)
            {
                ScenarioRoutePoint from = source.Points[point - 1];
                ScenarioRoutePoint to = source.Points[point];
                List<int> leg = ResolveLeg(map, network, from.X, from.Y, to.X, to.Y);
                for (int i = cells.Count > 0 && leg.Count > 0 && cells[cells.Count - 1] == leg[0] ? 1 : 0;
                    i < leg.Count; i++)
                    cells.Add(leg[i]);
                network.Stamp(leg, source.Type);
            }
            if (cells.Count > 1)
                network.routes.Add(new MapTransportRoute(source.Name, source.Type, cells));
        }
        return network;
    }

    public bool HasRoad(int x, int y, int width) => Has(roads, x, y, width);
    public bool HasRail(int x, int y, int width) => Has(rails, x, y, width);
    public bool HasFastTransport(int x, int y, int width) =>
        HasRoad(x, y, width) || HasRail(x, y, width);

    // Built corridors are much cheaper than crossing the underlying country,
    // but rough terrain still matters a little. This makes the pathfinder join
    // a nearby road instead of drawing a geometric shortcut over open ground.
    public float MovementCost(MapData map, int x, int y)
    {
        float terrain = TerrainRules.MovementCost(map.GetCell(x, y).Terrain);
        int id = y * map.Width + x;
        if (rails[id]) return 0.22f + terrain * 0.06f;
        if (roads[id]) return 0.30f + terrain * 0.08f;
        return terrain;
    }

    private static bool Has(bool[] mask, int x, int y, int width)
    {
        int id = y * width + x;
        return x >= 0 && y >= 0 && id >= 0 && id < mask.Length && x < width && mask[id];
    }

    private void Stamp(IReadOnlyList<int> cells, InfrastructureRouteType type)
    {
        bool[] mask = type == InfrastructureRouteType.Railway ? rails : roads;
        foreach (int id in cells) if (id >= 0 && id < mask.Length) mask[id] = true;
    }

    private static List<int> ResolveLeg(MapData map, MapTransportNetwork network,
        int fromX, int fromY, int toX, int toY)
    {
        int start = FindWalkable(map, fromX, fromY);
        int goal = FindWalkable(map, toX, toY);
        var empty = new List<int>();
        if (start < 0 || goal < 0) return empty;
        int length = map.Width * map.Height;
        var distance = new float[length];
        var previous = new int[length];
        for (int i = 0; i < length; i++) { distance[i] = float.PositiveInfinity; previous[i] = -1; }
        var heap = new GridHeap();
        distance[start] = 0f;
        heap.Push(start, 0f);
        while (heap.Count > 0)
        {
            int id = heap.Pop(out float cost);
            if (cost > distance[id]) continue;
            if (id == goal) break;
            int x = id % map.Width, y = id / map.Width;
            for (int direction = 0; direction < 4; direction++)
            {
                int nx = x + MapData.NeighborX[direction], ny = y + MapData.NeighborY[direction];
                if (!map.IsWalkable(nx, ny)) continue;
                int next = ny * map.Width + nx;
                // Existing transport is a natural junction. Otherwise prefer
                // easy ground, with a tiny deterministic bias to break large
                // fields of equal-cost stair steps consistently.
                float step = network.HasFastTransport(nx, ny, map.Width)
                    ? 0.20f
                    : TerrainRules.MovementCost(map.GetCell(nx, ny).Terrain);
                step += ((nx * 31 + ny * 17) & 7) * 0.001f;
                float candidate = cost + step;
                if (candidate >= distance[next]) continue;
                distance[next] = candidate;
                previous[next] = id;
                heap.Push(next, candidate);
            }
        }
        if (start != goal && previous[goal] < 0) return empty;
        var route = new List<int>();
        for (int id = goal; id >= 0; id = previous[id])
        {
            route.Add(id);
            if (id == start) break;
        }
        route.Reverse();
        return route;
    }

    private static int FindWalkable(MapData map, int x, int y)
    {
        if (map.IsWalkable(x, y)) return y * map.Width + x;
        for (int radius = 1; radius <= 8; radius++)
        for (int dx = -radius; dx <= radius; dx++)
        for (int dy = -radius; dy <= radius; dy++)
        {
            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) continue;
            int nx = x + dx, ny = y + dy;
            if (map.IsWalkable(nx, ny)) return ny * map.Width + nx;
        }
        return -1;
    }
}

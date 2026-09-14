using System;
using System.Collections.Generic;

// Mutable scenario data used by the map designer. Gameplay receives a copy,
// so painting the preview can never mutate a running MapData instance.
public sealed class ScenarioMap
{
    private readonly TerrainType[,] terrain;
    private readonly int[,] owners;
    private readonly List<ScenarioSettlement> settlements = new List<ScenarioSettlement>();
    private readonly List<ScenarioInfrastructureRoute> infrastructureRoutes =
        new List<ScenarioInfrastructureRoute>();
    private readonly List<ScenarioInfrastructureSite> infrastructureSites =
        new List<ScenarioInfrastructureSite>();

    public string Name { get; }
    public string Period { get; }
    public string Description { get; }
    public int PlayerManpower { get; }
    public int EnemyManpower { get; }
    public double WestLongitude { get; }
    public double EastLongitude { get; }
    public double SouthLatitude { get; }
    public double NorthLatitude { get; }
    public IReadOnlyList<ScenarioSettlement> Settlements => settlements;
    public IReadOnlyList<ScenarioInfrastructureRoute> InfrastructureRoutes => infrastructureRoutes;
    public IReadOnlyList<ScenarioInfrastructureSite> InfrastructureSites => infrastructureSites;
    public int Width => terrain.GetLength(0);
    public int Height => terrain.GetLength(1);

    public ScenarioMap(string name, string period, string description, int width, int height,
        int playerManpower, int enemyManpower)
        : this(name, period, description, width, height, playerManpower, enemyManpower,
            0d, 1d, 0d, 1d)
    {
    }

    public ScenarioMap(string name, string period, string description, int width, int height,
        int playerManpower, int enemyManpower, double westLongitude, double eastLongitude,
        double southLatitude, double northLatitude)
    {
        if (width < 16) throw new ArgumentOutOfRangeException(nameof(width));
        if (height < 16) throw new ArgumentOutOfRangeException(nameof(height));
        if (eastLongitude <= westLongitude) throw new ArgumentOutOfRangeException(nameof(eastLongitude));
        if (northLatitude <= southLatitude) throw new ArgumentOutOfRangeException(nameof(northLatitude));
        Name = name;
        Period = period;
        Description = description;
        PlayerManpower = playerManpower;
        EnemyManpower = enemyManpower;
        WestLongitude = westLongitude;
        EastLongitude = eastLongitude;
        SouthLatitude = southLatitude;
        NorthLatitude = northLatitude;
        terrain = new TerrainType[width, height];
        owners = new int[width, height];
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            owners[x, y] = -1;
    }

    private ScenarioMap(ScenarioMap source)
        : this(source.Name, source.Period, source.Description, source.Width, source.Height,
            source.PlayerManpower, source.EnemyManpower, source.WestLongitude, source.EastLongitude,
            source.SouthLatitude, source.NorthLatitude)
    {
        Array.Copy(source.terrain, terrain, source.terrain.Length);
        Array.Copy(source.owners, owners, source.owners.Length);
        settlements.AddRange(source.settlements);
        infrastructureRoutes.AddRange(source.infrastructureRoutes);
        infrastructureSites.AddRange(source.infrastructureSites);
    }

    public ScenarioMap Copy() => new ScenarioMap(this);

    public TerrainType TerrainAt(int x, int y) => terrain[x, y];

    public int OwnerAt(int x, int y) => owners[x, y];

    public void Paint(int centerX, int centerY, DesignerBrush brush, int radius)
    {
        radius = Math.Max(0, Math.Min(8, radius));
        for (int y = centerY - radius; y <= centerY + radius; y++)
        for (int x = centerX - radius; x <= centerX + radius; x++)
        {
            if (!Inside(x, y)) continue;
            int dx = x - centerX, dy = y - centerY;
            if (dx * dx + dy * dy > radius * radius + radius) continue;
            if (brush == DesignerBrush.RedSide || brush == DesignerBrush.BlueSide || brush == DesignerBrush.Neutral)
            {
                int owner = brush == DesignerBrush.RedSide ? 0 : brush == DesignerBrush.BlueSide ? 1 : -1;
                if (owner < 0 || TerrainRules.IsWalkable(terrain[x, y])) owners[x, y] = owner;
                continue;
            }

            TerrainType next = BrushTerrain(brush);
            terrain[x, y] = next;
            if (!TerrainRules.IsWalkable(next)) owners[x, y] = -1;
        }
    }

    public bool IsPlayable(out string reason)
    {
        int red = CountOwned(0), blue = CountOwned(1);
        if (red == 0 || blue == 0)
        {
            reason = "Paint at least one walkable tile for both the red and blue sides.";
            return false;
        }
        reason = "Ready: " + red.ToString("N0") + " red tiles and " + blue.ToString("N0") + " blue tiles.";
        return true;
    }

    public int CountOwned(int ownerId)
    {
        int count = 0;
        for (int x = 0; x < Width; x++)
        for (int y = 0; y < Height; y++)
            if (owners[x, y] == ownerId) count++;
        return count;
    }

    public MapData CreateMapData()
    {
        var copy = new TerrainType[Width, Height];
        Array.Copy(terrain, copy, terrain.Length);
        var map = new MapData(copy);
        for (int x = 0; x < Width; x++)
        for (int y = 0; y < Height; y++)
            if (owners[x, y] >= 0) map.TryClaimCell(x, y, owners[x, y]);
        map.AttachTransportNetwork(MapTransportNetwork.Build(this, map));
        return map;
    }

    internal void SetTerrain(int x, int y, TerrainType value)
    {
        if (!Inside(x, y)) return;
        terrain[x, y] = value;
        if (!TerrainRules.IsWalkable(value)) owners[x, y] = -1;
    }

    internal void SetOwner(int x, int y, int ownerId)
    {
        if (Inside(x, y) && TerrainRules.IsWalkable(terrain[x, y])) owners[x, y] = ownerId;
    }

    // Settlement markers are stored as WGS84 coordinates and projected into
    // the scenario grid. The marker stays geographically correct even if the
    // terrain underneath is later repainted in the designer.
    internal void AddSettlement(string name, double latitude, double longitude, bool isMajor = false)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A settlement needs a name.", nameof(name));
        if (longitude < WestLongitude || longitude > EastLongitude ||
            latitude < SouthLatitude || latitude > NorthLatitude)
            throw new ArgumentOutOfRangeException(nameof(longitude), name + " is outside the scenario bounds.");

        ScenarioRoutePoint point = Project(latitude, longitude);
        settlements.Add(new ScenarioSettlement(name, latitude, longitude, point.X, point.Y, isMajor));
    }

    // Synthetic maps place features straight onto the grid. The historical
    // templates keep using the projected latitude/longitude overloads below.
    internal void AddGridSettlement(string name, int x, int y, bool isMajor = false)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A settlement needs a name.", nameof(name));
        settlements.Add(new ScenarioSettlement(name, 0d, 0d, x, y, isMajor));
    }

    internal void AddGridInfrastructure(string name, InfrastructureSiteType type, int x, int y,
        UnitSpawnCapability spawnCapability = UnitSpawnCapability.None)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Infrastructure needs a name.", nameof(name));
        if (spawnCapability == UnitSpawnCapability.None) spawnCapability = DefaultSpawnCapability(type);
        infrastructureSites.Add(new ScenarioInfrastructureSite(name, type, 0d, 0d, x, y, spawnCapability));
    }

    // Coordinates arrive as x, y pairs.
    internal void AddGridRoute(string name, InfrastructureRouteType type, params int[] coordinates)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A route needs a name.", nameof(name));
        if (coordinates == null || coordinates.Length < 4 || coordinates.Length % 2 != 0)
            throw new ArgumentException("A route needs at least two x, y pairs.", nameof(coordinates));
        var points = new ScenarioRoutePoint[coordinates.Length / 2];
        for (int i = 0; i < points.Length; i++)
            points[i] = new ScenarioRoutePoint(0d, 0d, coordinates[i * 2], coordinates[i * 2 + 1]);
        infrastructureRoutes.Add(new ScenarioInfrastructureRoute(name, type, points));
    }

    internal void AddInfrastructureRoute(string name, InfrastructureRouteType type,
        params string[] settlementNames)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A route needs a name.", nameof(name));
        if (settlementNames == null || settlementNames.Length < 2)
            throw new ArgumentException("A route needs at least two settlements.", nameof(settlementNames));
        var points = new ScenarioRoutePoint[settlementNames.Length];
        for (int i = 0; i < settlementNames.Length; i++)
        {
            ScenarioSettlement settlement = FindSettlement(settlementNames[i]);
            points[i] = new ScenarioRoutePoint(settlement.Latitude, settlement.Longitude,
                settlement.X, settlement.Y);
        }
        infrastructureRoutes.Add(new ScenarioInfrastructureRoute(name, type, points));
    }

    internal void AddInfrastructureAtSettlement(string name, InfrastructureSiteType type,
        string settlementName, UnitSpawnCapability spawnCapability = UnitSpawnCapability.None)
    {
        ScenarioSettlement settlement = FindSettlement(settlementName);
        AddInfrastructure(name, type, settlement.Latitude, settlement.Longitude, spawnCapability);
    }

    internal void AddInfrastructure(string name, InfrastructureSiteType type,
        double latitude, double longitude, UnitSpawnCapability spawnCapability = UnitSpawnCapability.None)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Infrastructure needs a name.", nameof(name));
        ScenarioRoutePoint point = Project(latitude, longitude);
        if (spawnCapability == UnitSpawnCapability.None)
            spawnCapability = DefaultSpawnCapability(type);
        infrastructureSites.Add(new ScenarioInfrastructureSite(name, type, latitude, longitude,
            point.X, point.Y, spawnCapability));
    }

    // This is the ownership-aware seam for the future deployment system. It
    // deliberately returns data only; unit prefabs and force limits belong to
    // the later spawning layer described in UNIT_SPAWN_PLAN.md.
    public IEnumerable<ScenarioInfrastructureSite> EligibleSpawnSites(int ownerId,
        UnitSpawnCapability capability)
    {
        if (ownerId < 0 || capability == UnitSpawnCapability.None) yield break;
        foreach (ScenarioInfrastructureSite site in infrastructureSites)
            if ((site.SpawnCapability & capability) != 0 && OwnerAt(site.X, site.Y) == ownerId &&
                TerrainRules.IsWalkable(TerrainAt(site.X, site.Y)))
                yield return site;
    }

    private ScenarioSettlement FindSettlement(string settlementName)
    {
        foreach (ScenarioSettlement settlement in settlements)
            if (string.Equals(settlement.Name, settlementName, StringComparison.Ordinal)) return settlement;
        throw new ArgumentException("Unknown settlement: " + settlementName, nameof(settlementName));
    }

    private ScenarioRoutePoint Project(double latitude, double longitude)
    {
        if (longitude < WestLongitude || longitude > EastLongitude ||
            latitude < SouthLatitude || latitude > NorthLatitude)
            throw new ArgumentOutOfRangeException(nameof(longitude), "Infrastructure is outside the scenario bounds.");
        int x = (int)Math.Round((longitude - WestLongitude) / (EastLongitude - WestLongitude) * (Width - 1));
        int y = (int)Math.Round((latitude - SouthLatitude) / (NorthLatitude - SouthLatitude) * (Height - 1));
        return new ScenarioRoutePoint(latitude, longitude, x, y);
    }

    private static UnitSpawnCapability DefaultSpawnCapability(InfrastructureSiteType type) => type switch
    {
        InfrastructureSiteType.Airfield => UnitSpawnCapability.Air,
        InfrastructureSiteType.RailHub => UnitSpawnCapability.Land,
        InfrastructureSiteType.SupplyDepot => UnitSpawnCapability.Land,
        InfrastructureSiteType.Port => UnitSpawnCapability.Land | UnitSpawnCapability.Naval,
        InfrastructureSiteType.IndustrialCenter => UnitSpawnCapability.Land,
        _ => UnitSpawnCapability.None
    };

    private bool Inside(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

    private static TerrainType BrushTerrain(DesignerBrush brush) => brush switch
    {
        DesignerBrush.Forest => TerrainType.Forest,
        DesignerBrush.Hills => TerrainType.Hills,
        DesignerBrush.Mountains => TerrainType.Mountains,
        DesignerBrush.Water => TerrainType.Water,
        DesignerBrush.River => TerrainType.River,
        DesignerBrush.Ford => TerrainType.Ford,
        DesignerBrush.Desert => TerrainType.Desert,
        _ => TerrainType.Land
    };
}

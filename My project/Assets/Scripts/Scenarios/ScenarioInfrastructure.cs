using System;
using System.Collections.Generic;

public enum InfrastructureRouteType
{
    Road,
    Railway
}

public enum InfrastructureSiteType
{
    Airfield,
    RailHub,
    SupplyDepot,
    Bridge,
    Port,
    IndustrialCenter
}

[Flags]
public enum UnitSpawnCapability
{
    None = 0,
    Land = 1,
    Air = 2,
    Naval = 4
}

public sealed class ScenarioRoutePoint
{
    public double Latitude { get; }
    public double Longitude { get; }
    public int X { get; }
    public int Y { get; }

    internal ScenarioRoutePoint(double latitude, double longitude, int x, int y)
    {
        Latitude = latitude;
        Longitude = longitude;
        X = x;
        Y = y;
    }
}

public sealed class ScenarioInfrastructureRoute
{
    private readonly ScenarioRoutePoint[] points;
    public string Name { get; }
    public InfrastructureRouteType Type { get; }
    public IReadOnlyList<ScenarioRoutePoint> Points => points;

    internal ScenarioInfrastructureRoute(string name, InfrastructureRouteType type,
        ScenarioRoutePoint[] routePoints)
    {
        Name = name;
        Type = type;
        points = routePoints;
    }
}

public sealed class ScenarioInfrastructureSite
{
    public string Name { get; }
    public InfrastructureSiteType Type { get; }
    public double Latitude { get; }
    public double Longitude { get; }
    public int X { get; }
    public int Y { get; }
    public UnitSpawnCapability SpawnCapability { get; }

    internal ScenarioInfrastructureSite(string name, InfrastructureSiteType type,
        double latitude, double longitude, int x, int y,
        UnitSpawnCapability spawnCapability)
    {
        Name = name;
        Type = type;
        Latitude = latitude;
        Longitude = longitude;
        X = x;
        Y = y;
        SpawnCapability = spawnCapability;
    }
}

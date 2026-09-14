// Central rules keep map generation, movement, and combat consistent.
public static class TerrainRules
{
    // Prototype supply penalty: an isolated pocket costs half as much to reduce.
    public const float EncirclementCostMultiplier = 0.5f;
    public static bool IsWalkable(TerrainType terrain) =>
        terrain != TerrainType.Water && terrain != TerrainType.River;

    // Route weight remains terrain-sensitive even though neutral occupation no
    // longer consumes manpower. Keeping these concepts separate prevents a
    // zero-cost map from making path selection arbitrary.
    public static int MovementCost(TerrainType terrain) => terrain switch
    {
        TerrainType.Coast => 2,
        TerrainType.Forest => 2,
        TerrainType.Hills => 4,
        TerrainType.Mountains => 6,
        TerrainType.Snow => 5,
        TerrainType.Desert => 3,
        TerrainType.Ford => 3,
        TerrainType.Water => int.MaxValue,
        TerrainType.River => int.MaxValue,
        _ => 1
    };

    public static int ExpansionCost(TerrainType terrain) => IsWalkable(terrain) ? 0 : int.MaxValue;

    // Enemy occupation retains its previous price; free wilderness does not
    // remove the cost of fighting an established garrison.
    public static int AttackCost(TerrainType terrain) => terrain switch
    {
        TerrainType.Coast => 14,
        TerrainType.Forest => 18,
        TerrainType.Hills => 24,
        TerrainType.Mountains => 36,
        TerrainType.Snow => 30,
        TerrainType.Desert => 20,
        TerrainType.Ford => 22,
        TerrainType.Water => int.MaxValue,
        TerrainType.River => int.MaxValue,
        _ => 12
    };

    public static int Defense(TerrainType terrain) => terrain switch
    {
        TerrainType.Forest => 24,
        TerrainType.Hills => 36,
        TerrainType.Mountains => 60,
        TerrainType.Snow => 48,
        TerrainType.Ford => 36,
        _ => 12
    };
}

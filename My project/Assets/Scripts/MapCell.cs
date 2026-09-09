public class MapCell
{
    public TerrainType Terrain { get; }
    // Ownership changes go through MapData so its territory index stays in sync.
    public int OwnerId { get; internal set; }
    public float Elevation { get; }
    public int Defense { get; set; }

    public MapCell(TerrainType terrain, float elevation = 0.5f)
    {
        Terrain = terrain;
        OwnerId = -1;
        Elevation = elevation;
    }
}

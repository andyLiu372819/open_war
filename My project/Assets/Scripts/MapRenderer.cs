using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class MapRenderer : MonoBehaviour
{
    private const int Detail = 4;
    private const int ChunkSize = 128;
    public const float CellsPerUnit = 8f;
    private sealed class Chunk
    {
        public GameObject Object;
        public Texture2D Texture;
        public Sprite Sprite;
        public bool Dirty;
    }
    private Chunk[,] chunks;
    private readonly Color32[] tilePixels = new Color32[Detail * Detail];
    public Bounds WorldBounds { get; private set; }
    public int ChunkCount => chunks == null ? 0 : chunks.Length;
    public int LastUploadedChunkCount { get; private set; }

    public static Color OwnerColor(int ownerId) => ownerId switch
    {
        0 => new Color32(224, 83, 76, 255),
        1 => new Color32(71, 151, 221, 255),
        2 => new Color32(220, 177, 62, 255),
        _ => new Color32(167, 110, 210, 255)
    };

    public void Draw(MapData map)
    {
        Release();
        SpriteRenderer rootRenderer = GetComponent<SpriteRenderer>();
        // The root keeps the map transform for input/HUD; child sprites render
        // independent chunks so a capture doesn't upload the entire world.
        rootRenderer.enabled = false;
        rootRenderer.sprite = null;
        int columns = (map.Width + ChunkSize - 1) / ChunkSize;
        int rows = (map.Height + ChunkSize - 1) / ChunkSize;
        chunks = new Chunk[columns, rows];
        WorldBounds = new Bounds(transform.position, Vector3.zero);
        Bounds bounds = WorldBounds;
        bounds.Encapsulate(transform.TransformPoint(CellToLocal(map, 0, 0)));
        bounds.Encapsulate(transform.TransformPoint(CellToLocal(map, map.Width, 0)));
        bounds.Encapsulate(transform.TransformPoint(CellToLocal(map, 0, map.Height)));
        bounds.Encapsulate(transform.TransformPoint(CellToLocal(map, map.Width, map.Height)));
        WorldBounds = bounds;

        for (int cy = 0; cy < rows; cy++)
        for (int cx = 0; cx < columns; cx++)
        {
            int startX = cx * ChunkSize, startY = cy * ChunkSize;
            int width = Mathf.Min(ChunkSize, map.Width - startX);
            int height = Mathf.Min(ChunkSize, map.Height - startY);
            var texture = new Texture2D(width * Detail, height * Detail, TextureFormat.RGBA32, false);
            texture.name = "Terrain " + cx + ", " + cy;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[texture.width * texture.height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                PaintTile(map, startX + x, startY + y);
                for (int py = 0; py < Detail; py++)
                for (int px = 0; px < Detail; px++)
                    pixels[(y * Detail + py) * texture.width + x * Detail + px] = tilePixels[py * Detail + px];
            }
            texture.SetPixels32(pixels);
            texture.Apply(false);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), CellsPerUnit * Detail, 0, SpriteMeshType.FullRect);
            var child = new GameObject(texture.name);
            child.layer = gameObject.layer;
            child.transform.SetParent(transform, false);
            child.transform.localPosition = CellToLocal(map, startX + width * 0.5f, startY + height * 0.5f);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = rootRenderer.sharedMaterial;
            renderer.color = rootRenderer.color;
            renderer.sortingLayerID = rootRenderer.sortingLayerID;
            renderer.sortingOrder = rootRenderer.sortingOrder;
            chunks[cx, cy] = new Chunk { Object = child, Texture = texture, Sprite = sprite };
        }
    }

    public Vector3 CellToLocal(MapData map, float x, float y) =>
        new Vector3((x - map.Width * 0.5f) / CellsPerUnit,
            (y - map.Height * 0.5f) / CellsPerUnit, 0f);

    public bool TryScreenToCell(MapData map, Camera camera, Vector2 screen, out int x, out int y)
    {
        x = y = -1;
        Ray ray = camera.ScreenPointToRay(screen);
        Plane plane = new Plane(transform.forward, transform.position);
        if (!plane.Raycast(ray, out float distance)) return false;
        Vector3 local = transform.InverseTransformPoint(ray.GetPoint(distance));
        x = Mathf.FloorToInt(local.x * CellsPerUnit + map.Width * 0.5f);
        y = Mathf.FloorToInt(local.y * CellsPerUnit + map.Height * 0.5f);
        return map.InsideBorder(x, y);
    }

    public void RefreshCell(MapData map, int x, int y)
    {
        UpdateTile(map, x, y);
        // This also updates borders across chunk seams.
        for (int i = 0; i < 4; i++) UpdateTile(map, x + MapData.NeighborX[i], y + MapData.NeighborY[i]);
    }

    private void UpdateTile(MapData map, int x, int y)
    {
        if (!map.InsideBorder(x, y) || chunks == null) return;
        Chunk chunk = chunks[x / ChunkSize, y / ChunkSize];
        PaintTile(map, x, y);
        chunk.Texture.SetPixels32((x % ChunkSize) * Detail, (y % ChunkSize) * Detail,
            Detail, Detail, tilePixels);
        chunk.Dirty = true;
    }

    private void LateUpdate()
    {
        LastUploadedChunkCount = 0;
        if (chunks == null) return;
        foreach (Chunk chunk in chunks)
        {
            if (!chunk.Dirty) continue;
            chunk.Texture.Apply(false);
            chunk.Dirty = false;
            LastUploadedChunkCount++;
        }
    }

    private void PaintTile(MapData map, int x, int y)
    {
        MapCell cell = map.GetCell(x, y);
        Color color = TerrainColor(cell);
        float west = map.GetCell(Mathf.Max(0, x - 1), y).Elevation;
        float north = map.GetCell(x, Mathf.Min(map.Height - 1, y + 1)).Elevation;
        float relief = Mathf.Clamp(1f + (west - cell.Elevation + north - cell.Elevation) * 5f, 0.65f, 1.25f);
        float variation = ((x * 17 + y * 31) % 11 - 5) * 0.006f;
        color *= relief + variation;
        Color faction = cell.OwnerId >= 0 ? OwnerColor(cell.OwnerId) : color;
        if (cell.OwnerId >= 0) color = Color.Lerp(color, faction, 0.55f);
        for (int py = 0; py < Detail; py++)
        for (int px = 0; px < Detail; px++)
        {
            Color pixel = color;
            if (cell.Terrain == TerrainType.Forest && ((px == 1 && py >= 1) || (px == 2 && py == 2)))
                pixel *= 0.73f;
            if ((cell.Terrain == TerrainType.Mountains || cell.Terrain == TerrainType.Snow) && py >= px)
                pixel *= 0.76f;
            if (cell.Terrain == TerrainType.Mountains && px == 1 && py == 3)
                pixel = Color.Lerp(pixel, Color.white, 0.45f);
            if (cell.Terrain == TerrainType.Ford && py % 2 == 0)
                pixel = Color.Lerp(pixel, new Color(0.65f, 0.64f, 0.49f), 0.8f);
            if (cell.OwnerId >= 0 &&
                ((px == 0 && !map.IsOwnedBy(x - 1, y, cell.OwnerId)) ||
                 (px == Detail - 1 && !map.IsOwnedBy(x + 1, y, cell.OwnerId)) ||
                 (py == 0 && !map.IsOwnedBy(x, y - 1, cell.OwnerId)) ||
                 (py == Detail - 1 && !map.IsOwnedBy(x, y + 1, cell.OwnerId))))
                pixel = Color.Lerp(faction, Color.white, 0.24f);
            pixel.a = 1f;
            tilePixels[py * Detail + px] = pixel;
        }
    }

    private static Color TerrainColor(MapCell cell) => cell.Terrain switch
    {
        TerrainType.Water => Color.Lerp(new Color32(21, 48, 66, 255), new Color32(51, 110, 128, 255), cell.Elevation / 0.35f),
        TerrainType.Coast => new Color32(182, 179, 130, 255),
        TerrainType.Forest => new Color32(66, 103, 70, 255),
        TerrainType.Hills => new Color32(132, 137, 96, 255),
        TerrainType.Mountains => new Color32(145, 142, 132, 255),
        TerrainType.Desert => new Color32(195, 172, 116, 255),
        TerrainType.Snow => new Color32(218, 226, 218, 255),
        TerrainType.River => new Color32(66, 137, 160, 255),
        TerrainType.Ford => new Color32(101, 157, 162, 255),
        _ => new Color32(122, 153, 93, 255)
    };

    private void Release()
    {
        if (chunks == null) return;
        foreach (Chunk chunk in chunks)
        {
            if (chunk == null) continue;
            if (chunk.Object != null) Destroy(chunk.Object);
            if (chunk.Sprite != null) Destroy(chunk.Sprite);
            if (chunk.Texture != null) Destroy(chunk.Texture);
        }
        chunks = null;
    }

    private void OnDestroy() => Release();
}

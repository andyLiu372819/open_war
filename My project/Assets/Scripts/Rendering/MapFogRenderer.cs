using UnityEngine;

// Paints unobserved ground dark. One texture at cell resolution is cheaper than
// re-tinting the terrain chunks, and it only needs rebuilding when the picture
// changes, which in a WeGo game is once a turn.
[RequireComponent(typeof(SpriteRenderer))]
public sealed class MapFogRenderer : MonoBehaviour
{
    private static readonly Color32 Hidden = new Color32(6, 11, 16, 208);
    private static readonly Color32 Seen = new Color32(0, 0, 0, 0);

    private Texture2D texture;
    private Sprite sprite;
    private Color32[] pixels;
    private GameObject overlay;
    private SpriteRenderer overlayRenderer;

    public bool Enabled { get; private set; }

    public void Build(MapData map, MapRenderer mapRenderer)
    {
        Release();
        if (map == null || mapRenderer == null) return;
        texture = new Texture2D(map.Width, map.Height, TextureFormat.RGBA32, false)
        {
            name = "Fog of war",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        pixels = new Color32[map.Width * map.Height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Hidden;
        texture.SetPixels32(pixels);
        texture.Apply(false);
        sprite = Sprite.Create(texture, new Rect(0f, 0f, map.Width, map.Height),
            new Vector2(0.5f, 0.5f), MapRenderer.CellsPerUnit);
        sprite.name = "Fog of war";
        overlay = new GameObject("Fog overlay");
        overlay.layer = gameObject.layer;
        overlay.transform.SetParent(transform, false);
        overlay.transform.localPosition = mapRenderer.CellToLocal(map, map.Width * 0.5f, map.Height * 0.5f);
        overlayRenderer = overlay.AddComponent<SpriteRenderer>();
        overlayRenderer.sprite = sprite;
        // Above the terrain and its routes, below nothing else that matters.
        overlayRenderer.sortingOrder = 6;
        Enabled = true;
    }

    // Repaints the whole mask. At 320 x 320 this is a hundred thousand bytes
    // once a turn, which is far cheaper than touching the terrain chunks.
    public void Refresh(FogOfWar fog, MapData map)
    {
        if (texture == null || fog == null || map == null) return;
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
            pixels[y * map.Width + x] = fog.IsVisible(x, y) ? Seen : Hidden;
        texture.SetPixels32(pixels);
        texture.Apply(false);
        if (overlayRenderer != null) overlayRenderer.enabled = !fog.Revealed;
    }

    private void Release()
    {
        if (overlay != null) Destroy(overlay);
        if (sprite != null) Destroy(sprite);
        if (texture != null) Destroy(texture);
        overlay = null;
        overlayRenderer = null;
        sprite = null;
        texture = null;
        Enabled = false;
    }

    private void OnDestroy() => Release();
}

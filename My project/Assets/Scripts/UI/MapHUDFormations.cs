using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Formation counters and drag-selection presentation. Resource, turn, and
// scenario-label UI remain in MapHUD's composition file.
public partial class MapHUD
{
    // Rebuilds the counter list only when the roster actually changes; the
    // positions themselves follow the camera every frame in LateUpdate.
    public void SyncDivisions(IReadOnlyList<Division> divisions, ICollection<Division> selected,
        FogOfWar fog = null)
    {
        if (divisionLayer == null || mapSprite == null) return;
        // Only formations the player can actually see get a counter.
        visibleDivisions.Clear();
        foreach (Division division in divisions)
            if (fog == null || fog.CanSee(division)) visibleDivisions.Add(division);
        bool matches = visibleDivisions.Count == divisionCounters.Count;
        for (int i = 0; matches && i < visibleDivisions.Count; i++)
            if (divisionCounters[i].Division != visibleDivisions[i]) matches = false;
        if (!matches)
        {
            foreach (DivisionCounter counter in divisionCounters) Destroy(counter.Root);
            divisionCounters.Clear();
            foreach (Division division in visibleDivisions) divisionCounters.Add(CreateCounter(division));
        }
        foreach (DivisionCounter counter in divisionCounters)
        {
            Division division = counter.Division;
            counter.LocalPosition = new Vector3(
                (division.X + 0.5f - map.Width * 0.5f) / MapRenderer.CellsPerUnit,
                (division.Y + 0.5f - map.Height * 0.5f) / MapRenderer.CellsPerUnit, 0f);
            bool chosen = selected != null && selected.Contains(division);
            counter.Rim.color = chosen
                ? new Color32(255, 236, 150, 255)
                : new Color32(10, 20, 26, 235);
            counter.Number.text = division.Number.ToString();
        }
    }

    private DivisionCounter CreateCounter(Division division)
    {
        var root = new GameObject("Division counter " + division.OwnerId + "-" + division.Number,
            typeof(RectTransform));
        root.transform.SetParent(divisionLayer, false);
        var rootRect = (RectTransform)root.transform;
        rootRect.sizeDelta = new Vector2(46f, 46f);

        Image rim = CreateCounterImage(rootRect, "Counter rim", discSprite, 46f, 46f);
        Image disc = CreateCounterImage(rootRect, "Counter disc", discSprite, 38f, 38f);
        disc.color = MapRenderer.OwnerColor(division.OwnerId);
        // The NATO infantry symbol: a box with crossed diagonals.
        Image symbol = CreateCounterImage(rootRect, "Counter infantry symbol", infantrySprite, 26f, 15f);
        symbol.color = Color.white;
        symbol.rectTransform.anchoredPosition = new Vector2(0f, 5f);

        TMP_Text number = Instantiate(manpowerText, rootRect);
        number.name = "Counter number";
        number.raycastTarget = false;
        number.alignment = TextAlignmentOptions.Center;
        number.fontSize = 13;
        number.fontStyle = FontStyles.Bold;
        number.color = Color.white;
        number.outlineWidth = 0.2f;
        number.outlineColor = new Color32(8, 16, 20, 255);
        number.enabled = true;
        RectTransform numberRect = number.rectTransform;
        numberRect.anchorMin = numberRect.anchorMax = numberRect.pivot = new Vector2(0.5f, 0.5f);
        numberRect.anchoredPosition = new Vector2(0f, -10f);
        numberRect.sizeDelta = new Vector2(34f, 14f);

        return new DivisionCounter
        {
            Division = division, Root = root, Rim = rim, Disc = disc,
            Symbol = symbol, Number = number
        };
    }

    private Image CreateCounterImage(RectTransform parent, string name, Sprite sprite, float width, float height)
    {
        var child = new GameObject(name, typeof(RectTransform), typeof(Image));
        child.transform.SetParent(parent, false);
        var image = child.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(width, height);
        return image;
    }

    // Two small procedural sprites, matching how the resource icons are made:
    // a soft-edged disc, and the infantry box with its diagonals.
    private Sprite CreateDiscSprite()
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Division disc";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = (x + 0.5f) / size * 2f - 1f;
            float v = (y + 0.5f) / size * 2f - 1f;
            float radius = Mathf.Sqrt(u * u + v * v);
            float alpha = Mathf.Clamp01((1f - radius) / (2f / size * 2f));
            pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
        }
        texture.SetPixels(pixels);
        texture.Apply(false, false);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        sprite.name = texture.name;
        generatedIconTextures.Add(texture);
        generatedIconSprites.Add(sprite);
        return sprite;
    }

    private Sprite CreateInfantrySprite()
    {
        const int width = 96, height = 56;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "Infantry symbol";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[width * height];
        const float stroke = 4.5f;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float px = x + 0.5f, py = y + 0.5f;
            // The rectangle's own outline.
            bool border = px <= stroke || py <= stroke ||
                px >= width - stroke || py >= height - stroke;
            // Both diagonals of the box, which is what marks infantry.
            float u = px / width, v = py / height;
            float rising = Mathf.Abs(v - u);
            float falling = Mathf.Abs(v - (1f - u));
            float diagonal = Mathf.Min(rising, falling) * height;
            bool cross = diagonal <= stroke * 0.85f;
            pixels[y * width + x] = new Color(1f, 1f, 1f, border || cross ? 1f : 0f);
        }
        texture.SetPixels(pixels);
        texture.Apply(false, false);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), width);
        sprite.name = texture.name;
        generatedIconTextures.Add(texture);
        generatedIconSprites.Add(sprite);
        return sprite;
    }

    // The drag rectangle: a translucent fill with four crisp edges, drawn in
    // canvas space from the two screen points the drag spans.
    public void ShowSelectionBox(Vector2 screenStart, Vector2 screenEnd)
    {
        if (divisionLayer == null) return;
        if (selectionBox == null) CreateSelectionBox();
        Canvas canvas = manpowerText.canvas;
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var parent = (RectTransform)selectionBox.parent;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenStart, uiCamera, out Vector2 a) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenEnd, uiCamera, out Vector2 b))
            return;
        var min = new Vector2(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y));
        var size = new Vector2(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
        selectionBox.gameObject.SetActive(true);
        selectionBox.localPosition = new Vector3(min.x, min.y, 0f);
        selectionBox.sizeDelta = size;
        const float edge = 2f;
        // bottom, top, left, right
        selectionEdges[0].sizeDelta = new Vector2(size.x, edge);
        selectionEdges[0].anchoredPosition = Vector2.zero;
        selectionEdges[1].sizeDelta = new Vector2(size.x, edge);
        selectionEdges[1].anchoredPosition = new Vector2(0f, size.y - edge);
        selectionEdges[2].sizeDelta = new Vector2(edge, size.y);
        selectionEdges[2].anchoredPosition = Vector2.zero;
        selectionEdges[3].sizeDelta = new Vector2(edge, size.y);
        selectionEdges[3].anchoredPosition = new Vector2(size.x - edge, 0f);
    }

    public void HideSelectionBox()
    {
        if (selectionBox != null) selectionBox.gameObject.SetActive(false);
    }

    private void CreateSelectionBox()
    {
        var boxObject = new GameObject("Selection box", typeof(RectTransform), typeof(Image));
        boxObject.transform.SetParent(divisionLayer.parent, false);
        selectionBox = (RectTransform)boxObject.transform;
        selectionBox.anchorMin = selectionBox.anchorMax = selectionBox.pivot = Vector2.zero;
        Image fill = boxObject.GetComponent<Image>();
        fill.color = new Color(1f, 0.88f, 0.42f, 0.14f);
        fill.raycastTarget = false;
        for (int i = 0; i < selectionEdges.Length; i++)
        {
            var edgeObject = new GameObject("Selection edge " + i, typeof(RectTransform), typeof(Image));
            edgeObject.transform.SetParent(selectionBox, false);
            var edgeRect = (RectTransform)edgeObject.transform;
            edgeRect.anchorMin = edgeRect.anchorMax = edgeRect.pivot = Vector2.zero;
            Image edgeImage = edgeObject.GetComponent<Image>();
            edgeImage.color = new Color(1f, 0.9f, 0.5f, 0.9f);
            edgeImage.raycastTarget = false;
            selectionEdges[i] = edgeRect;
        }
        boxObject.SetActive(false);
    }
}

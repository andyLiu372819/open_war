using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Code-built front end keeps the existing scene small while still providing a
// complete title screen and a playable, mouse-driven scenario editor.
public sealed partial class GameSetupUI : MonoBehaviour
{
    private static readonly Color Background = new Color32(12, 25, 34, 255);
    private static readonly Color Panel = new Color32(20, 39, 50, 246);
    private static readonly Color ButtonNormal = new Color32(40, 61, 72, 255);
    private static readonly Color ButtonSelected = new Color32(178, 69, 61, 255);
    private static readonly Color Accent = new Color32(235, 91, 79, 255);
    private static readonly Color Muted = new Color32(157, 181, 190, 255);

    private readonly Dictionary<EasternFrontTemplate, Image> templateButtons =
        new Dictionary<EasternFrontTemplate, Image>();
    private readonly Dictionary<DesignerBrush, Image> brushButtons =
        new Dictionary<DesignerBrush, Image>();
    private MapController controller;
    private TMP_Text textTemplate;
    private GameObject startPage;
    private GameObject designerPage;
    private TMP_Text scenarioTitle;
    private TMP_Text scenarioDescription;
    private TMP_Text designerStatus;
    private TMP_Text brushSizeText;
    private RawImage preview;
    private RectTransform infrastructureOverlay;
    private RectTransform settlementOverlay;
    private Texture2D previewTexture;
    private readonly List<GameObject> infrastructureObjects = new List<GameObject>();
    private readonly List<GameObject> settlementObjects = new List<GameObject>();
    private ScenarioMap design;
    private EasternFrontTemplate selectedTemplate = EasternFrontTemplate.OperationTyphoon1941;
    private DesignerBrush selectedBrush = DesignerBrush.Plains;
    private int brushRadius = 2;

    public static GameSetupUI Create(MapController mapController, MapHUD hud)
    {
        var root = new GameObject("Start screen", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(hud.OverlayRoot, false);
        var rect = (RectTransform)root.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Image background = root.GetComponent<Image>();
        background.color = Background;
        background.raycastTarget = true;
        var setup = root.AddComponent<GameSetupUI>();
        setup.controller = mapController;
        setup.textTemplate = hud.TextTemplate;
        setup.BuildStartPage();
        root.transform.SetAsLastSibling();
        return setup;
    }

    public void Close()
    {
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void BuildStartPage()
    {
        startPage = CreateContainer(transform, "Main menu");
        CreatePanel(startPage.transform, "Red flank", 0f, 0f, 18f, 1000f, Accent);
        CreatePanel(startPage.transform, "Blue flank", 1582f, 0f, 18f, 1000f,
            new Color32(74, 153, 219, 255));
        CreateText(startPage.transform, "Game title", "OPEN WAR", 410f, 725f, 780f, 110f,
            78, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        CreateText(startPage.transform, "Game subtitle", "COMMAND THE FRONT", 500f, 685f, 600f, 42f,
            24, Accent, TextAlignmentOptions.Center, FontStyles.Bold);
        CreateText(startPage.transform, "Game description",
            "Shape the battlefield, commit your reserves, and push through a living territorial front.",
            440f, 595f, 720f, 65f, 23, Muted, TextAlignmentOptions.Center);

        CreateButton(startPage.transform, "Quick play", "QUICK PLAY", 555f, 455f, 490f, 82f,
            ButtonSelected, () => controller.StartQuickGame(), 29);
        CreateButton(startPage.transform, "Open map designer", "MAP DESIGNER", 555f, 350f, 490f, 82f,
            ButtonNormal, ShowDesigner, 29);
        CreateText(startPage.transform, "Quick play note",
            "Quick Play uses the original 1,024 × 512 procedural world.",
            510f, 285f, 580f, 36f, 18, Muted, TextAlignmentOptions.Center);
        CreateText(startPage.transform, "Version label", "EASTERN FRONT SCENARIO TOOLS",
            30f, 28f, 520f, 32f, 16, new Color32(102, 132, 144, 255), TextAlignmentOptions.Left);
    }

    private void ShowDesigner()
    {
        if (designerPage == null) BuildDesignerPage();
        startPage.SetActive(false);
        designerPage.SetActive(true);
        LoadTemplate(selectedTemplate);
    }

    private void ShowStartPage()
    {
        designerPage.SetActive(false);
        startPage.SetActive(true);
    }

    private void BuildDesignerPage()
    {
        designerPage = CreateContainer(transform, "Map designer");
        designerPage.SetActive(false);
        CreateText(designerPage.transform, "Designer title", "MAP DESIGNER", 28f, 928f, 500f, 48f,
            34, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
        CreateText(designerPage.transform, "Designer subtitle",
            "Choose a template, paint terrain or control, then launch the edited battlefield.",
            420f, 935f, 940f, 34f, 19, Muted, TextAlignmentOptions.Left);
        CreateButton(designerPage.transform, "Back to title", "BACK", 1435f, 925f, 135f, 48f,
            ButtonNormal, ShowStartPage, 20);

        GameObject left = CreatePanel(designerPage.transform, "Designer controls", 24f, 80f, 370f, 820f, Panel);
        CreateText(left.transform, "Template heading", "SCENARIO TEMPLATE", 20f, 764f, 330f, 32f,
            18, Accent, TextAlignmentOptions.Left, FontStyles.Bold);
        float templateY = 688f;
        foreach (EasternFrontTemplate template in ScenarioTemplates.All)
        {
            EasternFrontTemplate captured = template;
            Button button = CreateButton(left.transform, "Template " + template,
                ScenarioTemplates.ShortName(template), 20f, templateY, 330f, 50f,
                ButtonNormal, () => LoadTemplate(captured), 15);
            templateButtons[template] = button.GetComponent<Image>();
            templateY -= 56f;
        }

        scenarioTitle = CreateText(left.transform, "Scenario title", "", 20f, 420f, 330f, 35f,
            23, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
        scenarioDescription = CreateText(left.transform, "Scenario description", "", 20f, 318f, 330f, 97f,
            17, Muted, TextAlignmentOptions.TopLeft);
        CreateText(left.transform, "Terrain heading", "TERRAIN BRUSH", 20f, 280f, 330f, 28f,
            17, Accent, TextAlignmentOptions.Left, FontStyles.Bold);

        DesignerBrush[] terrainBrushes =
        {
            DesignerBrush.Plains, DesignerBrush.Forest, DesignerBrush.Hills, DesignerBrush.Mountains,
            DesignerBrush.Water, DesignerBrush.River, DesignerBrush.Ford, DesignerBrush.Desert
        };
        for (int i = 0; i < terrainBrushes.Length; i++)
        {
            DesignerBrush brush = terrainBrushes[i];
            int column = i % 4, row = i / 4;
            DesignerBrush captured = brush;
            Button button = CreateButton(left.transform, "Brush " + brush, BrushLabel(brush),
                20f + column * 82f, 228f - row * 48f, 76f, 40f,
                ButtonNormal, () => SelectBrush(captured), 12);
            brushButtons[brush] = button.GetComponent<Image>();
        }

        CreateText(left.transform, "Control heading", "CONTROL BRUSH", 20f, 132f, 330f, 28f,
            17, Accent, TextAlignmentOptions.Left, FontStyles.Bold);
        DesignerBrush[] ownerBrushes = { DesignerBrush.RedSide, DesignerBrush.BlueSide, DesignerBrush.Neutral };
        Color[] ownerColors =
        {
            new Color32(139, 57, 54, 255), new Color32(48, 102, 145, 255), new Color32(65, 75, 80, 255)
        };
        for (int i = 0; i < ownerBrushes.Length; i++)
        {
            DesignerBrush brush = ownerBrushes[i];
            DesignerBrush captured = brush;
            Button button = CreateButton(left.transform, "Brush " + brush, BrushLabel(brush),
                20f + i * 110f, 75f, 102f, 42f, ownerColors[i], () => SelectBrush(captured), 14);
            brushButtons[brush] = button.GetComponent<Image>();
        }

        GameObject previewPanel = CreatePanel(designerPage.transform, "Map preview frame",
            420f, 228f, 1150f, 690f, new Color32(6, 15, 21, 255));
        var previewObject = new GameObject("Editable map preview", typeof(RectTransform), typeof(RawImage),
            typeof(RectMask2D), typeof(MapDesignerCanvas));
        previewObject.transform.SetParent(previewPanel.transform, false);
        RectTransform previewRect = (RectTransform)previewObject.transform;
        previewRect.anchorMin = Vector2.zero;
        previewRect.anchorMax = Vector2.one;
        previewRect.offsetMin = new Vector2(8f, 8f);
        previewRect.offsetMax = new Vector2(-8f, -8f);
        preview = previewObject.GetComponent<RawImage>();
        preview.color = Color.white;
        preview.raycastTarget = true;
        previewObject.GetComponent<MapDesignerCanvas>().Initialize(this);
        var infrastructureObject = new GameObject("Infrastructure overlay", typeof(RectTransform));
        infrastructureObject.transform.SetParent(previewObject.transform, false);
        infrastructureOverlay = (RectTransform)infrastructureObject.transform;
        infrastructureOverlay.anchorMin = Vector2.zero;
        infrastructureOverlay.anchorMax = Vector2.one;
        infrastructureOverlay.offsetMin = infrastructureOverlay.offsetMax = Vector2.zero;
        var settlementObject = new GameObject("Settlement overlay", typeof(RectTransform));
        settlementObject.transform.SetParent(previewObject.transform, false);
        settlementOverlay = (RectTransform)settlementObject.transform;
        settlementOverlay.anchorMin = Vector2.zero;
        settlementOverlay.anchorMax = Vector2.one;
        settlementOverlay.offsetMin = settlementOverlay.offsetMax = Vector2.zero;

        GameObject footer = CreatePanel(designerPage.transform, "Designer footer", 420f, 28f, 1150f, 175f, Panel);
        designerStatus = CreateText(footer.transform, "Designer status", "", 22f, 105f, 750f, 46f,
            18, Muted, TextAlignmentOptions.Left);
        CreateText(footer.transform, "Brush size heading", "BRUSH SIZE", 22f, 55f, 130f, 34f,
            16, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
        CreateButton(footer.transform, "Smaller brush", "−", 155f, 48f, 48f, 42f,
            ButtonNormal, () => ChangeBrushSize(-1), 25);
        brushSizeText = CreateText(footer.transform, "Brush size", "", 208f, 50f, 62f, 40f,
            20, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        CreateButton(footer.transform, "Larger brush", "+", 275f, 48f, 48f, 42f,
            ButtonNormal, () => ChangeBrushSize(1), 25);
        CreateButton(footer.transform, "Reset template", "RESET TEMPLATE", 690f, 48f, 200f, 72f,
            ButtonNormal, () => LoadTemplate(selectedTemplate), 18);
        CreateButton(footer.transform, "Play designed map", "PLAY MAP", 910f, 42f, 215f, 84f,
            ButtonSelected, PlayDesignedMap, 25);
        CreateText(footer.transform, "Designer disclaimer",
            "Settlements and infrastructure use projected geography; terrain and fronts are gameplay-scaled.",
            22f, 14f, 650f, 28f, 14, new Color32(105, 133, 144, 255), TextAlignmentOptions.Left);
    }

    private void LoadTemplate(EasternFrontTemplate template)
    {
        selectedTemplate = template;
        design = ScenarioTemplates.Create(template);
        if (previewTexture == null || previewTexture.width != design.Width || previewTexture.height != design.Height)
        {
            if (previewTexture != null) Destroy(previewTexture);
            previewTexture = new Texture2D(design.Width, design.Height, TextureFormat.RGBA32, false);
            previewTexture.name = "Map designer preview";
            previewTexture.filterMode = FilterMode.Point;
            previewTexture.wrapMode = TextureWrapMode.Clamp;
            preview.texture = previewTexture;
        }
        RefreshInfrastructureOverlay();
        RefreshSettlementLabels();
        RefreshDesigner();
    }

    private void SelectBrush(DesignerBrush brush)
    {
        selectedBrush = brush;
        RefreshHighlights();
    }

    private void ChangeBrushSize(int delta)
    {
        brushRadius = Mathf.Clamp(brushRadius + delta, 0, 8);
        brushSizeText.text = (brushRadius * 2 + 1) + " × " + (brushRadius * 2 + 1);
    }

    internal void PaintAtNormalized(float u, float v)
    {
        if (design == null) return;
        int x = Mathf.Clamp(Mathf.FloorToInt(u * design.Width), 0, design.Width - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(v * design.Height), 0, design.Height - 1);
        design.Paint(x, y, selectedBrush, brushRadius);
        RefreshDesigner();
    }

    private void PlayDesignedMap()
    {
        if (!design.IsPlayable(out string reason))
        {
            designerStatus.text = reason;
            designerStatus.color = Accent;
            return;
        }
        if (!controller.StartDesignedGame(design.Copy()))
        {
            designerStatus.text = "The designed map could not be started.";
            designerStatus.color = Accent;
        }
    }

    private void RefreshDesigner()
    {
        scenarioTitle.text = design.Name + "  ·  " + design.Period;
        scenarioDescription.text = design.Description;
        design.IsPlayable(out string status);
        int roads = 0, rails = 0, spawnSites = 0;
        foreach (ScenarioInfrastructureRoute route in design.InfrastructureRoutes)
            if (route.Type == InfrastructureRouteType.Road) roads++; else rails++;
        foreach (ScenarioInfrastructureSite site in design.InfrastructureSites)
            if (site.SpawnCapability != UnitSpawnCapability.None) spawnSites++;
        designerStatus.text = status + "\nINFRA  " + roads + " roads  ·  " + rails + " rails  ·  " +
            design.InfrastructureSites.Count + " sites  ·  " + spawnSites + " future spawn sites";
        designerStatus.color = Muted;
        brushSizeText.text = (brushRadius * 2 + 1) + " × " + (brushRadius * 2 + 1);
        RefreshPreview();
        RefreshHighlights();
    }

    private void RefreshHighlights()
    {
        foreach (KeyValuePair<EasternFrontTemplate, Image> pair in templateButtons)
            pair.Value.color = pair.Key == selectedTemplate ? ButtonSelected : ButtonNormal;
        foreach (KeyValuePair<DesignerBrush, Image> pair in brushButtons)
        {
            if (pair.Key == selectedBrush) pair.Value.color = ButtonSelected;
            else if (pair.Key == DesignerBrush.RedSide) pair.Value.color = new Color32(139, 57, 54, 255);
            else if (pair.Key == DesignerBrush.BlueSide) pair.Value.color = new Color32(48, 102, 145, 255);
            else if (pair.Key == DesignerBrush.Neutral) pair.Value.color = new Color32(65, 75, 80, 255);
            else pair.Value.color = ButtonNormal;
        }
    }

    private void RefreshSettlementLabels()
    {
        foreach (GameObject oldObject in settlementObjects)
        {
            if (oldObject == null) continue;
            oldObject.SetActive(false);
            Destroy(oldObject);
        }
        settlementObjects.Clear();
        if (design == null || settlementOverlay == null) return;

        // The preview has a fixed 1,134 x 674 reference-space interior. Marker
        // centers use the exact projected grid position; only text is nudged.
        const float previewWidth = 1134f, previewHeight = 674f;
        var occupied = new List<Rect>();
        foreach (ScenarioSettlement settlement in design.Settlements)
        {
            float u = settlement.X / (float)(design.Width - 1);
            float v = settlement.Y / (float)(design.Height - 1);
            float markerX = u * previewWidth;
            float markerY = v * previewHeight;
            float markerSize = settlement.IsMajor ? 9f : 6f;

            var markerObject = new GameObject("Settlement Marker " + settlement.Name,
                typeof(RectTransform), typeof(Image));
            markerObject.transform.SetParent(settlementOverlay, false);
            var markerRect = (RectTransform)markerObject.transform;
            Place(markerRect, markerX - markerSize * 0.5f, markerY - markerSize * 0.5f,
                markerSize, markerSize);
            markerRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image marker = markerObject.GetComponent<Image>();
            marker.raycastTarget = false;
            marker.color = settlement.IsMajor
                ? new Color32(255, 241, 188, 255)
                : new Color32(224, 215, 181, 255);
            settlementObjects.Add(markerObject);

            float labelHeight = settlement.IsMajor ? 24f : 20f;
            float labelWidth = Mathf.Clamp(settlement.Name.Length * (settlement.IsMajor ? 10f : 7.4f) + 16f,
                70f, 175f);
            Rect[] candidates =
            {
                new Rect(markerX + 8f, markerY + 2f, labelWidth, labelHeight),
                new Rect(markerX + 8f, markerY - labelHeight - 2f, labelWidth, labelHeight),
                new Rect(markerX - labelWidth - 8f, markerY + 2f, labelWidth, labelHeight),
                new Rect(markerX - labelWidth - 8f, markerY - labelHeight - 2f, labelWidth, labelHeight),
                new Rect(markerX - labelWidth * 0.5f, markerY + 8f, labelWidth, labelHeight),
                new Rect(markerX - labelWidth * 0.5f, markerY - labelHeight - 8f, labelWidth, labelHeight)
            };
            Rect best = candidates[0];
            float bestScore = float.PositiveInfinity;
            foreach (Rect raw in candidates)
            {
                Rect candidate = new Rect(
                    Mathf.Clamp(raw.x, 3f, previewWidth - raw.width - 3f),
                    Mathf.Clamp(raw.y, 3f, previewHeight - raw.height - 3f), raw.width, raw.height);
                float score = Mathf.Abs(candidate.x - raw.x) + Mathf.Abs(candidate.y - raw.y);
                foreach (Rect used in occupied)
                {
                    float overlapWidth = Mathf.Max(0f, Mathf.Min(candidate.xMax, used.xMax) - Mathf.Max(candidate.xMin, used.xMin));
                    float overlapHeight = Mathf.Max(0f, Mathf.Min(candidate.yMax, used.yMax) - Mathf.Max(candidate.yMin, used.yMin));
                    score += overlapWidth * overlapHeight * 5f;
                }
                if (score >= bestScore) continue;
                best = candidate;
                bestScore = score;
            }

            TMP_Text label = Instantiate(textTemplate, settlementOverlay);
            label.gameObject.SetActive(true);
            label.name = "Settlement Label " + settlement.Name;
            label.text = settlement.Name;
            label.raycastTarget = false;
            label.fontSize = settlement.IsMajor ? 15 : 12;
            label.fontStyle = settlement.IsMajor ? FontStyles.Bold : FontStyles.Normal;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.color = settlement.IsMajor ? Color.white : new Color32(231, 224, 199, 255);
            label.outlineWidth = 0.18f;
            label.outlineColor = new Color32(5, 12, 16, 255);
            label.enableAutoSizing = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            Place(label.rectTransform, best.x, best.y, best.width, best.height);
            settlementObjects.Add(label.gameObject);
            occupied.Add(new Rect(best.x - 2f, best.y - 1f, best.width + 4f, best.height + 2f));
        }
    }

    private void RefreshPreview()
    {
        var pixels = new Color32[design.Width * design.Height];
        for (int y = 0; y < design.Height; y++)
        for (int x = 0; x < design.Width; x++)
        {
            Color terrain = TerrainColor(design.TerrainAt(x, y));
            int owner = design.OwnerAt(x, y);
            Color color = owner >= 0 ? Color.Lerp(terrain, MapRenderer.OwnerColor(owner), 0.62f) : terrain;
            if (owner >= 0 && HasDifferentNeighbor(x, y, owner))
                color = Color.Lerp(color, Color.white, 0.22f);
            pixels[y * design.Width + x] = color;
        }
        previewTexture.SetPixels32(pixels);
        previewTexture.Apply(false, false);
    }

    private bool HasDifferentNeighbor(int x, int y, int owner)
    {
        return (x > 0 && design.OwnerAt(x - 1, y) != owner) ||
            (x + 1 < design.Width && design.OwnerAt(x + 1, y) != owner) ||
            (y > 0 && design.OwnerAt(x, y - 1) != owner) ||
            (y + 1 < design.Height && design.OwnerAt(x, y + 1) != owner);
    }

    private static Color TerrainColor(TerrainType terrain) => terrain switch
    {
        TerrainType.Water => new Color32(31, 78, 101, 255),
        TerrainType.River => new Color32(54, 132, 161, 255),
        TerrainType.Ford => new Color32(98, 157, 158, 255),
        TerrainType.Forest => new Color32(58, 100, 65, 255),
        TerrainType.Hills => new Color32(131, 136, 91, 255),
        TerrainType.Mountains => new Color32(138, 136, 130, 255),
        TerrainType.Desert => new Color32(189, 163, 108, 255),
        _ => new Color32(115, 148, 86, 255)
    };

    private static string BrushLabel(DesignerBrush brush) => brush switch
    {
        DesignerBrush.Mountains => "MTNS",
        DesignerBrush.RedSide => "RED",
        DesignerBrush.BlueSide => "BLUE",
        DesignerBrush.Neutral => "NEUTRAL",
        _ => brush.ToString().ToUpperInvariant()
    };

    private GameObject CreateContainer(Transform parent, string objectName)
    {
        var child = new GameObject(objectName, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)child.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return child;
    }

    private GameObject CreatePanel(Transform parent, string objectName, float x, float y,
        float width, float height, Color color)
    {
        var panel = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        Place((RectTransform)panel.transform, x, y, width, height);
        Image image = panel.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return panel;
    }

    private Button CreateButton(Transform parent, string objectName, string label, float x, float y,
        float width, float height, Color color, Action action, int fontSize)
    {
        var buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        Place((RectTransform)buttonObject.transform, x, y, width, height);
        Image image = buttonObject.GetComponent<Image>();
        image.color = color;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colors.selectedColor = Color.white;
        button.colors = colors;
        button.onClick.AddListener(() => action());
        CreateText(buttonObject.transform, "Label", label, 8f, 4f, width - 16f, height - 8f,
            fontSize, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        return button;
    }

    private TMP_Text CreateText(Transform parent, string objectName, string value, float x, float y,
        float width, float height, int fontSize, Color color, TextAlignmentOptions alignment,
        FontStyles style = FontStyles.Normal)
    {
        TMP_Text text = Instantiate(textTemplate, parent);
        text.gameObject.SetActive(true);
        text.name = objectName;
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.outlineWidth = 0f;
        text.raycastTarget = false;
        text.enableAutoSizing = false;
        Place(text.rectTransform, x, y, width, height);
        return text;
    }

    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private void OnDestroy()
    {
        if (previewTexture != null) Destroy(previewTexture);
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MapHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text manpowerText;
    // The slider spans a fraction of the reserve, never the whole reserve at
    // the low end: a 0% commitment would be an order that cannot move.
    private const float MinimumCommitment = 0.05f;
    private sealed class TerritoryLabel
    {
        public PlayerData Player;
        public TMP_Text Text;
        public Vector3 LocalCenter;
        public bool HasTerritory;
        public int Version = -1;
    }
    private readonly List<TerritoryLabel> labels = new List<TerritoryLabel>();
    private MapData map;
    private SpriteRenderer mapSprite;
    private Camera mapCamera;
    private TMP_Text statusText;
    private PlayerData localPlayer;
    private Slider commitmentSlider;
    private TMP_Text commitmentLabel;
    private TMP_Text cellInfo;
    private TMP_Text pocketInfo;
    private readonly TMP_Text[] resourceTexts = new TMP_Text[4];
    private RectTransform commitmentPanel;
    private int recallableTroops, pocketTroops = -1, pocketCells = -1;
    private int displayedCommitment = -1, displayedPercent = -1;
    private static string PocketCost => Mathf.RoundToInt(TerrainRules.EncirclementCostMultiplier * 100f) + "%";

    // Share of the reserve the next order takes with it.
    public float Commitment => commitmentSlider == null ? 0.5f : commitmentSlider.value;

    public void Bind(PlayerData playerData, MapData mapData, SpriteRenderer spriteRenderer)
    {
        map = mapData;
        mapSprite = spriteRenderer;
        mapCamera = Camera.main;
        localPlayer = playerData;
        labels.Clear();
        AddLabel(playerData, manpowerText);
        TMP_Text controls = Instantiate(manpowerText, manpowerText.transform.parent);
        controls.name = "Map controls";
        controls.fontSize = 20;
        controls.alignment = TextAlignmentOptions.TopLeft;
        controls.text = "LEFT CLICK  Advance / attack     RIGHT CLICK  Recall     1-9 / 0  Commitment\nWASD / ARROWS / MIDDLE DRAG  Move     WHEEL  Zoom     SHIFT  Faster     F  Overview";
        PlaceOverlay(controls.rectTransform, new Vector2(0, 1), new Vector2(20, -18), new Vector2(1500, 80));
        statusText = Instantiate(controls, controls.transform.parent);
        statusText.name = "Order status";
        statusText.fontSize = 24;
        statusText.alignment = TextAlignmentOptions.BottomLeft;
        // Leave the bottom-right corner free for the commitment panel.
        PlaceOverlay(statusText.rectTransform, Vector2.zero, new Vector2(20, 18), new Vector2(1180, 65));
        CreateCommitmentPanel(controls.transform.parent);
        CreateResourceBar(controls.transform.parent);
        cellInfo = Instantiate(controls, controls.transform.parent);
        cellInfo.name = "Cell cost readout";
        cellInfo.fontSize = 21;
        cellInfo.color = new Color32(235, 216, 162, 255);
        PlaceOverlay(cellInfo.rectTransform, new Vector2(0, 1), new Vector2(20, -156), new Vector2(1500, 35));
        cellInfo.text = "Hover over land to inspect its occupation cost.";
        pocketInfo = Instantiate(controls, controls.transform.parent);
        pocketInfo.name = "Pocket assault readout";
        pocketInfo.fontSize = 20;
        pocketInfo.alignment = TextAlignmentOptions.BottomLeft;
        PlaceOverlay(pocketInfo.rectTransform, Vector2.zero, new Vector2(20, 100), new Vector2(1180, 35));
        pocketInfo.text = "Encircled wilderness: FREE | Enemy pockets: " + PocketCost + " cost | Automatic capture";
        SetStatus("You are red. Set your commitment, then click wilderness or a rival to advance.");
        Refresh();
    }

    public void AddPlayer(PlayerData player)
    {
        TMP_Text text = Instantiate(manpowerText, manpowerText.transform.parent);
        text.name = "Territory " + player.Id;
        AddLabel(player, text);
        Refresh();
    }

    private void AddLabel(PlayerData player, TMP_Text text)
    {
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 30;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.outlineWidth = 0.2f;
        text.outlineColor = new Color32(16, 25, 28, 255);
        text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        labels.Add(new TerritoryLabel { Player = player, Text = text });
    }

    private static void PlaceOverlay(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    public void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }

    public void SetCellInfo(string message) { if (cellInfo != null) cellInfo.text = message; }

    public void SetAttackState(int manualTroops, int automaticTroops, int cells)
    {
        recallableTroops = manualTroops;
        UpdateCommitmentLabel();
        if (pocketInfo == null || (pocketTroops == automaticTroops && pocketCells == cells)) return;
        pocketTroops = automaticTroops; pocketCells = cells;
        pocketInfo.text = cells > 0 || automaticTroops > 0
            ? "ENCIRCLED  " + cells.ToString("N0") + " cells | AUTO ASSAULT  " + automaticTroops.ToString("N0") +
                " troops | Wilderness FREE | Enemy " + PocketCost + " cost"
            : "Encircled wilderness: FREE | Enemy pockets: " + PocketCost + " cost | Automatic capture";
    }

    public bool BlocksMapInput(Vector2 screenPosition)
    {
        if (commitmentPanel == null) return false;
        Canvas canvas = manpowerText.canvas;
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        return RectTransformUtility.RectangleContainsScreenPoint(commitmentPanel, screenPosition, uiCamera);
    }

    // Number keys and any future presets route through here so the slider, the
    // label, and the value the controller reads never disagree.
    public void SetCommitment(float ratio)
    {
        if (commitmentSlider == null) return;
        commitmentSlider.value = Mathf.Clamp(ratio, MinimumCommitment, 1f);
        UpdateCommitmentLabel();
    }

    private void CreateResourceBar(Transform parent)
    {
        var panel = new GameObject("Resource bar", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        Image background = panel.GetComponent<Image>();
        background.color = new Color(0.05f, 0.09f, 0.12f, 0.85f);
        background.raycastTarget = false;
        var rect = (RectTransform)panel.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -82f);
        rect.sizeDelta = new Vector2(-40f, 64f);
        string[] names = { "Manpower", "Gold", "Industry", "Land" };
        Color32[] colors = { new Color32(244, 244, 235, 255), new Color32(255, 213, 111, 255),
            new Color32(152, 218, 238, 255), new Color32(174, 225, 163, 255) };
        for (int i = 0; i < resourceTexts.Length; i++)
        {
            TMP_Text text = Instantiate(manpowerText, panel.transform);
            text.name = "Resource " + names[i];
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.Left;
            text.fontSize = 22;
            text.enableAutoSizing = true;
            text.fontSizeMin = 12;
            text.fontSizeMax = 22;
            text.color = colors[i];
            text.outlineWidth = 0f;
            RectTransform label = text.rectTransform;
            label.anchorMin = new Vector2(i / 4f, 0f);
            label.anchorMax = new Vector2((i + 1) / 4f, 1f);
            label.offsetMin = new Vector2(14f, 4f);
            label.offsetMax = new Vector2(-8f, -4f);
            resourceTexts[i] = text;
        }
    }

    private void RefreshResources()
    {
        if (resourceTexts[0] == null || localPlayer == null) return;
        int land = map.CountTerritory(localPlayer.Id);
        resourceTexts[0].text = "<size=16>MANPOWER</size>\n" + localPlayer.Manpower.ToString("N0") +
            "  (+" + PlayerData.RecruitmentPerSecond(land).ToString("N0") + "/s)";
        resourceTexts[1].text = "<size=16>GOLD</size>\n" + localPlayer.Gold.ToString("N1") +
            "  (+" + PlayerData.GoldPerSecond(land).ToString("N1") + "/s)";
        resourceTexts[2].text = "<size=16>INDUSTRY</size>\n" + localPlayer.Industry.ToString("N2") +
            "  (+" + PlayerData.IndustryPerSecond(land).ToString("N2") + "/s)";
        resourceTexts[3].text = "<size=16>OWNED LAND</size>\n" + land.ToString("N0") + " tiles";
    }

    public void Refresh()
    {
        foreach (TerritoryLabel label in labels)
        {
            label.Text.text = label.Player.Manpower.ToString("N0");
            int version = map.GetTerritoryVersion(label.Player.Id);
            if (label.Version == version) continue;
            label.Version = version;
            label.HasTerritory = map.TryGetTerritoryCenter(label.Player.Id, out float x, out float y);
            label.Text.enabled = label.HasTerritory;
            // Grid coordinates stay independent of the texture's visual detail.
            label.LocalCenter = new Vector3((x - map.Width * 0.5f) / MapRenderer.CellsPerUnit,
                (y - map.Height * 0.5f) / MapRenderer.CellsPerUnit, 0f);
        }
        UpdateCommitmentLabel();
        RefreshResources();
    }

    private void UpdateCommitmentLabel()
    {
        if (commitmentLabel == null) return;
        int troops = localPlayer == null ? 0 :
            Mathf.Clamp(Mathf.RoundToInt((localPlayer.Manpower + recallableTroops) * Commitment), 0, localPlayer.Manpower + recallableTroops);
        int percent = Mathf.RoundToInt(Commitment * 100f);
        if (troops == displayedCommitment && percent == displayedPercent) return;
        displayedCommitment = troops; displayedPercent = percent;
        commitmentLabel.text = "NEXT  " + percent + "%   ·   " +
            troops.ToString("N0") + " troops";
    }

    // Built in code because the scene has no UI prefabs: the canvas already
    // carries a GraphicRaycaster and a scaler, so the slider only needs its
    // standard background / fill / handle rects wired up.
    private void CreateCommitmentPanel(Transform parent)
    {
        var panel = new GameObject("Troop commitment", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        commitmentPanel = (RectTransform)panel.transform;
        PlaceOverlay((RectTransform)panel.transform, new Vector2(1f, 0f),
            new Vector2(-20f, 18f), new Vector2(360f, 76f));
        panel.GetComponent<Image>().color = new Color(0.05f, 0.09f, 0.12f, 0.78f);

        commitmentLabel = Instantiate(manpowerText, panel.transform);
        commitmentLabel.name = "Commitment readout";
        commitmentLabel.raycastTarget = false;
        commitmentLabel.fontSize = 21;
        commitmentLabel.fontStyle = FontStyles.Bold;
        commitmentLabel.alignment = TextAlignmentOptions.Center;
        commitmentLabel.color = Color.white;
        commitmentLabel.outlineWidth = 0f;
        RectTransform labelRect = commitmentLabel.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.anchoredPosition = new Vector2(0f, -8f);
        labelRect.sizeDelta = new Vector2(-24f, 26f);

        var sliderObject = new GameObject("Commitment slider", typeof(RectTransform), typeof(Slider));
        sliderObject.transform.SetParent(panel.transform, false);
        var sliderRect = (RectTransform)sliderObject.transform;
        sliderRect.anchorMin = new Vector2(0f, 0f);
        sliderRect.anchorMax = new Vector2(1f, 0f);
        sliderRect.pivot = new Vector2(0.5f, 0f);
        sliderRect.anchoredPosition = new Vector2(0f, 14f);
        sliderRect.sizeDelta = new Vector2(-24f, 20f);

        Image background = CreateBar(sliderRect, "Background", new Color(0.16f, 0.2f, 0.24f, 1f));
        background.rectTransform.anchorMin = new Vector2(0f, 0.25f);
        background.rectTransform.anchorMax = new Vector2(1f, 0.75f);
        background.rectTransform.sizeDelta = Vector2.zero;

        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderRect, false);
        var fillAreaRect = (RectTransform)fillArea.transform;
        fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRect.anchoredPosition = new Vector2(-5f, 0f);
        fillAreaRect.sizeDelta = new Vector2(-20f, 0f);
        Image fill = CreateBar(fillAreaRect, "Fill", MapRenderer.OwnerColor(0));
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.sizeDelta = new Vector2(10f, 0f);

        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(sliderRect, false);
        var handleAreaRect = (RectTransform)handleArea.transform;
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.sizeDelta = new Vector2(-20f, 0f);
        Image handle = CreateBar(handleAreaRect, "Handle", new Color(0.96f, 0.94f, 0.88f, 1f));
        handle.rectTransform.anchorMin = new Vector2(0f, 0f);
        handle.rectTransform.anchorMax = new Vector2(0f, 1f);
        handle.rectTransform.sizeDelta = new Vector2(20f, 0f);

        commitmentSlider = sliderObject.GetComponent<Slider>();
        commitmentSlider.fillRect = fill.rectTransform;
        commitmentSlider.handleRect = handle.rectTransform;
        commitmentSlider.targetGraphic = handle;
        commitmentSlider.direction = Slider.Direction.LeftToRight;
        commitmentSlider.minValue = MinimumCommitment;
        commitmentSlider.maxValue = 1f;
        commitmentSlider.wholeNumbers = false;
        commitmentSlider.value = 0.5f;
        commitmentSlider.onValueChanged.AddListener(_ => UpdateCommitmentLabel());
    }

    private static Image CreateBar(Transform parent, string objectName, Color color)
    {
        var child = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        child.transform.SetParent(parent, false);
        var image = child.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private void LateUpdate()
    {
        if (mapCamera == null) return;
        foreach (TerritoryLabel label in labels)
        {
            if (!label.HasTerritory) continue;
            Vector3 screen = mapCamera.WorldToScreenPoint(mapSprite.transform.TransformPoint(label.LocalCenter));
            label.Text.enabled = screen.z > 0;
            if (!label.Text.enabled) continue;
            RectTransform rect = label.Text.rectTransform;
            Canvas canvas = label.Text.canvas;
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)rect.parent, screen, uiCamera, out Vector2 point))
                rect.localPosition = new Vector3(point.x, point.y, 0f);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public partial class MapHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text manpowerText;
    // The slider reserves a share for possible combat. Wilderness itself can
    // still be occupied by a zero-manpower order when the reserve is empty.
    private const float MinimumCommitment = 0.05f;
    private const float TerritoryLabelLift = 30f;
    private sealed class TerritoryLabel
    {
        public PlayerData Player;
        public TMP_Text Text;
        public Vector3 LocalCenter;
        public bool HasTerritory;
        public int Version = -1;
    }
    private sealed class SettlementLabel
    {
        public TMP_Text Text;
        public Image Marker;
        public Vector3 LocalPosition;
        public Vector2 TextOffset;
    }
    private sealed class InfrastructureLabel
    {
        public TMP_Text Text;
        public Image Marker;
        public Vector3 LocalPosition;
        public Vector2 TextOffset;
    }
    private readonly List<TerritoryLabel> labels = new List<TerritoryLabel>();
    private readonly List<SettlementLabel> settlementLabels = new List<SettlementLabel>();
    // One NATO counter per division: a faction-coloured disc carrying the
    // infantry cross and the unit's number.
    private sealed class DivisionCounter
    {
        public Division Division;
        public GameObject Root;
        public Image Rim;
        public Image Disc;
        public Image Symbol;
        public TMP_Text Number;
        public Vector3 LocalPosition;
    }
    private readonly List<DivisionCounter> divisionCounters = new List<DivisionCounter>();
    private Sprite discSprite;
    private Sprite infantrySprite;
    private RectTransform divisionLayer;
    private RectTransform selectionBox;
    private readonly List<Division> visibleDivisions = new List<Division>();
    private TMP_Text fogDebugLabel;
    private readonly RectTransform[] selectionEdges = new RectTransform[4];
    private readonly List<InfrastructureLabel> infrastructureLabels = new List<InfrastructureLabel>();
    private MapData map;
    private SpriteRenderer mapSprite;
    private Camera mapCamera;
    private TMP_Text statusText;
    private PlayerData localPlayer;
    private Slider commitmentSlider;
    private TMP_Text commitmentLabel;
    private TMP_Text cellInfo;
    private TMP_Text pocketInfo;
    private readonly TMP_Text[] resourceTexts = new TMP_Text[5];
    private TMP_Text recruitLabel;
    private TMP_Text divisionLabel;
    private TMP_Text simulationClockLabel;
    private TMP_Text pauseLabel;
    private Image pauseBackground;
    private static readonly int[] SimulationSpeeds = { 1, 2, 4 };
    private readonly Image[] speedButtons = new Image[3];
    private readonly Image[] stanceButtons = new Image[4];
    public event System.Action PauseRequested;
    public event System.Action<int> SpeedRequested;
    public event System.Action<DivisionStance> StanceRequested;
    // Raised by the button; MapController owns the actual roster.
    public event System.Action RaiseDivisionRequested;
    private int divisionAffordable;
    private readonly List<Sprite> generatedIconSprites = new List<Sprite>();
    private readonly List<Texture2D> generatedIconTextures = new List<Texture2D>();
    private RectTransform commitmentPanel;
    private int recallableTroops, pocketTroops = -1, pocketCells = -1;
    private int displayedCommitment = -1, displayedPercent = -1;
    private static string PocketCost => Mathf.RoundToInt(TerrainRules.EncirclementCostMultiplier * 100f) + "%";

    // Share of the reserve the next order takes with it.
    public float Commitment => commitmentSlider == null ? 0.5f : commitmentSlider.value;
    public TMP_Text TextTemplate => manpowerText;
    public Transform OverlayRoot => manpowerText.transform.parent;

    public void SetInitialLabelVisible(bool visible) => manpowerText.gameObject.SetActive(visible);

    public void Bind(PlayerData playerData, MapData mapData, SpriteRenderer spriteRenderer,
        ScenarioMap scenario = null)
    {
        map = mapData;
        mapSprite = spriteRenderer;
        mapCamera = Camera.main;
        localPlayer = playerData;
        labels.Clear();
        settlementLabels.Clear();
        infrastructureLabels.Clear();
        foreach (DivisionCounter counter in divisionCounters) Destroy(counter.Root);
        divisionCounters.Clear();
        if (selectionBox != null) { Destroy(selectionBox.gameObject); selectionBox = null; }
        if (discSprite == null) discSprite = CreateDiscSprite();
        if (infantrySprite == null) infantrySprite = CreateInfantrySprite();
        if (divisionLayer == null)
        {
            // Counters sit above the map labels but below the fixed panels.
            var layerObject = new GameObject("Division counters", typeof(RectTransform));
            layerObject.transform.SetParent(manpowerText.transform.parent, false);
            divisionLayer = (RectTransform)layerObject.transform;
            divisionLayer.anchorMin = Vector2.zero;
            divisionLayer.anchorMax = Vector2.one;
            divisionLayer.offsetMin = divisionLayer.offsetMax = Vector2.zero;
        }
        AddLabel(playerData, manpowerText);
        if (scenario != null)
        {
            AddInfrastructureLabels(scenario.InfrastructureSites);
            AddSettlementLabels(scenario.Settlements);
        }
        TMP_Text controls = Instantiate(manpowerText, manpowerText.transform.parent);
        controls.name = "Map controls";
        controls.fontSize = 20;
        controls.alignment = TextAlignmentOptions.TopLeft;
        controls.text = "LEFT CLICK / DRAG  Select     SHIFT  Add to selection     CTRL+1-9  Store group     1-9  Recall group     RIGHT CLICK  Send\nF1-F4  Attack / Defend / Reserve / Redeploy     SPACE  Pause / Resume     [ / ]  Speed     B  Raise division     R  Recruit     WASD  Move     F  Overview";
        PlaceOverlay(controls.rectTransform, new Vector2(0, 1), new Vector2(20, -18), new Vector2(1500, 80));
        statusText = Instantiate(controls, controls.transform.parent);
        statusText.name = "Order status";
        statusText.fontSize = 24;
        statusText.alignment = TextAlignmentOptions.BottomLeft;
        // Leave the bottom-right corner free for the commitment panel.
        PlaceOverlay(statusText.rectTransform, Vector2.zero, new Vector2(20, 18), new Vector2(1180, 65));
        fogDebugLabel = Instantiate(controls, controls.transform.parent);
        fogDebugLabel.name = "Fog debug banner";
        fogDebugLabel.fontSize = 22;
        fogDebugLabel.fontStyle = FontStyles.Bold;
        fogDebugLabel.alignment = TextAlignmentOptions.TopRight;
        fogDebugLabel.color = new Color32(255, 176, 96, 255);
        PlaceOverlay(fogDebugLabel.rectTransform, new Vector2(1f, 1f), new Vector2(-20f, -120f),
            new Vector2(440f, 30f));
        fogDebugLabel.text = "DEBUG  ·  FOG LIFTED  ·  F9";
        fogDebugLabel.enabled = false;
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
        pocketInfo.text = "Wilderness: FREE | Enemy pockets: " + PocketCost + " cost | Automatic capture";
        SetStatus("You are red. Click a division to select it, then right-click to send it into the wilderness.");
        Refresh();
    }

    private void AddInfrastructureLabels(IReadOnlyList<ScenarioInfrastructureSite> sites)
    {
        Transform parent = manpowerText.transform.parent;
        foreach (ScenarioInfrastructureSite site in sites)
        {
            var markerObject = new GameObject("Map infrastructure marker " + site.Name,
                typeof(RectTransform), typeof(Image));
            markerObject.transform.SetParent(parent, false);
            Image marker = markerObject.GetComponent<Image>();
            marker.raycastTarget = false;
            marker.color = InfrastructureColor(site.Type);
            RectTransform markerRect = marker.rectTransform;
            markerRect.anchorMin = markerRect.anchorMax = markerRect.pivot = new Vector2(0.5f, 0.5f);
            markerRect.sizeDelta = new Vector2(15f, 15f);

            TMP_Text text = Instantiate(manpowerText, parent);
            text.name = "Map infrastructure label " + site.Name;
            text.text = InfrastructureCode(site.Type) + "  " + site.Name;
            text.raycastTarget = false;
            text.fontSize = 11;
            text.fontStyle = FontStyles.Bold;
            text.color = InfrastructureColor(site.Type);
            text.outlineWidth = 0.2f;
            text.outlineColor = new Color32(5, 12, 16, 255);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.5f);
            bool placeLeft = site.X > map.Width * 0.74f;
            textRect.pivot = new Vector2(placeLeft ? 1f : 0f, 0.5f);
            textRect.sizeDelta = new Vector2(235f, 22f);

            float verticalOffset = 17f + ((site.X * 11 + site.Y * 5 + (int)site.Type) % 3) * 11f;
            infrastructureLabels.Add(new InfrastructureLabel
            {
                Text = text,
                Marker = marker,
                LocalPosition = new Vector3(
                    (site.X + 0.5f - map.Width * 0.5f) / MapRenderer.CellsPerUnit,
                    (site.Y + 0.5f - map.Height * 0.5f) / MapRenderer.CellsPerUnit, 0f),
                TextOffset = new Vector2(placeLeft ? -10f : 10f, verticalOffset)
            });
        }
    }

    private void AddSettlementLabels(IReadOnlyList<ScenarioSettlement> settlements)
    {
        Transform parent = manpowerText.transform.parent;
        foreach (ScenarioSettlement settlement in settlements)
        {
            var markerObject = new GameObject("Map settlement marker " + settlement.Name,
                typeof(RectTransform), typeof(Image));
            markerObject.transform.SetParent(parent, false);
            Image marker = markerObject.GetComponent<Image>();
            marker.raycastTarget = false;
            marker.color = settlement.IsMajor
                ? new Color32(255, 240, 181, 255)
                : new Color32(224, 215, 181, 255);
            RectTransform markerRect = marker.rectTransform;
            markerRect.anchorMin = markerRect.anchorMax = markerRect.pivot = new Vector2(0.5f, 0.5f);
            markerRect.sizeDelta = Vector2.one * (settlement.IsMajor ? 10f : 7f);
            markerRect.localRotation = Quaternion.Euler(0f, 0f, 45f);

            TMP_Text text = Instantiate(manpowerText, parent);
            text.name = "Map settlement label " + settlement.Name;
            text.text = settlement.Name;
            text.raycastTarget = false;
            text.fontSize = settlement.IsMajor ? 17 : 13;
            text.fontStyle = settlement.IsMajor ? FontStyles.Bold : FontStyles.Normal;
            text.color = settlement.IsMajor ? Color.white : new Color32(231, 224, 199, 255);
            text.outlineWidth = 0.2f;
            text.outlineColor = new Color32(5, 12, 16, 255);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.5f);
            bool placeLeft = settlement.X > map.Width * 0.76f;
            textRect.pivot = new Vector2(placeLeft ? 1f : 0f, 0.5f);
            textRect.sizeDelta = new Vector2(190f, settlement.IsMajor ? 28f : 23f);

            float verticalOffset = ((settlement.X * 17 + settlement.Y * 7) % 3 - 1) * 12f;
            settlementLabels.Add(new SettlementLabel
            {
                Text = text,
                Marker = marker,
                LocalPosition = new Vector3(
                    (settlement.X + 0.5f - map.Width * 0.5f) / MapRenderer.CellsPerUnit,
                    (settlement.Y + 0.5f - map.Height * 0.5f) / MapRenderer.CellsPerUnit, 0f),
                TextOffset = new Vector2(placeLeft ? -9f : 9f, verticalOffset)
            });
        }
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
            : "Wilderness: FREE | Enemy pockets: " + PocketCost + " cost | Automatic capture";
    }

    public void SetSimulationState(bool paused, int speed, double simulationTime)
    {
        if (simulationClockLabel == null) return;
        var elapsed = System.TimeSpan.FromSeconds(System.Math.Max(0d, simulationTime));
        string clock = ((int)elapsed.TotalHours).ToString("00") + ":" +
            elapsed.Minutes.ToString("00") + ":" + elapsed.Seconds.ToString("00");
        simulationClockLabel.text = "SIM " + clock + "  \u00b7  " +
            (paused ? "PAUSED" : "RUNNING " + speed + "x");
        simulationClockLabel.color = paused
            ? new Color32(255, 213, 111, 255)
            : new Color32(244, 244, 235, 255);
        if (pauseLabel != null) pauseLabel.text = paused ? "RESUME  (SPACE)" : "PAUSE  (SPACE)";
        if (pauseBackground != null)
            pauseBackground.color = paused
                ? new Color32(48, 102, 145, 255)
                : new Color32(139, 57, 54, 255);
        for (int i = 0; i < speedButtons.Length; i++)
            if (speedButtons[i] != null)
                speedButtons[i].color = SimulationSpeeds[i] == speed
                    ? new Color32(198, 142, 56, 255)
                    : new Color32(40, 56, 66, 255);
    }

    // Highlights whichever stance the current selection is set to.
    public void SetStanceHighlight(DivisionStance? stance)
    {
        for (int i = 0; i < stanceButtons.Length; i++)
        {
            if (stanceButtons[i] == null) continue;
            bool active = stance.HasValue && (int)stance.Value == i;
            stanceButtons[i].color = active
                ? new Color32(198, 142, 56, 255)
                : new Color32(40, 56, 66, 255);
        }
    }

    // A standing reminder that the fog is lifted, so a debug run is never
    // mistaken for the real picture.
    public void SetFogDebug(bool revealed)
    {
        if (fogDebugLabel != null) fogDebugLabel.enabled = revealed;
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

    private void CreateCommitmentPanel(Transform parent)
    {
        var panel = new GameObject("Troop commitment", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        commitmentPanel = (RectTransform)panel.transform;
        PlaceOverlay((RectTransform)panel.transform, new Vector2(1f, 0f),
            new Vector2(-20f, 18f), new Vector2(360f, 300f));
        panel.GetComponent<Image>().color = new Color(0.05f, 0.09f, 0.12f, 0.78f);

        simulationClockLabel = Instantiate(manpowerText, panel.transform);
        simulationClockLabel.name = "Simulation clock";
        simulationClockLabel.raycastTarget = false;
        simulationClockLabel.fontSize = 20;
        simulationClockLabel.fontStyle = FontStyles.Bold;
        simulationClockLabel.alignment = TextAlignmentOptions.Center;
        simulationClockLabel.color = Color.white;
        simulationClockLabel.outlineWidth = 0f;
        simulationClockLabel.enableAutoSizing = true;
        simulationClockLabel.fontSizeMin = 12;
        simulationClockLabel.fontSizeMax = 20;
        simulationClockLabel.text = "SIM 00:00:00  ·  RUNNING 1x";
        RectTransform turnRect = simulationClockLabel.rectTransform;
        turnRect.anchorMin = new Vector2(0f, 1f);
        turnRect.anchorMax = new Vector2(1f, 1f);
        turnRect.pivot = new Vector2(0.5f, 1f);
        turnRect.anchoredPosition = new Vector2(0f, -6f);
        turnRect.sizeDelta = new Vector2(-24f, 30f);

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
        labelRect.anchoredPosition = new Vector2(0f, -40f);
        labelRect.sizeDelta = new Vector2(-24f, 26f);

        var sliderObject = new GameObject("Commitment slider", typeof(RectTransform), typeof(Slider));
        sliderObject.transform.SetParent(panel.transform, false);
        var sliderRect = (RectTransform)sliderObject.transform;
        sliderRect.anchorMin = new Vector2(0f, 0f);
        sliderRect.anchorMax = new Vector2(1f, 0f);
        sliderRect.pivot = new Vector2(0.5f, 0f);
        sliderRect.anchoredPosition = new Vector2(0f, 214f);
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

        var recruitObject = new GameObject("Recruit button", typeof(RectTransform),
            typeof(Image), typeof(Button));
        recruitObject.transform.SetParent(panel.transform, false);
        var recruitRect = (RectTransform)recruitObject.transform;
        recruitRect.anchorMin = new Vector2(0f, 0f);
        recruitRect.anchorMax = new Vector2(1f, 0f);
        recruitRect.pivot = new Vector2(0.5f, 0f);
        recruitRect.anchoredPosition = new Vector2(0f, 146f);
        recruitRect.sizeDelta = new Vector2(-24f, 40f);
        Image recruitBackground = recruitObject.GetComponent<Image>();
        recruitBackground.color = new Color32(139, 57, 54, 255);
        Button recruitButton = recruitObject.GetComponent<Button>();
        recruitButton.targetGraphic = recruitBackground;
        recruitButton.onClick.AddListener(RecruitMaximum);
        recruitLabel = Instantiate(manpowerText, recruitObject.transform);
        recruitLabel.name = "Recruit readout";
        recruitLabel.raycastTarget = false;
        recruitLabel.fontSize = 17;
        recruitLabel.fontStyle = FontStyles.Bold;
        recruitLabel.alignment = TextAlignmentOptions.Center;
        recruitLabel.color = Color.white;
        recruitLabel.outlineWidth = 0f;
        recruitLabel.enableAutoSizing = true;
        recruitLabel.fontSizeMin = 11;
        recruitLabel.fontSizeMax = 17;
        RectTransform recruitTextRect = recruitLabel.rectTransform;
        recruitTextRect.anchorMin = Vector2.zero;
        recruitTextRect.anchorMax = Vector2.one;
        recruitTextRect.offsetMin = new Vector2(6f, 2f);
        recruitTextRect.offsetMax = new Vector2(-6f, -2f);
        UpdateRecruitLabel();

        var divisionObject = new GameObject("Raise division button", typeof(RectTransform),
            typeof(Image), typeof(Button));
        divisionObject.transform.SetParent(panel.transform, false);
        var divisionRect = (RectTransform)divisionObject.transform;
        divisionRect.anchorMin = new Vector2(0f, 0f);
        divisionRect.anchorMax = new Vector2(1f, 0f);
        divisionRect.pivot = new Vector2(0.5f, 0f);
        divisionRect.anchoredPosition = new Vector2(0f, 100f);
        divisionRect.sizeDelta = new Vector2(-24f, 40f);
        Image divisionBackground = divisionObject.GetComponent<Image>();
        divisionBackground.color = new Color32(48, 102, 145, 255);
        Button divisionButton = divisionObject.GetComponent<Button>();
        divisionButton.targetGraphic = divisionBackground;
        divisionButton.onClick.AddListener(() => RaiseDivisionRequested?.Invoke());
        divisionLabel = Instantiate(manpowerText, divisionObject.transform);
        divisionLabel.name = "Raise division readout";
        divisionLabel.raycastTarget = false;
        divisionLabel.fontSize = 17;
        divisionLabel.fontStyle = FontStyles.Bold;
        divisionLabel.alignment = TextAlignmentOptions.Center;
        divisionLabel.color = Color.white;
        divisionLabel.outlineWidth = 0f;
        divisionLabel.enableAutoSizing = true;
        divisionLabel.fontSizeMin = 11;
        divisionLabel.fontSizeMax = 17;
        RectTransform divisionTextRect = divisionLabel.rectTransform;
        divisionTextRect.anchorMin = Vector2.zero;
        divisionTextRect.anchorMax = Vector2.one;
        divisionTextRect.offsetMin = new Vector2(6f, 2f);
        divisionTextRect.offsetMax = new Vector2(-6f, -2f);
        UpdateDivisionLabel();

        // One row of stance buttons: attack, defend, reserve, redeploy.
        string[] stanceNames = { "ATTACK", "DEFEND", "RESERVE", "REDEPLOY" };
        for (int i = 0; i < stanceNames.Length; i++)
        {
            int index = i;
            var stanceObject = new GameObject("Stance " + stanceNames[i],
                typeof(RectTransform), typeof(Image), typeof(Button));
            stanceObject.transform.SetParent(panel.transform, false);
            var stanceRect = (RectTransform)stanceObject.transform;
            stanceRect.anchorMin = new Vector2(i / 4f, 0f);
            stanceRect.anchorMax = new Vector2((i + 1) / 4f, 0f);
            stanceRect.pivot = new Vector2(0.5f, 0f);
            stanceRect.anchoredPosition = new Vector2(0f, 58f);
            stanceRect.sizeDelta = new Vector2(-16f, 34f);
            Image stanceBackground = stanceObject.GetComponent<Image>();
            stanceBackground.color = new Color32(40, 56, 66, 255);
            stanceButtons[i] = stanceBackground;
            Button stanceButton = stanceObject.GetComponent<Button>();
            stanceButton.targetGraphic = stanceBackground;
            stanceButton.onClick.AddListener(() => StanceRequested?.Invoke((DivisionStance)index));
            TMP_Text stanceText = Instantiate(manpowerText, stanceObject.transform);
            stanceText.name = "Stance label " + stanceNames[i];
            stanceText.raycastTarget = false;
            stanceText.fontSize = 12;
            stanceText.fontStyle = FontStyles.Bold;
            stanceText.alignment = TextAlignmentOptions.Center;
            stanceText.color = Color.white;
            stanceText.outlineWidth = 0f;
            stanceText.enableAutoSizing = true;
            stanceText.fontSizeMin = 8;
            stanceText.fontSizeMax = 12;
            stanceText.text = stanceNames[i];
            RectTransform stanceTextRect = stanceText.rectTransform;
            stanceTextRect.anchorMin = Vector2.zero;
            stanceTextRect.anchorMax = Vector2.one;
            stanceTextRect.offsetMin = new Vector2(2f, 1f);
            stanceTextRect.offsetMax = new Vector2(-2f, -1f);
        }

        var pauseObject = new GameObject("Simulation pause button", typeof(RectTransform),
            typeof(Image), typeof(Button));
        pauseObject.transform.SetParent(panel.transform, false);
        var pauseRect = (RectTransform)pauseObject.transform;
        pauseRect.anchorMin = new Vector2(0f, 0f);
        pauseRect.anchorMax = new Vector2(0.52f, 0f);
        pauseRect.pivot = new Vector2(0.5f, 0f);
        pauseRect.anchoredPosition = new Vector2(0f, 12f);
        pauseRect.sizeDelta = new Vector2(-16f, 40f);
        pauseBackground = pauseObject.GetComponent<Image>();
        pauseBackground.color = new Color32(139, 57, 54, 255);
        Button pauseButton = pauseObject.GetComponent<Button>();
        pauseButton.targetGraphic = pauseBackground;
        pauseButton.onClick.AddListener(() => PauseRequested?.Invoke());
        pauseLabel = Instantiate(manpowerText, pauseObject.transform);
        pauseLabel.name = "Simulation pause readout";
        pauseLabel.raycastTarget = false;
        pauseLabel.fontSize = 15;
        pauseLabel.fontStyle = FontStyles.Bold;
        pauseLabel.alignment = TextAlignmentOptions.Center;
        pauseLabel.color = Color.white;
        pauseLabel.outlineWidth = 0f;
        pauseLabel.enableAutoSizing = true;
        pauseLabel.fontSizeMin = 9;
        pauseLabel.fontSizeMax = 15;
        pauseLabel.text = "PAUSE  (SPACE)";
        RectTransform pauseTextRect = pauseLabel.rectTransform;
        pauseTextRect.anchorMin = Vector2.zero;
        pauseTextRect.anchorMax = Vector2.one;
        pauseTextRect.offsetMin = new Vector2(4f, 2f);
        pauseTextRect.offsetMax = new Vector2(-4f, -2f);

        for (int i = 0; i < SimulationSpeeds.Length; i++)
        {
            int speed = SimulationSpeeds[i];
            var speedObject = new GameObject("Speed " + speed + "x button", typeof(RectTransform),
                typeof(Image), typeof(Button));
            speedObject.transform.SetParent(panel.transform, false);
            var speedRect = (RectTransform)speedObject.transform;
            speedRect.anchorMin = new Vector2(0.52f + i * 0.16f, 0f);
            speedRect.anchorMax = new Vector2(0.68f + i * 0.16f, 0f);
            speedRect.pivot = new Vector2(0.5f, 0f);
            speedRect.anchoredPosition = new Vector2(0f, 12f);
            speedRect.sizeDelta = new Vector2(-8f, 40f);
            Image speedBackground = speedObject.GetComponent<Image>();
            speedBackground.color = i == 0
                ? new Color32(198, 142, 56, 255)
                : new Color32(40, 56, 66, 255);
            speedButtons[i] = speedBackground;
            Button speedButton = speedObject.GetComponent<Button>();
            speedButton.targetGraphic = speedBackground;
            speedButton.onClick.AddListener(() => SpeedRequested?.Invoke(speed));
            TMP_Text speedText = Instantiate(manpowerText, speedObject.transform);
            speedText.name = "Speed " + speed + "x readout";
            speedText.raycastTarget = false;
            speedText.fontSize = 14;
            speedText.fontStyle = FontStyles.Bold;
            speedText.alignment = TextAlignmentOptions.Center;
            speedText.color = Color.white;
            speedText.outlineWidth = 0f;
            speedText.text = speed + "x";
            RectTransform speedTextRect = speedText.rectTransform;
            speedTextRect.anchorMin = Vector2.zero;
            speedTextRect.anchorMax = Vector2.one;
            speedTextRect.offsetMin = Vector2.zero;
            speedTextRect.offsetMax = Vector2.zero;
        }
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
                // Lifted clear of the division counters, which muster on the
                // territory centre and would otherwise sit straight on top.
                rect.localPosition = new Vector3(point.x, point.y + TerritoryLabelLift, 0f);
        }
        foreach (DivisionCounter counter in divisionCounters)
        {
            Vector3 screen = mapCamera.WorldToScreenPoint(
                mapSprite.transform.TransformPoint(counter.LocalPosition));
            bool onScreen = screen.z > 0f && screen.x >= -40f && screen.y >= -40f &&
                screen.x <= Screen.width + 40f && screen.y <= Screen.height + 40f;
            counter.Root.SetActive(onScreen);
            if (!onScreen) continue;
            Canvas counterCanvas = manpowerText.canvas;
            Camera counterCamera = counterCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : counterCanvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)counter.Root.transform.parent, screen, counterCamera, out Vector2 counterPoint))
                ((RectTransform)counter.Root.transform).localPosition =
                    new Vector3(counterPoint.x, counterPoint.y, 0f);
        }
        foreach (SettlementLabel label in settlementLabels)
        {
            Vector3 screen = mapCamera.WorldToScreenPoint(mapSprite.transform.TransformPoint(label.LocalPosition));
            bool visible = screen.z > 0f && screen.x >= -20f && screen.y >= -20f &&
                screen.x <= Screen.width + 20f && screen.y <= Screen.height + 20f;
            label.Text.enabled = visible;
            label.Marker.enabled = visible;
            if (!visible) continue;
            Canvas canvas = label.Text.canvas;
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)label.Text.rectTransform.parent, screen, uiCamera, out Vector2 point)) continue;
            label.Marker.rectTransform.localPosition = new Vector3(point.x, point.y, 0f);
            point += label.TextOffset;
            label.Text.rectTransform.localPosition = new Vector3(point.x, point.y, 0f);
        }
        foreach (InfrastructureLabel label in infrastructureLabels)
        {
            Vector3 screen = mapCamera.WorldToScreenPoint(mapSprite.transform.TransformPoint(label.LocalPosition));
            bool visible = screen.z > 0f && screen.x >= -20f && screen.y >= -20f &&
                screen.x <= Screen.width + 20f && screen.y <= Screen.height + 20f;
            label.Text.enabled = visible;
            label.Marker.enabled = visible;
            if (!visible) continue;
            Canvas canvas = label.Text.canvas;
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)label.Text.rectTransform.parent, screen, uiCamera, out Vector2 point)) continue;
            label.Marker.rectTransform.localPosition = new Vector3(point.x, point.y, 0f);
            point += label.TextOffset;
            label.Text.rectTransform.localPosition = new Vector3(point.x, point.y, 0f);
        }
    }

    private static string InfrastructureCode(InfrastructureSiteType type) => type switch
    {
        InfrastructureSiteType.Airfield => "A",
        InfrastructureSiteType.RailHub => "H",
        InfrastructureSiteType.SupplyDepot => "D",
        InfrastructureSiteType.Bridge => "B",
        InfrastructureSiteType.Port => "P",
        _ => "I"
    };

    private static Color InfrastructureColor(InfrastructureSiteType type) => type switch
    {
        InfrastructureSiteType.Airfield => new Color32(157, 220, 239, 255),
        InfrastructureSiteType.RailHub => new Color32(231, 231, 218, 255),
        InfrastructureSiteType.SupplyDepot => new Color32(237, 166, 83, 255),
        InfrastructureSiteType.Bridge => new Color32(137, 199, 224, 255),
        InfrastructureSiteType.Port => new Color32(105, 178, 229, 255),
        _ => new Color32(255, 210, 91, 255)
    };

    private void OnDestroy()
    {
        foreach (Sprite sprite in generatedIconSprites) Destroy(sprite);
        foreach (Texture2D texture in generatedIconTextures) Destroy(texture);
    }
}

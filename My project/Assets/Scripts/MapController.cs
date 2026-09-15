using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(MapRenderer), typeof(SpriteRenderer))]
public partial class MapController : MonoBehaviour
{
    [SerializeField, Min(64)] private int duelSize = DuelMap.DefaultSize;
    [SerializeField] private int terrainSeed = 1847;
    [SerializeField] private MapHUD hud;
    // Diplomacy is deliberately data-driven even though current scenarios have
    // no allied faction. When an id is added here, its observed defensive lines
    // use the allied green relationship colour instead of hostile red.
    [SerializeField] private int[] alliedFactionIds = new int[0];
    // Number keys are a fast alternative to dragging the commitment slider.
    private static readonly Key[] CommitmentKeys =
    {
        Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
        Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0
    };
    private readonly List<PlayerData> players = new List<PlayerData>();
    private PlayerData player;
    private MapData map;
    private MapRenderer mapRenderer;
    private Camera mapCamera;
    private EncirclementSystem encirclement;
    private CameraController cameraController;
    private GameSetupUI setupUI;
    private MapInfrastructureRenderer infrastructureRenderer;
    private MapOrderRenderer orderRenderer;
    private MapFogRenderer fogRenderer;
    private FogOfWar fog;
    public FogOfWar Fog => fog;
    private int hoverCell = -1, hoverOwner = -2, hoverCost = -1;
    private bool hoverEncircled;
    private DivisionSystem divisions;
    // Selection is a set now: a drag can pick up several divisions at once.
    private readonly List<Division> selection = new List<Division>();
    private readonly List<Division> boxHits = new List<Division>();
    // Left-drag draws a selection box; a press that never moves is a click.
    private Vector2 dragStart;
    private bool dragging;
    private bool dragOnMap;
    private const float DragThreshold = 7f;
    private Vector2 holdStart;
    private bool holdDragging;
    private bool holdOnMap;
    private readonly List<int> holdLine = new List<int>();
    private readonly Dictionary<int, List<Division>> controlGroups = new Dictionary<int, List<Division>>();
    [SerializeField, Min(0.02f)] private float fixedTickDuration = (float)GameClock.DefaultFixedTickDuration;
    private const int MaxTicksPerFrame = 32;
    private GameClock gameClock;
    private GameSimulation gameSimulation;
    private static readonly Key[] GroupKeys =
    {
        Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
        Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9
    };
    // Ordered to match DivisionStance: attack, defend, reserve, redeploy.
    private static readonly Key[] StanceKeys = { Key.F1, Key.F2, Key.F3, Key.F4 };
    // Read-only access also allows simulation/scene verification without reflection.
    public MapData Map => map;
    public PlayerData LocalPlayer => player;
    public EncirclementSystem Encirclement => encirclement;
    public DivisionSystem Divisions => divisions;
    public Division SelectedDivision => selection.Count > 0 ? selection[0] : null;
    public IReadOnlyList<Division> Selection => selection;
    public GameClock Clock => gameClock;
    public GameSimulation Simulation => gameSimulation;


    private void Start()
    {
        mapRenderer = GetComponent<MapRenderer>();
        infrastructureRenderer = GetComponent<MapInfrastructureRenderer>();
        if (infrastructureRenderer == null)
            infrastructureRenderer = gameObject.AddComponent<MapInfrastructureRenderer>();
        orderRenderer = GetComponent<MapOrderRenderer>();
        if (orderRenderer == null) orderRenderer = gameObject.AddComponent<MapOrderRenderer>();
        fogRenderer = GetComponent<MapFogRenderer>();
        if (fogRenderer == null) fogRenderer = gameObject.AddComponent<MapFogRenderer>();
        mapCamera = Camera.main;
        cameraController = mapCamera.GetComponent<CameraController>();
        if (cameraController != null) cameraController.enabled = false;
        setupUI = GameSetupUI.Create(this, hud);
        hud.SetInitialLabelVisible(false);
        mapCamera.backgroundColor = new Color32(12, 25, 34, 255);
    }

    public void StartQuickGame()
    {
        if (map != null) return;
        ScenarioMap duel = DuelMap.Create(duelSize, terrainSeed, out MapData generatedMap);
        // Four divisions for you and three each for the rivals, at 10,000
        // military population apiece, plus a little over for the first rebuild.
        StartGame(generatedMap, DuelMap.StartingMilitary, DuelMap.StartingMilitary,
            "You are red, on the western half of a mirrored island. Select divisions, right-click to send them, " +
            "right-drag along your border to hold a line, and press SPACE whenever you need to pause.", duel, true);
    }

    public bool StartDesignedGame(ScenarioMap scenario)
    {
        if (map != null || scenario == null || !scenario.IsPlayable(out _)) return false;
        StartGame(scenario.CreateMapData(), scenario.PlayerManpower, scenario.EnemyManpower,
            scenario.Name + " — wilderness is free and passive manpower growth is paused.", scenario);
        return true;
    }

    private void StartGame(MapData gameMap, int playerManpower, int enemyManpower, string openingStatus,
        ScenarioMap scenario, bool duelDeployment = false)
    {
        map = gameMap;
        players.Clear();
        selection.Clear();
        for (int id = 0; id < 4; id++)
            players.Add(new PlayerData(id, id == 0 ? playerManpower : id == 1 ? enemyManpower : 900,
                PlayerData.StartingCivilians));
        player = players[0];
        divisions = new DivisionSystem(map, players);
        encirclement = new EncirclementSystem(map, players);
        encirclement.Refresh();
        // Quick Play lays out a scripted order of battle; other starts simply
        // field whatever divisions their opening military will pay for.
        if (duelDeployment) DuelMap.Deploy(map, scenario, divisions, players);
        else
            foreach (PlayerData faction in players)
                while (faction.Manpower >= Division.Cost && divisions.TryForm(faction, out _)) { }
        // Inspector floats are rounded to microseconds so the intended 0.12s
        // cadence does not become 0.119999997s when promoted to double.
        gameClock = new GameClock(System.Math.Round(fixedTickDuration, 6));
        gameSimulation = new GameSimulation(map, players, divisions, encirclement, player.Id);
        mapRenderer.enabled = true;
        mapRenderer.Draw(map);
        infrastructureRenderer.Draw(scenario, map, mapRenderer);
        orderRenderer.Invalidate();
        hud.SetInitialLabelVisible(true);
        hud.Bind(player, map, GetComponent<SpriteRenderer>(), scenario);
        hud.RaiseDivisionRequested -= FormDivisionFromHud;
        hud.RaiseDivisionRequested += FormDivisionFromHud;
        hud.PauseRequested -= TogglePauseFromHud;
        hud.PauseRequested += TogglePauseFromHud;
        hud.SpeedRequested -= SetSimulationSpeedFromHud;
        hud.SpeedRequested += SetSimulationSpeedFromHud;
        hud.StanceRequested -= SetSelectionStanceFromHud;
        hud.StanceRequested += SetSelectionStanceFromHud;
        for (int id = 1; id < players.Count; id++) hud.AddPlayer(players[id]);
        hud.SyncDivisions(divisions.Divisions, selection);
        mapCamera.backgroundColor = new Color32(16, 31, 42, 255);
        if (cameraController != null)
        {
            cameraController.enabled = true;
            cameraController.SetMapBounds(mapRenderer.WorldBounds);
        }
        fog = new FogOfWar(map, player.Id);
        fogRenderer.Build(map, mapRenderer);
        RefreshFog();
        hud.SetStatus(openingStatus);
        hud.Refresh();
        if (setupUI != null)
        {
            setupUI.Close();
            setupUI = null;
        }
    }

    private void Update()
    {
        if (map == null) return;
        bool hudDirty = false;
        ReadMouse();
        ReadKeyboard(ref hudDirty);

        // Unity delivers wall-clock time; gameplay only sees fixed ticks from
        // GameClock. The cap avoids a long frame stall without dropping time,
        // so any backlog can be consumed on following frames.
        gameClock.AddRealTime(Time.unscaledDeltaTime);
        int resolved = 0;
        while (resolved++ < MaxTicksPerFrame && gameClock.TryConsumeTick())
        {
            SimulationTickResult result = gameSimulation.Tick(gameClock.FixedTickDuration, hud.Commitment);
            PresentSimulationTick(result, ref hudDirty);
        }

        for (int i = selection.Count - 1; i >= 0; i--)
            if (selection[i].IsDestroyed) selection.RemoveAt(i);
        hud.SyncDivisions(divisions.Divisions, selection, fog);
        hud.SetAttackState(0, encirclement.RemainingTroops(player.Id),
            map.EncircledCellCount(player.Id));
        hud.SetSimulationState(gameClock.Paused, gameClock.SpeedMultiplier, gameClock.SimulationTime);
        hud.SetStanceHighlight(SelectedDivision?.Stance);
        orderRenderer.Draw(divisions.Divisions, player.Id, map, mapRenderer, fog, alliedFactionIds);
        UpdateHoveredCell(Mouse.current);
        if (hudDirty) hud.Refresh();
    }

    private void PresentSimulationTick(SimulationTickResult result, ref bool hudDirty)
    {
        foreach (int cell in result.CapturedCells)
            mapRenderer.RefreshCell(map, cell % map.Width, cell / map.Width);
        if (result.FogRefreshDue) RefreshFog();
        if (result.EconomyTicks > 0 || result.CapturedCells.Count > 0 ||
            result.FriendlyLosses > 0 || result.EnemyLosses > 0)
            hudDirty = true;
        if (result.FriendlyDestroyed > 0 || result.EnemyDestroyed > 0)
        {
            hud.SetStatus("Combat report: " + result.FriendlyLosses.ToString("N0") +
                " friendly casualties, " + result.EnemyLosses.ToString("N0") +
                " enemy casualties; " + result.FriendlyDestroyed + " friendly and " +
                result.EnemyDestroyed + " enemy formations destroyed.");
        }
    }

    public bool TogglePause()
    {
        bool paused = gameClock.TogglePaused();
        hud.SetSimulationState(paused, gameClock.SpeedMultiplier, gameClock.SimulationTime);
        hud.SetStatus(paused ? "Simulation paused. Orders remain available."
            : "Simulation resumed at " + gameClock.SpeedMultiplier + "x.");
        return paused;
    }

    public void SetSimulationPaused(bool paused)
    {
        gameClock.SetPaused(paused);
        hud.SetSimulationState(paused, gameClock.SpeedMultiplier, gameClock.SimulationTime);
    }

    public bool SetSimulationSpeed(int multiplier)
    {
        if (!gameClock.SetSpeed(multiplier)) return false;
        hud.SetSimulationState(gameClock.Paused, multiplier, gameClock.SimulationTime);
        hud.SetStatus("Simulation speed set to " + multiplier + "x.");
        return true;
    }

    public void RefreshFog()
    {
        if (fog == null) return;
        fog.Refresh(divisions.Divisions);
        fogRenderer.Refresh(fog, map);
        hud.SetFogDebug(fog.Revealed);
    }

    // Debug reveal, for watching what the enemy is actually doing.
    public bool ToggleFogDebug()
    {
        if (fog == null) return false;
        fog.Revealed = !fog.Revealed;
        RefreshFog();
        hud.SetStatus(fog.Revealed
            ? "DEBUG: fog lifted — enemy positions and movements are visible."
            : "Fog of war restored.");
        return fog.Revealed;
    }

    private void FormDivisionFromHud() => FormDivision();

    private void TogglePauseFromHud() => TogglePause();

    private void SetSimulationSpeedFromHud(int multiplier) => SetSimulationSpeed(multiplier);

    private void SetSelectionStanceFromHud(DivisionStance stance) => SetSelectionStance(stance);

    public bool FormDivision()
    {
        if (player.Manpower < Division.Cost)
        {
            hud.SetStatus("A division costs " + Division.Cost.ToString("N0") +
                " military population. Recruit more with R first.");
            return false;
        }
        if (!divisions.TryForm(player, out Division raised))
        {
            hud.SetStatus("No clear ground to muster a new division on.");
            return false;
        }
        selection.Clear();
        selection.Add(raised);
        hud.SetStatus("Raised the " + raised.Number + Ordinal(raised.Number) + " Infantry Division for " +
            Division.Cost.ToString("N0") + " military population.");
        hud.SyncDivisions(divisions.Divisions, selection);
        hud.Refresh();
        return true;
    }

    private static string Ordinal(int number)
    {
        if (number % 100 >= 11 && number % 100 <= 13) return "th";
        switch (number % 10)
        {
            case 1: return "st";
            case 2: return "nd";
            case 3: return "rd";
            default: return "th";
        }
    }

    private void UpdateHoveredCell(Mouse mouse)
    {
        if (mouse == null || hud.BlocksMapInput(mouse.position.ReadValue()) ||
            !mapRenderer.TryScreenToCell(map, mapCamera, mouse.position.ReadValue(), out int x, out int y))
        {
            if (hoverCell != -1) hud.SetCellInfo("");
            hoverCell = -1;
            return;
        }
        MapCell cell = map.GetCell(x, y);
        int id = y * map.Width + x;
        int cost = map.GetAdvanceCost(x, y, player.Id);
        bool isolated = map.IsEncircledBy(x, y, player.Id);
        if (id == hoverCell && hoverOwner == cell.OwnerId && hoverCost == cost && hoverEncircled == isolated) return;
        hoverCell = id; hoverOwner = cell.OwnerId; hoverCost = cost; hoverEncircled = isolated;
        string terrain = cell.Terrain == TerrainType.Land ? "Plains" : cell.Terrain.ToString();
        if (!map.IsWalkable(x, y)) hud.SetCellInfo(terrain + " | Land movement blocked");
        else if (cell.OwnerId == player.Id) hud.SetCellInfo(terrain + " | Your territory");
        else hud.SetCellInfo(terrain + " | " + (cell.OwnerId < 0 ? "Wilderness" : "Enemy") +
            " | " + (cost == 0 ? "FREE" : cost + " troops per action") + (isolated ? (cell.OwnerId < 0
                ? " | ENCIRCLED: FREE CAPTURE"
                : " | ENCIRCLED: " + Mathf.RoundToInt(TerrainRules.EncirclementCostMultiplier * 100f) + "% cost") : ""));
    }


}

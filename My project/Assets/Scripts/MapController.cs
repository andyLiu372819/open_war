using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(MapRenderer), typeof(SpriteRenderer))]
public class MapController : MonoBehaviour
{
    [SerializeField, Min(64)] private int width = 1024;
    [SerializeField, Min(64)] private int height = 512;
    [SerializeField] private int terrainSeed = 1847;
    [SerializeField, Range(1, 10)] private int advanceRadius = 4;
    [SerializeField, Min(0.01f)] private float advanceInterval = 0.035f;
    // How much longer than the best approach a border stretch's route may be and
    // still join the attack, and how many separate prongs may open at once.
    [SerializeField, Range(1f, 3f)] private float frontSpread = 1.5f;
    [SerializeField, Range(1, 12)] private int maxFronts = 6;
    [SerializeField] private MapHUD hud;
    [SerializeField, Min(0.1f)] private float encirclementCheckInterval = 0.75f;
    [SerializeField, Min(0.02f)] private float pocketAssaultInterval = 0.1f;
    // Number keys are a fast alternative to dragging the commitment slider.
    private static readonly Key[] CommitmentKeys =
    {
        Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
        Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0
    };
    private readonly List<PlayerData> players = new List<PlayerData>();
    private readonly List<LineRenderer> routeLines = new List<LineRenderer>();
    private PlayerData player;
    private MapData map;
    private MapRenderer mapRenderer;
    private Camera mapCamera;
    private ExpansionOrder order;
    private float recruitmentTimer;
    private float advanceTimer;
    private LineRenderer targetMarker;
    private Material orderMaterial;
    private EncirclementSystem encirclement;
    private float encirclementTimer;
    private float pocketTimer;
    private int hoverCell = -1, hoverOwner = -2, hoverCost = -1;
    private bool hoverEncircled;

    // Read-only access also allows simulation/scene verification without reflection.
    public MapData Map => map;
    public PlayerData LocalPlayer => player;
    public ExpansionOrder CurrentOrder => order;
    public EncirclementSystem Encirclement => encirclement;

    // The smallest useful commitment is one action on the cheapest terrain.
    public static int MinimumCommitment => TerrainRules.ExpansionCost(TerrainType.Land);

    // Troops the next order would take from the reserve at the current setting.
    public int PlannedCommitment => player == null ? 0 :
        Mathf.Clamp(Mathf.RoundToInt((player.Manpower + (order?.RemainingTroops ?? 0)) * hud.Commitment),
            0, player.Manpower + (order?.RemainingTroops ?? 0));

    private void Start()
    {
        map = new MapData(width, height, terrainSeed);
        StartingTerritories.Create(map, 4);
        for (int id = 0; id < 4; id++) players.Add(new PlayerData(id, id == 0 ? 1600 : 900));
        player = players[0];
        encirclement = new EncirclementSystem(map, players);
        encirclement.Refresh();
        mapRenderer = GetComponent<MapRenderer>();
        mapCamera = Camera.main;
        mapRenderer.Draw(map);
        hud.Bind(player, map, GetComponent<SpriteRenderer>());
        for (int id = 1; id < players.Count; id++) hud.AddPlayer(players[id]);
        mapCamera.backgroundColor = new Color32(16, 31, 42, 255);
        CameraController cameraController = mapCamera.GetComponent<CameraController>();
        if (cameraController != null) cameraController.SetMapBounds(mapRenderer.WorldBounds);
        CreateOrderGraphics();
    }

    private void Update()
    {
        if (map == null) return;
        recruitmentTimer += Time.deltaTime;
        bool hudDirty = false;
        while (recruitmentTimer >= 1f)
        {
            recruitmentTimer -= 1f;
            foreach (PlayerData faction in players)
            {
                int territory = map.CountTerritory(faction.Id);
                faction.TickEconomy(territory);
            }
            hudDirty = true;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.rightButton.wasPressedThisFrame) CancelOrder();
            if (mouse.leftButton.wasPressedThisFrame &&
                !hud.BlocksMapInput(mouse.position.ReadValue()) &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                if (mapRenderer.TryScreenToCell(map, mapCamera, mouse.position.ReadValue(), out int x, out int y))
                    IssueOrder(x, y);
            }
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            for (int i = 0; i < CommitmentKeys.Length; i++)
            {
                if (!keyboard[CommitmentKeys[i]].wasPressedThisFrame) continue;
                hud.SetCommitment((i + 1) * 0.1f);
                break;
            }
        }

        if (order != null)
        {
            advanceTimer += Time.deltaTime;
            int steps = 0;
            while (order != null && advanceTimer >= advanceInterval && steps++ < 8)
            {
                advanceTimer -= advanceInterval;
                AdvanceResult result = order.Step(out int x, out int y);
                if (result == AdvanceResult.OutOfTroops || result == AdvanceResult.Complete)
                {
                    FinishOrder(result);
                    hudDirty = true;
                    break;
                }
                if (result == AdvanceResult.Captured) mapRenderer.RefreshCell(map, x, y);
                int defenderId = order.LastDefenderId;
                if (defenderId >= 0 && defenderId < players.Count)
                {
                    ApplyDefenderLoss(defenderId);
                }
                if (result == AdvanceResult.Fighting)
                    hud.SetStatus("Attacking " + map.GetCell(x, y).Terrain + " — defense remaining: " +
                        map.GetCell(x, y).Defense + "   ·   " + Troops(order.RemainingTroops) + " still in the field.");
                else
                    hud.SetStatus("Pushing in the selected direction on " +
                        Fronts(order.FrontCount) + "   ·   " + Troops(order.RemainingTroops) + " of " +
                        Troops(order.CommittedTroops) + " still in the field.");
                hudDirty = true;
                if (order.IsComplete) FinishOrder(AdvanceResult.Complete);
            }
            // Do not bank a large catch-up burst during a slow frame.
            advanceTimer = Mathf.Min(advanceTimer, advanceInterval);
        }
        encirclementTimer += Time.deltaTime;
        if (encirclementTimer >= encirclementCheckInterval)
        {
            encirclementTimer = 0f;
            encirclement.Refresh();
            hudDirty = true;
        }
        pocketTimer += Time.deltaTime;
        int pocketSteps = 0;
        while (pocketTimer >= pocketAssaultInterval && pocketSteps++ < 8)
        {
            pocketTimer -= pocketAssaultInterval;
            foreach (PlayerData faction in players)
            {
                float share = faction.Id == player.Id ? hud.Commitment : 0.5f;
                AdvanceResult result = encirclement.Step(faction.Id, share, out int x, out int y);
                if (result == AdvanceResult.Captured) mapRenderer.RefreshCell(map, x, y);
                if (result == AdvanceResult.Captured || result == AdvanceResult.Fighting)
                {
                    ApplyDefenderLoss(encirclement.LastDefenderId);
                    hudDirty = true;
                }
                if (encirclement.LastRefund > 0) hudDirty = true;
            }
        }
        pocketTimer = Mathf.Min(pocketTimer, pocketAssaultInterval);
        hud.SetAttackState(order?.RemainingTroops ?? 0, encirclement.RemainingTroops(player.Id),
            map.EncircledCellCount(player.Id));
        UpdateHoveredCell(mouse);
        if (hudDirty) hud.Refresh();
    }

    private void ApplyDefenderLoss(int defenderId)
    {
        if (defenderId < 0 || defenderId >= players.Count) return;
        PlayerData defender = players[defenderId];
        int loss = Mathf.Min(12, defender.Manpower);
        if (loss > 0) defender.TrySpendManpower(loss);
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
            " | " + cost + " troops per action" + (isolated ? (cell.OwnerId < 0
                ? " | ENCIRCLED: FREE CAPTURE"
                : " | ENCIRCLED: " + Mathf.RoundToInt(TerrainRules.EncirclementCostMultiplier * 100f) + "% cost") : ""));
    }

    public bool IssueOrder(int x, int y)
    {
        int commitment = PlannedCommitment;
        if (commitment < MinimumCommitment)
        {
            hud.SetStatus("Too few troops committed. Raise the commitment or wait for recruits.");
            return false;
        }
        // Build the order before touching the reserve, so a rejected destination
        // leaves both the reserve and any running advance untouched.
        if (!ExpansionOrder.TryCreate(map, player.Id, x, y, advanceRadius, commitment,
            out ExpansionOrder next, frontSpread, maxFronts))
        {
            hud.SetStatus("Choose reachable wilderness or a rival. Water is blocked; rivers have marked crossings.");
            return false;
        }
        if (order != null) player.AddManpower(order.Recall());
        player.TrySpendManpower(commitment);
        order = next;
        advanceTimer = 0f;
        ShowOrder();
        hud.SetAttackState(order.RemainingTroops, encirclement.RemainingTroops(player.Id), map.EncircledCellCount(player.Id));
        hud.SetStatus("Pushing past (" + x + ", " + y + ") with " + Troops(commitment) +
            " on " + Fronts(order.FrontCount) + " — right-click to recall.");
        hud.Refresh();
        return true;
    }

    public void CancelOrder()
    {
        if (order != null)
        {
            int returned = order.Recall();
            player.AddManpower(returned);
            hud.SetStatus("Advance recalled — " + Troops(returned) + " returned to the reserve.");
            hud.Refresh();
        }
        else hud.SetStatus("Click a destination to advance.");
        order = null;
        hud.SetAttackState(0, encirclement.RemainingTroops(player.Id), map.EncircledCellCount(player.Id));
        advanceTimer = 0f;
        HideOrderGraphics();
    }

    private void FinishOrder(AdvanceResult reason)
    {
        int returned = order.Recall();
        player.AddManpower(returned);
        order = null;
        advanceTimer = 0f;
        HideOrderGraphics();
        if (reason == AdvanceResult.OutOfTroops)
            hud.SetStatus("The force cannot fund another action — " + Troops(returned) + " returned. Commit again to press on.");
        else
            hud.SetStatus("No eligible land remains ahead — " + Troops(returned) + " returned to the reserve.");
    }

    private static string Troops(int amount) => amount.ToString("N0") + " troops";

    private static string Fronts(int count) => count + (count == 1 ? " front" : " fronts");

    private void CreateOrderGraphics()
    {
        orderMaterial = new Material(Shader.Find("Sprites/Default"));
        targetMarker = CreateLine("Destination", 0.035f, new Color(1f, 0.87f, 0.36f));
        targetMarker.loop = true;
    }

    private LineRenderer CreateLine(string objectName, float lineWidth, Color color)
    {
        var child = new GameObject(objectName);
        child.transform.SetParent(transform, false);
        var line = child.AddComponent<LineRenderer>();
        line.sharedMaterial = orderMaterial;
        line.useWorldSpace = false;
        line.startWidth = line.endWidth = lineWidth;
        line.startColor = line.endColor = color;
        line.sortingOrder = 5;
        line.enabled = false;
        return line;
    }

    // One line per prong, reused across orders so a wide attack does not leak
    // GameObjects every time the destination changes.
    private LineRenderer RouteLine(int index)
    {
        while (routeLines.Count <= index)
            routeLines.Add(CreateLine("Advance route " + routeLines.Count,
                0.017f, new Color(1f, 0.9f, 0.55f, 0.55f)));
        return routeLines[index];
    }

    private void HideOrderGraphics()
    {
        if (targetMarker != null) targetMarker.enabled = false;
        foreach (LineRenderer line in routeLines) line.enabled = false;
    }

    private void ShowOrder()
    {
        Vector3 center = mapRenderer.CellToLocal(map, order.TargetX + 0.5f, order.TargetY + 0.5f);
        float radius = 0.14f;
        targetMarker.positionCount = 4;
        targetMarker.SetPositions(new[] {
            center + new Vector3(-radius, -radius, -0.01f),
            center + new Vector3(-radius, radius, -0.01f),
            center + new Vector3(radius, radius, -0.01f),
            center + new Vector3(radius, -radius, -0.01f)
        });
        for (int i = 0; i < order.Routes.Count; i++)
        {
            IReadOnlyList<int> route = order.Routes[i];
            LineRenderer line = RouteLine(i);
            line.positionCount = route.Count;
            for (int step = 0; step < route.Count; step++)
            {
                int cell = route[step];
                Vector3 local = mapRenderer.CellToLocal(map, cell % map.Width + 0.5f, cell / map.Width + 0.5f);
                local.z = -0.01f;
                line.SetPosition(step, local);
            }
            line.enabled = true;
        }
        for (int i = order.Routes.Count; i < routeLines.Count; i++) routeLines[i].enabled = false;
        targetMarker.enabled = true;
    }

    private void OnDestroy()
    {
        if (orderMaterial != null) Destroy(orderMaterial);
    }
}

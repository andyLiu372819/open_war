using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[InitializeOnLoad]
public static class SceneSmoke
{
    static int stage;
    static double next;
    static float oldZoom;
    static Vector3 oldPosition;
    static Vector2 clickPosition;
    static int targetX, targetY;
    static int startingOwned;
    static long tickAtSpeedTest;
    static long tickAtPause;
    static int pocketX, pocketY;
    static bool runtimeError;
    static int setupStage;
    static SceneSmoke()
    {
        if (SessionState.GetBool("OpenWarSmoke", false))
        {
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
        }
    }
    public static void Begin()
    {
        SessionState.SetBool("OpenWarSmoke", true);
        EditorSceneManager.OpenScene("Assets/Scenes/game.unity");
        EditorApplication.EnterPlaymode();
    }
    static void OnLog(string message, string stack, LogType type)
    {
        // Unity 6.5 can log a SearchDatabase indexing exception on a fresh
        // batch project. Track game errors separately from that editor issue.
        if ((type == LogType.Exception || type == LogType.Error || type == LogType.Assert) &&
            !stack.Contains("UnityEditor.Search"))
            runtimeError = true;
    }
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (EditorApplication.timeSinceStartup < next) return;
        try
        {
            var controller = UnityEngine.Object.FindFirstObjectByType<MapController>();
            if (controller == null) return;
            if (controller.Map == null)
            {
                if (setupStage == 0)
                {
                    Check(GameObject.Find("Start screen") != null, "Start screen was not created");
                    Check(GameObject.Find("Open map designer") != null, "Map designer entry button is missing");
                    foreach (EasternFrontTemplate template in ScenarioTemplates.All)
                    {
                        ScenarioMap scenario = ScenarioTemplates.Create(template);
                        Check(scenario.Width == 160 && scenario.Height == 96, "Scenario template dimensions are incorrect");
                        if (template == EasternFrontTemplate.Sandbox)
                        {
                            Check(!scenario.IsPlayable(out _), "Blank sandbox should require painted sides");
                            Check(scenario.CountOwned(0) == 0 && scenario.CountOwned(1) == 0,
                                "Blank sandbox unexpectedly has starting territory");
                            Check(scenario.Settlements.Count == 0, "Blank sandbox unexpectedly has settlements");
                            Check(scenario.InfrastructureRoutes.Count == 0 && scenario.InfrastructureSites.Count == 0,
                                "Blank sandbox unexpectedly has infrastructure");
                        }
                        else
                        {
                            Check(scenario.IsPlayable(out _), "Scenario template is not playable: " + template);
                            Check(scenario.Settlements.Count >= 20,
                                "Historical template is missing settlements: " + template);
                            Check(scenario.InfrastructureRoutes.Count >= 8 && scenario.InfrastructureSites.Count >= 10,
                                "Historical template is missing infrastructure: " + template);
                        }
                    }
                    Capture("start-screen.png", Camera.main);
                    GameObject.Find("Open map designer").GetComponent<Button>().onClick.Invoke();
                    setupStage++;
                    return;
                }
                Check(GameObject.Find("Map designer") != null, "Map designer did not open");
                Check(GameObject.Find("Editable map preview") != null, "Editable map preview is missing");
                Check(GameObject.Find("Settlement Label Moscow") != null,
                    "Geographic settlement labels are missing from the designer preview");
                Check(GameObject.Find("Infrastructure Site Vnukovo Airfield") != null &&
                    GameObject.Find("Road Moscow-Smolensk Highway") != null &&
                    GameObject.Find("Rail track Moscow-Smolensk Railway") != null,
                    "Typhoon infrastructure is missing from the designer preview");
                foreach (EasternFrontTemplate template in ScenarioTemplates.All)
                    Check(GameObject.Find("Template " + template) != null, "Template button is missing: " + template);
                foreach (string brush in new[] { "Plains", "Forest", "Hills", "Mountains", "Water", "River", "Ford",
                    "Desert", "RedSide", "BlueSide", "Neutral" })
                    Check(GameObject.Find("Brush " + brush) != null, "Designer brush is missing: " + brush);
                Capture("map-designer.png", Camera.main);
                GameObject.Find("Template Stalingrad1942").GetComponent<Button>().onClick.Invoke();
                Check(GameObject.Find("Settlement Label Stalingrad") != null,
                    "Stalingrad labels did not load");
                Check(GameObject.Find("Infrastructure Site Pitomnik Airfield") != null,
                    "Stalingrad infrastructure did not load");
                Capture("map-designer-stalingrad.png", Camera.main);
                GameObject.Find("Template Kursk1943").GetComponent<Button>().onClick.Invoke();
                Check(GameObject.Find("Settlement Label Kursk") != null, "Kursk labels did not load");
                Check(GameObject.Find("Infrastructure Site Kursk Airfield") != null,
                    "Kursk infrastructure did not load");
                Capture("map-designer-kursk.png", Camera.main);
                GameObject.Find("Template OperationBagration1944").GetComponent<Button>().onClick.Invoke();
                Check(GameObject.Find("Settlement Label Minsk") != null, "Bagration labels did not load");
                Check(GameObject.Find("Infrastructure Site Borisov Berezina Bridge") != null,
                    "Bagration infrastructure did not load");
                Capture("map-designer-bagration.png", Camera.main);
                GameObject.Find("Template Sandbox").GetComponent<Button>().onClick.Invoke();
                Check(GameObject.Find("Settlement Label Minsk") == null,
                    "Sandbox retained a historical settlement label");
                Check(GameObject.Find("Infrastructure legend") == null &&
                    GameObject.Find("Infrastructure Site Borisov Berezina Bridge") == null,
                    "Sandbox retained historical infrastructure");
                Capture("map-designer-sandbox.png", Camera.main);
                controller.StartQuickGame();
                return;
            }
            var camera = Camera.main;
            var renderer = controller.GetComponent<MapRenderer>();
            var map = controller.Map;
            Mouse mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
            switch (stage)
            {
                case 0:
                    InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    // Quick Play is now a square mirrored duel island.
                    Check(map.Width == DuelMap.DefaultSize && map.Height == DuelMap.DefaultSize,
                        "Scene dimensions incorrect");
                    Check(map.Width * map.Height > 100000, "The duel map is under 100,000 cells");
                    Check(renderer.ChunkCount == 9, "Expected 9 map rendering chunks");
                    Check(Mathf.Approximately(renderer.WorldBounds.size.x, 40f) &&
                        Mathf.Approximately(renderer.WorldBounds.size.y, 40f), "Incorrect duel map bounds");
                    int mirrored = 0, edgeLand = 0;
                    for (int mx = 0; mx < map.Width; mx++)
                    for (int my = 0; my < map.Height; my++)
                    {
                        if (map.GetCell(mx, my).Terrain != map.GetCell(map.Width - 1 - mx, my).Terrain) mirrored++;
                        if ((mx == 0 || my == 0 || mx == map.Width - 1 || my == map.Height - 1) &&
                            map.GetCell(mx, my).Terrain != TerrainType.Water) edgeLand++;
                    }
                    Check(mirrored == 0, "The scene map halves are not mirror images");
                    Check(edgeLand == 0, "The scene map is not ringed by ocean");
                    Check(map.CountTerritory(0) == map.CountTerritory(1) && map.CountTerritory(0) > 0,
                        "The two sides did not start with equal ground");
                    Check(map.CountTerritory(0) > 20000, "A nation does not hold its whole half");
                    Check(controller.Divisions.CountFor(0) == controller.Divisions.CountFor(1),
                        "The two sides did not start with equal armies");
                    Check(map.TransportNetwork != null && map.TransportNetwork.Routes.Count >= 6,
                        "Quick Play infrastructure did not become a transport network");
                    var renderedRoad = GameObject.Find("Road West Southern Road");
                    Check(renderedRoad != null &&
                        renderedRoad.GetComponent<LineRenderer>().positionCount > 20,
                        "The road is still a straight waypoint-to-waypoint decoration");
                    renderer.RefreshCell(map, 127, 127);
                    renderer.SendMessage("LateUpdate");
                    Check(renderer.LastUploadedChunkCount == 3, "Chunk seam update did not update exactly three affected chunks");
                    renderer.SendMessage("LateUpdate");
                    Check(renderer.LastUploadedChunkCount == 0, "Unchanged textures were uploaded again");
                    CheckPartialChunks(camera);
                    map.TryGetTerritoryCenter(0, out float cx, out float cy);
                    // Quick Play raises four divisions at 10,000 military apiece.
                    Check(controller.Divisions.CountFor(0) == DuelMap.StartingDivisions &&
                        controller.Divisions.CountFor(1) == DuelMap.StartingDivisions,
                        "The scripted order of battle did not field thirty divisions a side");
                    int onFrontier = 0, holdingLine = 0;
                    foreach (Division formation in controller.Divisions.Divisions)
                    {
                        if (formation.OwnerId != 0) continue;
                        Check(formation.Stance == DivisionStance.Defend,
                            "A starting division is not on the defensive");
                        if (formation.HeldLine.Count > 0) holdingLine++;
                        if (Mathf.Abs(formation.X - map.Width / 2) < map.Width / 8) onFrontier++;
                    }
                    Check(holdingLine == DuelMap.BorderDivisions,
                        "The frontier divisions were not given a line to hold");
                    Check(onFrontier >= DuelMap.BorderDivisions, "Too few divisions posted on the frontier");
                    // Fog of war: enemy formations are not drawn until observed.
                    Check(controller.Fog != null && !controller.Fog.Revealed, "Fog should start closed");
                    int hiddenEnemies = 0;
                    foreach (Division formation in controller.Divisions.Divisions)
                        if (formation.OwnerId == 1 && !controller.Fog.CanSee(formation)) hiddenEnemies++;
                    Check(hiddenEnemies > 0, "Every enemy division is visible through the fog");
                    Check(GameObject.Find("Fog overlay") != null, "The fog mask is not drawn");
                    Check(!GameObject.Find("Fog debug banner").GetComponent<TMP_Text>().enabled,
                        "The debug banner shows while the fog is closed");
                    Check(controller.ToggleFogDebug() && controller.Fog.Revealed, "Debug reveal did not engage");
                    Check(GameObject.Find("Fog debug banner").GetComponent<TMP_Text>().enabled,
                        "The debug banner is not shown when the fog is lifted");
                    foreach (Division formation in controller.Divisions.Divisions)
                        Check(controller.Fog.CanSee(formation), "Debug reveal left a division hidden");
                    Check(!controller.ToggleFogDebug() && !controller.Fog.Revealed,
                        "Debug reveal did not switch back off");
                    Check(controller.Divisions.Divisions[0].Number == 1 &&
                        controller.Divisions.Divisions[0].Strength == Division.Cost,
                        "First division was numbered or equipped wrongly");
                    Check(GameObject.Find("Division counter 0-1") != null &&
                        GameObject.Find("Counter infantry symbol") != null,
                        "Division counter or its NATO infantry symbol is missing");
                    startingOwned = map.CountTerritory(0);
                    Division smokeDivision = controller.Divisions.Divisions[0];
                    targetX = smokeDivision.X; targetY = smokeDivision.Y;
                    bool found = true;
                    Check(found, "No smoke test target");
                    var world = renderer.transform.TransformPoint(renderer.CellToLocal(map, targetX + 0.5f, targetY + 0.5f));
                    clickPosition = camera.WorldToScreenPoint(world);
                    Check(renderer.TryScreenToCell(map, camera, clickPosition, out int gx, out int gy) && gx == targetX && gy == targetY,
                        "Screen/grid conversion failed after texture detail change");
                    controller.LocalPlayer.AddManpower(100000);
                    Capture("economy-overview.png", camera);
                    // Pressing on a division selects it; pressing open ground pans.
                    ApplyInput(mouse, new MouseState { position = clickPosition }.WithButton(MouseButton.Left));
                    next = EditorApplication.timeSinceStartup + 0.5;
                    break;
                case 1:
                    Check(controller.SelectedDivision != null, "Left click did not select a division");
                    oldZoom = camera.orthographicSize;
                    ApplyInput(mouse, new MouseState { position = clickPosition, scroll = new Vector2(0, 1) });
                    next = EditorApplication.timeSinceStartup + 0.5;
                    break;
                case 2:
                    Check(camera.orthographicSize < oldZoom * 0.85f, "Wheel zoom is not fast enough");
                    oldPosition = camera.transform.position;
                    ApplyInput(mouse, new MouseState { position = clickPosition + new Vector2(40, 20), delta = new Vector2(40, 20) }.WithButton(MouseButton.Middle));
                    next = EditorApplication.timeSinceStartup + 0.5;
                    break;
                case 3:
                    Check(Vector3.Distance(oldPosition, camera.transform.position) > 0.1f, "Middle drag did not pan");
                    ApplyInput(mouse, new MouseState { position = clickPosition });
                    camera.GetComponent<CameraController>().FrameMap();
                    Check(!controller.Clock.Paused && controller.Clock.SpeedMultiplier == 1,
                        "A new game did not start running continuously at 1x");
                    tickAtSpeedTest = controller.Clock.TickCount;
                    Check(controller.SetSimulationSpeed(4), "Could not select 4x simulation speed");
                    next = EditorApplication.timeSinceStartup + 1;
                    break;
                case 4:
                    Check(controller.Clock.TickCount > tickAtSpeedTest,
                        "Continuous simulation did not advance without an End Turn command");
                    controller.SetSimulationPaused(true);
                    tickAtPause = controller.Clock.TickCount;
                    // Nothing has been ordered anywhere, and clicking no longer
                    // launches a border attack, so the front must have stayed put.
                    // Idle divisions still hold their own two-cell frontage.
                    Check(map.CountTerritory(0) > 0, "Player lost all territory");
                    Check(map.CountTerritory(0) <= startingOwned + 60,
                        "Territory grew without a division being sent: a border attack is still running");
                    Check(GameObject.Find("Fog overlay") != null, "The fog mask went missing");
                    foreach (var text in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
                    {
                        if (text.name == "Map controls" || text.name == "Order status" || text.name == "Commitment readout" ||
                            text.name == "Cell cost readout" || text.name == "Pocket assault readout" ||
                            text.name == "Recruit readout" || text.name == "Raise division readout" ||
                            text.name == "Counter number" || text.name == "Simulation clock" ||
                            text.name == "Simulation pause readout" || text.name.StartsWith("Speed ") ||
                            text.name == "Fog debug banner" ||
                            text.name.StartsWith("Stance label ") ||
                            // Quick Play now carries named towns and facilities.
                            text.name.StartsWith("Map settlement label ") ||
                            text.name.StartsWith("Map infrastructure label ") ||
                            text.name.StartsWith("Infrastructure ") ||
                            text.name.StartsWith("Resource ")) continue;
                        Check(!text.text.Contains("Manpower"), "Territory label contains Manpower");
                        // Only the military population is painted on the ground.
                        Check(!text.text.Contains("CIVILIAN") && !text.text.Contains("Civilian"),
                            "Territory label shows civilians instead of military only");
                        foreach (char c in text.text) Check(char.IsDigit(c) || c == ',', "Territory label is not numeric");
                    }
                    Check(controller.LocalPlayer.Gold > 0 && controller.LocalPlayer.Industry > 0, "Economy tick did not produce resources");
                    Check(controller.LocalPlayer.Gold == controller.LocalPlayer.Industry * 2, "Resource production ratio wrong");
                    foreach (string name in new[] { "Military", "Civilians", "Gold", "Industry", "Land" })
                    {
                        var resourceText = GameObject.Find("Resource " + name).GetComponent<TMP_Text>();
                        Check(resourceText != null && resourceText.text.Length > 0, "Missing resource readout: " + name);
                        Check(!resourceText.raycastTarget, "Resource readout blocks map input");
                    }
                    Check(GameObject.Find("Resource Gold").GetComponent<TMP_Text>().text.Contains("/sim sec"), "Resource income rate missing");
                    Check(GameObject.Find("Resource Civilians").GetComponent<TMP_Text>().text.Contains("/sim sec"),
                        "Civilian growth rate is not shown in the HUD");
                    Check(GameObject.Find("Resource Military").GetComponent<TMP_Text>().text.Contains("RECRUITED"),
                        "Military readout does not say it is recruited rather than grown");
                    Check(controller.LocalPlayer.Civilians > 0, "Civilian population did not grow over time");
                    // Recruiting must move all three ledgers together.
                    int soldiersBefore = controller.LocalPlayer.Manpower;
                    decimal coinsBefore = controller.LocalPlayer.Gold;
                    decimal civiliansBefore = controller.LocalPlayer.Civilians;
                    int expected = controller.LocalPlayer.AffordableRecruits();
                    Check(expected > 0, "Nothing was affordable to recruit");
                    GameObject.Find("Recruit button").GetComponent<Button>().onClick.Invoke();
                    Check(controller.LocalPlayer.Manpower == soldiersBefore + expected,
                        "Recruit button did not add the affordable soldiers");
                    Check(controller.LocalPlayer.Gold == coinsBefore - expected &&
                        controller.LocalPlayer.Civilians == civiliansBefore - expected,
                        "Recruitment did not charge one coin and one civilian per soldier");
                    Check(controller.LocalPlayer.AffordableRecruits() == 0,
                        "Recruiting everything affordable left a further batch available");
                    Check(GameObject.Find("Gold coin icon") != null, "Gold coin icon missing");
                    Check(GameObject.Find("Industry wrench icon") != null, "Industry wrench icon missing");
                    Check(GameObject.Find("Destination") == null && GameObject.Find("Advance route 0") == null,
                        "Order route or destination marker should not be rendered");
                    Capture("economy-advance.png", camera);
                    Check(!controller.OrderSelected(-1, -1), "Unreachable destination accepted");
                    Time.timeScale = 1f;
                    next = EditorApplication.timeSinceStartup + 0.5;
                    break;
                case 5:
                    var hud = UnityEngine.Object.FindFirstObjectByType<MapHUD>();
                    Check(controller.Clock.Paused && controller.Clock.TickCount == tickAtPause,
                        "Pause allowed authoritative simulation time to advance");
                    var panel = (RectTransform)GameObject.Find("Troop commitment").transform;
                    Vector2 panelPoint = RectTransformUtility.WorldToScreenPoint(null, panel.TransformPoint(panel.rect.center));
                    Check(hud.BlocksMapInput(panelPoint), "Commitment panel does not block map input");
                    // Drag selection: a box over the whole map picks up every
                    // division, and a box over empty ground picks up none.
                    Check(controller.SelectCellBox(0, 0, map.Width - 1, map.Height - 1) ==
                        controller.Divisions.CountFor(0), "Full-map box did not select every division");
                    Check(controller.Selection.Count > 1, "Box selection is still single-unit");
                    Check(GameObject.Find("Selection box") == null,
                        "Selection box lingers when no drag is in progress");
                    hud.ShowSelectionBox(new Vector2(100f, 100f), new Vector2(400f, 320f));
                    var boxObject = GameObject.Find("Selection box");
                    Check(boxObject != null && boxObject.activeInHierarchy, "Drag did not draw a selection box");
                    Check(GameObject.Find("Selection edge 3") != null, "Selection box has no edges");
                    hud.HideSelectionBox();
                    Check(!boxObject.activeInHierarchy, "Selection box stayed up after the drag ended");
                    Division grouped = controller.SelectedDivision;
                    int groupTargetX = Mathf.Clamp(grouped.X + 12, 1, map.Width - 2);
                    Check(controller.OrderSelected(groupTargetX, grouped.Y),
                        "Group order was refused");
                    var groupTargets = new System.Collections.Generic.HashSet<int>();
                    foreach (Division member in controller.Selection)
                        Check(groupTargets.Add(member.TargetY * map.Width + member.TargetX),
                            "Two divisions in the group were sent to the same cell");
                    Check(controller.SelectCellBox(0, 0, 0, 0) == 0, "Empty box selected something");
                    Check(controller.Selection.Count == 0, "Empty box left a selection behind");

                    // A division told to hold a stretch of its own border gets
                    // a defensive deployment route, and a wider stretch spreads it thinner.
                    map.TryGetTerritoryCenter(0, out float holdX, out float holdY);
                    Check(controller.TrySelectAt((int)holdX, (int)holdY) ||
                        controller.SelectCellBox(0, 0, map.Width - 1, map.Height - 1) > 0,
                        "Could not select a division to hold the line");
                    while (controller.Selection.Count > 1)
                        controller.SelectCellBox(controller.SelectedDivision.X, controller.SelectedDivision.Y,
                            controller.SelectedDivision.X, controller.SelectedDivision.Y);
                    Division holder = controller.SelectedDivision;
                    Check(holder != null, "No division available to hold a line");
                    int narrowHeld = controller.AssignHoldCells(holder.X, holder.Y - 2, holder.X, holder.Y + 2);
                    Check(narrowHeld > 0, "A narrow hold line was refused");
                    Check(holder.Stance == DivisionStance.Defend, "Holding a line did not dig the division in");
                    int narrowDefence = holder.LineDefense;
                    int wideHeld = controller.AssignHoldCells(holder.X, holder.Y - 12, holder.X, holder.Y + 12);
                    Check(wideHeld > narrowHeld, "A wider drag did not take in more ground");
                    Check(holder.LineDefense < narrowDefence,
                        "Stretching a division over a wider line did not weaken it");
                    // Arrows and defence lines are drawn on the map itself.
                    var overlay = controller.GetComponent<MapOrderRenderer>();
                    overlay.Draw(controller.Divisions.Divisions, 0, map, renderer);
                    var trace = GameObject.Find("Defence line " + holder.Number);
                    Check(trace != null, "The held line is not drawn on the map");
                    // The forward-line trace is a square wave, so it carries far
                    // more points than the handful of cells it runs along.
                    var traceLine = trace.GetComponent<LineRenderer>();
                    Check(traceLine.positionCount >= holder.HeldLine.Count,
                        "The defence trace does not cover the held line");
                    Color friendlyBlue = MapOrderRenderer.DefenceColour(true, false);
                    Color alliedGreen = MapOrderRenderer.DefenceColour(false, true);
                    Color enemyRed = MapOrderRenderer.DefenceColour(false, false);
                    Check(Vector4.Distance(traceLine.startColor, friendlyBlue) < .02f,
                        "Friendly defensive lines are not blue");
                    Check(alliedGreen.g > alliedGreen.r && alliedGreen.g > alliedGreen.b,
                        "Allied defensive-line colour is not green");
                    Check(enemyRed.r > enemyRed.g * 2f && enemyRed.r > enemyRed.b * 2f,
                        "Enemy defensive-line colour is not red");
                    var enemyTrace = GameObject.Find("Enemy defence line 1");
                    Check(enemyTrace != null &&
                        Vector4.Distance(enemyTrace.GetComponent<LineRenderer>().startColor, enemyRed) < .02f,
                        "An observed enemy defensive line was not rendered in red");
                    Check(traceLine.numCornerVertices == 0,
                        "The defensive trace still rounds its rigid corners");
                    Check(traceLine.sortingOrder > 8,
                        "Defensive lines do not render above movement arrows");
                    bool squareCorner = false;
                    for (int c = 2; c < traceLine.positionCount; c++)
                    {
                        Vector3 before = traceLine.GetPosition(c - 1) - traceLine.GetPosition(c - 2);
                        Vector3 after = traceLine.GetPosition(c) - traceLine.GetPosition(c - 1);
                        if (before.sqrMagnitude < 1e-8f || after.sqrMagnitude < 1e-8f) continue;
                        if (Mathf.Abs(Vector3.Dot(before.normalized, after.normalized)) < .15f)
                        { squareCorner = true; break; }
                    }
                    Check(squareCorner, "The defensive trace has no square crenellated corner");
                    var spineReport = new System.Text.StringBuilder();
                    for (int c = 0; c < Mathf.Min(4, holder.HeldLine.Count); c++)
                        spineReport.Append(holder.HeldLine[c] % map.Width).Append(',')
                            .Append(holder.HeldLine[c] / map.Width).Append(' ');
                    var pointReport = new System.Text.StringBuilder();
                    for (int c = 0; c < Mathf.Min(4, traceLine.positionCount); c++)
                        pointReport.Append(traceLine.GetPosition(c).ToString("F3")).Append(' ');
                    Debug.Log("SYMBOLOGY: trace " + traceLine.positionCount + " points for " +
                        holder.HeldLine.Count + " cells, defence " + holder.LineDefense +
                        " | cells " + spineReport + "| points " + pointReport);
                    // Capture the defence trace before adding attack arrows, so
                    // its relationship colour and rigid teeth can be inspected alone.
                    int middleHeld = holder.HeldLine[holder.HeldLine.Count / 2];
                    camera.transform.position = renderer.transform.TransformPoint(
                        renderer.CellToLocal(map, middleHeld % map.Width + 0.5f,
                            middleHeld / map.Width + 0.5f)) + new Vector3(0, 0, -10);
                    camera.orthographicSize = 2.1f;
                    Capture("defence-lines.png", camera);
                    camera.GetComponent<CameraController>().FrameMap();
                    // And an ordered division draws a solid-headed axis of advance.
                    Check(controller.OrderSelected(Mathf.Clamp(holder.X - 20, 2, map.Width - 3), holder.Y),
                        "Could not order the holder so an arrow would be drawn");
                    overlay.Draw(controller.Divisions.Divisions, 0, map, renderer);
                    var head = GameObject.Find("Attack head " + holder.Number);
                    Check(head != null, "The axis of advance has no arrowhead");
                    var headLine = head.GetComponent<LineRenderer>();
                    Check(headLine.widthCurve.Evaluate(1f) < headLine.widthCurve.Evaluate(0f),
                        "The arrowhead does not taper to a point");
                    var shaft = GameObject.Find("Attack shaft " + holder.Number);
                    Check(shaft != null,
                        "The axis of advance has no shaft");
                    var shaftLine = shaft.GetComponent<LineRenderer>();
                    Check(shaftLine.positionCount > 20 && shaftLine.numCornerVertices >= 6,
                        "The movement arrow was not smoothed into a continuous axis");
                    // Close-up of the front so the symbology can be inspected.
                    camera.transform.position = renderer.transform.TransformPoint(
                        renderer.CellToLocal(map, holder.X + 0.5f, holder.Y + 0.5f)) + new Vector3(0, 0, -10);
                    camera.orthographicSize = 2.6f;
                    Capture("symbology.png", camera);
                    camera.GetComponent<CameraController>().FrameMap();
                    // Stand the test order down again so the pocket stage can
                    // prove the pocket closed without anything marching.
                    Check(controller.SetSelectionStance(DivisionStance.Defend),
                        "Could not stand the test order down");
                    Check(holder.Route.Count == 0, "Standing down did not drop the order");
                    // Raising a division costs exactly 10,000 military population.
                    int beforeMilitary = controller.LocalPlayer.Manpower;
                    int beforeCount = controller.Divisions.CountFor(0);
                    Check(controller.FormDivision(), "Could not raise a division with ample military");
                    Check(controller.LocalPlayer.Manpower == beforeMilitary - Division.Cost,
                        "Raising a division did not charge 10,000 military population");
                    Check(controller.Divisions.CountFor(0) == beforeCount + 1 &&
                        controller.SelectedDivision != null &&
                        controller.SelectedDivision.Number == beforeCount + 1,
                        "New division was not numbered after the existing ones");
                    // Sending it claims wilderness where it marches.
                    Division sent = controller.SelectedDivision;
                    int ownedBefore = map.CountTerritory(0);
                    int sentX = Mathf.Clamp(sent.X + 25, 1, map.Width - 2);
                    Check(controller.OrderSelected(sentX, sent.Y), "Could not send a division into wilderness");
                    // Step the roster directly so the check does not depend on
                    // how many frames the batch editor happens to run.
                    var marchCaptured = new System.Collections.Generic.List<int>();
                    var marchDefenders = new System.Collections.Generic.List<int>();
                    int startX = sent.X, startY = sent.Y;
                    for (int i = 0; i < 80; i++) controller.Divisions.Step(marchCaptured, marchDefenders);
                    Check(sent.X != startX || sent.Y != startY, "Division never left its muster point");
                    Check(map.CountTerritory(0) > ownedBefore, "Marching division claimed no wilderness");
                    Check(sent.Strength == Division.Cost, "Marching through wilderness cost strength");
                    controller.enabled = false;
                    Time.timeScale = 0f;
                    // Halt everything first: the pocket must close through
                    // automatic reduction, not because something marched over it.
                    foreach (Division formation in controller.Divisions.Divisions)
                        controller.Divisions.SetStance(formation, DivisionStance.Defend);
                    CreatePocket(map, renderer, controller.Divisions);
                    controller.Encirclement.Refresh();
                    Check(map.IsEncircledBy(pocketX + 3, pocketY + 3, 0), "Scene pocket was not detected");
                    camera.transform.position = renderer.transform.TransformPoint(renderer.CellToLocal(map, pocketX + 3.5f, pocketY + 3.5f)) + new Vector3(0, 0, -10);
                    camera.orthographicSize = 1.4f;
                    hud.SetCommitment(.5f);
                    next = EditorApplication.timeSinceStartup + .5;
                    break;
                case 6:
                    var hoverWorld = renderer.transform.TransformPoint(renderer.CellToLocal(map, pocketX + 3.5f, pocketY + 3.5f));
                    ApplyInput(mouse, new MouseState { position = camera.WorldToScreenPoint(hoverWorld) });
                    Check(GameObject.Find("Cell cost readout").GetComponent<TMP_Text>().text.Contains("ENCIRCLED"), "Hover cost did not show discount");
                    Capture("economy-pocket-before.png", camera);
                    controller.enabled = true;
                    controller.SetSimulationPaused(false);
                    Check(controller.SetSimulationSpeed(4), "Could not resume the pocket test at 4x");
                    Check(controller.SetSelectionStance(DivisionStance.Defend),
                        "A stance order was refused while the simulation was running");
                    Check(GameObject.Find("Simulation clock").GetComponent<TMP_Text>().text.Contains("RUNNING 4x"),
                        "Simulation clock does not report the running speed");
                    Check(GameObject.Find("Simulation pause button") != null &&
                        GameObject.Find("Speed 1x button") != null && GameObject.Find("Speed 4x button") != null &&
                        GameObject.Find("Stance ATTACK") != null && GameObject.Find("Stance REDEPLOY") != null,
                        "Simulation and stance controls are missing from the HUD");
                    next = EditorApplication.timeSinceStartup + 3;
                    break;
                case 7:
                    Check(!controller.Clock.Paused && controller.Clock.TickCount > tickAtPause,
                        "Resume did not continue simulation from the paused tick");
                    for (int x = 1; x <= 5; x++)
                    for (int y = 1; y <= 5; y++)
                        Check(map.IsOwnedBy(pocketX + x, pocketY + y, 0), "Automatic scene assault left a pocket tile");
                    // No division was anywhere near the pocket, so it closed
                    // through automatic reduction rather than by being marched over.
                    foreach (Division formation in controller.Divisions.Divisions)
                        Check(formation.X < pocketX - 1 || formation.X > pocketX + 7 ||
                            formation.Y < pocketY - 1 || formation.Y > pocketY + 7,
                            "A division stood in the pocket, so automatic capture proves nothing");
                    Check(controller.LocalPlayer.Manpower >= 0, "Automatic assault overspent reserve");
                    Capture("economy-pocket-after.png", camera);
                    Time.timeScale = 1f;
                    next = EditorApplication.timeSinceStartup + .5;
                    break;
                default:
                    Check(!runtimeError, "Runtime logged an error");
                    Debug.Log("SCENE_SMOKE_PASS: mirrored 102,400-cell duel map with whole-half starts, thirty-division orders of battle, fog of war and its debug reveal, civilian growth and coin-priced recruitment, land-scaled gold/industry production, resource totals and income HUD, chunk uploads, numeric territory labels, mouse input, expansion, camera, commitment, refunds, and automatic pocket capture.");
                    SessionState.SetBool("OpenWarSmoke", false);
                    EditorApplication.Exit(0);
                    return;
            }
            stage++;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            SessionState.SetBool("OpenWarSmoke", false);
            EditorApplication.Exit(1);
        }
    }

    static void CreatePocket(MapData map, MapRenderer renderer, DivisionSystem roster)
    {
        // Both nations start holding their whole halves, so there is no neutral
        // ground left. Find an all-walkable patch we own and vacate it.
        bool found = false;
        for (int y = 20; y < map.Height - 20 && !found; y += 5)
        for (int x = 20; x < map.Width / 2 - 20 && !found; x += 5)
        {
            bool clear = true;
            for (int dx = 0; dx <= 6 && clear; dx++)
            for (int dy = 0; dy <= 6 && clear; dy++)
                clear = map.IsWalkable(x + dx, y + dy);
            // Keep clear of the garrison: a division standing in the pocket
            // would make automatic reduction impossible to demonstrate.
            if (clear && roster != null)
                foreach (Division formation in roster.Divisions)
                    if (formation.X >= x - 2 && formation.X <= x + 8 &&
                        formation.Y >= y - 2 && formation.Y <= y + 8) { clear = false; break; }
            if (clear) { pocketX = x; pocketY = y; found = true; }
        }
        Check(found, "No walkable ground for the runtime pocket fixture");
        for (int dx = 0; dx <= 6; dx++)
        for (int dy = 0; dy <= 6; dy++) map.Vacate(pocketX + dx, pocketY + dy);
        for (int dx = 0; dx <= 6; dx++)
        for (int dy = 0; dy <= 6; dy++)
        {
            if (dx == 0 || dy == 0 || dx == 6 || dy == 6) map.TryClaimCell(pocketX + dx, pocketY + dy, 0);
            else if (dx == 3 && dy == 3) map.TryClaimCell(pocketX + dx, pocketY + dy, 1);
            renderer.RefreshCell(map, pocketX + dx, pocketY + dy);
        }
        renderer.SendMessage("LateUpdate");
    }

    static void CheckPartialChunks(Camera camera)
    {
        var cells = new TerrainType[259, 131];
        for (int x = 0; x < 259; x++)
        for (int y = 0; y < 131; y++) cells[x, y] = TerrainType.Land;
        var map = new MapData(cells);
        var obj = new GameObject("Partial chunk test", typeof(SpriteRenderer), typeof(MapRenderer));
        var renderer = obj.GetComponent<MapRenderer>();
        renderer.Draw(map);
        Check(renderer.ChunkCount == 6, "Partial chunks not allocated correctly");
        map.TryClaimCell(258, 130, 0);
        renderer.RefreshCell(map, 258, 130);
        renderer.SendMessage("LateUpdate");
        Check(renderer.LastUploadedChunkCount == 1, "Partial corner update touched incorrect chunks");
        bool cornerFound = false;
        foreach (var sprite in obj.GetComponentsInChildren<SpriteRenderer>())
            if (sprite.sprite != null && sprite.sprite.texture.width == 12 && sprite.sprite.texture.height == 12)
                cornerFound = true;
        Check(cornerFound, "Partial texture dimensions incorrect");
        obj.transform.position = new Vector3(2f, 3f, 0f);
        obj.transform.localScale = new Vector3(1.2f, 1.5f, 1f);
        obj.transform.rotation = Quaternion.Euler(0, 0, 15);
        Vector3 world = obj.transform.TransformPoint(renderer.CellToLocal(map, 128.5f, 100.5f));
        Check(renderer.TryScreenToCell(map, camera, camera.WorldToScreenPoint(world), out int gx, out int gy) && gx == 128 && gy == 100,
            "Transformed chunked map picking failed");
        // Hide immediately; Destroy finishes at the end of the frame.
        obj.SetActive(false);
        UnityEngine.Object.Destroy(obj);
    }

    static void ApplyInput(Mouse mouse, MouseState state)
    {
        // Batch mode has no focused Game view. Drive a dynamic input update
        // explicitly, exercise the real component handlers, then release it.
        InputSystem.EnableDevice(mouse);
        InputSystem.QueueStateEvent(mouse, state);
        InputSystem.Update();
        UnityEngine.Object.FindFirstObjectByType<MapController>().SendMessage("Update");
        Camera.main.GetComponent<CameraController>().SendMessage("Update");
        InputSystem.QueueStateEvent(mouse, new MouseState { position = state.position });
        InputSystem.Update();
        // Let the controller see the release as well. Selection and orders fire
        // on button-up, so without this the click depends on frame timing.
        UnityEngine.Object.FindFirstObjectByType<MapController>().SendMessage("Update");
    }

    static void Capture(string path, Camera camera)
    {
        var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        var target = new RenderTexture(1600, 1000, 24);
        var oldTarget = camera.targetTexture;
        var oldActive = RenderTexture.active;
        camera.targetTexture = target;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1f;
        Canvas.ForceUpdateCanvases();
        UnityEngine.Object.FindFirstObjectByType<MapHUD>().SendMessage("LateUpdate");
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        image.Apply();
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../", Path.GetFileName(path)));
        File.WriteAllBytes(output, image.EncodeToPNG());
        // A batch run started with -nographics still reaches this point and still
        // writes a PNG, but every pixel is the same flat colour. Without this
        // check each GameObject assertion above still passes and a blank capture
        // is indistinguishable from a good render.
        Color32[] pixels = image.GetPixels32();
        Color32 first = pixels[0];
        bool varied = false;
        for (int i = 1; i < pixels.Length && !varied; i += 37)
            varied = pixels[i].r != first.r || pixels[i].g != first.g || pixels[i].b != first.b;
        camera.targetTexture = oldTarget;
        RenderTexture.active = oldActive;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Re-lay-out before anything hit-tests the UI again: the rects are
        // still holding the camera-space corners used for the render.
        Canvas.ForceUpdateCanvases();
        UnityEngine.Object.DestroyImmediate(image);
        UnityEngine.Object.DestroyImmediate(target);
        Check(varied, "Capture wrote a blank image, so this run had no usable " +
            "graphics device. Re-run without -nographics: " + path);
    }
}

[InitializeOnLoad]
public static class DesignerSmoke
{
    static int stage;
    static double next;

    static DesignerSmoke()
    {
        if (SessionState.GetBool("OpenWarDesignerSmoke", false)) EditorApplication.update += Tick;
    }

    public static void Begin()
    {
        SessionState.SetBool("OpenWarDesignerSmoke", true);
        EditorSceneManager.OpenScene("Assets/Scenes/game.unity");
        EditorApplication.EnterPlaymode();
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < next) return;
        try
        {
            var controller = UnityEngine.Object.FindFirstObjectByType<MapController>();
            if (controller == null) return;
            switch (stage)
            {
                case 0:
                    Check(controller.Map == null, "Game started before the title screen");
                    GameObject.Find("Open map designer").GetComponent<Button>().onClick.Invoke();
                    next = EditorApplication.timeSinceStartup + .25;
                    break;
                case 1:
                    GameObject.Find("Template Sandbox").GetComponent<Button>().onClick.Invoke();
                    var sandboxPreview = GameObject.Find("Editable map preview").GetComponent<RawImage>();
                    var sandboxTexture = (Texture2D)sandboxPreview.texture;
                    Check(sandboxTexture.GetPixel(0, 0) == sandboxTexture.GetPixel(
                        sandboxTexture.width / 2, sandboxTexture.height / 2),
                        "Sandbox preview is not a uniform plains canvas");
                    Check(GameObject.Find("Settlement Label Moscow") == null,
                        "Sandbox retained historical settlement labels");
                    Check(GameObject.Find("Infrastructure legend") == null,
                        "Sandbox retained historical infrastructure");
                    GameObject.Find("Template Kursk1943").GetComponent<Button>().onClick.Invoke();
                    Check(GameObject.Find("Settlement Label Kursk") != null,
                        "Kursk settlement label did not load in the designer");
                    Check(GameObject.Find("Infrastructure Site Kursk Airfield") != null &&
                        GameObject.Find("Road Oryol-Kharkov Highway") != null &&
                        GameObject.Find("Rail track Oryol-Kharkov Railway") != null,
                        "Kursk infrastructure did not load in the designer");
                    GameObject.Find("Brush Water").GetComponent<Button>().onClick.Invoke();
                    GameObject previewObject = GameObject.Find("Editable map preview");
                    var preview = previewObject.GetComponent<RawImage>();
                    var texture = (Texture2D)preview.texture;
                    Color before = texture.GetPixel(texture.width / 2, texture.height / 2);
                    var rect = (RectTransform)previewObject.transform;
                    var pointer = new PointerEventData(EventSystem.current)
                    {
                        button = PointerEventData.InputButton.Left,
                        position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center))
                    };
                    ExecuteEvents.Execute(previewObject, pointer, ExecuteEvents.pointerDownHandler);
                    ExecuteEvents.Execute(previewObject, pointer, ExecuteEvents.pointerUpHandler);
                    Color after = texture.GetPixel(texture.width / 2, texture.height / 2);
                    Check(before != after, "Painting the map preview did not change it");
                    GameObject.Find("Reset template").GetComponent<Button>().onClick.Invoke();
                    GameObject.Find("Play designed map").GetComponent<Button>().onClick.Invoke();
                    next = EditorApplication.timeSinceStartup + .5;
                    break;
                default:
                    Check(controller.Map != null, "Play Map did not launch the scenario");
                    Check(controller.Map.Width == 160 && controller.Map.Height == 96, "Designed map dimensions are incorrect");
                    Check(controller.Map.CountTerritory(0) > 0 && controller.Map.CountTerritory(1) > 0,
                        "Designed map did not preserve both starting sides");
                    Check(GameObject.Find("Resource Gold") != null && GameObject.Find("Industry wrench icon") != null,
                        "Gameplay HUD was not created for the designed map");
                    Check(GameObject.Find("Map settlement label Kursk") != null,
                        "Scenario settlements were not carried into gameplay");
                    Check(GameObject.Find("Map infrastructure label Kursk Airfield") != null &&
                        GameObject.Find("Road Oryol-Kharkov Highway") != null,
                        "Scenario infrastructure was not carried into gameplay");
                    Check(GameObject.Find("Resource Military").GetComponent<TMP_Text>().text.Contains("RECRUITED"),
                        "Designed-game HUD does not show that military is recruited");
                    Check(GameObject.Find("Resource Civilians") != null && GameObject.Find("Recruit button") != null,
                        "Designed-game HUD is missing the civilian population or recruit control");
                    Debug.Log("DESIGNER_SMOKE_PASS: title navigation, geographic settlements and infrastructure, blank sandbox, painting, reset, civilian growth with coin-priced recruitment, and Play Map launch.");
                    SessionState.SetBool("OpenWarDesignerSmoke", false);
                    EditorApplication.Exit(0);
                    return;
            }
            stage++;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            SessionState.SetBool("OpenWarDesignerSmoke", false);
            EditorApplication.Exit(1);
        }
    }
}

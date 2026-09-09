using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using TMPro;

[InitializeOnLoad]
public static class SceneSmoke
{
    static int stage;
    static double next;
    static float oldZoom;
    static Vector3 oldPosition;
    static Vector2 clickPosition;
    static int targetX, targetY;
    static int pocketX, pocketY;
    static bool runtimeError;
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
            if (controller == null || controller.Map == null) return;
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
                    Check(map.Width == 1024 && map.Height == 512, "Scene dimensions incorrect");
                    Check(renderer.ChunkCount == 32, "Expected 32 map rendering chunks");
                    Check(Mathf.Approximately(renderer.WorldBounds.size.x, 128f) &&
                        Mathf.Approximately(renderer.WorldBounds.size.y, 64f), "Incorrect large map bounds");
                    renderer.RefreshCell(map, 127, 127);
                    renderer.SendMessage("LateUpdate");
                    Check(renderer.LastUploadedChunkCount == 3, "Chunk seam update did not update exactly three affected chunks");
                    renderer.SendMessage("LateUpdate");
                    Check(renderer.LastUploadedChunkCount == 0, "Unchanged textures were uploaded again");
                    CheckPartialChunks(camera);
                    map.TryGetTerritoryCenter(0, out float cx, out float cy);
                    bool found = false;
                    for (int offset = 20; offset < 40 && !found; offset++)
                    {
                        targetX = (int)cx + offset; targetY = (int)cy + 8;
                        found = ExpansionOrder.TryCreate(map, 0, targetX, targetY, 4, 100000, out _);
                    }
                    Check(found, "No smoke test target");
                    var world = renderer.transform.TransformPoint(renderer.CellToLocal(map, targetX + 0.5f, targetY + 0.5f));
                    clickPosition = camera.WorldToScreenPoint(world);
                    Check(renderer.TryScreenToCell(map, camera, clickPosition, out int gx, out int gy) && gx == targetX && gy == targetY,
                        "Screen/grid conversion failed after texture detail change");
                    controller.LocalPlayer.AddManpower(100000);
                    Capture("economy-overview.png", camera);
                    ApplyInput(mouse, new MouseState { position = clickPosition }.WithButton(MouseButton.Left));
                    next = EditorApplication.timeSinceStartup + 0.5;
                    break;
                case 1:
                    Check(controller.CurrentOrder != null, "Left click did not create an order");
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
                    Time.timeScale = 8f;
                    next = EditorApplication.timeSinceStartup + 3;
                    break;
                case 4:
                    Check(map.CountTerritory(0) > 100, "Timed expansion did not capture territory");
                    foreach (var text in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
                    {
                        if (text.name == "Map controls" || text.name == "Order status" || text.name == "Commitment readout" ||
                            text.name == "Cell cost readout" || text.name == "Pocket assault readout" || text.name.StartsWith("Resource ")) continue;
                        Check(!text.text.Contains("Manpower"), "Territory label contains Manpower");
                        foreach (char c in text.text) Check(char.IsDigit(c) || c == ',', "Territory label is not numeric");
                    }
                    Check(controller.LocalPlayer.Gold > 0 && controller.LocalPlayer.Industry > 0, "Economy tick did not produce resources");
                    Check(controller.LocalPlayer.Gold == controller.LocalPlayer.Industry * 2, "Resource production ratio wrong");
                    foreach (string name in new[] { "Manpower", "Gold", "Industry", "Land" })
                    {
                        var resourceText = GameObject.Find("Resource " + name).GetComponent<TMP_Text>();
                        Check(resourceText != null && resourceText.text.Length > 0, "Missing resource readout: " + name);
                        Check(!resourceText.raycastTarget, "Resource readout blocks map input");
                    }
                    Check(GameObject.Find("Resource Gold").GetComponent<TMP_Text>().text.Contains("/s"), "Resource income rate missing");
                    Capture("economy-advance.png", camera);
                    ApplyInput(mouse, new MouseState { position = clickPosition }.WithButton(MouseButton.Right));
                    Check(controller.CurrentOrder == null, "Cancellation failed");
                    Check(!controller.IssueOrder(-1, -1), "Invalid order accepted");
                    Time.timeScale = 1f;
                    next = EditorApplication.timeSinceStartup + 0.5;
                    break;
                case 5:
                    var hud = UnityEngine.Object.FindFirstObjectByType<MapHUD>();
                    var panel = (RectTransform)GameObject.Find("Troop commitment").transform;
                    Vector2 panelPoint = RectTransformUtility.WorldToScreenPoint(null, panel.TransformPoint(panel.rect.center));
                    Check(hud.BlocksMapInput(panelPoint), "Commitment panel does not block map input");
                    ApplyInput(mouse, new MouseState { position = panelPoint }.WithButton(MouseButton.Left));
                    Check(controller.CurrentOrder == null, "Slider panel click issued an attack");
                    hud.SetCommitment(1f);
                    int total = controller.LocalPlayer.Manpower;
                    Check(controller.PlannedCommitment == total, "100% commitment preview wrong");
                    map.TryGetTerritoryCenter(1, out float rivalX, out float rivalY);
                    Check(controller.IssueOrder((int)rivalX, (int)rivalY), "100% order could not be issued");
                    Check(controller.LocalPlayer.Manpower == 0, "100% commitment left reserve");
                    Check(controller.IssueOrder((int)rivalX, (int)rivalY), "Could not redirect a 100% committed force");
                    controller.CancelOrder();
                    Check(controller.LocalPlayer.Manpower == total, "Redirect/recall leaked troops");
                    controller.enabled = false;
                    Time.timeScale = 0f;
                    CreatePocket(map, renderer);
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
                    Time.timeScale = 4f;
                    next = EditorApplication.timeSinceStartup + 3;
                    break;
                case 7:
                    for (int x = 1; x <= 5; x++)
                    for (int y = 1; y <= 5; y++)
                        Check(map.IsOwnedBy(pocketX + x, pocketY + y, 0), "Automatic scene assault left a pocket tile");
                    Check(controller.CurrentOrder == null, "Pocket capture needed a manual order");
                    Check(controller.LocalPlayer.Manpower >= 0, "Automatic assault overspent reserve");
                    Capture("economy-pocket-after.png", camera);
                    Time.timeScale = 1f;
                    next = EditorApplication.timeSinceStartup + .5;
                    break;
                default:
                    Check(!runtimeError, "Runtime logged an error");
                    Debug.Log("SCENE_SMOKE_PASS: land-scaled gold/industry production, resource totals and income HUD, 524,288 tiles, chunk uploads, numeric territory labels, mouse input, expansion, camera, commitment, refunds, and automatic pocket capture.");
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

    static void CreatePocket(MapData map, MapRenderer renderer)
    {
        bool found = false;
        for (int y = 20; y < map.Height - 20 && !found; y += 5)
        for (int x = 20; x < map.Width - 20 && !found; x += 5)
        {
            bool clear = true;
            for (int dx = 0; dx <= 6 && clear; dx++)
            for (int dy = 0; dy <= 6 && clear; dy++)
                clear = map.IsWalkable(x + dx, y + dy) && map.GetCell(x + dx, y + dy).OwnerId == -1;
            if (clear) { pocketX = x; pocketY = y; found = true; }
        }
        Check(found, "No neutral ground for runtime pocket fixture");
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
        camera.targetTexture = oldTarget;
        RenderTexture.active = oldActive;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        UnityEngine.Object.DestroyImmediate(image);
        UnityEngine.Object.DestroyImmediate(target);
    }
}

# Verification — 7 September 2026

- Compiled all gameplay scripts against the installed Unity 6000.5.4f1 assemblies: no C# errors or warnings.
- Standalone .NET 8 simulation tests passed: broad directional fronts, terrain-weighted routes, water barriers, unreachable destinations, fords, combat costs and defense, resource pauses/resumption, invalid actions, neutral orders avoiding uninvolved factions, and territory-center placement.
- Generated and tested 256 × 160 maps for seeds 1847, 42, and 2026. Each had all ten terrain categories, four 80-cell starts, and a reachable rival attack destination.
- An isolated Unity 6000.5.4f1 scene test passed startup, terrain texture generation, screen/grid conversion, numeric labels, left-click orders, timed expansion, wheel zoom, middle-drag panning, right-click cancellation, and invalid destinations. Batch-mode input was driven through explicit Input System updates and the actual component handlers.
- Inspected `overview.png` and `advance.png`, rendered by Unity. The advance screenshot uses additional test manpower to finish the order quickly; the actual player starts with 1,600.

The isolated editor logged a UnityEditor.Search.SearchDatabase indexing exception on startup. It was excluded from game-runtime error tracking; there were no gameplay exceptions. The successful run is recorded in `unity-smoke.log` with `SCENE_SMOKE_PASS`.

The temporary Unity Library/Temp/Logs caches were removed after verification. The test project's Assets, Packages, ProjectSettings, and editor harness remain for reproduction. Before repeating that scene test after gameplay changes, refresh its copied scripts and scene from `../My project`.

Run simulation tests with a .NET 8 SDK:

```powershell
dotnet run --project Verification/SimulationTests.csproj
```

The isolated scene harness is `Unity/Assets/Editor/SceneSmoke.cs`. It can be run with Unity's `-batchmode -force-d3d11 -projectPath <absolute Verification/Unity path> -executeMethod SceneSmoke.Begin -logFile <output log>` arguments. It needs access to the local Unity licensing service.

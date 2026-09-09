# open_war

A Unity/C# territorial strategy learning project. The working prototype includes a 524,288-cell terrain map, directional expansion, committed attacking forces, encirclement, and land-based manpower, gold, and industry income.

## Open the game

1. Add the **My project** folder in Unity Hub.
2. Open with Unity **6000.5.4f1** and allow packages/assets to import.
3. Open `Assets/Scenes/game.unity` and press Play.

See [the project guide](My%20project/README.md) for controls, gameplay rules, architecture, and tuning values.

## Run simulation checks

With the .NET 8 SDK installed, run from the repository root:

```powershell
dotnet run --project Verification/SimulationTests.csproj
```

`Verification` contains the simulation scenarios and historical verification reports. Unity-generated caches, binaries, screenshots, local backup archives, and logs are excluded from Git.

The unique Unity scene test harness is preserved at `Verification/Unity/Assets/Editor/SceneSmoke.cs`. Its isolated test project is local scratch data: to recreate it, copy the game's Assets, Packages, and ProjectSettings into `Verification/Unity`, retaining that Editor harness. Run `SceneSmoke.Begin` through Unity's `-executeMethod` option in batch mode with graphics enabled. The harness changes only its test scene, including extra test manpower and accelerated simulation time.

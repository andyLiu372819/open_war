# open_war

A Unity/C# territorial strategy learning project running on deterministic, pause-capable real-time simulation ticks with 1x, 2x, and 4x speeds. The working prototype includes a mirrored 102,400-cell duel map and a 524,288-cell scenario engine, infantry formations with mutual casualties and destruction, terrain-following transport networks that affect routing and speed, directional expansion, encirclement reduced from every direction at once, free wilderness occupation, separate civilian and military populations with coin-priced recruitment, and land-based gold and industry production.

## Open the game

1. Add the **My project** folder in Unity Hub.
2. Open with Unity **6000.5.4f1** and allow packages/assets to import.
3. Open `Assets/Scenes/game.unity` and press Play. Choose Quick Play for the original world or Map Designer for an editable scenario, four geolocated historical-operation templates with transport and logistics infrastructure, and a blank Sandbox.

See [the project guide](My%20project/README.md) for controls, gameplay rules, architecture, and tuning values.

## Run simulation checks

With the .NET 8 SDK installed, run from the repository root:

```powershell
dotnet run --project Verification/SimulationTests.csproj
```

`Verification` contains the simulation scenarios and historical verification reports. Unity-generated caches, binaries, screenshots, local backup archives, and logs are excluded from Git.

The unique Unity scene test harness is preserved at `Verification/Unity/Assets/Editor/SceneSmoke.cs`. Its isolated test project is local scratch data: to recreate it, copy the game's Assets, Packages, and ProjectSettings into `Verification/Unity`, retaining that Editor harness. Run `SceneSmoke.Begin` through Unity's `-executeMethod` option in batch mode with graphics enabled. The harness changes only its test scene, including extra test manpower and accelerated simulation time.

# Watcher

Watcher is a lightweight RimWorld 1.6 performance monitor. Press **Shift+6** in game to open or close its dashboard.

## Build

1. Install the **.NET Framework 4.7.2 Developer Pack** (not just the Runtime) to get the `net472` reference assemblies, plus the .NET SDK or a C# build tool such as Visual Studio/MSBuild.
2. Set `RimWorldDir` to your RimWorld installation directory and `HarmonyDir` to the folder containing `0Harmony.dll` (often the Harmony mod's `Current/Assemblies` folder). The equivalent environment variables are `RIMWORLD_DIR` and `HARMONY_DIR`.
3. Build `Watcher.csproj` in Release configuration. The resulting `Watcher.dll` is copied into `Watcher/Assemblies`.
4. Copy the `Watcher` folder into RimWorld's `Mods` directory and enable it in the mod list.

Example:

```powershell
dotnet build .\Watcher\Watcher.csproj -c Release -p:RimWorldDir="D:\Games\Steam\steamapps\common\RimWorld" -p:HarmonyDir="D:\Games\Steam\steamapps\workshop\content\294100\2009463077\Current\Assemblies"
```

Game assemblies are referenced from `RimWorldWin64_Data/Managed`. Harmony is referenced from the separately installed Harmony mod. Neither is distributed with this project.

## What is measured

- **FPS** is calculated from Unity's unscaled frame delta; frame time is the same sampled value in milliseconds.
- **Tick duration** is measured around `TickManager.DoSingleTick` using a monotonic stopwatch. Average and maximum values cover the current monitoring session. Counts use configured warning and critical thresholds.
- **TPS** is a one-second rolling observed tick rate. It is not a promise that the simulation is keeping pace with wall time.
- **GC collections** are read from .NET collection counters. They do not provide per-frame allocated bytes or reliable attribution to a mod.
- **Memory** is the managed heap size reported by `GC.GetTotalMemory(false)`; it is not total process or GPU memory.
- Pawn/map counts and game speed are snapshots from the active game state.

Watcher reports a long tick's measured source as `TickManager.DoSingleTick` (the whole game simulation tick) and records its measured duration. This does not identify which system or mod consumed that time, so mod attribution remains unknown. Events are correlated observations. Rendering time and main-thread utilization are omitted because the game does not expose reliable measurements to this mod.

The monitor uses one Harmony prefix/postfix pair, a fixed-size graph ring buffer, a bounded event queue, and no per-tick logging or stack traces. Telemetry is session-only and is reset on game load. Settings are stored using RimWorld's normal mod settings mechanism.

## Controls

The dashboard shows current metrics, a 60-second history graph, and recent long-tick events. Settings expose the long-tick and critical thresholds, retained event count, and history window. The event list can be cleared from the dashboard.

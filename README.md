# Watcher

Watcher is a lightweight RimWorld performance monitor. Press **Shift+6** in game to open or close its dashboard.

<img width="1920" height="512" alt="Banner" src="https://github.com/user-attachments/assets/08b86c56-b73b-484d-859c-3f46b72610f9" />

## Steam Workshop

Watcher is available for download on Steam Workshop. You can find it [here](https://steamcommunity.com/sharedfiles/filedetails/?id=3813463709).

## Build

1. Install the **.NET Framework 4.7.2 Developer Pack** (not just the Runtime) to get the `net472` reference assemblies, plus the .NET SDK or a C# build tool such as Visual Studio/MSBuild.
2. Set `RimWorldDir` to your RimWorld installation directory and `HarmonyDir` to the folder containing `0Harmony.dll` (often the Harmony mod's `Current/Assemblies` folder). The equivalent environment variables are `RIMWORLD_DIR` and `HARMONY_DIR`.
3. Build `Watcher.csproj` in Release configuration. The resulting `Watcher.dll` is copied into `Watcher/Assemblies`.
4. Copy the `Watcher` folder into RimWorld's `Mods` directory and enable it in the mod list.

Game assemblies are referenced from `RimWorldWin64_Data/Managed`. Harmony is referenced from the separately installed Harmony mod. Neither is distributed with this project.

## What is measured

- **FPS** is calculated from Unity's unscaled frame delta; frame time is the same sampled value in milliseconds.
- **Tick duration** is measured around `TickManager.DoSingleTick` using a monotonic stopwatch. Average and maximum values cover the current monitoring session. The 95th percentile uses up to the latest 1,200 ticks and refreshes once per second, making recurring slow ticks visible without treating a single spike as typical. Counts use configured warning and critical thresholds.
- **TPS** is a one-second rolling observed tick rate. It is not a promise that the simulation is keeping pace with wall time.
- **GC collections** are session counts derived from .NET collection counters. They do not provide per-frame allocated bytes or reliable attribution to a mod.
- **Memory** is the managed heap size reported by `GC.GetTotalMemory(false)`; it is not total process or GPU memory.
- Pawn/map counts and game speed are snapshots from the active game state; the pawn count includes spawned pawns on loaded maps.

For each long tick, Watcher records the whole `TickManager.DoSingleTick` duration and the longest of three measured slices: map pre-tick, the `TickList.Tick` thing/pawn batch, and map post-tick. The event shows the slice and its duration alongside the whole-tick duration. This narrows down which broad phase coincided with the slow tick; it does not prove that phase caused the delay or identify which mod consumed the time. Mod attribution is not inferred. Rendering time and main-thread utilization are omitted because the game does not expose reliable measurements to this mod.

For extra context, event rows list Harmony owners with patches on the measured method. Hover the list to see the full names. These are mods that patch that method, not a ranking of their runtime cost.

The monitor uses a small set of Harmony timing hooks around the overall tick and three broad tick phases, a fixed-size graph ring buffer, a bounded event queue, and no per-tick logging or stack traces. Telemetry is session-only and is reset on game load. Settings are stored using RimWorld's normal mod settings mechanism.

## Controls

The dashboard shows current metrics, a 60-second history graph, and recent long-tick events. Settings expose the long-tick and critical thresholds, retained event count, and history window. The event list can be cleared from the dashboard.

## License

This repository is licensed under the [MIT License](LICENSE).

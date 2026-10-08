using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace Watcher
{
    internal sealed class CaptureProfile
    {
        public readonly string Name;
        public readonly string Explanation;
        public readonly string NextCheck;
        public readonly string MeasuredScope;
        public readonly MethodBase Method;
        public int Calls;
        public double TotalMilliseconds;
        public float PeakMilliseconds;

        public CaptureProfile(string name, string explanation, string nextCheck, string measuredScope, MethodBase method)
        {
            Name = name;
            Explanation = explanation;
            NextCheck = nextCheck;
            MeasuredScope = measuredScope;
            Method = method;
        }

        public void Clear()
        {
            Calls = 0;
            TotalMilliseconds = 0;
            PeakMilliseconds = 0f;
        }
    }

    // Detailed hooks are installed only during a short, user-requested or opt-in automatic capture.
    internal static class DiagnosticCapture
    {
        private static readonly object Sync = new object();
        private static readonly CaptureProfile[] Profiles = CreateProfiles();
        private static int active;
        private static int autoStartPending;
        private static float remainingSeconds;
        private static float lastDurationSeconds;
        private static int startPawns;
        private static int startMaps;
        private static long startLongTicks;
        private static int endPawns;
        private static int endMaps;
        private static long endLongTicks;
        private static string[] ownerCache;

        public static bool IsActive => System.Threading.Volatile.Read(ref active) != 0;
        public static bool HasReport { get; private set; }
        public static float RemainingSeconds => remainingSeconds;
        public static float LastDurationSeconds => lastDurationSeconds;
        public static IReadOnlyList<CaptureProfile> Results => Profiles;

        private static CaptureProfile[] CreateProfiles()
        {
            return new[]
            {
                new CaptureProfile("Pawn job and AI updates", "Job tracker work, including selecting and advancing pawn jobs.", "If this stays high, inspect frequent job retries, large pawn counts, and mods that add job checks.", "Pawn_JobTracker.JobTrackerTick", AccessTools.Method(typeof(Verse.AI.Pawn_JobTracker), "JobTrackerTick")),
                new CaptureProfile("Pathfinding", "Synchronous path searches and pathfinder queue processing. Queue work can overlap individual path searches.", "If this stays high, inspect repeated path requests, map/region complexity, and mods issuing extra path queries.", "PathFinder.FindPathNow and PathFinder.PathFinderTick", null),
                new CaptureProfile("Nearby target searches", "Reachable-thing searches used by jobs and other gameplay systems.", "If this stays high, inspect repeated reachable-target scans and work types that search across many things.", "GenClosest.ClosestThingReachable", FindClosestReachable()),
                new CaptureProfile("Pawn health updates", "Health and hediff work performed during pawn ticks.", "If this stays high, compare pawn and hediff counts and check for repeated health-related work.", "Pawn_HealthTracker.HealthTick", AccessTools.Method(typeof(Pawn_HealthTracker), "HealthTick")),
                new CaptureProfile("Map region rebuilds", "Rebuilding dirty regions and room data after map changes.", "If this spikes, inspect recent terrain, door, building, or map changes that dirty regions.", "RegionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms", AccessTools.Method(typeof(RegionAndRoomUpdater), "TryRebuildDirtyRegionsAndRooms"))
            };
        }

        private static MethodBase FindClosestReachable()
        {
            return AccessTools.GetDeclaredMethods(typeof(GenClosest))
                .FirstOrDefault(method => method.Name == "ClosestThingReachable" && method.GetParameters().Length == 14);
        }

        private static IEnumerable<MethodBase> FindPathMethods()
        {
            return AccessTools.GetDeclaredMethods(typeof(PathFinder))
                .Where(method => method.Name == "FindPathNow" || method.Name == "PathFinderTick");
        }

        public static void Start(float seconds)
        {
            lock (Sync)
            {
                System.Threading.Volatile.Write(ref active, 0);
                RemoveHooks();
                foreach (CaptureProfile profile in Profiles) profile.Clear();
                remainingSeconds = Math.Max(1f, seconds);
                lastDurationSeconds = remainingSeconds;
                ownerCache = null;
                HasReport = false;
                startPawns = PerformanceData.Pawns;
                startMaps = PerformanceData.Maps;
                startLongTicks = PerformanceData.LongTickCount;
                endPawns = startPawns;
                endMaps = startMaps;
                endLongTicks = startLongTicks;
                InstallHooks();
                System.Threading.Volatile.Write(ref autoStartPending, 0);
                System.Threading.Volatile.Write(ref active, 1);
            }
        }

        public static void Reset()
        {
            lock (Sync)
            {
                System.Threading.Volatile.Write(ref active, 0);
                RemoveHooks();
                foreach (CaptureProfile profile in Profiles) profile.Clear();
                remainingSeconds = 0f;
                lastDurationSeconds = 0f;
                ownerCache = null;
                HasReport = false;
                startPawns = startMaps = endPawns = endMaps = 0;
                startLongTicks = endLongTicks = 0;
                System.Threading.Volatile.Write(ref autoStartPending, 0);
                System.Threading.Volatile.Write(ref active, 0);
            }
        }

        public static void Stop()
        {
            if (!IsActive) return;
            lock (Sync)
            {
                lastDurationSeconds = Math.Max(0f, lastDurationSeconds - remainingSeconds);
                endPawns = PerformanceData.Pawns;
                endMaps = PerformanceData.Maps;
                endLongTicks = PerformanceData.LongTickCount;
                remainingSeconds = 0f;
                System.Threading.Volatile.Write(ref active, 0);
                RemoveHooks();
                HasReport = true;
            }
        }

        public static void Update(float unscaledDeltaTime)
        {
            if (System.Threading.Interlocked.Exchange(ref autoStartPending, 0) != 0 && !IsActive && WatcherMod.Settings != null)
                Start(WatcherMod.Settings.captureSeconds);
            if (!IsActive || unscaledDeltaTime <= 0f) return;
            remainingSeconds -= unscaledDeltaTime;
            if (remainingSeconds <= 0f) Stop();
        }

        public static void Record(int profileIndex, long startedAt)
        {
            if (startedAt == 0 || !IsActive) return;
            float elapsed = (float)((Stopwatch.GetTimestamp() - startedAt) * 1000.0 / Stopwatch.Frequency);
            lock (Sync)
            {
                if (!IsActive || profileIndex < 0 || profileIndex >= Profiles.Length) return;
                CaptureProfile profile = Profiles[profileIndex];
                profile.Calls++;
                profile.TotalMilliseconds += elapsed;
                if (elapsed > profile.PeakMilliseconds) profile.PeakMilliseconds = elapsed;
            }
        }

        public static string GetOwners(CaptureProfile profile)
        {
            if (ownerCache == null) BuildOwnerCache();
            int index = Array.IndexOf(Profiles, profile);
            if (index == 1)
            {
                var owners = FindPathMethods().Select(TickStageInstrumentation.GetPatchOwners)
                    .Where(name => !string.IsNullOrEmpty(name)).Distinct().ToArray();
                return owners.Length == 0 ? null : string.Join(", ", owners);
            }
            if (profile.Method == null) return "Method unavailable in this game version";
            return index < 0 ? null : ownerCache[index];
        }

        private static void BuildOwnerCache()
        {
            ownerCache = new string[Profiles.Length];
            for (int i = 0; i < Profiles.Length; i++)
                ownerCache[i] = TickStageInstrumentation.GetPatchOwners(Profiles[i].Method);
        }

        public static string BuildTextReport()
        {
            if (!HasReport) return "No diagnostic capture has completed yet.";
            var text = new System.Text.StringBuilder(1200);
            text.Append("Watcher diagnostic capture — ").Append(lastDurationSeconds.ToString("F0")).AppendLine(" seconds");
            text.AppendLine("Timings are inclusive and can overlap; do not add category totals together.");
            text.Append("Colony snapshot: ").Append(startPawns).Append(" → ").Append(endPawns).Append(" spawned pawns; ")
                .Append(startMaps).Append(" → ").Append(endMaps).Append(" maps; ")
                .Append(Math.Max(0, endLongTicks - startLongTicks)).AppendLine(" long ticks during capture.");
            text.AppendLine("These values provide context and do not by themselves establish a cause.");
            text.AppendLine();
            foreach (CaptureProfile profile in Profiles.OrderByDescending(item => item.TotalMilliseconds))
            {
                text.Append(profile.Name).Append(": ");
                if (profile.Calls == 0)
                {
                    text.AppendLine("not observed during this capture.");
                    text.Append("  What this covers: ").AppendLine(profile.Explanation);
                    text.Append("  Measured methods: ").AppendLine(profile.MeasuredScope);
                    continue;
                }

                text.Append(profile.TotalMilliseconds.ToString("F1")).Append(" ms total across ")
                    .Append(profile.Calls).Append(" calls; peak ").Append(profile.PeakMilliseconds.ToString("F2")).AppendLine(" ms.");
                text.Append("  ").AppendLine(profile.Explanation);
                text.Append("  Measured methods: ").AppendLine(profile.MeasuredScope);
                string owners = GetOwners(profile);
                text.Append("  Harmony patches on this method: ").AppendLine(string.IsNullOrEmpty(owners) ? "none found" : owners);
                text.Append("  Suggested next check: ").AppendLine(profile.NextCheck);
            }
            text.AppendLine();
            text.AppendLine("Patch owners are candidates for investigation, not proof of cause. Mods can affect work indirectly or patch methods outside these measured scopes.");
            text.AppendLine("Pathfinding can include time waiting for scheduled work. This report measures elapsed method time, not per-mod CPU time.");
            return text.ToString();
        }

        public static void AutoStartAfterCriticalTick()
        {
            if (WatcherMod.Settings == null || !WatcherMod.Settings.autoCaptureOnCritical || IsActive) return;
            System.Threading.Interlocked.Exchange(ref autoStartPending, 1);
        }

        private static class JobTrackerCapturePatch
        {
            private static void Prefix(out long __state) => __state = IsActive ? Stopwatch.GetTimestamp() : 0;
            private static void Postfix(long __state) => Record(0, __state);
        }

        private static class PathfindingCapturePatch
        {
            private static void Prefix(out long __state) => __state = IsActive ? Stopwatch.GetTimestamp() : 0;
            private static void Postfix(long __state) => Record(1, __state);
        }

        private static class ClosestReachableCapturePatch
        {
            private static void Prefix(out long __state) => __state = IsActive ? Stopwatch.GetTimestamp() : 0;
            private static void Postfix(long __state) => Record(2, __state);
        }

        private static class HealthCapturePatch
        {
            private static void Prefix(out long __state) => __state = IsActive ? Stopwatch.GetTimestamp() : 0;
            private static void Postfix(long __state) => Record(3, __state);
        }

        private static class RegionRebuildCapturePatch
        {
            private static void Prefix(out long __state) => __state = IsActive ? Stopwatch.GetTimestamp() : 0;
            private static void Postfix(long __state) => Record(4, __state);
        }

        private static readonly Harmony CaptureHarmony = new Harmony("soc1almemory.watcher.diagnosticcapture");

        private static void InstallHooks()
        {
            Patch(Profiles[0].Method, typeof(JobTrackerCapturePatch));
            foreach (MethodBase method in FindPathMethods()) Patch(method, typeof(PathfindingCapturePatch));
            Patch(Profiles[2].Method, typeof(ClosestReachableCapturePatch));
            Patch(Profiles[3].Method, typeof(HealthCapturePatch));
            Patch(Profiles[4].Method, typeof(RegionRebuildCapturePatch));
        }

        private static void Patch(MethodBase target, Type callbacks)
        {
            if (target == null) return;
            MethodInfo prefix = AccessTools.Method(callbacks, "Prefix");
            MethodInfo postfix = AccessTools.Method(callbacks, "Postfix");
            try
            {
                CaptureHarmony.Patch(target, new HarmonyMethod(prefix), new HarmonyMethod(postfix));
            }
            catch (Exception exception)
            {
                Log.Warning("Watcher could not profile " + target.DeclaringType?.FullName + "." + target.Name + ": " + exception.Message);
            }
        }

        private static void RemoveHooks() => CaptureHarmony.UnpatchAll(CaptureHarmony.Id);
    }
}

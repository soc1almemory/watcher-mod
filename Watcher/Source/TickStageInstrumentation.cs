using System.Diagnostics;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Verse;

namespace Watcher
{
    // Patch callbacks deliberately avoid Harmony's __originalMethod injection. Some compatibility
    // layers invoke Harmony postfixes themselves and do not populate that special argument.
    internal static class TickStageInstrumentation
    {
        private const string WatcherOwner = "soc1almemory.watcher.performance";
        private static readonly MethodBase MapPreTick = AccessTools.Method(typeof(Map), "MapPreTick");
        private static readonly MethodBase TickListTick = AccessTools.Method(typeof(TickList), "Tick");
        private static readonly MethodBase MapPostTick = AccessTools.Method(typeof(Map), "MapPostTick");

        public static void Record(byte stage, long startedAt)
        {
            float milliseconds = (float)((Stopwatch.GetTimestamp() - startedAt) * 1000.0 / Stopwatch.Frequency);
            PerformanceData.RecordStage(stage, milliseconds);
        }

        // Patch owners are context for the measured method, not proof that a mod caused the delay.
        public static string GetPatchOwners(byte stage)
        {
            if (stage == 0) return null;
            MethodBase method = stage == 1 ? MapPreTick : stage == 2 ? TickListTick : MapPostTick;
            if (method == null) return null;
            Patches patches = Harmony.GetPatchInfo(method);
            if (patches == null || patches.Owners == null || patches.Owners.Count == 0) return null;

            StringBuilder names = new StringBuilder(96);
            var mods = LoadedModManager.RunningModsListForReading;
            int listed = 0;
            int additional = 0;
            foreach (string owner in patches.Owners)
            {
                if (string.Equals(owner, WatcherOwner, System.StringComparison.OrdinalIgnoreCase)) continue;
                if (listed >= 4) { additional++; continue; }

                string name = owner;
                for (int i = 0; i < mods.Count; i++)
                {
                    if (string.Equals(mods[i].PackageId, owner, System.StringComparison.OrdinalIgnoreCase))
                    {
                        name = mods[i].Name;
                        break;
                    }
                }

                if (listed > 0) names.Append(", ");
                names.Append(name);
                listed++;
            }
            if (additional > 0) names.Append(" +").Append(additional).Append(" more");
            return listed == 0 ? null : names.ToString();
        }
    }

    [HarmonyPatch(typeof(Map), "MapPreTick")]
    internal static class MapPreTickInstrumentation
    {
        [HarmonyPrefix] private static void Prefix(out long __state) => __state = Stopwatch.GetTimestamp();
        [HarmonyPostfix] private static void Postfix(long __state) => TickStageInstrumentation.Record(1, __state);
    }

    [HarmonyPatch(typeof(TickList), "Tick")]
    internal static class TickListInstrumentation
    {
        [HarmonyPrefix] private static void Prefix(out long __state) => __state = Stopwatch.GetTimestamp();
        [HarmonyPostfix] private static void Postfix(long __state) => TickStageInstrumentation.Record(2, __state);
    }

    [HarmonyPatch(typeof(Map), "MapPostTick")]
    internal static class MapPostTickInstrumentation
    {
        [HarmonyPrefix] private static void Prefix(out long __state) => __state = Stopwatch.GetTimestamp();
        [HarmonyPostfix] private static void Postfix(long __state) => TickStageInstrumentation.Record(3, __state);
    }
}

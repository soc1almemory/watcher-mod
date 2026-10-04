using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Verse;

namespace Watcher
{
    // Measure only a few broad tick stages; these identify where time was spent, not which mod caused it.
    [HarmonyPatch]
    internal static class TickStageInstrumentation
    {
        private const string WatcherOwner = "soc1almemory.watcher.performance";
        private static readonly MethodBase MapPreTick = AccessTools.Method(typeof(Map), "MapPreTick");
        private static readonly MethodBase TickListTick = AccessTools.Method(typeof(TickList), "Tick");
        private static readonly MethodBase MapPostTick = AccessTools.Method(typeof(Map), "MapPostTick");

        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return MapPreTick;
            yield return TickListTick;
            yield return MapPostTick;
        }

        [HarmonyPrefix]
        private static void Prefix(MethodBase __originalMethod, out long __state)
        {
            __state = Stopwatch.GetTimestamp();
        }

        [HarmonyPostfix]
        private static void Postfix(MethodBase __originalMethod, long __state)
        {
            float milliseconds = (float)((Stopwatch.GetTimestamp() - __state) * 1000.0 / Stopwatch.Frequency);
            byte stage = __originalMethod.DeclaringType == typeof(TickList) ? (byte)2
                : __originalMethod.Name == "MapPreTick" ? (byte)1 : (byte)3;
            PerformanceData.RecordStage(stage, milliseconds);
        }

        // Patch owners are contextual clues for the measured method, not proof that a mod caused the delay.
        public static string GetPatchOwners(byte stage)
        {
            if (stage == 0) return "Not isolated";
            MethodBase method = stage == 1 ? MapPreTick : stage == 2 ? TickListTick : MapPostTick;
            Patches patches = Harmony.GetPatchInfo(method);
            if (patches == null || patches.Owners == null || patches.Owners.Count == 0)
                return "None recorded";

            StringBuilder names = new StringBuilder(96);
            int listed = 0;
            int additional = 0;
            foreach (string owner in patches.Owners)
            {
                if (string.Equals(owner, WatcherOwner, System.StringComparison.OrdinalIgnoreCase)) continue;
                if (listed >= 4) { additional++; continue; }

                string name = owner;
                var mods = LoadedModManager.RunningModsListForReading;
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
            return listed == 0 ? "None recorded" : names.ToString();
        }
    }
}

using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace Watcher
{
    // Measure only a few broad tick stages; these identify where time was spent, not which mod caused it.
    [HarmonyPatch]
    internal static class TickStageInstrumentation
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Map), "MapPreTick");
            yield return AccessTools.Method(typeof(TickList), "Tick");
            yield return AccessTools.Method(typeof(Map), "MapPostTick");
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
    }
}

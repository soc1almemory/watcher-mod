using System;
using System.Diagnostics;
using HarmonyLib;
using Verse;

namespace Watcher
{
    [HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
    internal static class TickInstrumentation
    {
        [ThreadStatic] private static Stopwatch timer;

        [HarmonyPrefix]
        private static void Prefix()
        {
            if (timer == null) timer = new Stopwatch();
            timer.Restart();
        }

        [HarmonyPostfix]
        private static void Postfix()
        {
            timer.Stop();
            PerformanceData.TickFinished((float)timer.Elapsed.TotalMilliseconds);
        }
    }
}

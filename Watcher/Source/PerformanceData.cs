using System;
using UnityEngine;
using Verse;

namespace Watcher
{
    internal struct Sample
    {
        public float FrameMs;
        public float TickMs;
        public float Tps;
        public float Fps;
        public long UtcTicks;
    }

    internal sealed class WatcherEvent
    {
        public DateTime Time;
        public string Source;
        public string PatchedBy;
        public string StageMethod;
        public string GameSpeed;
        public float StageDurationMs;
        public float DurationMs;
        public float StageSharePercent;
        public int StageCallCount;
        public int PawnCount;
        public int MapCount;
        public bool Critical;
        private string diagnosticText;

        public string GetDiagnosticText()
        {
            if (diagnosticText != null) return diagnosticText;
            var text = new System.Text.StringBuilder(320);
            text.Append("Tick duration: ").Append(DurationMs.ToString("F1")).Append(" ms. ");
            if (StageMethod == null)
                text.AppendLine("No measured broad phase stood out; important work may be outside Watcher's current phase hooks.");
            else
                text.Append(Source).Append(" was the slowest measured phase: ").Append(StageDurationMs.ToString("F1"))
                    .Append(" ms across ").Append(StageCallCount).Append(" invocation(s), about ").Append(StageSharePercent.ToString("F0"))
                    .AppendLine("% of the whole tick. This phase timing includes all code running inside it.");

            text.Append("Colony snapshot: ").Append(PawnCount).Append(" spawned pawns across ").Append(MapCount).Append(" map(s)");
            if (!string.IsNullOrEmpty(GameSpeed)) text.Append("; game speed ").Append(GameSpeed);
            text.AppendLine(".");
            if (StageMethod == null)
                text.Append("Direct patch owners: none available for an unisolated phase.");
            else if (string.IsNullOrEmpty(PatchedBy))
                text.Append("Direct patch evidence: no loaded mod Harmony patch was found on ").Append(StageMethod)
                    .Append(". Mods may still affect called methods or increase the amount of work indirectly.");
            else
                text.Append("Patch context on ").Append(StageMethod).Append(": ").Append(PatchedBy)
                    .Append(". These mods patch this method; the timing does not show which patch consumed the time.");
            diagnosticText = text.ToString();
            return diagnosticText;
        }
    }

    internal sealed class CircularSamples
    {
        private readonly Sample[] data = new Sample[1500];
        private int next;
        private int count;

        public int Count => count;
        public Sample At(int index) => data[(next - count + index + data.Length) % data.Length];

        public void Clear()
        {
            next = 0;
            count = 0;
        }

        public void Add(Sample value)
        {
            data[next] = value;
            next = (next + 1) % data.Length;
            if (count < data.Length) count++;
        }
    }

    // Fixed storage avoids shifting the whole event list whenever the oldest entry expires.
    internal sealed class EventBuffer
    {
        private const int Capacity = 500;
        private readonly WatcherEvent[] data = new WatcherEvent[Capacity];
        private int first;
        private int count;

        public int Count => count;
        public WatcherEvent this[int index] => data[(first + index) % Capacity];

        public void Clear()
        {
            Array.Clear(data, 0, data.Length);
            first = 0;
            count = 0;
        }

        public void Add(WatcherEvent item, int limit)
        {
            limit = Mathf.Clamp(limit, 1, Capacity);
            Trim(limit);
            if (count == limit)
            {
                data[first] = item;
                first = (first + 1) % Capacity;
                return;
            }

            data[(first + count) % Capacity] = item;
            count++;
        }

        public void Trim(int limit)
        {
            limit = Mathf.Clamp(limit, 1, Capacity);
            while (count > limit)
            {
                data[first] = null;
                first = (first + 1) % Capacity;
                count--;
            }
        }
    }

    internal static class PerformanceData
    {
        private const int TickWindowCapacity = 1200;
        private static readonly float[] recentTickDurations = new float[TickWindowCapacity];
        private static readonly float[] tickPercentileScratch = new float[TickWindowCapacity];
        private static int nextTickDuration;
        private static int tickDurationCount;

        public static readonly CircularSamples History = new CircularSamples();
        public static readonly EventBuffer Events = new EventBuffer();
        public static float LastTickMs;
        public static float AverageTickMs;
        public static float MaxTickMs;
        public static float P95TickMs;
        public static float Fps;
        public static float Tps;
        public static float FrameMs;
        public static long LongTickCount;
        public static int Gc0;
        public static int Gc1;
        public static int Gc2;
        public static int Pawns;
        public static int Maps;
        public static long ManagedMemoryBytes;
        public static float LargestStageMs;
        public static byte LargestStage;
        private static readonly float[] stageDurations = new float[4];
        private static readonly int[] stageCallCounts = new int[4];
        private static double tickTotal;
        private static int tickSamples;
        private static int frameCounter;
        private static float tpsElapsed;
        private static int tpsTicks;
        private static float sampleElapsed;
        private static float graphElapsed;
        private static float stateElapsed;
        private static int gc0AtReset;
        private static int gc1AtReset;
        private static int gc2AtReset;

        public static void Reset()
        {
            DiagnosticCapture.Reset();
            History.Clear();
            Events.Clear();
            LastTickMs = AverageTickMs = MaxTickMs = Fps = Tps = FrameMs = 0f;
            P95TickMs = 0f;
            LongTickCount = 0;
            LargestStageMs = 0f;
            LargestStage = 0;
            Array.Clear(stageDurations, 0, stageDurations.Length);
            Array.Clear(stageCallCounts, 0, stageCallCounts.Length);
            tickTotal = 0;
            tickSamples = 0;
            nextTickDuration = 0;
            tickDurationCount = 0;
            frameCounter = 0;
            tpsElapsed = sampleElapsed = graphElapsed = stateElapsed = 0f;
            tpsTicks = 0;
            gc0AtReset = GC.CollectionCount(0);
            gc1AtReset = GC.CollectionCount(1);
            gc2AtReset = GC.CollectionCount(2);
            Gc0 = Gc1 = Gc2 = 0;
            Pawns = Maps = 0;
            ManagedMemoryBytes = GC.GetTotalMemory(false);
            UpdateStateSnapshot();
        }

        public static void BeginTick()
        {
            LargestStageMs = 0f;
            LargestStage = 0;
            Array.Clear(stageDurations, 0, stageDurations.Length);
            Array.Clear(stageCallCounts, 0, stageCallCounts.Length);
        }

        public static void RecordStage(byte stage, float milliseconds)
        {
            if (stage <= 0 || stage >= stageDurations.Length) return;
            stageDurations[stage] += milliseconds;
            stageCallCounts[stage]++;
            if (stageDurations[stage] > LargestStageMs)
            {
                LargestStageMs = stageDurations[stage];
                LargestStage = stage;
            }
        }

        public static void TickFinished(float milliseconds)
        {
            LastTickMs = milliseconds;
            tickTotal += milliseconds;
            tickSamples++;
            AverageTickMs = (float)(tickTotal / tickSamples);
            if (milliseconds > MaxTickMs) MaxTickMs = milliseconds;
            recentTickDurations[nextTickDuration] = milliseconds;
            nextTickDuration = (nextTickDuration + 1) % TickWindowCapacity;
            if (tickDurationCount < TickWindowCapacity) tickDurationCount++;
            tpsTicks++;
            float longThreshold = WatcherMod.Settings?.longTickMs ?? 20f;
            if (milliseconds >= longThreshold)
            {
                LongTickCount++;
                AddEvent(milliseconds);
            }
        }

        private static void AddEvent(float duration)
        {
            int limit = WatcherMod.Settings?.maxEvents ?? 100;
            string source;
            switch (LargestStage)
            {
                case 1: source = "Map pre-tick"; break;
                case 2: source = "Thing/pawn batch"; break;
                case 3: source = "Map post-tick"; break;
                default: source = "No phase isolated"; break;
            }
            string patchedBy = TickStageInstrumentation.GetPatchOwners(LargestStage);
            bool critical = duration >= (WatcherMod.Settings?.criticalTickMs ?? 50f);
            float stageShare = duration > 0f ? Mathf.Clamp01(LargestStageMs / duration) * 100f : 0f;
            int stageCalls = LargestStage > 0 ? stageCallCounts[LargestStage] : 0;
            string stageMethod = LargestStage == 1 ? "Map.MapPreTick" : LargestStage == 2 ? "TickList.Tick" : LargestStage == 3 ? "Map.MapPostTick" : null;
            Events.Add(new WatcherEvent
            {
                Time = DateTime.Now,
                Source = source,
                PatchedBy = patchedBy,
                StageMethod = stageMethod,
                GameSpeed = Find.TickManager?.CurTimeSpeed.ToString(),
                StageDurationMs = LargestStageMs,
                StageSharePercent = stageShare,
                StageCallCount = stageCalls,
                PawnCount = Pawns,
                MapCount = Maps,
                DurationMs = duration,
                Critical = critical,
            }, limit);
            if (critical) DiagnosticCapture.AutoStartAfterCriticalTick();
        }

        public static void FrameUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            DiagnosticCapture.Update(dt);
            if (dt > 0f)
            {
                frameCounter++;
                sampleElapsed += dt;
                tpsElapsed += dt;
                FrameMs = dt * 1000f;
                if (tpsElapsed >= 1f)
                {
                    Tps = tpsTicks / tpsElapsed;
                    Fps = frameCounter / sampleElapsed;
                    frameCounter = 0; tpsTicks = 0;
                    sampleElapsed = 0f; tpsElapsed = 0f;
                }
            }

            stateElapsed += dt;
            if (stateElapsed >= 1f)
            {
                stateElapsed = 0f;
                Gc0 = GC.CollectionCount(0) - gc0AtReset;
                Gc1 = GC.CollectionCount(1) - gc1AtReset;
                Gc2 = GC.CollectionCount(2) - gc2AtReset;
                ManagedMemoryBytes = GC.GetTotalMemory(false);
                UpdateP95TickDuration();
                UpdateStateSnapshot();
            }

            if (FrameMs > 0f)
            {
                graphElapsed += dt;
                if (graphElapsed >= 0.25f)
                {
                    History.Add(new Sample { FrameMs = FrameMs, TickMs = LastTickMs, Fps = Fps, Tps = Tps, UtcTicks = DateTime.UtcNow.Ticks });
                    graphElapsed = 0f;
                }
            }
            Events.Trim(WatcherMod.Settings?.maxEvents ?? 100);
        }

        public static void ClearEvents() => Events.Clear();

        private static void UpdateStateSnapshot()
        {
            Pawns = 0;
            Maps = 0;
            if (Current.Game == null || Find.Maps == null) return;

            Maps = Find.Maps.Count;
            for (int i = 0; i < Find.Maps.Count; i++)
                Pawns += Find.Maps[i].mapPawns?.AllPawnsSpawned?.Count ?? 0;
        }

        private static void UpdateP95TickDuration()
        {
            if (tickDurationCount == 0)
            {
                P95TickMs = 0f;
                return;
            }

            Array.Copy(recentTickDurations, tickPercentileScratch, tickDurationCount);
            Array.Sort(tickPercentileScratch, 0, tickDurationCount);
            int percentileIndex = (int)Math.Ceiling(tickDurationCount * 0.95) - 1;
            P95TickMs = tickPercentileScratch[percentileIndex];
        }
    }
}

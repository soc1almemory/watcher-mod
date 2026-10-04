using System;
using System.Collections.Generic;
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
        public float StageDurationMs;
        public float DurationMs;
        public bool Critical;
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

    internal static class PerformanceData
    {
        public static readonly CircularSamples History = new CircularSamples();
        public static readonly List<WatcherEvent> Events = new List<WatcherEvent>(100);
        public static float LastTickMs;
        public static float AverageTickMs;
        public static float MaxTickMs;
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
        private static double tickTotal;
        private static int tickSamples;
        private static int frameCounter;
        private static float tpsElapsed;
        private static int tpsTicks;
        private static float sampleElapsed;
        private static float graphElapsed;
        private static float stateElapsed;

        public static void Reset()
        {
            History.Clear();
            Events.Clear();
            LastTickMs = AverageTickMs = MaxTickMs = Fps = Tps = FrameMs = 0f;
            LongTickCount = 0;
            LargestStageMs = 0f;
            LargestStage = 0;
            tickTotal = 0;
            tickSamples = 0;
            frameCounter = 0;
            tpsElapsed = sampleElapsed = graphElapsed = stateElapsed = 0f;
            tpsTicks = 0;
            Gc0 = GC.CollectionCount(0); Gc1 = GC.CollectionCount(1); Gc2 = GC.CollectionCount(2);
        }

        public static void BeginTick()
        {
            LargestStageMs = 0f;
            LargestStage = 0;
        }

        public static void RecordStage(byte stage, float milliseconds)
        {
            if (milliseconds > LargestStageMs)
            {
                LargestStageMs = milliseconds;
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
            if (Events.Count >= limit) Events.RemoveAt(0);
            string source;
            switch (LargestStage)
            {
                case 1: source = "Map pre-tick systems"; break;
                case 2: source = "Thing and pawn tick batch"; break;
                case 3: source = "Map post-tick systems"; break;
                default: source = "No slow measured stage isolated"; break;
            }
            Events.Add(new WatcherEvent
            {
                Time = DateTime.Now,
                Source = source,
                StageDurationMs = LargestStageMs,
                DurationMs = duration,
                Critical = duration >= (WatcherMod.Settings?.criticalTickMs ?? 50f)
            });
        }

        public static void FrameUpdate()
        {
            float dt = Time.unscaledDeltaTime;
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
                Gc0 = GC.CollectionCount(0); Gc1 = GC.CollectionCount(1); Gc2 = GC.CollectionCount(2);
                ManagedMemoryBytes = GC.GetTotalMemory(false);
                if (Current.Game != null)
                {
                    Pawns = 0;
                    if (Find.Maps != null)
                    {
                        Maps = Find.Maps.Count;
                        for (int i = 0; i < Find.Maps.Count; i++) Pawns += Find.Maps[i].mapPawns?.AllPawnsSpawned?.Count ?? 0;
                    }
                    else Maps = 0;
                }
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
            PruneEvents();
        }

        private static void PruneEvents()
        {
            int limit = WatcherMod.Settings?.maxEvents ?? 100;
            while (Events.Count > limit) Events.RemoveAt(0);
        }

        public static void ClearEvents() => Events.Clear();
    }
}

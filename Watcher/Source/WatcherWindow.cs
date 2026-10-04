using System;
using UnityEngine;
using Verse;

namespace Watcher
{
    public sealed class WatcherWindow : Window
    {
        public static WatcherWindow Instance { get; private set; }
        private Vector2 eventScroll;
        private bool showFps = true;
        private bool showTps = true;
        private bool showTick = true;
        private bool showFrame = true;

        public WatcherWindow()
        {
            Instance = this;
            forcePause = false;
            doCloseX = true;
            absorbInputAroundWindow = false;
            closeOnClickedOutside = false;
            draggable = true;
            resizeable = true;
            windowRect = new Rect(120f, 80f, 680f, 460f);
            preventCameraMotion = false;
        }

        public override Vector2 InitialSize => new Vector2(680f, 460f);

        public override void PreClose()
        {
            if (Instance == this) Instance = null;
            base.PreClose();
        }

        public override void DoWindowContents(Rect inRect)
        {
            Rect header = new Rect(inRect.x, inRect.y, inRect.width, 25f);
            Text.Font = GameFont.Small;
            Widgets.Label(header, "WATCHER  <color=#999999>PERFORMANCE</color>");

            Rect metrics = new Rect(inRect.x, inRect.y + 29f, inRect.width, 72f);
            Widgets.DrawMenuSection(metrics);
            float col = (metrics.width - 20f) / 5f;
            Metric(new Rect(metrics.x + 8f + col * 0, metrics.y + 5f, col, 29f), "FPS", PerformanceData.Fps.ToString("F1"));
            Metric(new Rect(metrics.x + 8f + col * 1, metrics.y + 5f, col, 29f), "TPS", PerformanceData.Tps.ToString("F1"));
            Metric(new Rect(metrics.x + 8f + col * 2, metrics.y + 5f, col, 29f), "TICK NOW / AVG", PerformanceData.LastTickMs.ToString("F1") + " / " + PerformanceData.AverageTickMs.ToString("F1") + " ms");
            Metric(new Rect(metrics.x + 8f + col * 3, metrics.y + 5f, col, 29f), "FRAME", PerformanceData.FrameMs.ToString("F1") + " ms");
            Metric(new Rect(metrics.x + 8f + col * 4, metrics.y + 5f, col, 29f), "MANAGED HEAP", (PerformanceData.ManagedMemoryBytes / 1048576f).ToString("F0") + " MB");
            Metric(new Rect(metrics.x + 8f + col * 0, metrics.y + 38f, col, 29f), "PAWNS / MAPS", PerformanceData.Pawns + " / " + PerformanceData.Maps);
            Metric(new Rect(metrics.x + 8f + col * 1, metrics.y + 38f, col, 29f), "GAME SPEED", Find.TickManager?.CurTimeSpeed.ToString() ?? "—");
            Metric(new Rect(metrics.x + 8f + col * 2, metrics.y + 38f, col, 29f), "PEAK TICK", PerformanceData.MaxTickMs.ToString("F1") + " ms");
            Metric(new Rect(metrics.x + 8f + col * 3, metrics.y + 38f, col, 29f), "LONG TICKS", PerformanceData.LongTickCount.ToString());
            Metric(new Rect(metrics.x + 8f + col * 4, metrics.y + 38f, col, 29f), "GC 0 / 1 / 2", PerformanceData.Gc0 + " / " + PerformanceData.Gc1 + " / " + PerformanceData.Gc2);

            Rect graph = new Rect(inRect.x, metrics.yMax + 6f, inRect.width, 145f);
            Widgets.DrawMenuSection(graph);
            Widgets.Label(new Rect(graph.x + 8, graph.y + 4, 145, 18), "HISTORY  ·  " + WatcherMod.Settings.historySeconds + " s");
            Rect toggles = new Rect(graph.x + 168, graph.y + 2, graph.width - 176, 20);
            Widgets.CheckboxLabeled(new Rect(toggles.x, toggles.y, 53, 20), "FPS", ref showFps);
            Widgets.CheckboxLabeled(new Rect(toggles.x + 54, toggles.y, 53, 20), "TPS", ref showTps);
            Widgets.CheckboxLabeled(new Rect(toggles.x + 108, toggles.y, 58, 20), "Tick", ref showTick);
            Widgets.CheckboxLabeled(new Rect(toggles.x + 166, toggles.y, 62, 20), "Frame", ref showFrame);
            DrawGraph(new Rect(graph.x + 8, graph.y + 23, graph.width - 16, graph.height - 30));

            Rect events = new Rect(inRect.x, graph.yMax + 6f, inRect.width, inRect.yMax - graph.yMax - 12f);
            Widgets.DrawMenuSection(events);
            Widgets.Label(new Rect(events.x + 8, events.y + 4, events.width - 100, 18), "SLOW TICK DETAILS  ·  largest measured stage, not proven cause");
            if (Widgets.ButtonText(new Rect(events.xMax - 72, events.y + 3, 64, 20), "Clear")) PerformanceData.ClearEvents();
            Rect list = new Rect(events.x + 6, events.y + 25, events.width - 12, events.height - 29);
            float contentHeight = PerformanceData.Events.Count * 22f;
            Rect view = new Rect(0, 0, list.width - 16, Mathf.Max(list.height, contentHeight));
            Widgets.BeginScrollView(list, ref eventScroll, view);
            for (int i = PerformanceData.Events.Count - 1; i >= 0; i--)
            {
                WatcherEvent item = PerformanceData.Events[i];
                float y = (PerformanceData.Events.Count - 1 - i) * 22f;
                Rect row = new Rect(0, y, view.width, 21f);
                if (i % 2 == 0) Widgets.DrawLightHighlight(row);
                GUI.color = item.Critical ? new Color(1f, .45f, .35f) : Color.white;
                Widgets.Label(new Rect(row.x + 4, row.y, 56, row.height), item.Time.ToString("HH:mm:ss"));
                Widgets.Label(new Rect(row.x + 64, row.y, 68, row.height), item.Critical ? "CRITICAL" : "WARNING");
                Widgets.Label(new Rect(row.x + 140, row.y, 260, row.height), item.Source);
                Widgets.Label(new Rect(row.x + 408, row.y, 82, row.height), item.StageDurationMs.ToString("F1") + " ms stage");
                Widgets.Label(new Rect(row.x + 500, row.y, 82, row.height), item.DurationMs.ToString("F1") + " ms tick");
                GUI.color = Color.white;
            }
            Widgets.EndScrollView();
        }

        private static void Metric(Rect rect, string label, string value)
        {
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = new Color(.65f, .7f, .75f);
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 16), label);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(rect.x, rect.y + 15, rect.width, rect.height - 15), value);
            Text.Font = GameFont.Small;
        }

        private void DrawGraph(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(.055f, .06f, .07f));
            int count = PerformanceData.History.Count;
            if (count < 2) return;
            long newest = PerformanceData.History.At(count - 1).UtcTicks;
            long cutoff = newest - (long)(WatcherMod.Settings.historySeconds * TimeSpan.TicksPerSecond);
            int first = 0;
            while (first < count - 1 && PerformanceData.History.At(first).UtcTicks < cutoff) first++;
            float maxMs = 50f;
            for (int i = first; i < count; i++)
            {
                Sample s = PerformanceData.History.At(i);
                maxMs = Mathf.Max(maxMs, s.FrameMs, s.TickMs);
            }
            float maxFps = 60f;
            float maxTps = 60f;
            for (int i = first; i < count; i++)
            {
                Sample s = PerformanceData.History.At(i);
                maxFps = Mathf.Max(maxFps, s.Fps);
                maxTps = Mathf.Max(maxTps, s.Tps);
            }
            DrawSeries(rect, first, count, maxMs, s => s.FrameMs, new Color(.35f, .7f, 1f), showFrame);
            DrawSeries(rect, first, count, maxMs, s => s.TickMs, new Color(1f, .65f, .25f), showTick);
            DrawSeries(rect, first, count, maxMs, s => s.Fps, new Color(.35f, .85f, .55f), showFps, maxFps);
            DrawSeries(rect, first, count, maxMs, s => s.Tps, new Color(.8f, .5f, .95f), showTps, maxTps);
        }

        private static void DrawSeries(Rect rect, int first, int count, float max, System.Func<Sample, float> selector, Color color, bool enabled, float scale = -1f)
        {
            if (!enabled) return;
            int length = count - first;
            if (length < 2) return;
            Vector2 prev = default(Vector2);
            for (int i = 0; i < length; i++)
            {
                float val = selector(PerformanceData.History.At(first + i));
                float normalized = scale > 0f ? Mathf.Clamp01(val / scale) : Mathf.Clamp01(val / max);
                Vector2 point = new Vector2(rect.x + rect.width * i / (length - 1), rect.yMax - normalized * rect.height);
                if (i > 0) Widgets.DrawLine(prev, point, color, 2f);
                prev = point;
            }
        }
    }
}

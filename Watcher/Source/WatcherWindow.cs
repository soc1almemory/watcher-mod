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
            windowRect = new Rect(180f, 100f, 790f, 620f);
            preventCameraMotion = false;
        }

        public override Vector2 InitialSize => new Vector2(790f, 620f);

        public override void PreClose()
        {
            if (Instance == this) Instance = null;
            base.PreClose();
        }

        public override void DoWindowContents(Rect inRect)
        {
            Rect header = new Rect(inRect.x, inRect.y, inRect.width, 32f);
            Text.Font = GameFont.Medium;
            Widgets.Label(header, "WATCHER  <color=#999999>PERFORMANCE</color>");
            Text.Font = GameFont.Small;

            Rect metrics = new Rect(inRect.x, inRect.y + 38f, inRect.width, 105f);
            Widgets.DrawMenuSection(metrics);
            float col = metrics.width / 5f;
            Metric(new Rect(metrics.x + col * 0, metrics.y + 10, col, 38), "FPS", PerformanceData.Fps.ToString("F1"));
            Metric(new Rect(metrics.x + col * 1, metrics.y + 10, col, 38), "TPS", PerformanceData.Tps.ToString("F1"));
            Metric(new Rect(metrics.x + col * 2, metrics.y + 10, col, 38), "TICK / AVG", PerformanceData.LastTickMs.ToString("F1") + " / " + PerformanceData.AverageTickMs.ToString("F1") + " ms");
            Metric(new Rect(metrics.x + col * 3, metrics.y + 10, col, 38), "FRAME", PerformanceData.FrameMs.ToString("F1") + " ms");
            Metric(new Rect(metrics.x + col * 4, metrics.y + 10, col, 38), "MANAGED HEAP", (PerformanceData.ManagedMemoryBytes / 1048576f).ToString("F0") + " MB");
            Metric(new Rect(metrics.x + 8, metrics.y + 58, col * 1.25f, 34), "PAWNS / MAPS", PerformanceData.Pawns + " / " + PerformanceData.Maps);
            Metric(new Rect(metrics.x + col * 1.25f, metrics.y + 58, col * 1.25f, 34), "GAME SPEED", Find.TickManager?.CurTimeSpeed.ToString() ?? "—");
            Metric(new Rect(metrics.x + col * 2.6f, metrics.y + 58, col * 1.1f, 34), "MAX TICK", PerformanceData.MaxTickMs.ToString("F1") + " ms");
            Metric(new Rect(metrics.x + col * 3.75f, metrics.y + 58, col * 1.1f, 34), "LONG TICKS", PerformanceData.LongTickCount.ToString());
            Metric(new Rect(metrics.x + col * 4.55f, metrics.y + 58, col * .4f, 34), "GC", PerformanceData.Gc0 + "/" + PerformanceData.Gc1 + "/" + PerformanceData.Gc2);

            Rect graph = new Rect(inRect.x, metrics.yMax + 8f, inRect.width, 215f);
            Widgets.DrawMenuSection(graph);
            Widgets.Label(new Rect(graph.x + 10, graph.y + 5, 150, 22), "PERFORMANCE HISTORY");
            Rect toggles = new Rect(graph.x + 240, graph.y + 5, graph.width - 250, 22);
            Widgets.CheckboxLabeled(new Rect(toggles.x, toggles.y, 55, 22), "FPS", ref showFps);
            Widgets.CheckboxLabeled(new Rect(toggles.x + 58, toggles.y, 55, 22), "TPS", ref showTps);
            Widgets.CheckboxLabeled(new Rect(toggles.x + 116, toggles.y, 58, 22), "Tick", ref showTick);
            Widgets.CheckboxLabeled(new Rect(toggles.x + 178, toggles.y, 66, 22), "Frame", ref showFrame);
            DrawGraph(new Rect(graph.x + 12, graph.y + 32, graph.width - 24, graph.height - 42));

            Rect events = new Rect(inRect.x, graph.yMax + 8f, inRect.width, inRect.yMax - graph.yMax - 14f);
            Widgets.DrawMenuSection(events);
            Widgets.Label(new Rect(events.x + 10, events.y + 5, events.width - 120, 24), "RECENT LONG TICKS  ·  source: game tick");
            if (Widgets.ButtonText(new Rect(events.xMax - 90, events.y + 4, 78, 24), "Clear")) PerformanceData.ClearEvents();
            Rect list = new Rect(events.x + 8, events.y + 32, events.width - 16, events.height - 38);
            float contentHeight = PerformanceData.Events.Count * 27f;
            Rect view = new Rect(0, 0, list.width - 16, Mathf.Max(list.height, contentHeight));
            Widgets.BeginScrollView(list, ref eventScroll, view);
            for (int i = PerformanceData.Events.Count - 1; i >= 0; i--)
            {
                WatcherEvent item = PerformanceData.Events[i];
                float y = (PerformanceData.Events.Count - 1 - i) * 27f;
                Rect row = new Rect(0, y, view.width, 25f);
                if (i % 2 == 0) Widgets.DrawLightHighlight(row);
                GUI.color = item.Critical ? new Color(1f, .45f, .35f) : Color.white;
                Widgets.Label(new Rect(row.x + 6, row.y, 75, row.height), item.Time.ToString("HH:mm:ss"));
                Widgets.Label(new Rect(row.x + 86, row.y, 185, row.height), item.Critical ? "CRITICAL LONG TICK" : "LONG TICK");
                Widgets.Label(new Rect(row.x + 275, row.y, 300, row.height), item.Source);
                Widgets.Label(new Rect(row.x + 585, row.y, 125, row.height), item.DurationMs.ToString("F1") + " ms");
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
            Text.Font = GameFont.Medium;
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
            DrawSeries(rect, first, count, maxMs, s => s.FrameMs, new Color(.35f, .7f, 1f), showFrame);
            DrawSeries(rect, first, count, maxMs, s => s.TickMs, new Color(1f, .65f, .25f), showTick);
            DrawSeries(rect, first, count, maxMs, s => s.Fps, new Color(.35f, .85f, .55f), showFps, 60f);
            DrawSeries(rect, first, count, maxMs, s => s.Tps, new Color(.8f, .5f, .95f), showTps, 60f);
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

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
            resizeable = false;
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
            Widgets.Label(header, "WATCHER");

            Rect metrics = new Rect(inRect.x, inRect.y + 29f, inRect.width, 96f);
            Widgets.DrawMenuSection(metrics);
            float col = (metrics.width - 20f) / 5f;
            Metric(new Rect(metrics.x + 8f + col * 0, metrics.y + 5f, col, 40f), "FPS", PerformanceData.Fps.ToString("F1"));
            Metric(new Rect(metrics.x + 8f + col * 1, metrics.y + 5f, col, 40f), "TPS", PerformanceData.Tps.ToString("F1"));
            Metric(new Rect(metrics.x + 8f + col * 2, metrics.y + 5f, col, 40f), "TICK NOW / AVG", PerformanceData.LastTickMs.ToString("F1") + " / " + PerformanceData.AverageTickMs.ToString("F1") + " ms");
            Metric(new Rect(metrics.x + 8f + col * 3, metrics.y + 5f, col, 40f), "FRAME", PerformanceData.FrameMs.ToString("F1") + " ms");
            Metric(new Rect(metrics.x + 8f + col * 4, metrics.y + 5f, col, 40f), "MANAGED HEAP", (PerformanceData.ManagedMemoryBytes / 1048576f).ToString("F0") + " MB");
            Metric(new Rect(metrics.x + 8f + col * 0, metrics.y + 50f, col, 40f), "PAWNS / MAPS", PerformanceData.Pawns + " / " + PerformanceData.Maps);
            Metric(new Rect(metrics.x + 8f + col * 1, metrics.y + 50f, col, 40f), "GAME SPEED", Find.TickManager?.CurTimeSpeed.ToString() ?? "—");
            Metric(new Rect(metrics.x + 8f + col * 2, metrics.y + 50f, col, 40f), "PEAK TICK", PerformanceData.MaxTickMs.ToString("F1") + " ms");
            Metric(new Rect(metrics.x + 8f + col * 3, metrics.y + 50f, col, 40f), "LONG TICKS", PerformanceData.LongTickCount.ToString());
            Metric(new Rect(metrics.x + 8f + col * 4, metrics.y + 50f, col, 40f), "GC 0 / 1 / 2", PerformanceData.Gc0 + " / " + PerformanceData.Gc1 + " / " + PerformanceData.Gc2);

            Rect graph = new Rect(inRect.x, metrics.yMax + 6f, inRect.width, 151f);
            Widgets.DrawMenuSection(graph);
            Widgets.Label(new Rect(graph.x + 8, graph.y + 3, 145, 22), "HISTORY  ·  " + WatcherMod.Settings.historySeconds + " s");
            float legendX = graph.x + 160f;
            float legendY = graph.y + 2f;
            showFps = DrawLegend(new Rect(legendX, legendY, 58f, 24f), "FPS", new Color(.35f, .85f, .55f), showFps);
            showTps = DrawLegend(new Rect(legendX + 61f, legendY, 58f, 24f), "TPS", new Color(.8f, .5f, .95f), showTps);
            showTick = DrawLegend(new Rect(legendX + 122f, legendY, 62f, 24f), "Tick", new Color(1f, .65f, .25f), showTick);
            showFrame = DrawLegend(new Rect(legendX + 187f, legendY, 74f, 24f), "Frame", new Color(.35f, .7f, 1f), showFrame);
            DrawGraph(new Rect(graph.x + 8, graph.y + 29, graph.width - 16, graph.height - 36));

            Rect events = new Rect(inRect.x, graph.yMax + 6f, inRect.width, inRect.yMax - graph.yMax - 12f);
            Widgets.DrawMenuSection(events);
            Widgets.Label(new Rect(events.x + 8, events.y + 3, events.width - 100, 24), "SLOW TICK MONITORING");
            if (Widgets.ButtonText(new Rect(events.xMax - 72, events.y + 2, 64, 22), "Clear")) PerformanceData.ClearEvents();
            Rect list = new Rect(events.x + 6, events.y + 28, events.width - 12, events.height - 32);
            float contentHeight = PerformanceData.Events.Count * 23f;
            Rect view = new Rect(0, 0, list.width - 16, Mathf.Max(list.height, contentHeight));
            Widgets.BeginScrollView(list, ref eventScroll, view);
            for (int i = PerformanceData.Events.Count - 1; i >= 0; i--)
            {
                WatcherEvent item = PerformanceData.Events[i];
                float y = (PerformanceData.Events.Count - 1 - i) * 23f;
                Rect row = new Rect(0, y, view.width, 22f);
                if (i % 2 == 0) Widgets.DrawLightHighlight(row);
                GUI.color = item.Critical ? new Color(1f, .45f, .35f) : Color.white;
                Widgets.Label(new Rect(row.x + 4, row.y, 54, row.height), item.Time.ToString("HH:mm:ss"));
                Widgets.Label(new Rect(row.x + 62, row.y, 48, row.height), item.Critical ? "CRIT" : "WARN");
                Widgets.Label(new Rect(row.x + 116, row.y, 122, row.height), item.Source);
                Rect patchRect = new Rect(row.x + 244, row.y, 192, row.height);
                Widgets.Label(patchRect, item.PatchedBy ?? "—");
                if (!string.IsNullOrEmpty(item.PatchTooltip))
                    TooltipHandler.TipRegion(patchRect, new TipSignal(item.PatchTooltip, item.GetHashCode()));
                Widgets.Label(new Rect(row.x + 442, row.y, 74, row.height), item.StageDurationMs.ToString("F1") + " part");
                Widgets.Label(new Rect(row.x + 522, row.y, 84, row.height), item.DurationMs.ToString("F1") + " total");
                GUI.color = Color.white;
            }
            Widgets.EndScrollView();
        }

        private static void Metric(Rect rect, string label, string value)
        {
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(.65f, .7f, .75f);
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 18), label);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(rect.x, rect.y + 19, rect.width, rect.height - 19), value);
            Text.Font = GameFont.Small;
        }

        private static bool DrawLegend(Rect rect, string label, Color seriesColor, bool enabled)
        {
            if (Widgets.ButtonInvisible(rect)) enabled = !enabled;
            Color tint = enabled ? seriesColor : new Color(.42f, .44f, .46f);
            Widgets.DrawBoxSolid(new Rect(rect.x + 1f, rect.y + 10f, 13f, 3f), tint);
            GUI.color = tint;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(rect.x + 18f, rect.y, rect.width - 18f, rect.height), label);
            GUI.color = Color.white;
            return enabled;
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

using UnityEngine;
using Verse;

namespace Watcher
{
    internal sealed class DiagnosticReportWindow : Window
    {
        private readonly string report = DiagnosticCapture.BuildTextReport();
        private Vector2 scroll;

        public DiagnosticReportWindow()
        {
            doCloseX = true;
            draggable = true;
            resizeable = false;
            absorbInputAroundWindow = false;
            windowRect = new Rect(160f, 110f, 620f, 440f);
        }

        public override Vector2 InitialSize => new Vector2(620f, 440f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width - 100f, 26f), "DIAGNOSTIC CAPTURE REPORT");
            Rect body = new Rect(inRect.x, inRect.y + 30f, inRect.width, inRect.height - 72f);
            Widgets.DrawMenuSection(body);
            Rect view = new Rect(0f, 0f, body.width - 24f, 720f);
            Widgets.BeginScrollView(body, ref scroll, view);
            bool oldWordWrap = Text.WordWrap;
            Text.WordWrap = true;
            Widgets.Label(new Rect(8f, 8f, view.width - 16f, 700f), report);
            Text.WordWrap = oldWordWrap;
            Widgets.EndScrollView();
            if (Widgets.ButtonText(new Rect(inRect.xMax - 104f, inRect.yMax - 32f, 96f, 28f), "Copy report"))
                GUIUtility.systemCopyBuffer = report;
        }
    }
}

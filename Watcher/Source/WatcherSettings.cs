using UnityEngine;
using Verse;

namespace Watcher
{
    public sealed class WatcherSettings : ModSettings
    {
        public float longTickMs = 20f;
        public float criticalTickMs = 50f;
        public int maxEvents = 100;
        public int historySeconds = 60;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref longTickMs, "longTickMs", 20f);
            Scribe_Values.Look(ref criticalTickMs, "criticalTickMs", 50f);
            Scribe_Values.Look(ref maxEvents, "maxEvents", 100);
            Scribe_Values.Look(ref historySeconds, "historySeconds", 60);
            base.ExposeData();
            longTickMs = Mathf.Clamp(longTickMs, 1f, 250f);
            criticalTickMs = Mathf.Clamp(criticalTickMs, longTickMs, 1000f);
            maxEvents = Mathf.Clamp(maxEvents, 10, 500);
            historySeconds = Mathf.Clamp(historySeconds, 10, 300);
        }

        public void DoWindowContents(Rect rect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);
            listing.Label("Long tick threshold: " + longTickMs.ToString("F0") + " ms");
            longTickMs = listing.Slider(longTickMs, 5f, 100f);
            listing.Label("Critical tick threshold: " + criticalTickMs.ToString("F0") + " ms");
            criticalTickMs = listing.Slider(criticalTickMs, longTickMs, 250f);
            listing.Label("Recent event limit: " + maxEvents);
            maxEvents = (int)listing.Slider(maxEvents, 10, 500);
            listing.Label("Graph history: " + historySeconds + " seconds");
            historySeconds = (int)listing.Slider(historySeconds, 10, 300);
            listing.End();
        }
    }
}

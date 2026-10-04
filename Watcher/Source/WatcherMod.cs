using HarmonyLib;
using Verse;

namespace Watcher
{
    [StaticConstructorOnStartup]
    public sealed class WatcherMod : Mod
    {
        public static WatcherSettings Settings;
        private static readonly Harmony Harmony = new Harmony("soc1almemory.watcher.performance");

        public WatcherMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<WatcherSettings>();
            Harmony.PatchAll();
        }

        public override string SettingsCategory() => "Watcher";

        public override void DoSettingsWindowContents(UnityEngine.Rect inRect)
        {
            Settings.DoWindowContents(inRect);
        }
    }
}

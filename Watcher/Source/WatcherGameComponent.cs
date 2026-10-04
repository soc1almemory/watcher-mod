using UnityEngine;
using Verse;

namespace Watcher
{
    public sealed class WatcherGameComponent : GameComponent
    {
        public WatcherGameComponent(Game game) { }

        public override void GameComponentUpdate()
        {
            PerformanceData.FrameUpdate();
            if (!Input.GetKeyDown(KeyCode.Alpha6) && !Input.GetKeyDown(KeyCode.Keypad6)) return;
            if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift)) return;
            if (WatcherWindow.Instance != null) Find.WindowStack.TryRemove(WatcherWindow.Instance);
            else Find.WindowStack.Add(new WatcherWindow());
        }

        public override void StartedNewGame() => PerformanceData.Reset();
        public override void LoadedGame() => PerformanceData.Reset();
    }
}

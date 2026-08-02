using System;

namespace NineGrid.Flow
{
    /// <summary>
    /// 遗物栏同步入口：由 Presentation RelicHudController 注册，InBattle 调用。
    /// </summary>
    public static class RelicHudHook
    {
        public static Action SyncFromCore;
        public static Action Clear;

        public static Action WireController;

        /// <summary>#98 右键丢弃：返回是否已受理（含 IntentIntake 拒收）。</summary>
        public static Func<string, bool> TryDiscardRelic;

        public static void RequestWire()
        {
            WireController?.Invoke();
        }

        public static void RequestSync()
        {
            SyncFromCore?.Invoke();
        }

        public static void RequestClear()
        {
            Clear?.Invoke();
        }
    }
}

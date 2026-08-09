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

        /// <summary>ADR-0027：拖入回收区丢弃；返回是否已受理（含 IntentIntake 拒收）。</summary>
        public static Func<string, bool> TryDiscardRelic;

        /// <summary>ADR-0027：左键在遗物槽上开始拖动（可穿透半黑屏）。</summary>
        public static Func<UnityEngine.Camera, UnityEngine.Vector2, bool> TryBeginDragRelic;

        /// <summary>ADR-0027：右键开遗物详述（可穿透半黑屏）。</summary>
        public static Func<UnityEngine.Camera, UnityEngine.Vector2, bool> TryInspectRelic;

        /// <summary>
        /// ADR-0035：Settled 提交遗物倒计时剩余（SourceDefId=relic.*，键=装配id.键，值为剩余文本）。
        /// </summary>
        public static Action<string, string, string> CommitCountdownRemaining;

        /// <summary>ADR-0035：Settled 清除遗物倒计时投影键。</summary>
        public static Action<string, string> ClearCountdownRemaining;

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

using System;

namespace NineGrid.Flow.Tutorial
{
    /// <summary>教学局与 BattleSession 的窄装配缝：战败重开阶段时抑制常规战败收口。</summary>
    public static class TutorialBattleSessionHook
    {
        private static Func<bool> sSuppressAvatarDefeatEnd;
        private static Action sOnAvatarDefeatRestartRequested;

        public static void Bind(
            Func<bool> suppressAvatarDefeatEnd,
            Action onAvatarDefeatRestartRequested)
        {
            sSuppressAvatarDefeatEnd = suppressAvatarDefeatEnd;
            sOnAvatarDefeatRestartRequested = onAvatarDefeatRestartRequested;
        }

        public static void Clear()
        {
            sSuppressAvatarDefeatEnd = null;
            sOnAvatarDefeatRestartRequested = null;
        }

        public static bool ShouldSuppressAvatarDefeatEnd()
        {
            return sSuppressAvatarDefeatEnd != null && sSuppressAvatarDefeatEnd();
        }

        public static void RequestAvatarDefeatRestart()
        {
            sOnAvatarDefeatRestartRequested?.Invoke();
        }
    }
}

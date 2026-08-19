using System;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 导演主线由忙转闲时通知战败收口闸：组合根 / 会话注入，禁止业务旁路 Raise。
    /// </summary>
    public static class AvatarDefeatEndHook
    {
        public static Action NotifyMainlineBecameIdle;

        public static void Reset()
        {
            NotifyMainlineBecameIdle = null;
        }

        public static void RaiseMainlineBecameIdle()
        {
            NotifyMainlineBecameIdle?.Invoke();
        }
    }
}

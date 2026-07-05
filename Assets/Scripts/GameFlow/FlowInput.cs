using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

using NineGrid.UI;

namespace NineGrid.GameFlow
{
    /// <summary>流程 / 场景控制器共用的鼠标读值：新 Input System 优先，旧 Input Manager 兜底。</summary>
    static class FlowInput
    {
        /// <summary>原始左键按下（不受新手教程拦截）。</summary>
        public static bool RawPrimaryClickThisFrame()
        {
            if (Input.GetMouseButtonDown(0))
            {
                return true;
            }

#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                return true;
            }
#endif
            return false;
        }

        /// <summary>可用于推进玩法的点击：新手教程等待确认时会消耗并返回 false。</summary>
        public static bool TryGameplayClick()
        {
            if (NewbieTutorialController.TryConsumeDismissClick())
            {
                return false;
            }

            if (NewbieTutorialController.IsAwaitingDismiss)
            {
                return false;
            }

            return RawPrimaryClickThisFrame();
        }

        public static bool PrimaryClickThisFrame() => TryGameplayClick();
    }
}

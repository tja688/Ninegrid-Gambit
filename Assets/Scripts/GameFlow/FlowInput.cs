using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NineGrid.GameFlow
{
    /// <summary>流程 / 场景控制器共用的鼠标读值：新 Input System 优先，旧 Input Manager 兜底。</summary>
    static class FlowInput
    {
        public static bool PrimaryClickThisFrame()
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
    }
}

using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace NineGrid.GameFlow
{
    /// <summary>
    /// 调试热键读键：优先新 Input System，旧 Input Manager 兜底。
    /// 项目 Active Input Handling = Both，但小键盘在仅走 <see cref="Input.GetKeyDown"/> 时经常收不到，
    /// 与 <c>AnchorChainLauncher</c> 对空格的处理一致。
    /// </summary>
    static class DebugHotkeyInput
    {
        public static bool WasPressedThisFrame(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (key == KeyCode.Escape && kb.escapeKey.wasPressedThisFrame)
                {
                    return true;
                }

                var control = ResolveNumpad(kb, key);
                if (control != null && control.wasPressedThisFrame)
                {
                    return true;
                }
            }
#endif
            return Input.GetKeyDown(key);
        }

#if ENABLE_INPUT_SYSTEM
        static KeyControl ResolveNumpad(Keyboard kb, KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Keypad0: return kb.numpad0Key;
                case KeyCode.Keypad1: return kb.numpad1Key;
                case KeyCode.Keypad2: return kb.numpad2Key;
                case KeyCode.Keypad3: return kb.numpad3Key;
                case KeyCode.Keypad4: return kb.numpad4Key;
                case KeyCode.Keypad5: return kb.numpad5Key;
                case KeyCode.Keypad6: return kb.numpad6Key;
                case KeyCode.Keypad7: return kb.numpad7Key;
                case KeyCode.Keypad8: return kb.numpad8Key;
                case KeyCode.Keypad9: return kb.numpad9Key;
                default: return null;
            }
        }
#endif
    }
}

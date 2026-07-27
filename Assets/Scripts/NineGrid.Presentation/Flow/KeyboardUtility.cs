using UnityEngine;
using UnityEngine.InputSystem;

namespace NineGrid.Flow
{
    /// <summary>
    /// 键盘读口：New Input System only 下替代 <see cref="Input.GetKey"/> / GetKeyDown。
    /// Dev 绑定仍可用 <see cref="KeyCode"/>，此处映射到 Input System <see cref="Key"/>。
    /// </summary>
    public static class KeyboardUtility
    {
        public static bool GetKeyDown(KeyCode keyCode)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !TryMap(keyCode, out var key))
            {
                return false;
            }

            return keyboard[key].wasPressedThisFrame;
        }

        public static bool GetKey(KeyCode keyCode)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !TryMap(keyCode, out var key))
            {
                return false;
            }

            return keyboard[key].isPressed;
        }

        public static bool GetKeyUp(KeyCode keyCode)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !TryMap(keyCode, out var key))
            {
                return false;
            }

            return keyboard[key].wasReleasedThisFrame;
        }

        private static bool TryMap(KeyCode keyCode, out Key key)
        {
            switch (keyCode)
            {
                case KeyCode.None:
                    key = Key.None;
                    return false;

                case KeyCode.Escape:
                    key = Key.Escape;
                    return true;
                case KeyCode.Backslash:
                    key = Key.Backslash;
                    return true;
                case KeyCode.Return:
                    key = Key.Enter;
                    return true;
                case KeyCode.Space:
                    key = Key.Space;
                    return true;
                case KeyCode.Tab:
                    key = Key.Tab;
                    return true;
                case KeyCode.Backspace:
                    key = Key.Backspace;
                    return true;
                case KeyCode.Delete:
                    key = Key.Delete;
                    return true;
                case KeyCode.LeftShift:
                    key = Key.LeftShift;
                    return true;
                case KeyCode.RightShift:
                    key = Key.RightShift;
                    return true;
                case KeyCode.LeftControl:
                    key = Key.LeftCtrl;
                    return true;
                case KeyCode.RightControl:
                    key = Key.RightCtrl;
                    return true;
                case KeyCode.LeftAlt:
                    key = Key.LeftAlt;
                    return true;
                case KeyCode.RightAlt:
                    key = Key.RightAlt;
                    return true;

                case KeyCode.F1:
                    key = Key.F1;
                    return true;
                case KeyCode.F2:
                    key = Key.F2;
                    return true;
                case KeyCode.F3:
                    key = Key.F3;
                    return true;
                case KeyCode.F4:
                    key = Key.F4;
                    return true;
                case KeyCode.F5:
                    key = Key.F5;
                    return true;
                case KeyCode.F6:
                    key = Key.F6;
                    return true;
                case KeyCode.F7:
                    key = Key.F7;
                    return true;
                case KeyCode.F8:
                    key = Key.F8;
                    return true;
                case KeyCode.F9:
                    key = Key.F9;
                    return true;
                case KeyCode.F10:
                    key = Key.F10;
                    return true;
                case KeyCode.F11:
                    key = Key.F11;
                    return true;
                case KeyCode.F12:
                    key = Key.F12;
                    return true;

                case KeyCode.Alpha0:
                    key = Key.Digit0;
                    return true;
                case KeyCode.Alpha1:
                    key = Key.Digit1;
                    return true;
                case KeyCode.Alpha2:
                    key = Key.Digit2;
                    return true;
                case KeyCode.Alpha3:
                    key = Key.Digit3;
                    return true;
                case KeyCode.Alpha4:
                    key = Key.Digit4;
                    return true;
                case KeyCode.Alpha5:
                    key = Key.Digit5;
                    return true;
                case KeyCode.Alpha6:
                    key = Key.Digit6;
                    return true;
                case KeyCode.Alpha7:
                    key = Key.Digit7;
                    return true;
                case KeyCode.Alpha8:
                    key = Key.Digit8;
                    return true;
                case KeyCode.Alpha9:
                    key = Key.Digit9;
                    return true;

                case KeyCode.Keypad0:
                    key = Key.Numpad0;
                    return true;
                case KeyCode.Keypad1:
                    key = Key.Numpad1;
                    return true;
                case KeyCode.Keypad2:
                    key = Key.Numpad2;
                    return true;
                case KeyCode.Keypad3:
                    key = Key.Numpad3;
                    return true;
                case KeyCode.Keypad4:
                    key = Key.Numpad4;
                    return true;
                case KeyCode.Keypad5:
                    key = Key.Numpad5;
                    return true;
                case KeyCode.Keypad6:
                    key = Key.Numpad6;
                    return true;
                case KeyCode.Keypad7:
                    key = Key.Numpad7;
                    return true;
                case KeyCode.Keypad8:
                    key = Key.Numpad8;
                    return true;
                case KeyCode.Keypad9:
                    key = Key.Numpad9;
                    return true;
                case KeyCode.KeypadEnter:
                    key = Key.NumpadEnter;
                    return true;
                case KeyCode.KeypadPeriod:
                    key = Key.NumpadPeriod;
                    return true;
                case KeyCode.KeypadDivide:
                    key = Key.NumpadDivide;
                    return true;
                case KeyCode.KeypadMultiply:
                    key = Key.NumpadMultiply;
                    return true;
                case KeyCode.KeypadMinus:
                    key = Key.NumpadMinus;
                    return true;
                case KeyCode.KeypadPlus:
                    key = Key.NumpadPlus;
                    return true;

                case KeyCode.UpArrow:
                    key = Key.UpArrow;
                    return true;
                case KeyCode.DownArrow:
                    key = Key.DownArrow;
                    return true;
                case KeyCode.LeftArrow:
                    key = Key.LeftArrow;
                    return true;
                case KeyCode.RightArrow:
                    key = Key.RightArrow;
                    return true;

                default:
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        key = Key.A + (keyCode - KeyCode.A);
                        return true;
                    }

                    key = Key.None;
                    return false;
            }
        }
    }
}

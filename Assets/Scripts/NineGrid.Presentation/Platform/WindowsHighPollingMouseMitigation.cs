using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace NineGrid.Presentation.Platform
{
    /// <summary>
    /// 标记类型：Windows Player 高回报率鼠标兜底见条件编译实现，
    /// 以及 docs/adr/0006-windows-high-polling-mouse-mitigation.md。
    /// </summary>
    public static class WindowsHighPollingMouseMitigationInfo
    {
        public const string AdrId = "0006";
        public const string UnityIssueId = "UUM-142550";
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    /// <summary>
    /// Windows Player 高回报率鼠标兜底：RIDEV_NOLEGACY 掐掉 legacy WM_MOUSE 洪水，
    /// 再每帧用 GetCursorPos / GetAsyncKeyState 注入 Input System。
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class WindowsHighPollingMouseMitigation : MonoBehaviour
    {
        private const ushort HidUsagePageGeneric = 0x01;
        private const ushort HidUsageGenericMouse = 0x02;
        private const uint RidevNolegacy = 0x00000030;
        private const int VkLButton = 0x01;
        private const int VkRButton = 0x02;
        private const int VkMButton = 0x04;

        private Vector2 _lastPos;
        private bool _hasLastPos;
        private bool _registered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject(nameof(WindowsHighPollingMouseMitigation));
            DontDestroyOnLoad(go);
            go.AddComponent<WindowsHighPollingMouseMitigation>();
        }

        private void OnEnable()
        {
            ApplyNolegacy();
            InputSystem.onBeforeUpdate += InjectMouseState;
        }

        private void OnDisable()
        {
            InputSystem.onBeforeUpdate -= InjectMouseState;
        }

        private void ApplyNolegacy()
        {
            if (_registered)
            {
                return;
            }

            var devices = new[]
            {
                new RAWINPUTDEVICE
                {
                    usUsagePage = HidUsagePageGeneric,
                    usUsage = HidUsageGenericMouse,
                    dwFlags = RidevNolegacy,
                    hwndTarget = IntPtr.Zero,
                },
            };

            if (!RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<RAWINPUTDEVICE>()))
            {
                Debug.LogWarning(
                    "[WindowsHighPollingMouseMitigation] RegisterRawInputDevices(RIDEV_NOLEGACY) failed; "
                    + "high polling mice may still stall the main thread.");
                return;
            }

            _registered = true;
        }

        private void InjectMouseState()
        {
            if (!Application.isFocused)
            {
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            if (!TryReadClientPosition(out var pos))
            {
                return;
            }

            var delta = _hasLastPos ? pos - _lastPos : Vector2.zero;
            _lastPos = pos;
            _hasLastPos = true;

            var lmb = (GetAsyncKeyState(VkLButton) & 0x8000) != 0;
            var rmb = (GetAsyncKeyState(VkRButton) & 0x8000) != 0;
            var mmb = (GetAsyncKeyState(VkMButton) & 0x8000) != 0;

            var state = new MouseState
            {
                position = pos,
                delta = delta,
            };
            state = state.WithButton(MouseButton.Left, lmb);
            state = state.WithButton(MouseButton.Right, rmb);
            state = state.WithButton(MouseButton.Middle, mmb);
            InputSystem.QueueStateEvent(mouse, state);
        }

        private static bool TryReadClientPosition(out Vector2 unityPos)
        {
            unityPos = default;
            if (!GetCursorPos(out var point))
            {
                return false;
            }

            var hwnd = GetActiveWindow();
            if (hwnd == IntPtr.Zero)
            {
                hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            }

            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            if (!ScreenToClient(hwnd, ref point))
            {
                return false;
            }

            // Win32 client: origin top-left, Y down. Unity: origin bottom-left, Y up.
            unityPos = new Vector2(point.X, Screen.height - point.Y);
            return true;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices(
            [In] RAWINPUTDEVICE[] pRawInputDevices,
            uint uiNumDevices,
            uint cbSize);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();
    }
#endif
}

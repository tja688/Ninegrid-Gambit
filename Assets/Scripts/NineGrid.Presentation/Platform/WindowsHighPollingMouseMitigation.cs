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

        /// <summary>命令行带该参数可整体关闭 NOLEGACY + 轮询注入（打包版排除法验证用）。</summary>
        public const string DisableArg = "-ng-no-rawinput";
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    /// <summary>
    /// Windows Player 高回报率鼠标兜底：RIDEV_NOLEGACY 掐掉 legacy WM_MOUSE 洪水，
    /// 再每帧用 GetCursorPos / GetAsyncKeyState 注入 Input System。
    /// NOLEGACY 只在「窗口聚焦且光标在客户区内」时启用：legacy 消息同时承担
    /// 标题栏拖动、边框缩放、关闭按钮与点击激活（非客户区交互），常开会把窗口变成
    /// 拖不动、关不掉的死窗口。光标移出客户区或窗口失焦时切回 legacy 允许档
    /// （保持 Raw Input 注册但 dwFlags=0），窗口框架行为恢复系统默认。
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
        private bool _nolegacyActive;
        private bool _nolegacyFailureLogged;
        private bool _legacyRestoreFailureLogged;
        private IntPtr _cachedHwnd;
        private bool _mainWindowResolveAttempted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(
                        args[i],
                        WindowsHighPollingMouseMitigationInfo.DisableArg,
                        StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log("[WindowsHighPollingMouseMitigation] 已按命令行参数禁用（走引擎默认鼠标路径）。");
                    return;
                }
            }

            var go = new GameObject(nameof(WindowsHighPollingMouseMitigation));
            DontDestroyOnLoad(go);
            go.AddComponent<WindowsHighPollingMouseMitigation>();
        }

        private void OnEnable()
        {
            InputSystem.onBeforeUpdate += OnBeforeInputUpdate;
        }

        private void OnDisable()
        {
            InputSystem.onBeforeUpdate -= OnBeforeInputUpdate;
            // 退出/禁用时确保 legacy 消息已恢复，别让死窗口状态泄漏到关停路径。
            UpdateLegacySuppression(false);
        }

        private void OnBeforeInputUpdate()
        {
            var hasClientPoint = TryReadClientPosition(out var pos, out var insideClient);

            // NOLEGACY 期望态：聚焦 + 光标确在客户区内。任何一条不满足（含句柄未解析、
            // 光标在标题栏/边框/窗外）都回到 legacy 允许档，把非客户区交互还给系统。
            UpdateLegacySuppression(Application.isFocused && hasClientPoint && insideClient);

            if (!Application.isFocused || !hasClientPoint)
            {
                return;
            }

            InjectMouseState(pos);
        }

        private void UpdateLegacySuppression(bool suppress)
        {
            // 初始态 _nolegacyActive=false：首次需要 NOLEGACY 前不注册任何 Raw Input，
            // 保持引擎自身注册不被顶掉。
            if (suppress == _nolegacyActive)
            {
                return;
            }

            var devices = new[]
            {
                new RAWINPUTDEVICE
                {
                    usUsagePage = HidUsagePageGeneric,
                    usUsage = HidUsageGenericMouse,
                    dwFlags = suppress ? RidevNolegacy : 0u,
                    hwndTarget = IntPtr.Zero,
                },
            };

            if (!RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<RAWINPUTDEVICE>()))
            {
                if (suppress && !_nolegacyFailureLogged)
                {
                    _nolegacyFailureLogged = true;
                    Debug.LogWarning(
                        "[WindowsHighPollingMouseMitigation] RegisterRawInputDevices(RIDEV_NOLEGACY) failed; "
                        + "high polling mice may still stall the main thread.");
                }
                else if (!suppress && !_legacyRestoreFailureLogged)
                {
                    _legacyRestoreFailureLogged = true;
                    Debug.LogWarning(
                        "[WindowsHighPollingMouseMitigation] RegisterRawInputDevices(legacy restore) failed; "
                        + "window chrome (drag/resize/close) may stay unresponsive.");
                }

                return;
            }

            _nolegacyActive = suppress;
        }

        private void InjectMouseState(Vector2 pos)
        {
            var mouse = Mouse.current;
            if (mouse == null)
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

        private bool TryReadClientPosition(out Vector2 unityPos, out bool insideClient)
        {
            unityPos = default;
            insideClient = false;
            if (!GetCursorPos(out var point))
            {
                return false;
            }

            var hwnd = GetActiveWindow();
            if (hwnd == IntPtr.Zero)
            {
                hwnd = ResolveFallbackWindowHandle();
            }

            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            _cachedHwnd = hwnd;
            if (!ScreenToClient(hwnd, ref point))
            {
                // 句柄可能已失效（窗口重建等），丢弃缓存下帧重解析。
                _cachedHwnd = IntPtr.Zero;
                _mainWindowResolveAttempted = false;
                return false;
            }

            if (GetClientRect(hwnd, out var clientRect))
            {
                insideClient = point.X >= 0
                    && point.Y >= 0
                    && point.X < clientRect.Right
                    && point.Y < clientRect.Bottom;
            }

            // Win32 client: origin top-left, Y down. Unity: origin bottom-left, Y up.
            unityPos = new Vector2(point.X, Screen.height - point.Y);
            return true;
        }

        private IntPtr ResolveFallbackWindowHandle()
        {
            if (_cachedHwnd != IntPtr.Zero)
            {
                return _cachedHwnd;
            }

            // Process.MainWindowHandle 会枚举全系统顶层窗口，禁止每帧调用：只解析一次并缓存。
            if (_mainWindowResolveAttempted)
            {
                return IntPtr.Zero;
            }

            _mainWindowResolveAttempted = true;
            using (var process = System.Diagnostics.Process.GetCurrentProcess())
            {
                return process.MainWindowHandle;
            }
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

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
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
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();
    }
#endif
}

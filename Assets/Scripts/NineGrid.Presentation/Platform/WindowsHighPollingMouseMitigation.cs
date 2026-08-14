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

    /// <summary>看门狗可读的运行时快照（主线程写入）。</summary>
    public static class WindowsHighPollingMouseMitigationRuntime
    {
        public static volatile bool MitigationEnabled = true;
        public static volatile bool NolegacyActive;
        public static volatile bool HasNativeFocus;
        public static volatile bool InsideClient;
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    /// <summary>
    /// Windows Player 高回报率鼠标兜底：RIDEV_NOLEGACY 掐掉 legacy WM_MOUSE 洪水，
    /// 再每帧用 GetCursorPos / GetAsyncKeyState 注入 Input System。
    /// 聚焦且光标在客户区内启用 NOLEGACY；光标在标题栏/边框时用 RIDEV_REMOVE 交还 chrome；
    /// 失焦时保持 NOLEGACY（禁止 dwFlags=0 重注册，避免 Alt-Tab 切回消息洪水）。
    /// WM_ACTIVATE / WM_SETFOCUS 在 WndProc 内立刻重开 NOLEGACY，不等待 onBeforeUpdate。
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class WindowsHighPollingMouseMitigation : MonoBehaviour
    {
        private const ushort HidUsagePageGeneric = 0x01;
        private const ushort HidUsageGenericMouse = 0x02;
        private const uint RidevRemove = 0x00000001;
        private const uint RidevNolegacy = 0x00000030;
        private const int VkLButton = 0x01;
        private const int VkRButton = 0x02;
        private const int VkMButton = 0x04;
        private const int ClientHysteresisPx = 4;
        private const int WmActivate = 0x0006;
        private const int WmSetfocus = 0x0007;
        private const int WaInactive = 0;
        private const int GwlpWndproc = -4;
        private const int MaxWndProcInstallFrames = 300;

        private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        private Vector2 _lastPos;
        private bool _hasLastPos;
        private bool _nolegacyActive;
        private bool _wasInsideClient;
        private bool _nolegacyFailureLogged;
        private bool _removeFailureLogged;
        private IntPtr _cachedHwnd;
        private bool _mainWindowResolveAttempted;
        private bool _wndProcInstalled;
        private IntPtr _originalWndProc;
        private WndProcDelegate _wndProcDelegate;
        private int _wndProcInstallFrames;

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
                    WindowsHighPollingMouseMitigationRuntime.MitigationEnabled = false;
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
            RemoveRegistration();
            UninstallWndProc();
        }

        private void Update()
        {
            if (_wndProcInstalled)
            {
                return;
            }

            if (_wndProcInstallFrames++ >= MaxWndProcInstallFrames)
            {
                return;
            }

            var hwnd = ResolveWindowHandle();
            if (hwnd != IntPtr.Zero)
            {
                TryInstallWndProc(hwnd);
            }
        }

        private void OnBeforeInputUpdate()
        {
            if (!WindowsHighPollingMouseMitigationRuntime.MitigationEnabled)
            {
                return;
            }

            var hwnd = ResolveWindowHandle();
            var hasNativeFocus = hwnd != IntPtr.Zero && GetForegroundWindow() == hwnd;
            WindowsHighPollingMouseMitigationRuntime.HasNativeFocus = hasNativeFocus;

            if (!hasNativeFocus)
            {
                // 失焦：保持 NOLEGACY，不改注册；不注入指针（防窗外点击串入）。
                WindowsHighPollingMouseMitigationRuntime.InsideClient = false;
                return;
            }

            var hasClientPoint = TryReadClientPosition(hwnd, out var pos, out var insideClient);
            WindowsHighPollingMouseMitigationRuntime.InsideClient = insideClient;

            if (hasClientPoint && insideClient)
            {
                EnsureNolegacy();
                InjectMouseState(pos);
                return;
            }

            if (hasClientPoint && !insideClient)
            {
                RemoveRegistration();
            }
        }

        private void EnsureNolegacy()
        {
            if (_nolegacyActive)
            {
                WindowsHighPollingMouseMitigationRuntime.NolegacyActive = true;
                return;
            }

            if (!RegisterMouseDevice(RidevNolegacy))
            {
                if (!_nolegacyFailureLogged)
                {
                    _nolegacyFailureLogged = true;
                    Debug.LogWarning(
                        "[WindowsHighPollingMouseMitigation] RegisterRawInputDevices(RIDEV_NOLEGACY) failed; "
                        + "high polling mice may still stall the main thread.");
                }

                return;
            }

            _nolegacyActive = true;
            WindowsHighPollingMouseMitigationRuntime.NolegacyActive = true;
        }

        private void RemoveRegistration()
        {
            if (!_nolegacyActive)
            {
                WindowsHighPollingMouseMitigationRuntime.NolegacyActive = false;
                return;
            }

            if (!RegisterMouseDevice(RidevRemove))
            {
                if (!_removeFailureLogged)
                {
                    _removeFailureLogged = true;
                    Debug.LogWarning(
                        "[WindowsHighPollingMouseMitigation] RegisterRawInputDevices(RIDEV_REMOVE) failed; "
                        + "window chrome (drag/resize/close) may stay unresponsive.");
                }

                return;
            }

            _nolegacyActive = false;
            WindowsHighPollingMouseMitigationRuntime.NolegacyActive = false;
        }

        private bool RegisterMouseDevice(uint flags)
        {
            var devices = new[]
            {
                new RAWINPUTDEVICE
                {
                    usUsagePage = HidUsagePageGeneric,
                    usUsage = HidUsageGenericMouse,
                    dwFlags = flags,
                    hwndTarget = IntPtr.Zero,
                },
            };

            return RegisterRawInputDevices(
                devices,
                (uint)devices.Length,
                (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
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

        private bool TryReadClientPosition(IntPtr hwnd, out Vector2 unityPos, out bool insideClient)
        {
            unityPos = default;
            insideClient = false;
            if (!GetCursorPos(out var point))
            {
                return false;
            }

            if (!ScreenToClient(hwnd, ref point))
            {
                _cachedHwnd = IntPtr.Zero;
                _mainWindowResolveAttempted = false;
                _wndProcInstalled = false;
                return false;
            }

            if (GetClientRect(hwnd, out var clientRect))
            {
                var margin = _wasInsideClient ? -ClientHysteresisPx : ClientHysteresisPx;
                insideClient = point.X >= margin
                    && point.Y >= margin
                    && point.X < clientRect.Right - margin
                    && point.Y < clientRect.Bottom - margin;
                _wasInsideClient = insideClient;
            }

            unityPos = new Vector2(point.X, Screen.height - point.Y);
            return true;
        }

        private IntPtr ResolveWindowHandle()
        {
            if (_cachedHwnd != IntPtr.Zero && IsWindow(_cachedHwnd))
            {
                return _cachedHwnd;
            }

            _cachedHwnd = IntPtr.Zero;
            if (_mainWindowResolveAttempted)
            {
                return IntPtr.Zero;
            }

            _mainWindowResolveAttempted = true;
            using (var process = System.Diagnostics.Process.GetCurrentProcess())
            {
                var hwnd = process.MainWindowHandle;
                if (hwnd != IntPtr.Zero && IsWindow(hwnd))
                {
                    _cachedHwnd = hwnd;
                }
            }

            return _cachedHwnd;
        }

        private void TryInstallWndProc(IntPtr hwnd)
        {
            if (_wndProcInstalled || hwnd == IntPtr.Zero)
            {
                return;
            }

            _wndProcDelegate = CustomWndProc;
            _originalWndProc = GetWindowLongPtr(hwnd, GwlpWndproc);
            if (_originalWndProc == IntPtr.Zero)
            {
                return;
            }

            var newProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);
            if (SetWindowLongPtr(hwnd, GwlpWndproc, newProc) == IntPtr.Zero)
            {
                return;
            }

            _wndProcInstalled = true;
        }

        private void UninstallWndProc()
        {
            if (!_wndProcInstalled || _cachedHwnd == IntPtr.Zero || _originalWndProc == IntPtr.Zero)
            {
                return;
            }

            SetWindowLongPtr(_cachedHwnd, GwlpWndproc, _originalWndProc);
            _wndProcInstalled = false;
            _originalWndProc = IntPtr.Zero;
            _wndProcDelegate = null;
        }

        private IntPtr CustomWndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WmActivate)
            {
                var active = (wParam.ToInt32() & 0xFFFF) != WaInactive;
                if (active)
                {
                    EnsureNolegacy();
                }
            }
            else if (msg == WmSetfocus)
            {
                EnsureNolegacy();
            }

            return CallWindowProc(_originalWndProc, hwnd, msg, wParam, lParam);
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
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(
            IntPtr lpPrevWndFunc,
            IntPtr hWnd,
            uint msg,
            IntPtr wParam,
            IntPtr lParam);
    }
#endif
}

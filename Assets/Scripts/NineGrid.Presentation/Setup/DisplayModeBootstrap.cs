using UnityEngine;

namespace NineGrid.Presentation.Setup
{
    /// <summary>
    /// 打包体窗口模式引导：启动强制窗口化，尺寸从 16:9 档位中选「客户区 + 窗框/任务栏余量
    /// 能完整落进当前桌面」的最大一档（1080p 桌面 → 1600x900；2K 及以上 → 1920x1080）。
    /// 直接钉死 1920x1080 会在 1080p 屏上产出比桌面还大的窗口，被系统钳在左上角。
    /// 改档后（Windows）把窗口居中到所在显示器工作区；未改档则保留玩家上次摆放的位置。
    /// 窗口再放大时的整数倍缩放与黑边由主摄像机 PixelPerfectCamera（Crop Frame: Windowbox）承担。
    /// </summary>
    public static class DisplayModeBootstrap
    {
        public const int TargetWidth = 1920;
        public const int TargetHeight = 1080;

#if !UNITY_EDITOR
        // 从大到小；全部 16:9，与 PixelPerfectCamera 960x540 参考分辨率整倍对齐。
        private static readonly Vector2Int[] WindowedPresets =
        {
            new Vector2Int(TargetWidth, TargetHeight),
            new Vector2Int(1600, 900),
            new Vector2Int(1280, 720),
            new Vector2Int(960, 540),
        };

        // 窗框 + 标题栏 + 任务栏的保守余量（物理像素）：客户区 + 余量 ≤ 桌面才算放得下。
        private const int ChromeMarginX = 32;
        private const int ChromeMarginY = 96;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnforceWindowedResolution()
        {
            var target = PickWindowedSize();
            if (Screen.fullScreenMode == FullScreenMode.Windowed
                && Screen.width == target.x
                && Screen.height == target.y)
            {
                return;
            }

            Screen.SetResolution(target.x, target.y, FullScreenMode.Windowed);
#if UNITY_STANDALONE_WIN
            WindowsWindowCenterer.Schedule(target);
#endif
        }

        private static Vector2Int PickWindowedSize()
        {
            var desktopWidth = Display.main.systemWidth;
            var desktopHeight = Display.main.systemHeight;
            if (desktopWidth <= 0 || desktopHeight <= 0)
            {
                desktopWidth = Screen.currentResolution.width;
                desktopHeight = Screen.currentResolution.height;
            }

            foreach (var preset in WindowedPresets)
            {
                if (preset.x + ChromeMarginX <= desktopWidth
                    && preset.y + ChromeMarginY <= desktopHeight)
                {
                    return preset;
                }
            }

            return WindowedPresets[WindowedPresets.Length - 1];
        }
#endif

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        /// <summary>
        /// 一次性窗口居中：等 SetResolution 帧末真正生效后，把外框中心对齐到
        /// 所在显示器的工作区（排除任务栏）中心，然后自毁。
        /// </summary>
        private sealed class WindowsWindowCenterer : MonoBehaviour
        {
            private const int MaxWaitFrames = 60;
            private const uint MonitorDefaultToNearest = 0x00000002;
            private const uint SwpNoSize = 0x0001;
            private const uint SwpNoZOrder = 0x0004;
            private const uint SwpNoActivate = 0x0010;

            private Vector2Int mTarget;

            public static void Schedule(Vector2Int target)
            {
                var go = new GameObject(nameof(WindowsWindowCenterer))
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };
                DontDestroyOnLoad(go);
                go.AddComponent<WindowsWindowCenterer>().mTarget = target;
            }

            private System.Collections.IEnumerator Start()
            {
                for (var i = 0; i < MaxWaitFrames; i++)
                {
                    if (Screen.fullScreenMode == FullScreenMode.Windowed
                        && Screen.width == mTarget.x
                        && Screen.height == mTarget.y)
                    {
                        break;
                    }

                    yield return null;
                }

                // 多等一帧让窗口外框尺寸随客户区落定，再按外框算居中位置。
                yield return null;
                CenterOnWorkArea();
                Destroy(gameObject);
            }

            private static void CenterOnWorkArea()
            {
                if (Screen.fullScreenMode != FullScreenMode.Windowed)
                {
                    return;
                }

                var hwnd = GetActiveWindow();
                if (hwnd == System.IntPtr.Zero)
                {
                    using (var process = System.Diagnostics.Process.GetCurrentProcess())
                    {
                        hwnd = process.MainWindowHandle;
                    }
                }

                if (hwnd == System.IntPtr.Zero || !GetWindowRect(hwnd, out var windowRect))
                {
                    return;
                }

                var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
                var info = new MONITORINFO
                {
                    cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>(),
                };
                if (monitor == System.IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
                {
                    return;
                }

                var windowWidth = windowRect.Right - windowRect.Left;
                var windowHeight = windowRect.Bottom - windowRect.Top;
                var workWidth = info.rcWork.Right - info.rcWork.Left;
                var workHeight = info.rcWork.Bottom - info.rcWork.Top;
                var x = info.rcWork.Left + Mathf.Max(0, (workWidth - windowWidth) / 2);
                var y = info.rcWork.Top + Mathf.Max(0, (workHeight - windowHeight) / 2);
                SetWindowPos(hwnd, System.IntPtr.Zero, x, y, 0, 0, SwpNoSize | SwpNoZOrder | SwpNoActivate);
            }

            [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
            private struct RECT
            {
                public int Left;
                public int Top;
                public int Right;
                public int Bottom;
            }

            [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
            private struct MONITORINFO
            {
                public int cbSize;
                public RECT rcMonitor;
                public RECT rcWork;
                public uint dwFlags;
            }

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            private static extern System.IntPtr GetActiveWindow();

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            private static extern bool GetWindowRect(System.IntPtr hWnd, out RECT lpRect);

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            private static extern System.IntPtr MonitorFromWindow(System.IntPtr hWnd, uint dwFlags);

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            private static extern bool GetMonitorInfo(System.IntPtr hMonitor, ref MONITORINFO lpmi);

            [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
            private static extern bool SetWindowPos(
                System.IntPtr hWnd,
                System.IntPtr hWndInsertAfter,
                int x,
                int y,
                int cx,
                int cy,
                uint uFlags);
        }
#endif
    }
}

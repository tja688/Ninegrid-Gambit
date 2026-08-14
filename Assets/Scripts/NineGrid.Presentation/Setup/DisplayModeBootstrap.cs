using System;
using UnityEngine;
#if UNITY_STANDALONE_WIN
using System.Runtime.InteropServices;
#endif

namespace NineGrid.Presentation.Setup
{
    /// <summary>
    /// 打包体窗口模式引导：启动强制窗口化，尺寸从 16:9 档位中选「客户区 + 窗框/任务栏余量
    /// 能完整落进当前桌面」的最大一档（1080p 桌面 → 1600x900；2K 及以上 → 1920x1080）。
    /// 直接钉死 1920x1080 会在 1080p 屏上产出比桌面还大的窗口，被系统钳在左上角。
    /// 窗口位置交给 Unity / 系统默认摆放，不另写 Win32 居中。
    /// 窗口再放大时的整数倍缩放与黑边由主摄像机 PixelPerfectCamera（960x540、Windowbox、Upscale RT）承担；
    /// 本类在 Windows 上持续把 Win32 客户区尺寸同步回 <see cref="Screen.SetResolution"/>，
    /// 避免最大化/拖拽后 Unity 仍按启动分辨率渲染、画面不随窗口整数倍放大。
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
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallWindowRenderSync()
        {
            var go = new GameObject(nameof(WindowRenderSync))
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            DontDestroyOnLoad(go);
            go.AddComponent<WindowRenderSync>();
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

        /// <summary>
        /// 把原生窗口客户区尺寸写回 Unity 渲染分辨率，驱动 PixelPerfectCamera 重算整数倍 zoom。
        /// </summary>
        private sealed class WindowRenderSync : MonoBehaviour
        {
            private int _lastWidth;
            private int _lastHeight;
            private bool _initialized;

            private void Update()
            {
                if (Screen.fullScreenMode != FullScreenMode.Windowed)
                {
                    return;
                }

#if UNITY_STANDALONE_WIN
                if (TryGetNativeClientSize(out var nativeWidth, out var nativeHeight))
                {
                    TryApply(nativeWidth, nativeHeight);
                    return;
                }
#endif
                TryApply(Screen.width, Screen.height);
            }

            private void TryApply(int width, int height)
            {
                if (width <= 0 || height <= 0)
                {
                    return;
                }

                if (_initialized && width == _lastWidth && height == _lastHeight)
                {
                    return;
                }

                _lastWidth = width;
                _lastHeight = height;
                _initialized = true;

                if (Screen.width == width
                    && Screen.height == height
                    && Screen.fullScreenMode == FullScreenMode.Windowed)
                {
                    return;
                }

                Screen.SetResolution(width, height, FullScreenMode.Windowed);
            }

#if UNITY_STANDALONE_WIN
            [StructLayout(LayoutKind.Sequential)]
            private struct RECT
            {
                public int Left;
                public int Top;
                public int Right;
                public int Bottom;
            }

            [DllImport("user32.dll")]
            private static extern IntPtr GetActiveWindow();

            [DllImport("user32.dll")]
            private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

            private static bool TryGetNativeClientSize(out int width, out int height)
            {
                width = 0;
                height = 0;

                var hwnd = GetActiveWindow();
                if (hwnd == IntPtr.Zero)
                {
                    return false;
                }

                if (!GetClientRect(hwnd, out var rect))
                {
                    return false;
                }

                width = rect.Right - rect.Left;
                height = rect.Bottom - rect.Top;
                return width > 0 && height > 0;
            }
#endif
        }
#endif
    }
}

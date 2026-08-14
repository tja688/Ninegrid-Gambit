using UnityEngine;

namespace NineGrid.Presentation.Setup
{
    /// <summary>
    /// 打包体显示模式引导：启动即无边框全屏（桌面原生分辨率），覆盖 Unity 记忆的旧窗口偏好。
    /// 16:9 内容按高度铺满、宽屏左右留黑边由主摄像机 PixelPerfectCamera（960x540、StretchFill）承担。
    /// Editor Play 不强制全屏（#if !UNITY_EDITOR）。
    /// </summary>
    public static class DisplayModeBootstrap
    {
#if !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnforceFullscreenBeforeSceneLoad()
        {
            ApplyFullscreenIfNeeded();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnforceFullscreenAfterSceneLoad()
        {
            // Unity 有时在第一帧之后才应用上次窗口偏好，再断言一次即可（不每帧 SetResolution）。
            ApplyFullscreenIfNeeded();
        }

        private static void ApplyFullscreenIfNeeded()
        {
            var width = Display.main.systemWidth;
            var height = Display.main.systemHeight;
            if (width <= 0 || height <= 0)
            {
                width = Screen.currentResolution.width;
                height = Screen.currentResolution.height;
            }

            if (Screen.fullScreenMode == FullScreenMode.FullScreenWindow
                && Screen.width == width
                && Screen.height == height)
            {
                return;
            }

            Screen.SetResolution(width, height, FullScreenMode.FullScreenWindow);
        }
#endif
    }
}

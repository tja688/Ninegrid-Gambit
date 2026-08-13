using UnityEngine;

namespace NineGrid.Presentation.Setup
{
    /// <summary>
    /// 打包体窗口模式引导：启动即强制 1920x1080 窗口化。
    /// 覆盖 Unity 记忆的上次分辨率 / 全屏偏好（旧包可能留下原生分辨率全屏）；
    /// 窗口再放大时的整数倍缩放与黑边由主摄像机 PixelPerfectCamera（Crop Frame: Windowbox）承担。
    /// </summary>
    public static class DisplayModeBootstrap
    {
        public const int TargetWidth = 1920;
        public const int TargetHeight = 1080;

#if !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnforceWindowedResolution()
        {
            if (Screen.fullScreenMode == FullScreenMode.Windowed
                && Screen.width == TargetWidth
                && Screen.height == TargetHeight)
            {
                return;
            }

            Screen.SetResolution(TargetWidth, TargetHeight, FullScreenMode.Windowed);
        }
#endif
    }
}

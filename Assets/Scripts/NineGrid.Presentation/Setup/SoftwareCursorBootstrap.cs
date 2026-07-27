using UnityEngine;

namespace NineGrid.Presentation.Setup
{
    /// <summary>
    /// 启动时用 ForceSoftware 覆盖 PlayerSettings 默认硬件光标。
    /// </summary>
    public static class SoftwareCursorBootstrap
    {
        private static Texture2D sDisplayTexture;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            sDisplayTexture = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            ReapplyForceSoftware();
        }

        /// <summary>
        /// 重新套用 ForceSoftware 软件光标（供 Dev kill-switch 还原）。
        /// </summary>
        public static void ReapplyForceSoftware()
        {
            var settings = Resources.Load<SoftwareCursorSettingsSO>(SoftwareCursorSettingsSO.ResourcePath);
            if (settings == null || settings.CursorTexture == null)
            {
                return;
            }

            if (sDisplayTexture != null && sDisplayTexture != settings.CursorTexture)
            {
                Object.Destroy(sDisplayTexture);
                sDisplayTexture = null;
            }

            if (sDisplayTexture == null)
            {
                sDisplayTexture = settings.BuildDisplayTexture();
            }

            if (sDisplayTexture == null)
            {
                return;
            }

            Cursor.SetCursor(sDisplayTexture, settings.BuildDisplayHotspot(), CursorMode.ForceSoftware);
        }
    }
}

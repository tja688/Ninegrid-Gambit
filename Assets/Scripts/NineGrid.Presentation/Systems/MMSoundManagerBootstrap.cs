using MoreMountains.Tools;
using UnityEngine;

namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// Runtime-owned MMSoundManager host with the project's formal author defaults.
    /// </summary>
    internal static class MMSoundManagerRuntimeHost
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstance()
        {
            if (MMSoundManager.HasInstance)
            {
                return;
            }

            var host = new GameObject("MMSoundManager_Runtime");
            var manager = host.AddComponent<MMSoundManager>();
            manager.settingsSo = Resources.Load<MMSoundManagerSettingsSO>("MMSoundManagerSettings");
            if (manager.settingsSo == null)
            {
                Debug.LogWarning(
                    "[Audio] MMSoundManagerSettings asset is unavailable; Music/Sfx tracks still use MMSoundManager defaults.");
            }
        }
    }
}

using UnityEngine;

namespace NineGrid.Presentation.Systems.Vfx
{
    /// <summary>独立型 VFX 的世界空间锚点；不触碰卡级变换塔。</summary>
    internal static class VfxIndependentSpatialRoot
    {
        private static Transform sRoot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            sRoot = null;
        }

        public static Transform Root
        {
            get
            {
                // Unity == null：Destroyed DDOL 根在 DisableDomainReload 下仍可能占着静态引用。
                if (sRoot != null)
                {
                    return sRoot;
                }

                var host = new GameObject("VfxIndependentSpatialRoot");
                if (Application.isPlaying)
                {
                    Object.DontDestroyOnLoad(host);
                }

                sRoot = host.transform;
                return sRoot;
            }
        }
    }
}

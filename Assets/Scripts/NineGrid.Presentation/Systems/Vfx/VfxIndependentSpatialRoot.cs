using UnityEngine;

namespace NineGrid.Presentation.Systems.Vfx
{
    /// <summary>独立型 VFX 的世界空间锚点；不触碰卡级变换塔。</summary>
    internal static class VfxIndependentSpatialRoot
    {
        private static Transform sRoot;

        public static Transform Root
        {
            get
            {
                if (sRoot != null)
                {
                    return sRoot;
                }

                var host = new GameObject("VfxIndependentSpatialRoot");
                Object.DontDestroyOnLoad(host);
                sRoot = host.transform;
                return sRoot;
            }
        }
    }
}

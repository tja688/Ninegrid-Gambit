#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    public static class VfxWorkbenchLauncher
    {
        public const string MenuPath = "NineGrid/视觉特效/VFX 绑定调试工作台";

        [MenuItem(MenuPath)]
        public static void OpenWorkbench()
        {
            var url = VfxWorkbenchServer.LaunchUrl;
            Application.OpenURL(url);
            Debug.Log("[VfxWorkbench] 已启动 loopback 工作台：" + url);
        }
    }
}
#endif

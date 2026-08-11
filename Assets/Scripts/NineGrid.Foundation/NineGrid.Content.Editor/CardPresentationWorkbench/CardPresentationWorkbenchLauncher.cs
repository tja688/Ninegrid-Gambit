#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    /// <summary>菜单启动器：打开鉴权 localhost 表现层配置工作台。</summary>
    public static class CardPresentationWorkbenchLauncher
    {
        public const string MenuPath = "NineGrid/表现层配置工作台（网页）";

        [MenuItem(MenuPath)]
        public static void OpenWorkbench()
        {
            var url = CardPresentationWorkbenchServer.LaunchUrl;
            Application.OpenURL(url);
            Debug.Log("[CardPresentationWorkbench] 已启动 loopback 工作台：" + url);
        }
    }
}
#endif

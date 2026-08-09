#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    /// <summary>#187 菜单启动器：打开鉴权 localhost 工作台，不持有第二套 session。</summary>
    public static class AudioWorkbenchLauncher
    {
        public const string MenuPath = "NineGrid/音频/声音绑定调音工作台";

        [MenuItem(MenuPath)]
        public static void OpenWorkbench()
        {
            var url = AudioWorkbenchServer.LaunchUrl;
            Application.OpenURL(url);
            Debug.Log("[AudioWorkbench] 已启动 loopback 工作台：" + url);
        }
    }
}
#endif

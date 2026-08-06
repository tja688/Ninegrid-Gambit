#if UNITY_EDITOR || DEVELOPMENT_BUILD

using NineGrid.Flow;
using UnityEngine;

namespace NineGrid.Presentation.Cheat
{
    /// <summary>
    /// F12 作弊面板热键宿主：常驻（DontDestroyOnLoad），任何场景 / 主菜单 / 战斗中都能唤起
    /// <see cref="CheatToolPanelController"/>。仿 PointerHitRouter 自举模式。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CheatToolHotkeyHost : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (FindFirstObjectByType<CheatToolHotkeyHost>() != null)
            {
                return;
            }

            var go = new GameObject(nameof(CheatToolHotkeyHost));
            DontDestroyOnLoad(go);
            go.AddComponent<CheatToolHotkeyHost>();
        }

        private void Update()
        {
            if (KeyboardUtility.GetKeyDown(KeyCode.F12))
            {
                CheatToolPanelController.TryToggle();
            }
        }
    }
}

#endif

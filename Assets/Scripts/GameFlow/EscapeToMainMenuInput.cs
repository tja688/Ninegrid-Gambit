using UnityEngine;

namespace NineGrid.GameFlow
{
    /// <summary>
    /// 全局 Esc：局内任意节点中止本局并回主菜单（已在主菜单时不处理）。
    /// 自举常驻，无需在场景中手动挂载。
    /// </summary>
    sealed class EscapeToMainMenuInput : MonoBehaviour
    {
        static EscapeToMainMenuInput s_active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (s_active != null)
            {
                return;
            }

            var go = new GameObject("[EscapeToMainMenu]");
            DontDestroyOnLoad(go);
            s_active = go.AddComponent<EscapeToMainMenuInput>();
        }

        void OnDestroy()
        {
            if (s_active == this)
            {
                s_active = null;
            }
        }

        void Update()
        {
            if (!DebugHotkeyInput.WasPressedThisFrame(KeyCode.Escape))
            {
                return;
            }

            var flow = GameFlowController.Instance;
            if (flow == null || !flow.IsBooted)
            {
                return;
            }

            if (flow.CurrentState == GameFlowState.MainMenu)
            {
                return;
            }

            Debug.Log("[EscapeToMainMenu] Esc → 回主菜单");
            flow.GoToMainMenu();
        }
    }
}

using System;
using NineGrid.Presentation.Visuals;
using UnityEngine;

namespace NineGrid.GameFlow
{
    /// <summary>
    /// 主菜单（MainPanelScene）：点「开始」进入战役。
    /// 首次通关 / 中途死亡回到这里；再开始时因已历序章，直接进入第一场战斗（战斗0）。
    ///
    /// 两种触发都支持：世界物体点击（SelectableSceneElement）或 uGUI Button 调 <see cref="StartGame"/>。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("开始按钮（世界物体，可选）")]
        [Tooltip("留空时按名字「开始」/「start」查找；仍找不到则「点击任意处开始」。")]
        [SerializeField] SelectableSceneElement startElement;
        [SerializeField] SceneElementPointerSelector pointerSelector;
        [Tooltip("找不到开始按钮时，是否允许点击场景任意处开始。")]
        [SerializeField] bool startOnAnyClickFallback = true;

        bool _started;

        public event Action GameStarted;

        void Start()
        {
            ResolveRefs();
        }

        void Update()
        {
            if (_started || !FlowInput.PrimaryClickThisFrame())
            {
                return;
            }

            if (startElement != null && pointerSelector != null)
            {
                if (pointerSelector.Hovered == startElement)
                {
                    StartGame();
                }

                return;
            }

            // 没有明确的开始按钮：点击任意处开始。
            if (startOnAnyClickFallback)
            {
                StartGame();
            }
        }

        /// <summary>供 uGUI Button.onClick 或世界点击调用：开始一局。</summary>
        public void StartGame()
        {
            if (_started)
            {
                return;
            }

            _started = true;
            GameStarted?.Invoke();
            var flow = GameFlowController.Instance;
            if (flow == null)
            {
                Debug.LogWarning("[MainMenu] 找不到 GameFlowController。");
                return;
            }

            // MainMenu 状态下 Advance 等价于 StartNewRun。
            flow.Advance();
        }

        void ResolveRefs()
        {
            if (pointerSelector == null)
            {
                pointerSelector = FindFirstObjectByType<SceneElementPointerSelector>();
            }

            if (startElement == null)
            {
                startElement = FindSelectable("开始") ?? FindSelectable("start") ?? FindSelectable("Start");
            }
        }

        static SelectableSceneElement FindSelectable(string objectName)
        {
            var go = GameObject.Find(objectName);
            return go != null ? go.GetComponent<SelectableSceneElement>() : null;
        }
    }
}

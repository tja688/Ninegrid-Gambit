using System;
using NineGrid.Presentation.Visuals;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NineGrid.GameFlow
{
    /// <summary>
    /// 主菜单（MainPanelScene）：点「开始游戏」始终进入序章；「退出游戏」退出应用。
    /// 战败 / 通关结算后回到此处；首次从 MainScene 启动则跳过本菜单，直接序章。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("开始按钮（世界物体，可选）")]
        [Tooltip("留空时按名字「开始」/「start」查找。")]
        [SerializeField] SelectableSceneElement startElement;
        [SerializeField] SceneElementPointerSelector pointerSelector;

        [Header("uGUI 按钮（可选）")]
        [SerializeField] Button startButton;
        [SerializeField] Button quitButton;

        [Tooltip("找不到任何开始入口时，是否允许点击场景任意处开始。")]
        [SerializeField] bool startOnAnyClickFallback;

        bool _started;
        bool _flowHooked;

        public event Action GameStarted;

        void Start()
        {
            ResolveRefs();
            WireButtons();
            TryHookFlow();
        }

        void OnDestroy()
        {
            UnhookFlow();
            UnwireButtons();
        }

        void Update()
        {
            if (!_flowHooked)
            {
                TryHookFlow();
            }

            if (_started || !startOnAnyClickFallback || !FlowInput.PrimaryClickThisFrame())
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

            StartGame();
        }

        /// <summary>供 uGUI Button.onClick 或世界点击调用：开始一局（序章）。</summary>
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
                _started = false;
                return;
            }

            flow.Advance();
        }

        /// <summary>供 uGUI Button.onClick 调用：退出游戏。</summary>
        public void QuitGame()
        {
            Debug.Log("[MainMenu] 退出游戏");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void TryHookFlow()
        {
            var flow = GameFlowController.Instance;
            if (flow == null || !flow.IsBooted)
            {
                return;
            }

            if (!_flowHooked)
            {
                flow.StateChanged += OnFlowStateChanged;
                _flowHooked = true;
            }

            if (flow.CurrentState == GameFlowState.MainMenu)
            {
                _started = false;
            }
        }

        void UnhookFlow()
        {
            if (!_flowHooked)
            {
                return;
            }

            var flow = GameFlowController.Instance;
            if (flow != null)
            {
                flow.StateChanged -= OnFlowStateChanged;
            }

            _flowHooked = false;
        }

        void OnFlowStateChanged(GameFlowState previous, GameFlowState next)
        {
            if (next == GameFlowState.MainMenu)
            {
                _started = false;
            }
        }

        void WireButtons()
        {
            if (startButton == null)
            {
                startButton = FindButtonByLabel("开始游戏");
            }

            if (quitButton == null)
            {
                quitButton = FindButtonByLabel("退出游戏");
            }

            if (startButton != null)
            {
                startButton.onClick.AddListener(StartGame);
                startOnAnyClickFallback = false;
            }

            if (quitButton != null)
            {
                quitButton.onClick.AddListener(QuitGame);
            }
        }

        void UnwireButtons()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(StartGame);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(QuitGame);
            }
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

        static Button FindButtonByLabel(string label)
        {
            var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                if (button == null)
                {
                    continue;
                }

                var text = button.GetComponentInChildren<TMP_Text>(true);
                if (text != null && string.Equals(text.text.Trim(), label, StringComparison.Ordinal))
                {
                    return button;
                }
            }

            return null;
        }

        static SelectableSceneElement FindSelectable(string objectName)
        {
            var go = GameObject.Find(objectName);
            return go != null ? go.GetComponent<SelectableSceneElement>() : null;
        }
    }
}

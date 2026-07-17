using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.TemporaryTest
{
    /// <summary>
    /// UITest 专用：主力 Bounce 扇形形态选择验收。
    /// 由 <see cref="Flow.UITestBootstrap"/> 转发小键盘 1；首次弹 3 选，再按消除并弹 6 选，点选后播退场动画再收尾。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UITestLivingFormChoiceTest : MonoBehaviour, Flow.IUITestKeyConsumer
    {
        private static readonly string[] ThreeOptionDefIds =
        {
            "Attack",
            "Armor",
            "Hp",
        };

        private static readonly string[] SixOptionDefIds =
        {
            "Attack",
            "Armor",
            "Hp",
            "help.gold_card",
            "help.healing_potion",
            "relic.lucky_coin",
        };

        private enum Phase
        {
            Idle,
            Three,
            Six,
        }

        [Tooltip("Bounce 选择器门面；留空则运行时 FindFirstObjectByType<SelectorManagerSingleton>。")]
        [SerializeField] private Flow.SelectorManagerSingleton selectorManager;

        private Phase _phase = Phase.Idle;

        private void Awake()
        {
            if (selectorManager == null)
            {
                selectorManager = FindFirstObjectByType<Flow.SelectorManagerSingleton>();
            }
        }

        /// <summary>小键盘 1：Idle→3 选；3 选→6 选；6 选→关闭。</summary>
        [ContextMenu("Keypad1 · 切换形态选择")]
        public void HandleKeypad1()
        {
            var manager = ResolveSelector();
            if (manager == null)
            {
                return;
            }

            switch (_phase)
            {
                case Phase.Idle:
                    ShowChoice(manager, ThreeOptionDefIds, Phase.Three);
                    break;
                case Phase.Three:
                    ShowChoice(manager, SixOptionDefIds, Phase.Six);
                    break;
                case Phase.Six:
                    manager.HideChoice();
                    _phase = Phase.Idle;
                    Debug.Log("[UITestLivingFormChoice] 已关闭形态选择。");
                    break;
            }
        }

        private void ShowChoice(
            Flow.SelectorManagerSingleton manager,
            IReadOnlyList<string> optionDefIds,
            Phase nextPhase)
        {
            _phase = nextPhase;
            manager.BeginBounceChoice(
                optionDefIds,
                OnPicked,
                OnSessionFinished,
                hoverOnNotice: true);
            Debug.Log($"[UITestLivingFormChoice] → {optionDefIds.Count} 选形态（Hover 描述走 Notice）。");
        }

        private void OnPicked(int index, string defId)
        {
            Debug.Log($"[UITestLivingFormChoice] 已点选 index={index} defId={defId}，退场动画播放中…");
        }

        private void OnSessionFinished()
        {
            _phase = Phase.Idle;
            Debug.Log("[UITestLivingFormChoice] 退场完成，形态选择已消除。");
        }

        private Flow.SelectorManagerSingleton ResolveSelector()
        {
            if (selectorManager != null)
            {
                return selectorManager;
            }

            selectorManager = FindFirstObjectByType<Flow.SelectorManagerSingleton>();
            if (selectorManager == null)
            {
                Debug.LogWarning("[UITestLivingFormChoice] 未找到 SelectorManagerSingleton。");
            }

            return selectorManager;
        }
    }
}

using System;
using System.Collections.Generic;
using NineGrid.Cards;
using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 选择器管理单例：Bounce 扇形选择的统一入场退场门面（宝箱开遗物等战斗内三选一）。
    /// </summary>
    public sealed class SelectorManagerSingleton : MonoBehaviour
    {
        private const string DefaultPlaceholderDefId = CardManagerSingleton.StandardDefId;

        [Tooltip("Bounce 扇形选择表现；留空则运行时在同物体上 GetComponent 或 AddComponent。")]
        [SerializeField] private BounceFanChoicePresenter bouncePresenter;

        [Tooltip("测试/缺省选项使用的卡牌 DefId；留空则用 CardManager 的 standard。")]
        [SerializeField] private string placeholderDefId = DefaultPlaceholderDefId;

        private Action<int, string> _onPicked;
        private Action _onFinished;
        private bool _sessionActive;

        public bool IsChoiceActive => _sessionActive;

        private void Awake()
        {
            EnsureBouncePresenter();
        }

        private void OnDestroy()
        {
            if (_sessionActive)
            {
                HideChoice();
            }
        }

        /// <summary>
        /// 打开 Bounce 扇形选择；数量默认 3，选项 DefId 使用 placeholderDefId 填充。
        /// </summary>
        public void BeginBounceChoice(
            int count,
            Action<int, string> onPicked,
            Action onFinished = null,
            bool hoverOnNotice = false)
        {
            count = Mathf.Max(1, count);
            var defId = string.IsNullOrWhiteSpace(placeholderDefId)
                ? DefaultPlaceholderDefId
                : placeholderDefId;
            var options = new string[count];
            for (var i = 0; i < count; i++)
            {
                options[i] = defId;
            }

            BeginBounceChoice(options, onPicked, onFinished, hoverOnNotice);
        }

        /// <summary>
        /// 打开 Bounce 扇形选择；选项数量与 DefId 由列表决定（会相对容器居中）。
        /// </summary>
        public void BeginBounceChoice(
            IReadOnlyList<string> optionDefIds,
            Action<int, string> onPicked,
            Action onFinished = null,
            bool hoverOnNotice = false)
        {
            if (optionDefIds == null || optionDefIds.Count == 0)
            {
                Debug.LogWarning("[SelectorManager] BeginBounceChoice 收到空选项列表。");
                return;
            }

            EnsureBouncePresenter();
            if (bouncePresenter == null)
            {
                Debug.LogError("[SelectorManager] BounceFanChoicePresenter 缺失。");
                return;
            }

            if (_sessionActive)
            {
                HideChoice();
            }

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            _onPicked = onPicked;
            _onFinished = onFinished;
            _sessionActive = true;
            bouncePresenter.Begin(optionDefIds, OnPresenterPicked, hoverOnNotice);
        }

        /// <summary>
        /// 强制收尾：杀动效、清回调。
        /// </summary>
        public void HideChoice()
        {
            _onPicked = null;
            _onFinished = null;
            _sessionActive = false;

            if (bouncePresenter != null)
            {
                bouncePresenter.Teardown();
            }
        }

        private void OnPresenterPicked(int index, string defId)
        {
            var callback = _onPicked;
            _onPicked = null;
            callback?.Invoke(index, defId);
            // 退场动画结束后由 Presenter 再回调完成；此处仅转发选择结果。
        }

        internal void NotifySessionFinished()
        {
            _sessionActive = false;
            _onPicked = null;
            var finished = _onFinished;
            _onFinished = null;

            if (bouncePresenter != null)
            {
                bouncePresenter.Teardown();
            }

            finished?.Invoke();
        }

        private void EnsureBouncePresenter()
        {
            if (bouncePresenter != null)
            {
                return;
            }

            bouncePresenter = GetComponent<BounceFanChoicePresenter>();
            if (bouncePresenter == null)
            {
                bouncePresenter = gameObject.AddComponent<BounceFanChoicePresenter>();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using NineGrid.Cards;
using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 选择器管理单例：Bounce 扇形 / 房间二选一 的统一入场退场门面与路由。
    /// </summary>
    public sealed class SelectorManagerSingleton : MonoBehaviour
    {
        private const string DefaultPlaceholderDefId = CardManagerSingleton.StandardDefId;


        [Tooltip("Bounce 扇形选择表现；留空则运行时在同物体上 GetComponent 或 AddComponent。")]
        [SerializeField] private BounceFanChoicePresenter bouncePresenter;

        [Tooltip("房间二选一表现；留空则运行时按 RoomChoisePanel 查找或在该物体上 AddComponent。")]
        [SerializeField] private RoomChoicePresenter roomChoicePresenter;

        [Tooltip("测试/缺省选项使用的卡牌 DefId；留空则用 CardManager 的 standard。")]
        [SerializeField] private string placeholderDefId = DefaultPlaceholderDefId;

        private Action<int, string> _onPicked;
        private Action _onFinished;
        private bool _sessionActive;
        private ChoiceKind _activeKind = ChoiceKind.None;

        private enum ChoiceKind
        {
            None,
            Bounce,
            Room,
        }

        public bool IsChoiceActive => _sessionActive;

        private void Awake()
        {
            EnsureBouncePresenter();
            EnsureRoomPresenter();
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
            _activeKind = ChoiceKind.Bounce;
            bouncePresenter.Begin(optionDefIds, OnPresenterPicked, hoverOnNotice);
        }

        /// <summary>
        /// 打开房间二选一；默认 optionIds 为 room_left / room_right。
        /// </summary>
        public void BeginRoomChoice(
            Action<int, string> onPicked,
            Action onFinished = null,
            bool hoverOnNotice = false)
        {
            BeginRoomChoice("room_left", "room_right", onPicked, onFinished, hoverOnNotice);
        }

        /// <summary>
        /// 打开房间二选一。
        /// </summary>
        public void BeginRoomChoice(
            string leftOptionId,
            string rightOptionId,
            Action<int, string> onPicked,
            Action onFinished = null,
            bool hoverOnNotice = false)
        {
            EnsureRoomPresenter();
            if (roomChoicePresenter == null)
            {
                Debug.LogError("[SelectorManager] RoomChoicePresenter 缺失。");
                return;
            }

            if (_sessionActive)
            {
                HideChoice();
            }

            _onPicked = onPicked;
            _onFinished = onFinished;
            _sessionActive = true;
            _activeKind = ChoiceKind.Room;
            roomChoicePresenter.Begin(
                leftOptionId,
                rightOptionId,
                OnPresenterPicked,
                NotifySessionFinished,
                hoverOnNotice);
        }

        /// <summary>
        /// 强制收尾：杀动效、清回调。
        /// </summary>
        public void HideChoice()
        {
            _onPicked = null;
            _onFinished = null;
            _sessionActive = false;
            var kind = _activeKind;
            _activeKind = ChoiceKind.None;

            if (kind == ChoiceKind.Bounce && bouncePresenter != null)
            {
                bouncePresenter.Teardown();
            }

            if (kind == ChoiceKind.Room && roomChoicePresenter != null)
            {
                roomChoicePresenter.Teardown();
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
            _activeKind = ChoiceKind.None;
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

        private void EnsureRoomPresenter()
        {
            if (roomChoicePresenter != null)
            {
                return;
            }

            roomChoicePresenter = FindFirstObjectByType<RoomChoicePresenter>(FindObjectsInactive.Include);
            if (roomChoicePresenter != null)
            {
                return;
            }

            var panel = GameObject.Find("RoomChoisePanel");
            if (panel == null)
            {
                var all = Resources.FindObjectsOfTypeAll<Transform>();
                for (var i = 0; i < all.Length; i++)
                {
                    if (all[i] != null && all[i].name == "RoomChoisePanel" && all[i].gameObject.scene.IsValid())
                    {
                        panel = all[i].gameObject;
                        break;
                    }
                }
            }

            if (panel != null)
            {
                roomChoicePresenter = panel.GetComponent<RoomChoicePresenter>();
                if (roomChoicePresenter == null)
                {
                    roomChoicePresenter = panel.AddComponent<RoomChoicePresenter>();
                }
            }
        }
    }
}

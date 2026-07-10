using System;
using DG.Tweening;
using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 房间二选一表现：左卡从上入场、从下出场；右卡从下入场、从上场。
    /// 由 SelectorManagerSingleton.BeginRoomChoice 驱动。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomChoicePresenter : MonoBehaviour
    {
        [Header("Cards")]
        [Tooltip("左侧房间卡；留空则运行时按子物体名 Room1 查找。")]
        [SerializeField] private Transform leftRoom;

        [Tooltip("右侧房间卡；留空则运行时按子物体名 Room2 查找。")]
        [SerializeField] private Transform rightRoom;

        [Tooltip("左卡入场起点；留空则按子物体名 Room1Start 查找。")]
        [SerializeField] private Transform leftStart;

        [Tooltip("左卡出场终点；留空则按子物体名 Room1End 查找。")]
        [SerializeField] private Transform leftEnd;

        [Tooltip("右卡入场起点；留空则按子物体名 Room2Start 查找。")]
        [SerializeField] private Transform rightStart;

        [Tooltip("右卡出场终点；留空则按子物体名 Room2End 查找。")]
        [SerializeField] private Transform rightEnd;

        [Header("Tween")]
        [Tooltip("单张入场时长（秒）。")]
        [SerializeField] private float enterDuration = 0.45f;

        [Tooltip("单张出场时长（秒）。")]
        [SerializeField] private float exitDuration = 0.4f;

        [Tooltip("左右入场错峰（秒）。")]
        [SerializeField] private float enterStagger = 0.08f;

        [Tooltip("左右出场错峰（秒）。")]
        [SerializeField] private float exitStagger = 0.06f;

        [Tooltip("点选命中用相机；留空则运行时取 Camera.main。")]
        [SerializeField] private Camera worldCamera;

        private Collider2D _leftCollider;
        private Collider2D _rightCollider;
        private Vector3 _leftRestLocal;
        private Vector3 _rightRestLocal;
        private Action<int, string> _onPicked;
        private Action _onSessionFinished;
        private bool _sessionLive;
        private bool _selectionLocked;
        private float _inputBlockRemaining;
        private string _leftOptionId = "room_left";
        private string _rightOptionId = "room_right";
        private int _hoveredSide = -1;
        private int _descriptionGeneration = -1;

        public bool IsSessionLive => _sessionLive;

        private void Awake()
        {
            EnsureBindings();
            CacheRestPositions();
            HideCardsAtStartAnchors();
        }

        private void OnDestroy()
        {
            Teardown();
        }

        private void Update()
        {
            if (!_sessionLive || _selectionLocked)
            {
                return;
            }

            if (_inputBlockRemaining > 0f)
            {
                _inputBlockRemaining = Mathf.Max(0f, _inputBlockRemaining - Time.unscaledDeltaTime);
                return;
            }

            UpdateHoverDescription();

            if (!WorldPointerUtility.WasPrimaryPressedThisFrame())
            {
                return;
            }

            if (WorldPointerUtility.TryPickCollider(worldCamera, _leftCollider))
            {
                BeginSelection(0, _leftOptionId);
                return;
            }

            if (WorldPointerUtility.TryPickCollider(worldCamera, _rightCollider))
            {
                BeginSelection(1, _rightOptionId);
            }
        }

        /// <summary>
        /// 打开房间二选一；optionIds 至少 2 个，对应左/右。
        /// </summary>
        public void Begin(
            string leftOptionId,
            string rightOptionId,
            Action<int, string> onPicked,
            Action onSessionFinished)
        {
            Teardown();
            EnsureBindings();
            CacheRestPositions();

            if (leftRoom == null || rightRoom == null)
            {
                Debug.LogError("[RoomChoicePresenter] 缺少 Room1 / Room2。");
                return;
            }

            _leftOptionId = string.IsNullOrWhiteSpace(leftOptionId) ? "room_left" : leftOptionId;
            _rightOptionId = string.IsNullOrWhiteSpace(rightOptionId) ? "room_right" : rightOptionId;
            _onPicked = onPicked;
            _onSessionFinished = onSessionFinished;
            _selectionLocked = false;
            _sessionLive = true;
            worldCamera = WorldPointerUtility.ResolveCamera(worldCamera);

            PlaceAt(leftRoom, leftStart != null ? leftStart.localPosition : _leftRestLocal + Vector3.up * 8f);
            PlaceAt(rightRoom, rightStart != null ? rightStart.localPosition : _rightRestLocal + Vector3.down * 8f);
            leftRoom.gameObject.SetActive(true);
            rightRoom.gameObject.SetActive(true);

            var safeEnter = Mathf.Max(0.05f, enterDuration);
            var safeStagger = Mathf.Max(0f, enterStagger);
            _inputBlockRemaining = safeEnter + safeStagger + 0.05f;

            leftRoom
                .DOLocalMove(_leftRestLocal, safeEnter)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .SetLink(leftRoom.gameObject, LinkBehaviour.KillOnDestroy);
            rightRoom
                .DOLocalMove(_rightRestLocal, safeEnter)
                .SetDelay(safeStagger)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .SetLink(rightRoom.gameObject, LinkBehaviour.KillOnDestroy);
        }

        public void Teardown()
        {
            _sessionLive = false;
            _selectionLocked = false;
            _inputBlockRemaining = 0f;
            _hoveredSide = -1;
            _onPicked = null;
            _onSessionFinished = null;
            ClearHoverDescription();

            if (leftRoom != null)
            {
                leftRoom.DOKill();
            }

            if (rightRoom != null)
            {
                rightRoom.DOKill();
            }
        }

        private void BeginSelection(int index, string optionId)
        {
            if (!_sessionLive || _selectionLocked)
            {
                return;
            }

            _selectionLocked = true;
            _hoveredSide = -1;
            ClearHoverDescription();
            var callback = _onPicked;
            _onPicked = null;
            callback?.Invoke(index, optionId);

            PlayExit(index);
        }

        private void UpdateHoverDescription()
        {
            var side = -1;
            if (WorldPointerUtility.TryPickCollider(worldCamera, _leftCollider))
            {
                side = 0;
            }
            else if (WorldPointerUtility.TryPickCollider(worldCamera, _rightCollider))
            {
                side = 1;
            }

            if (side == _hoveredSide)
            {
                return;
            }

            _hoveredSide = side;
            if (side < 0)
            {
                ClearHoverDescription();
                return;
            }

            ShowHoverDescription(side == 0 ? _leftOptionId : _rightOptionId);
        }

        private void ShowHoverDescription(string defId)
        {
            var mgr = DescriptionManagerSingleton.TryGetInstance();
            if (mgr == null)
            {
                return;
            }

            _descriptionGeneration = mgr.Show(defId);
        }

        private void ClearHoverDescription()
        {
            if (_descriptionGeneration < 0)
            {
                return;
            }

            var mgr = DescriptionManagerSingleton.TryGetInstance();
            mgr?.Clear(_descriptionGeneration);
            _descriptionGeneration = -1;
        }

        private void PlayExit(int selectedIndex)
        {
            EnsureBindings();
            var safeExit = Mathf.Max(0.05f, exitDuration);
            var safeStagger = Mathf.Max(0f, exitStagger);
            var pending = 0;

            void Track(Transform card, Vector3 endLocal, float delay)
            {
                if (card == null)
                {
                    return;
                }

                pending++;
                card.DOKill();
                card
                    .DOLocalMove(endLocal, safeExit)
                    .SetDelay(delay)
                    .SetEase(Ease.InBack)
                    .SetUpdate(true)
                    .SetLink(card.gameObject, LinkBehaviour.KillOnDestroy)
                    .OnComplete(() =>
                    {
                        pending--;
                        if (card != null)
                        {
                            card.gameObject.SetActive(false);
                        }

                        if (pending <= 0)
                        {
                            FinishSession();
                        }
                    });
            }

            var leftExit = leftEnd != null ? leftEnd.localPosition : _leftRestLocal + Vector3.down * 8f;
            var rightExit = rightEnd != null ? rightEnd.localPosition : _rightRestLocal + Vector3.up * 8f;

            // 选中侧先退，未选侧略迟退；左永远向下退，右永远向上退。
            if (selectedIndex == 0)
            {
                Track(leftRoom, leftExit, 0f);
                Track(rightRoom, rightExit, safeStagger);
            }
            else
            {
                Track(rightRoom, rightExit, 0f);
                Track(leftRoom, leftExit, safeStagger);
            }

            if (pending == 0)
            {
                FinishSession();
            }
        }

        private void FinishSession()
        {
            if (!_sessionLive)
            {
                // 可能已被 Teardown；仍通知一次完成更稳妥：仅在有回调时。
            }

            _sessionLive = false;
            var finished = _onSessionFinished;
            _onSessionFinished = null;
            finished?.Invoke();
        }

        private void EnsureBindings()
        {
            var root = transform;
            leftRoom ??= root.Find("Room1");
            rightRoom ??= root.Find("Room2");
            leftStart ??= root.Find("Room1Start");
            leftEnd ??= root.Find("Room1End");
            rightStart ??= root.Find("Room2Start");
            rightEnd ??= root.Find("Room2End");

            if (leftRoom != null)
            {
                _leftCollider = leftRoom.GetComponent<Collider2D>();
            }

            if (rightRoom != null)
            {
                _rightCollider = rightRoom.GetComponent<Collider2D>();
            }

            worldCamera = WorldPointerUtility.ResolveCamera(worldCamera);
        }

        private void CacheRestPositions()
        {
            if (leftRoom != null)
            {
                // 编辑器里房间通常已在落点；若当前在 start/end，则以「当前本地位置」为 rest，
                // 但 Start/End 已配好时 rest 取中间（与场景一致）。
                _leftRestLocal = new Vector3(
                    leftRoom.localPosition.x,
                    0f,
                    leftRoom.localPosition.z);
            }

            if (rightRoom != null)
            {
                _rightRestLocal = new Vector3(
                    rightRoom.localPosition.x,
                    0f,
                    rightRoom.localPosition.z);
            }
        }

        private void HideCardsAtStartAnchors()
        {
            EnsureBindings();
            if (leftRoom != null && leftStart != null)
            {
                leftRoom.localPosition = leftStart.localPosition;
                leftRoom.gameObject.SetActive(false);
            }

            if (rightRoom != null && rightStart != null)
            {
                rightRoom.localPosition = rightStart.localPosition;
                rightRoom.gameObject.SetActive(false);
            }
        }

        private static void PlaceAt(Transform card, Vector3 localPos)
        {
            if (card == null)
            {
                return;
            }

            card.DOKill();
            card.localPosition = localPos;
        }
    }
}

using System;
using System.Collections.Generic;
using DG.Tweening;
using NineGrid.Cards;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineGrid.Flow
{
    /// <summary>
    /// Bounce 扇形点选表现：入场弹性、悬停推挤、点选后未选项掉落 / 选中抬起。
    /// 由 SelectorManagerSingleton 驱动；卡牌以 RemovedMode Spawn，避免手牌/场地交互抢点。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BounceFanChoicePresenter : MonoBehaviour
    {
        private const string ChoiceSortingLayerName = "UI";
        private const int BaseSortingOrder = 6;

        [Header("Layout")]
        [Tooltip("选项容器相对本物体的本地偏移。")]
        [SerializeField] private Vector3 containerLocalOffset = new(0f, 0.5f, 0f);

        [Tooltip("相邻选项中心水平间距（世界本地单位）；扇形相对中心对称。")]
        [SerializeField] private float spacing = 1.1f;

        [Tooltip("相邻选项旋转步进（度）；居中扇形，两侧对称。")]
        [SerializeField] private float rotationStep = 5f;

        [Header("Hover")]
        [Tooltip("悬停时邻卡水平推开距离。")]
        [SerializeField] private float hoverPushOffset = 1.6f;

        [Tooltip("悬停卡本地抬升 Y。")]
        [SerializeField] private float hoverLiftY = 0.28f;

        [Tooltip("悬停缓动时长（秒）。")]
        [SerializeField] private float hoverDuration = 0.4f;

        [Tooltip("邻卡推开的阶梯延迟（秒 × 距离档）。")]
        [SerializeField] private float hoverSiblingDelayStep = 0.03f;

        [Tooltip("OutBack overshoot。")]
        [SerializeField] private float hoverOvershoot = 1.4f;

        [Header("Entry")]
        [Tooltip("入场前等待（秒）。")]
        [SerializeField] private float entryDelay = 0.42f;

        [Tooltip("卡与卡入场错峰（秒）。")]
        [SerializeField] private float entryStagger = 0.08f;

        [Tooltip("单张入场缩放时长（秒）。")]
        [SerializeField] private float entryDuration = 0.7f;

        [Header("Selection Exit")]
        [Tooltip("未选项抛物掉落初速度向上分量。")]
        [SerializeField] private float fallLaunchUpward = 3.4f;

        [Tooltip("掉落重力。")]
        [SerializeField] private float fallGravity = 24f;

        [Tooltip("掉落水平基础位移。")]
        [SerializeField] private float fallHorizontalReach = 4.2f;

        [Tooltip("距选中越远，水平位移额外步进。")]
        [SerializeField] private float fallHorizontalReachStep = 0.9f;

        [Tooltip("掉落自旋角度下限。")]
        [SerializeField] private float fallSpinMin = 55f;

        [Tooltip("掉落自旋角度上限。")]
        [SerializeField] private float fallSpinMax = 145f;

        [Tooltip("掉落错峰步进。")]
        [SerializeField] private float fallStaggerStep = 0.035f;

        [Tooltip("未选项强制掉落时长（秒）；固定播完后再收尾销毁，保证一直落到屏幕外。")]
        [SerializeField] private float fallDuration = 5f;

        [Tooltip("选中卡抬起时长。")]
        [SerializeField] private float selectedLiftDuration = 0.5f;

        [Tooltip("选中卡缩小到看不见的时长（秒）。")]
        [SerializeField] private float selectedShrinkDuration = 0.45f;

        [Tooltip("点选后额外缓冲再收尾销毁（秒）；会与掉落/缩小时长取较大值。")]
        [SerializeField] private float finishDelayAfterPick = 0.1f;

        [Tooltip("点选命中用相机；留空则运行时取 Camera.main。")]
        [SerializeField] private Camera worldCamera;

        private readonly List<BounceEntry> _entries = new();
        private readonly List<Tween> _hoverTweens = new();

        private Transform _cardsContainer;
        private Action<int, string> _onPicked;
        private int _hoveredIndex = -1;
        private float _entryBlockRemaining;
        private bool _selectionLocked;
        private bool _sessionLive;
        private int _pendingFallCount;

        private void Awake()
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            EnsureContainer();
        }

        private void OnDestroy()
        {
            Teardown();
        }

        private void Update()
        {
            if (!_sessionLive || _selectionLocked || _entries.Count == 0)
            {
                return;
            }

            if (_entryBlockRemaining > 0f)
            {
                _entryBlockRemaining = Mathf.Max(0f, _entryBlockRemaining - Time.unscaledDeltaTime);
                return;
            }

            var hovered = DetermineHoveredIndex();
            if (hovered == _hoveredIndex)
            {
                if (hovered >= 0 && Input.GetMouseButtonDown(0))
                {
                    BeginSelection(hovered);
                }

                return;
            }

            _hoveredIndex = hovered;
            if (hovered < 0)
            {
                AnimateReset();
            }
            else
            {
                AnimateHover(hovered);
            }
        }

        public void Begin(IReadOnlyList<string> optionDefIds, Action<int, string> onPicked)
        {
            Teardown();

            if (optionDefIds == null || optionDefIds.Count == 0)
            {
                Debug.LogWarning("[BounceFanChoice] 无选项。");
                return;
            }

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            EnsureContainer();
            _onPicked = onPicked;
            _selectionLocked = false;
            _hoveredIndex = -1;
            _entryBlockRemaining = 0f;
            _pendingFallCount = 0;
            _sessionLive = true;

            BuildEntries(optionDefIds);
            PlayEntryAnimation();
        }

        public void Teardown()
        {
            _sessionLive = false;
            _selectionLocked = false;
            _hoveredIndex = -1;
            _entryBlockRemaining = 0f;
            _onPicked = null;
            _pendingFallCount = 0;
            KillHoverTweens();
            ReleaseAllEntries();
        }

        private void EnsureContainer()
        {
            if (_cardsContainer != null)
            {
                return;
            }

            var existing = transform.Find("BounceCardsContainer");
            if (existing != null)
            {
                _cardsContainer = existing;
                _cardsContainer.localPosition = containerLocalOffset;
                return;
            }

            var go = new GameObject("BounceCardsContainer");
            _cardsContainer = go.transform;
            _cardsContainer.SetParent(transform, false);
            _cardsContainer.localPosition = containerLocalOffset;
        }

        private void BuildEntries(IReadOnlyList<string> optionDefIds)
        {
            var cardManager = CardManagerSingleton.Instance;
            if (cardManager == null)
            {
                Debug.LogError("[BounceFanChoice] CardManagerSingleton 缺失。");
                return;
            }

            var count = optionDefIds.Count;
            for (var i = 0; i < count; i++)
            {
                var defId = string.IsNullOrWhiteSpace(optionDefIds[i])
                    ? CardManagerSingleton.StandardDefId
                    : optionDefIds[i];

                var wrapper = new GameObject($"BounceOption_{i}_{defId}");
                wrapper.transform.SetParent(_cardsContainer, false);
                wrapper.transform.localPosition = new Vector3(GetCenteredOffsetX(i, count), 0f, 0f);
                wrapper.transform.localRotation = Quaternion.Euler(0f, 0f, GetCenteredRotationZ(i, count));
                wrapper.transform.localScale = Vector3.zero;

                var managed = cardManager.Spawn(defId, wrapper.transform, CardDisplayMode.RemovedMode);
                if (managed?.View == null)
                {
                    Destroy(wrapper);
                    continue;
                }

                managed.View.transform.localPosition = Vector3.zero;
                managed.View.transform.localRotation = Quaternion.identity;

                var sortingGroup = managed.View.GetComponent<SortingGroup>();
                var sorting = BaseSortingOrder + i;
                if (sortingGroup != null)
                {
                    ApplySorting(sortingGroup, sorting);
                }

                var collider = managed.View.GetComponent<Collider2D>();
                _entries.Add(new BounceEntry
                {
                    Wrapper = wrapper.transform,
                    Card = managed,
                    Collider = collider,
                    DefId = managed.DefId,
                    BaseLocalPosition = wrapper.transform.localPosition,
                    BaseLocalRotationZ = GetCenteredRotationZ(i, count),
                    SortingOrder = sorting,
                });
            }
        }

        private void PlayEntryAnimation()
        {
            var count = _entries.Count;
            var safeEntryDelay = Mathf.Max(0f, entryDelay);
            var safeEntryStagger = Mathf.Max(0f, entryStagger);
            var safeEntryDuration = Mathf.Max(0.1f, entryDuration);
            _entryBlockRemaining = count > 0
                ? safeEntryDelay + ((count - 1) * safeEntryStagger) + safeEntryDuration
                : 0f;

            for (var i = 0; i < count; i++)
            {
                var entry = _entries[i];
                if (entry.Wrapper == null)
                {
                    continue;
                }

                entry.Wrapper.localScale = Vector3.zero;
                entry.Wrapper
                    .DOScale(Vector3.one, safeEntryDuration)
                    .SetDelay(safeEntryDelay + (i * safeEntryStagger))
                    .SetEase(Ease.OutElastic)
                    .SetLink(entry.Wrapper.gameObject, LinkBehaviour.KillOnDestroy);
            }
        }

        private int DetermineHoveredIndex()
        {
            if (worldCamera == null || !TryGetPointerWorld(out var pointerWorld))
            {
                return -1;
            }

            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                var entry = _entries[i];
                if (entry.Collider != null && entry.Collider.OverlapPoint(pointerWorld))
                {
                    return i;
                }
            }

            return -1;
        }

        private void AnimateHover(int hoveredIndex)
        {
            KillHoverTweens();
            var safeDuration = Mathf.Max(0.05f, hoverDuration);
            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry.Wrapper == null)
                {
                    continue;
                }

                if (i == hoveredIndex)
                {
                    ApplySortingOrder(entry, BaseSortingOrder + _entries.Count + 20);
                    TrackHoverTween(entry.Wrapper
                        .DOLocalMove(entry.BaseLocalPosition + new Vector3(0f, hoverLiftY, 0f), safeDuration)
                        .SetEase(Ease.OutBack, Mathf.Max(0f, hoverOvershoot)));
                    TrackHoverTween(entry.Wrapper
                        .DOLocalRotate(Vector3.zero, safeDuration)
                        .SetEase(Ease.OutBack, Mathf.Max(0f, hoverOvershoot)));
                    continue;
                }

                ApplySortingOrder(entry, entry.SortingOrder);
                var direction = i < hoveredIndex ? -1f : 1f;
                var distance = Mathf.Abs(hoveredIndex - i);
                var targetPos = entry.BaseLocalPosition + new Vector3(direction * hoverPushOffset, 0f, 0f);
                var delay = Mathf.Max(0f, hoverSiblingDelayStep) * distance;

                TrackHoverTween(entry.Wrapper
                    .DOLocalMove(targetPos, safeDuration)
                    .SetDelay(delay)
                    .SetEase(Ease.OutBack, Mathf.Max(0f, hoverOvershoot)));
                TrackHoverTween(entry.Wrapper
                    .DOLocalRotate(new Vector3(0f, 0f, entry.BaseLocalRotationZ), safeDuration)
                    .SetDelay(delay)
                    .SetEase(Ease.OutBack, Mathf.Max(0f, hoverOvershoot)));
            }
        }

        private void AnimateReset()
        {
            KillHoverTweens();
            var safeDuration = Mathf.Max(0.05f, hoverDuration);
            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry.Wrapper == null)
                {
                    continue;
                }

                ApplySortingOrder(entry, entry.SortingOrder);
                TrackHoverTween(entry.Wrapper
                    .DOLocalMove(entry.BaseLocalPosition, safeDuration)
                    .SetEase(Ease.OutBack, Mathf.Max(0f, hoverOvershoot)));
                TrackHoverTween(entry.Wrapper
                    .DOLocalRotate(new Vector3(0f, 0f, entry.BaseLocalRotationZ), safeDuration)
                    .SetEase(Ease.OutBack, Mathf.Max(0f, hoverOvershoot)));
            }
        }

        private void BeginSelection(int selectedIndex)
        {
            if (selectedIndex < 0 || selectedIndex >= _entries.Count)
            {
                return;
            }

            _selectionLocked = true;
            _hoveredIndex = -1;
            KillHoverTweens();

            var selected = _entries[selectedIndex];
            var fallRemaining = 0;
            for (var i = 0; i < _entries.Count; i++)
            {
                if (i == selectedIndex)
                {
                    continue;
                }

                fallRemaining++;
                AnimateFallOff(i, selectedIndex, () =>
                {
                    fallRemaining--;
                    _pendingFallCount = fallRemaining;
                });
            }

            _pendingFallCount = fallRemaining;

            var safeSelectedLift = Mathf.Max(0.05f, selectedLiftDuration);
            var safeSelectedShrink = Mathf.Max(0.05f, selectedShrinkDuration);
            if (selected.Wrapper != null)
            {
                ApplySortingOrder(selected, BaseSortingOrder + _entries.Count + 40);
                selected.Wrapper
                    .DOLocalMove(selected.Wrapper.localPosition + new Vector3(0f, hoverLiftY, 0f), safeSelectedLift)
                    .SetEase(Ease.OutBack, hoverOvershoot)
                    .SetLink(selected.Wrapper.gameObject, LinkBehaviour.KillOnDestroy);
                selected.Wrapper
                    .DOLocalRotate(Vector3.zero, safeSelectedLift)
                    .SetEase(Ease.OutBack, hoverOvershoot)
                    .SetLink(selected.Wrapper.gameObject, LinkBehaviour.KillOnDestroy);
                selected.Wrapper
                    .DOScale(Vector3.zero, safeSelectedShrink)
                    .SetDelay(safeSelectedLift * 0.35f)
                    .SetEase(Ease.InBack)
                    .SetLink(selected.Wrapper.gameObject, LinkBehaviour.KillOnDestroy);
            }

            var defId = selected.DefId;
            var callback = _onPicked;
            _onPicked = null;
            callback?.Invoke(selectedIndex, defId);

            var fallExitDuration = Mathf.Max(0.1f, fallDuration)
                + Mathf.Max(0f, fallStaggerStep) * Mathf.Max(0, _entries.Count - 1);
            var selectedExitDuration = safeSelectedLift * 0.35f + safeSelectedShrink;
            var finishDelay = Mathf.Max(fallExitDuration, selectedExitDuration, finishDelayAfterPick);
            DOVirtual.DelayedCall(finishDelay, FinishSessionAfterPick)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }

        private void FinishSessionAfterPick()
        {
            if (!_sessionLive)
            {
                return;
            }

            var manager = SelectorManagerSingleton.Instance;
            if (manager != null)
            {
                manager.NotifySessionFinished();
            }
            else
            {
                Teardown();
            }
        }

        private void AnimateFallOff(int index, int selectedIndex, Action onComplete)
        {
            var entry = _entries[index];
            if (entry.Wrapper == null)
            {
                onComplete?.Invoke();
                return;
            }

            var wrapper = entry.Wrapper;
            var start = wrapper.position;
            var awayDirection = index < selectedIndex ? -1f : 1f;
            var distanceFromSelected = Mathf.Abs(index - selectedIndex);
            var horizontalReach = awayDirection
                * (fallHorizontalReach + distanceFromSelected * fallHorizontalReachStep);
            var gravity = Mathf.Max(0.1f, fallGravity);
            var launchUp = fallLaunchUpward;
            // 粗暴固定时长：按同一抛物线公式连播 5s，自然会落到屏幕外很远。
            var duration = Mathf.Max(0.1f, fallDuration);
            var velocityX = horizontalReach / duration;
            var startRotZ = wrapper.localEulerAngles.z;
            var spinZ = entry.BaseLocalRotationZ + awayDirection * UnityEngine.Random.Range(fallSpinMin, fallSpinMax);
            var stagger = Mathf.Max(0f, fallStaggerStep) * distanceFromSelected;

            DOTween.To(() => 0f, progress =>
                {
                    if (wrapper == null)
                    {
                        return;
                    }

                    var elapsed = progress * duration;
                    wrapper.position = new Vector3(
                        start.x + velocityX * elapsed,
                        start.y + launchUp * elapsed - 0.5f * gravity * elapsed * elapsed,
                        start.z);
                    wrapper.localRotation = Quaternion.Euler(
                        0f,
                        0f,
                        Mathf.LerpAngle(startRotZ, spinZ, progress));
                }, 1f, duration)
                .SetDelay(stagger)
                .SetEase(Ease.Linear)
                .SetLink(wrapper.gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(() =>
                {
                    if (wrapper != null)
                    {
                        wrapper.gameObject.SetActive(false);
                    }

                    onComplete?.Invoke();
                });
        }

        private float GetCenteredOffsetX(int index, int count)
        {
            return (index - (count - 1) * 0.5f) * spacing;
        }

        private float GetCenteredRotationZ(int index, int count)
        {
            return (index - (count - 1) * 0.5f) * -rotationStep;
        }

        private bool TryGetPointerWorld(out Vector3 worldPoint)
        {
            worldPoint = Vector3.zero;
            if (worldCamera == null)
            {
                return false;
            }

            var screen = Input.mousePosition;
            worldPoint = worldCamera.ScreenToWorldPoint(
                new Vector3(screen.x, screen.y, Mathf.Abs(worldCamera.transform.position.z)));
            worldPoint.z = 0f;
            return true;
        }

        private static void ApplySortingOrder(BounceEntry entry, int order)
        {
            if (entry.Card?.View == null)
            {
                return;
            }

            var sortingGroup = entry.Card.View.GetComponent<SortingGroup>();
            if (sortingGroup != null)
            {
                ApplySorting(sortingGroup, order);
            }
        }

        private static void ApplySorting(SortingGroup sortingGroup, int order)
        {
            sortingGroup.sortingLayerName = ChoiceSortingLayerName;
            sortingGroup.sortingOrder = order;
        }

        private void ReleaseAllEntries()
        {
            // 销毁/卸场景时绝不可走 Instance（会新建残留 CardManagerSingleton）。
            var cardManager = CardManagerSingleton.TryGetInstance();
            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry.Wrapper != null)
                {
                    entry.Wrapper.DOKill();
                }

                if (entry.Card != null && cardManager != null)
                {
                    cardManager.Release(entry.Card);
                }
                else if (entry.Card?.View != null)
                {
                    Destroy(entry.Card.View.gameObject);
                }

                if (entry.Wrapper != null)
                {
                    Destroy(entry.Wrapper.gameObject);
                }
            }

            _entries.Clear();
        }

        private void TrackHoverTween(Tween tween)
        {
            if (tween == null)
            {
                return;
            }

            _hoverTweens.Add(tween);
        }

        private void KillHoverTweens()
        {
            for (var i = _hoverTweens.Count - 1; i >= 0; i--)
            {
                var tween = _hoverTweens[i];
                if (tween != null && tween.IsActive())
                {
                    tween.Kill(false);
                }
            }

            _hoverTweens.Clear();
        }

        private sealed class BounceEntry
        {
            public Transform Wrapper;
            public ManagedCard Card;
            public Collider2D Collider;
            public string DefId;
            public Vector3 BaseLocalPosition;
            public float BaseLocalRotationZ;
            public int SortingOrder;
        }
    }
}

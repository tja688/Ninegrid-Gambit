using System;
using System.Collections.Generic;
using DG.Tweening;
using NineGrid.Cards;
using NineGrid.Cards.Anim;
using NineGrid.Core;
using NineGrid.Flow.Presentation;
using QFramework;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineGrid.Flow
{
    /// <summary>
    /// Bounce 扇形点选表现：入场弹性、悬停推挤、点选后未选项掉落 / 选中抬起。
    /// 由 SelectorManagerSingleton 驱动；卡牌以 RemovedMode Spawn，避免手牌/场地交互抢点。
    /// 选择命中用容器本地固定 AABB（相对静止中心），不跟悬停 tween，也不依赖卡面 Collider2D（ADR-0023）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BounceFanChoicePresenter : MonoBehaviour
    {
        private const string ChoiceSortingLayerName = "UI";
        private const int BaseSortingOrder = 6;

        [Header("Layout")]
        [Tooltip("选项容器相对本物体的本地偏移。")]
        [SerializeField] private Vector3 containerLocalOffset = new(0.4f, 0.5f, 0f);

        [Tooltip("相邻选项中心水平间距（世界本地单位）；扇形相对中心对称。")]
        [SerializeField] private float spacing = 1.1f;

        [Tooltip("相邻选项旋转步进（度）；居中扇形，两侧对称。")]
        [SerializeField] private float rotationStep = 5f;

        [Tooltip("选择判定框全尺寸（容器本地单位）；相对各选项静止中心，不跟随悬停推挤。")]
        [SerializeField] private Vector2 hitBoxSize = new(1.9f, 2.5f);

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
        private int _descriptionGeneration = -1;
        private float _entryBlockRemaining;
        private bool _selectionLocked;
        private bool _sessionLive;
        private bool _hoverOnNotice;
        private int _pendingFallCount;
        /// <summary>每次 Begin 递增；点选后 DelayedCall 必须校验，避免通关奖励退场回调拆掉下一轮（宝箱房）选项。</summary>
        private int _sessionId;
        private Tween _finishSessionTween;

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

            // 右键详述打开：遮挡 + 屏蔽选择；主键/右键只关详述，关后再点才选。
            if (CardInspectOverlayPresenter.IsOpen)
            {
                if (WorldPointerUtility.WasPrimaryPressedThisFrame()
                    || WorldPointerUtility.WasSecondaryPressedThisFrame())
                {
                    CardInspectOverlayPresenter.CloseIfOpen();
                }

                return;
            }

            var hovered = DetermineHoveredIndex();
            if (hovered >= 0)
            {
                if (WorldPointerUtility.WasPrimaryPressedThisFrame())
                {
                    BeginSelection(hovered);
                    return;
                }

                if (WorldPointerUtility.WasSecondaryPressedThisFrame())
                {
                    CardInspectOverlayPresenter.TryOpen(_entries[hovered].Card);
                    return;
                }
            }

            if (hovered == _hoveredIndex)
            {
                return;
            }

            _hoveredIndex = hovered;
            if (hovered < 0)
            {
                ClearHoverDescription();
                AnimateReset();
            }
            else
            {
                ShowHoverDescription(_entries[hovered].DefId);
                AnimateHover(hovered);
            }
        }

        public void Begin(
            IReadOnlyList<string> optionDefIds,
            Action<int, string> onPicked,
            bool hoverOnNotice = false)
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
            _hoverOnNotice = hoverOnNotice;
            _sessionId++;
            _sessionLive = true;

            BuildEntries(optionDefIds);
            // 数值只经 RewardOffered 指令 → 排期器/CardFaceStatHandler；视觉 spawn 后再冲刷。
            BattleBeatFlush.PresentLatestEventOfType(
                NineGridArchitecture.Current,
                CoreEventType.RewardOffered);
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
            _hoverOnNotice = false;
            ClearHoverDescription();
            KillFinishSessionTween();
            KillHoverTweens();
            ReleaseAllEntries();
        }

        private void EnsureContainer()
        {
            if (_cardsContainer == null)
            {
                var existing = transform.Find("BounceCardsContainer");
                if (existing != null)
                {
                    _cardsContainer = existing;
                }
                else
                {
                    var go = new GameObject("BounceCardsContainer");
                    _cardsContainer = go.transform;
                    _cardsContainer.SetParent(transform, false);
                }
            }

            _cardsContainer.localPosition = containerLocalOffset;
        }

        private void BuildEntries(IReadOnlyList<string> optionDefIds)
        {
            var cardManager = CardEntityLifecycleHook.CardsOrNull();
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
                // 保持 scale=1 套视觉 / Mask 锚定。入场动画 PlayEntryAnimation 再置 0 弹到 1。
                // 不得在此置 0：Begin 随后会 PresentLatestEventOfType(RewardOffered) 二次 Commit，
                // 若祖先 scale=0，世界空间锚定会把主图标钉出 Mask（遗物卡内容偏移时肉眼消失）。
                wrapper.transform.localScale = Vector3.one;

                // 负 Uid 纯表现卡：勿用 Spawn() 占 Core 正号段，否则会与后续 NewCard 洗回撞号。
                var choiceKind = InferChoicePresentationKind(defId);
                var managed = cardManager.SpawnPresentationOnly(
                    defId,
                    wrapper.transform,
                    CardDisplayMode.RemovedMode,
                    choiceKind);
                if (managed?.View == null)
                {
                    Destroy(wrapper);
                    continue;
                }

                managed.View.transform.localPosition = Vector3.zero;
                managed.View.transform.localRotation = Quaternion.identity;

                // 先切 UI SortingGroup，再套视觉：主视图 Mask 才能按有效层 Sync，
                // 避免 VisibleInsideMask 因层错位整段消失（登场无主图标）。
                var sortingGroup = managed.View.GetComponent<SortingGroup>();
                var sorting = BaseSortingOrder + i;
                if (sortingGroup != null)
                {
                    ApplySorting(sortingGroup, sorting);
                }

                // 只套视觉；攻/甲/血由 OfferReward 指令经排期器提交（禁止 clearCombatStats 数值旁路）。
                CoreCardPresentationMapper.ApplyVisualsByDefId(managed, choiceKind);
                CardMainVisualMaskAnchor.EnsureFaceBackgroundHexMask(
                    managed.MountedFaceRoot != null
                        ? managed.MountedFaceRoot
                        : managed.View.transform);
                CardMainVisualMaskAnchor.ResyncAllVisibleInsideMasks(managed.View.transform);

                _entries.Add(new BounceEntry
                {
                    Wrapper = wrapper.transform,
                    Card = managed,
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
            if (_cardsContainer == null
                || worldCamera == null
                || !TryGetPointerWorld(out var pointerWorld))
            {
                return -1;
            }

            var pointerLocal = (Vector2)_cardsContainer.InverseTransformPoint(pointerWorld);
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                if (ContainsHitBox(pointerLocal, _entries[i].BaseLocalPosition, hitBoxSize))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// 容器本地固定 AABB 命中（EditMode / 结构测可复用）。中心取静止 BaseLocalPosition，不跟悬停 tween。
        /// </summary>
        public static bool ContainsHitBox(Vector2 pointerLocal, Vector2 centerLocal, Vector2 boxSize)
        {
            var half = boxSize * 0.5f;
            return pointerLocal.x >= centerLocal.x - half.x
                   && pointerLocal.x <= centerLocal.x + half.x
                   && pointerLocal.y >= centerLocal.y - half.y
                   && pointerLocal.y <= centerLocal.y + half.y;
        }

        /// <summary>倒序遍历静止中心；同点重叠时取靠后（上层）选项。</summary>
        public static int ResolveHoveredIndex(
            Vector2 pointerLocal,
            IReadOnlyList<Vector2> baseLocalCenters,
            Vector2 boxSize)
        {
            if (baseLocalCenters == null || baseLocalCenters.Count == 0)
            {
                return -1;
            }

            for (var i = baseLocalCenters.Count - 1; i >= 0; i--)
            {
                if (ContainsHitBox(pointerLocal, baseLocalCenters[i], boxSize))
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
            ClearHoverDescription();
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
            var sessionIdAtPick = _sessionId;
            KillFinishSessionTween();
            _finishSessionTween = DOVirtual.DelayedCall(
                    finishDelay,
                    () => FinishSessionAfterPick(sessionIdAtPick))
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }

        private void FinishSessionAfterPick(int sessionIdAtPick)
        {
            _finishSessionTween = null;
            // 通关奖励点选后 Present 立即返回；约 5s 退场 DelayedCall 可能落在宝箱房新会话上。
            if (!_sessionLive || sessionIdAtPick != _sessionId)
            {
                return;
            }

            var manager = UnityEngine.Object.FindFirstObjectByType<SelectorManagerSingleton>();
            if (manager != null)
            {
                manager.NotifySessionFinished();
            }
            else
            {
                Teardown();
            }
        }

        private void KillFinishSessionTween()
        {
            if (_finishSessionTween == null)
            {
                return;
            }

            if (_finishSessionTween.IsActive())
            {
                _finishSessionTween.Kill(false);
            }

            _finishSessionTween = null;
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
            return WorldPointerUtility.TryGetPointerWorld(worldCamera, out worldPoint);
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
                CardMainVisualMaskAnchor.ResyncAllVisibleInsideMasks(entry.Card.View.transform);
            }
        }

        private static void ApplySorting(SortingGroup sortingGroup, int order)
        {
            sortingGroup.sortingLayerName = ChoiceSortingLayerName;
            sortingGroup.sortingOrder = order;
            // SpriteMask custom range 比对子节点 sortingLayerID 属性；必须与 SG 同层。
            CardMainVisualMaskAnchor.PropagateSortingLayerFromGroup(sortingGroup);
        }

        private void ReleaseAllEntries()
        {
            // _entries 中的 ManagedCard 可跨异步边界存活；UID 复用后须走实例所有权校验，
            // 绝不能仅凭 UID 释放后来者（见 CardManagerSingleton.Release(ManagedCard)）。
            // 销毁/卸场景时绝不可走 Instance（会新建残留 CardManagerSingleton）。
            var cardManager = CardEntityLifecycleHook.CardsOrNull();
            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry.Wrapper != null)
                {
                    entry.Wrapper.DOKill();
                }

                if (entry.Card != null && cardManager != null)
                {
                    cardManager.Release(entry.Card, "BounceFan.ReleaseEntry");
                }
                else if (entry.Card?.View != null)
                {
                    CardPresentationProbe.Despawn(
                        entry.Card.Uid,
                        "BounceFan.DestroyBypass",
                        reason: "cardManagerNull",
                        caller: nameof(ReleaseAllEntries));
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

        private void ShowHoverDescription(string defId)
        {
            // 动态描述 TMP 已退役；卡面 Basic_Description 为静态权威。
        }

        private void ClearHoverDescription()
        {
            _descriptionGeneration = -1;
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

        private static CardPresentationKind InferChoicePresentationKind(string defId)
        {
            if (string.IsNullOrEmpty(defId))
            {
                return CardPresentationKind.Unknown;
            }

            if (defId.StartsWith("relic.", StringComparison.OrdinalIgnoreCase))
            {
                return CardPresentationKind.Relic;
            }

            if (defId.StartsWith("help.", StringComparison.OrdinalIgnoreCase)
                || defId.StartsWith("player.", StringComparison.OrdinalIgnoreCase))
            {
                return CardPresentationKind.HelpCard;
            }

            if (defId.StartsWith("monster.", StringComparison.OrdinalIgnoreCase))
            {
                return CardPresentationKind.Monster;
            }

            if (string.Equals(defId, "Attack", StringComparison.Ordinal)
                || string.Equals(defId, "Armor", StringComparison.Ordinal)
                || string.Equals(defId, "Hp", StringComparison.Ordinal))
            {
                return CardPresentationKind.Item;
            }

            return CardPresentationKind.Unknown;
        }

        private sealed class BounceEntry
        {
            public Transform Wrapper;
            public ManagedCard Card;
            public string DefId;
            public Vector3 BaseLocalPosition;
            public float BaseLocalRotationZ;
            public int SortingOrder;
        }
    }
}

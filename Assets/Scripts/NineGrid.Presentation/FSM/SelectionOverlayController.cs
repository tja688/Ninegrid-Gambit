using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Presentation.Performance;
using UnityEngine;

namespace NineGrid.Presentation.FSM
{
    /// <summary>
    /// 选择覆盖层会话：承接入场 / 悬停 / 确认 / 退场原子表演，供适配器与 FSM 共用。
    /// 仅通关帮助卡与宝箱/遗物 OfferReward 在最右侧追加跳过卡。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SelectionOverlayController : MonoBehaviour
    {
        private enum SessionPhase
        {
            Idle,
            Entering,
            Interactive,
            Dismissing,
        }

        [Header("Layout")]
        [SerializeField] private Vector3 containerLocalOffset = new(0f, 0.5f, 0f);
        [SerializeField, Min(0)] private int baseSortingOrder = 420;
        [SerializeField] private SelectionFanLayout fanLayout = new();

        [Header("Assets")]
        [SerializeField] private GameObject cardPreviewPrefab;
        [SerializeField] private Transform optionsRoot;
        [SerializeField] private Transform confirmTargetSlot;
        [SerializeField] private Camera worldCamera;

        [Header("Performances")]
        [SerializeField] private SelectionOverlayEntrancePerformance entrancePerformance;
        [SerializeField] private SelectionOptionHoverPerformance hoverPerformance;
        [SerializeField] private SelectionOptionFallOffPerformance fallOffPerformance;
        [SerializeField] private SelectionOptionConfirmPerformance confirmPerformance;

        private readonly List<Transform> optionActors = new();
        private readonly List<SelectionOptionHoverPerformance.OptionActor> hoverActors = new();
        private readonly List<float> optionBaseRotations = new();
        private readonly List<int> optionSortingOrders = new();
        private readonly List<SelectionOverlayOptionDescriptor> optionDescriptors = new();

        private Transform optionsContainer;
        private SessionPhase phase = SessionPhase.Idle;
        private SelectionOverlaySessionKind sessionKind = SelectionOverlaySessionKind.None;
        private int hoveredIndex = -1;
        private int pendingFallCount;
        private bool confirmFinished;
        private int itemOptionItemUid;

        public SelectionOverlaySessionKind SessionKind => sessionKind;
        public bool IsInteractive => phase == SessionPhase.Interactive;
        public bool IsBusy => phase != SessionPhase.Idle;
        public int OptionCount => optionActors.Count;

        public bool HasPassOption =>
            optionDescriptors.Count > 0 && optionDescriptors[optionDescriptors.Count - 1].IsPass;

        public int PassIndex => HasPassOption ? optionDescriptors.Count - 1 : -1;

        public event Action<int, SelectionOverlayOptionDescriptor> SelectionCommitted;

        private void Awake()
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            EnsurePerformancesOnSameObject();
        }

        public void BeginRewardSession(CoreViewSnapshot snapshot, string poolId)
        {
            if (snapshot == null || snapshot.RewardOptions.Count == 0)
            {
                return;
            }

            var descriptors = new List<SelectionOverlayOptionDescriptor>();
            for (var i = 0; i < snapshot.RewardOptions.Count; i++)
            {
                RewardEntry entry = snapshot.RewardOptions[i];
                descriptors.Add(new SelectionOverlayOptionDescriptor(
                    FormatRewardLabel(entry),
                    entry.DefId));
            }

            if (SelectionOverlaySkipRules.SupportsRewardSkip(poolId))
            {
                descriptors.Add(new SelectionOverlayOptionDescriptor("跳过", isPass: true));
            }

            BeginSession(SelectionOverlaySessionKind.RewardChoice, descriptors);
        }

        public void BeginRoomSession(CoreViewSnapshot snapshot)
        {
            if (snapshot == null || snapshot.RoomOptions.Count == 0)
            {
                return;
            }

            var descriptors = new List<SelectionOverlayOptionDescriptor>();
            for (var i = 0; i < snapshot.RoomOptions.Count; i++)
            {
                RoomKind room = snapshot.RoomOptions[i];
                descriptors.Add(new SelectionOverlayOptionDescriptor(FormatRoomLabel(room), room.ToString()));
            }

            BeginSession(SelectionOverlaySessionKind.RoomChoice, descriptors);
        }

        public void BeginEnterRoomSession(RoomKind roomKind)
        {
            if (roomKind == RoomKind.None)
            {
                return;
            }

            var descriptors = new List<SelectionOverlayOptionDescriptor>
            {
                new("进入 " + FormatRoomLabel(roomKind), roomKind.ToString()),
            };
            BeginSession(SelectionOverlaySessionKind.EnterRoom, descriptors);
        }

        public void BeginItemOptionSession(int itemUid, IReadOnlyList<string> optionLabels)
        {
            if (optionLabels == null || optionLabels.Count == 0)
            {
                return;
            }

            itemOptionItemUid = itemUid;
            var descriptors = new List<SelectionOverlayOptionDescriptor>();
            for (var i = 0; i < optionLabels.Count; i++)
            {
                descriptors.Add(new SelectionOverlayOptionDescriptor(optionLabels[i], optionLabels[i]));
            }

            BeginSession(SelectionOverlaySessionKind.ItemOption, descriptors);
        }

        public int ItemOptionItemUid => itemOptionItemUid;

        public bool TryGetDescriptor(int index, out SelectionOverlayOptionDescriptor descriptor)
        {
            if (index < 0 || index >= optionDescriptors.Count)
            {
                descriptor = null;
                return false;
            }

            descriptor = optionDescriptors[index];
            return true;
        }

        public bool IsPassIndex(int index)
        {
            return HasPassOption && index == PassIndex;
        }

        public IEnumerator PlayEntranceCoroutine()
        {
            if (optionActors.Count == 0)
            {
                yield break;
            }

            phase = SessionPhase.Entering;
            hoveredIndex = -1;
            bool completed = false;
            hoverPerformance?.SetOptions(hoverActors);
            entrancePerformance?.Play(optionActors, () => completed = true);

            float timeout = entrancePerformance != null
                ? entrancePerformance.ComputeTotalDuration(optionActors.Count) + 0.35f
                : 0.1f;
            yield return WaitUntilOrTimeout(() => completed || entrancePerformance == null || !entrancePerformance.IsPlaying, timeout);

            if (phase == SessionPhase.Entering)
            {
                phase = SessionPhase.Interactive;
            }
        }

        public IEnumerator PlayDismissCoroutine(int selectedIndex)
        {
            if (selectedIndex < 0 || selectedIndex >= optionActors.Count || optionActors.Count == 0)
            {
                ClearSession();
                yield break;
            }

            phase = SessionPhase.Dismissing;
            hoveredIndex = -1;
            hoverPerformance?.StopAndRestore();

            pendingFallCount = 0;
            confirmFinished = false;

            for (var i = 0; i < optionActors.Count; i++)
            {
                if (i == selectedIndex)
                {
                    continue;
                }

                pendingFallCount++;
                int index = i;
                Transform actor = optionActors[i];
                fallOffPerformance?.Play(
                    actor,
                    index,
                    selectedIndex,
                    optionBaseRotations[index],
                    worldCamera,
                    OnFallFinished);
            }

            if (pendingFallCount == 0)
            {
                OnFallFinished();
            }

            Transform selected = optionActors[selectedIndex];
            bool confirmDone = false;
            confirmPerformance?.Play(
                selected,
                confirmTargetSlot,
                optionSortingOrders[selectedIndex],
                optionActors.Count,
                () => confirmDone = true);

            float timeout = 2.5f;
            yield return WaitUntilOrTimeout(() => confirmDone && pendingFallCount <= 0, timeout);
            ClearSession();
        }

        public IEnumerator PlaySkipDismissCoroutine()
        {
            if (!HasPassOption)
            {
                ClearSession();
                yield break;
            }

            yield return PlayDismissCoroutine(PassIndex);
        }

        public void UpdatePointerHover(Vector3 screenPosition)
        {
            if (phase != SessionPhase.Interactive || optionActors.Count == 0)
            {
                return;
            }

            int hovered = DetermineHoveredIndex(screenPosition);
            if (hovered == hoveredIndex)
            {
                return;
            }

            hoveredIndex = hovered;
            if (hoveredIndex < 0)
            {
                hoverPerformance?.PlayReset();
            }
            else
            {
                hoverPerformance?.PlayHover(hoveredIndex);
            }
        }

        public bool TryCommitPointerClick(Vector3 screenPosition)
        {
            if (phase != SessionPhase.Interactive)
            {
                return false;
            }

            int index = DetermineHoveredIndex(screenPosition);
            if (index < 0)
            {
                return false;
            }

            CommitSelection(index);
            return true;
        }

        public void CommitSelection(int index)
        {
            if (phase != SessionPhase.Interactive || index < 0 || index >= optionDescriptors.Count)
            {
                return;
            }

            phase = SessionPhase.Dismissing;
            SelectionCommitted?.Invoke(index, optionDescriptors[index]);
        }

        public void ClearSession()
        {
            phase = SessionPhase.Idle;
            sessionKind = SelectionOverlaySessionKind.None;
            hoveredIndex = -1;
            pendingFallCount = 0;
            confirmFinished = false;
            itemOptionItemUid = 0;

            entrancePerformance?.StopAndRestore();
            hoverPerformance?.StopAndRestore();
            fallOffPerformance?.StopAndRestore();
            confirmPerformance?.StopAndRestore();

            if (optionsContainer != null)
            {
                optionsContainer.DOKill();
                Destroy(optionsContainer.gameObject);
                optionsContainer = null;
            }

            SelectionOptionVisual.DestroyActors(optionActors);
            hoverActors.Clear();
            optionBaseRotations.Clear();
            optionSortingOrders.Clear();
            optionDescriptors.Clear();
        }

        private void BeginSession(
            SelectionOverlaySessionKind kind,
            IReadOnlyList<SelectionOverlayOptionDescriptor> descriptors)
        {
            ClearSession();
            sessionKind = kind;
            optionDescriptors.AddRange(descriptors);
            BuildOptions();
        }

        private void BuildOptions()
        {
            Transform parent = optionsRoot != null ? optionsRoot : transform;
            var containerObject = new GameObject("SelectionOverlay");
            optionsContainer = containerObject.transform;
            optionsContainer.SetParent(parent, false);
            optionsContainer.localPosition = containerLocalOffset;

            int count = optionDescriptors.Count;
            for (var i = 0; i < count; i++)
            {
                Vector3 localPosition = fanLayout.GetLocalPosition(i, count);
                float rotationZ = fanLayout.GetRotationZ(i);
                int sortingOrder = baseSortingOrder + i;

                Transform actor = SelectionOptionVisual.CreatePreviewCard(
                    optionsContainer,
                    i,
                    cardPreviewPrefab,
                    localPosition,
                    rotationZ,
                    sortingOrder);

                SelectionOptionVisual.ApplyLabel(actor, optionDescriptors[i].Label);
                actor.localScale = Vector3.zero;
                optionActors.Add(actor);
                optionBaseRotations.Add(rotationZ);
                optionSortingOrders.Add(sortingOrder);
                hoverActors.Add(new SelectionOptionHoverPerformance.OptionActor(
                    actor,
                    localPosition,
                    rotationZ,
                    sortingOrder));
            }
        }

        private int DetermineHoveredIndex(Vector3 screenPosition)
        {
            if (worldCamera == null || !TryGetPointerWorld(screenPosition, out Vector3 pointerWorld))
            {
                return -1;
            }

            for (var i = optionActors.Count - 1; i >= 0; i--)
            {
                Transform actor = optionActors[i];
                if (actor == null)
                {
                    continue;
                }

                var collider = actor.GetComponentInChildren<Collider2D>();
                if (collider != null && collider.OverlapPoint(pointerWorld))
                {
                    return i;
                }

                Bounds? bounds = ResolveActorBounds(actor);
                if (bounds.HasValue && bounds.Value.Contains(pointerWorld))
                {
                    return i;
                }
            }

            return -1;
        }

        private static Bounds? ResolveActorBounds(Transform actor)
        {
            var renderers = actor.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers.Length == 0)
            {
                return null;
            }

            Bounds bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private bool TryGetPointerWorld(Vector3 screenPosition, out Vector3 worldPoint)
        {
            worldPoint = Vector3.zero;
            if (worldCamera == null)
            {
                return false;
            }

            float depth = Mathf.Abs(worldCamera.transform.position.z);
            worldPoint = worldCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, depth));
            worldPoint.z = 0f;
            return true;
        }

        private void OnFallFinished()
        {
            pendingFallCount = Mathf.Max(0, pendingFallCount - 1);
        }

        private static string FormatRewardLabel(RewardEntry entry)
        {
            if (entry == null)
            {
                return "奖励";
            }

            string kind = entry.Kind == CardKind.Unknown ? string.Empty : entry.Kind.ToString();
            if (entry.Count > 1)
            {
                return entry.DefId + " x" + entry.Count + (string.IsNullOrEmpty(kind) ? string.Empty : " (" + kind + ")");
            }

            return string.IsNullOrEmpty(kind) ? entry.DefId : entry.DefId + " (" + kind + ")";
        }

        private static string FormatRoomLabel(RoomKind room)
        {
            switch (room)
            {
                case RoomKind.Battle: return "战斗";
                case RoomKind.Elite: return "精英";
                case RoomKind.Boss: return "Boss";
                case RoomKind.Shop: return "商店";
                case RoomKind.Tavern: return "酒馆";
                case RoomKind.Fountain: return "泉水";
                case RoomKind.Gold: return "金币";
                case RoomKind.Treasure: return "宝箱";
                case RoomKind.Event: return "事件";
                default: return room.ToString();
            }
        }

        private void EnsurePerformancesOnSameObject()
        {
            if (entrancePerformance == null)
            {
                entrancePerformance = GetComponent<SelectionOverlayEntrancePerformance>();
            }

            if (hoverPerformance == null)
            {
                hoverPerformance = GetComponent<SelectionOptionHoverPerformance>();
            }

            if (fallOffPerformance == null)
            {
                fallOffPerformance = GetComponent<SelectionOptionFallOffPerformance>();
            }

            if (confirmPerformance == null)
            {
                confirmPerformance = GetComponent<SelectionOptionConfirmPerformance>();
            }

            if (entrancePerformance == null)
            {
                entrancePerformance = gameObject.AddComponent<SelectionOverlayEntrancePerformance>();
            }

            if (hoverPerformance == null)
            {
                hoverPerformance = gameObject.AddComponent<SelectionOptionHoverPerformance>();
            }

            if (fallOffPerformance == null)
            {
                fallOffPerformance = gameObject.AddComponent<SelectionOptionFallOffPerformance>();
            }

            if (confirmPerformance == null)
            {
                confirmPerformance = gameObject.AddComponent<SelectionOptionConfirmPerformance>();
            }
        }

        private static IEnumerator WaitUntilOrTimeout(Func<bool> condition, float timeoutSeconds)
        {
            float elapsed = 0f;
            while (!condition() && elapsed < timeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private void OnDestroy()
        {
            ClearSession();
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
            {
                ClearSession();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Flow.BoardBriefTip;
using QFramework;
using TMPro;
using UnityEngine;

namespace NineGrid.Flow.BattleInfoPreview
{
    /// <summary>
    /// 战斗信息预览面板：正式进战、Opening 发牌前硬阻塞展示。
    /// 场景根：<c>UI面板/战斗信息展示BG</c>（选中该物体即可在 Inspector 改配置）。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("NineGrid/Flow/Battle Info Preview Presenter")]
    public sealed class BattleInfoPreviewPresenter : MonoBehaviour
    {
        public const string RootObjectName = "战斗信息展示BG";
        public const string DimmerAcquireReason = "battle-info-preview";
        public const string DefaultAvatarDefId = "avatar.default";
        public const string DefaultLeaveTrapDefId = RegularTrapPool.LeaveTrapDefId;

        private static BattleInfoPreviewPresenter sInstance;

        [Header("场景绑定")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text roomInfoText;
        [SerializeField] private Transform playerGroup;
        [SerializeField] private Transform monsterGroup;
        [SerializeField] private Transform environmentGroup;
        [SerializeField] private Transform playerBodySlot;
        [SerializeField] private List<BattleInfoPreviewSlotView> playerItemSlots = new List<BattleInfoPreviewSlotView>();
        [SerializeField] private List<BattleInfoPreviewSlotView> monsterSlots = new List<BattleInfoPreviewSlotView>();
        [SerializeField] private List<BattleInfoPreviewSlotView> environmentSlots = new List<BattleInfoPreviewSlotView>();
        [SerializeField] private BattleInfoPreviewSlotView playerBodySlotView;

        [Header("配置")]
        [SerializeField] private BattleInfoPreviewCopySO copyConfig;
        [Tooltip("悬停四角框 tint；默认白以保留素材金色。")]
        [SerializeField] private Color highlightColor = Color.white;
        [Tooltip("悬停四角框（九宫）；空则用 F_U_Frame3 默认路径。")]
        [SerializeField] private Sprite highlightSprite;
        [SerializeField] private string avatarDefId = DefaultAvatarDefId;
        [SerializeField] private bool previewLeaveTrap = true;
        [SerializeField] private string leaveTrapDefId = DefaultLeaveTrapDefId;

        [Header("玩家立绘")]
        [Tooltip("相对「玩家本体占位」场景原位的本地偏移（XY）。")]
        [SerializeField] private Vector2 avatarIconOffset = Vector2.zero;
        [Tooltip("玩家立绘本地缩放（1 = 场景原缩放）。")]
        [SerializeField] private float avatarIconScale = 1f;

        [Header("槽位图标外部缩放")]
        [Tooltip("叠在卡面 mainVisual 之上：怪物槽统一外缩放。")]
        [SerializeField] private float monsterIconExternalScale = 1f;
        [Tooltip("叠在卡面 mainVisual 之上：玩家道具/技能等槽统一外缩放（种类多时可整体缩小）。")]
        [SerializeField] private float playerItemIconExternalScale = 0.75f;
        [Tooltip("叠在卡面 mainVisual 之上：环境/机关槽统一外缩放。")]
        [SerializeField] private float environmentIconExternalScale = 1f;
        [Tooltip("叠在卡面 mainVisual 之上：玩家本体立绘图标外缩放（与上方「玩家立绘」槽 Transform 缩放独立）。")]
        [SerializeField] private float avatarIconExternalScale = 1f;

        private UniTaskCompletionSource _dismissTcs;
        private bool _dimmerHeld;
        private bool _open;
        private UiOverlayHitProxy _panelHitProxy;
        private Vector3 _avatarSlotBaseLocalPos;
        private Vector3 _avatarSlotBaseLocalScale = Vector3.one;
        private bool _avatarSlotBaseCaptured;

        public static bool IsOpen
        {
            get
            {
                return TryGetLiveInstance(out var live) && live._open;
            }
        }

        public static BattleInfoPreviewPresenter InstanceOrNull()
        {
            return TryGetLiveInstance(out var live) ? live : null;
        }

        public static bool TryGetLiveInstance(out BattleInfoPreviewPresenter live)
        {
            if (sInstance != null)
            {
                live = sInstance;
                return true;
            }

            live = FindFirstObjectByType<BattleInfoPreviewPresenter>(FindObjectsInactive.Include);
            if (live != null)
            {
                sInstance = live;
                return true;
            }

            live = null;
            return false;
        }

        /// <summary>正式 Run 进战前：展示并 await；取消时关面板且不进战。</summary>
        public static async UniTask ShowAndWaitAsync(NodeDeckOptions options, CancellationToken ct)
        {
            var presenter = EnsureExists();
            if (presenter == null)
            {
                Debug.LogWarning("[BattleInfoPreview] 场景未找到战斗信息展示BG，跳过预览。");
                return;
            }

            await presenter.ShowAndWaitInternalAsync(options, ct);
        }

        /// <summary>
        /// 请求关闭预览。详述开着时拒绝（由调用方先关详述），避免连带进战。
        /// </summary>
        public static void RequestDismiss()
        {
            if (!TryGetLiveInstance(out var live) || !live._open)
            {
                return;
            }

            if (CardInspectOverlayPresenter.IsOpen)
            {
                return;
            }

            live.CompleteDismiss();
        }

        public static BattleInfoPreviewPresenter EnsureExists()
        {
            if (TryGetLiveInstance(out var live))
            {
                live.EnsureBindings();
                return live;
            }

            var root = FindPanelRoot();
            if (root == null)
            {
                return null;
            }

            var presenter = root.GetComponent<BattleInfoPreviewPresenter>();
            if (presenter == null)
            {
                presenter = root.AddComponent<BattleInfoPreviewPresenter>();
            }

            presenter.panelRoot = root;
            presenter.EnsureBindings();
            sInstance = presenter;
            return presenter;
        }

        private void Awake()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            EnsureBindings();
            sInstance = this;
            if (panelRoot != null && panelRoot.activeSelf && !_open)
            {
                panelRoot.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(sInstance, this))
            {
                sInstance = null;
            }

            ForceCloseInternal(releaseDimmer: true);
        }

        private async UniTask ShowAndWaitInternalAsync(NodeDeckOptions options, CancellationToken ct)
        {
            EnsureBindings();
            if (panelRoot == null)
            {
                return;
            }

            ForceCloseInternal(releaseDimmer: true);

            BattleUiDimmerOverlay.EnsureBound(
                FindUiPanelRoot()?.transform.Find("半黑屏BG")?.gameObject);
            if (!BattleUiDimmerOverlay.TryAcquire(DimmerAcquireReason))
            {
                Debug.LogWarning("[BattleInfoPreview] 半黑屏 Acquire 失败，跳过预览。");
                return;
            }

            _dimmerHeld = true;
            _open = true;
            _dismissTcs = new UniTaskCompletionSource();

            panelRoot.SetActive(true);
            WirePanelHitProxy(active: true);
            ApplyContent(options);

            try
            {
                using (ct.Register(() =>
                       {
                           ForceCloseInternal(releaseDimmer: true, completeAwait: false);
                           _dismissTcs?.TrySetCanceled(ct);
                       }))
                {
                    await _dismissTcs.Task;
                }
            }
            finally
            {
                ForceCloseInternal(releaseDimmer: true);
            }
        }

        private void CompleteDismiss()
        {
            if (!_open)
            {
                return;
            }

            _dismissTcs?.TrySetResult();
        }

        private void ForceCloseInternal(bool releaseDimmer, bool completeAwait = true)
        {
            var wasOpen = _open;
            _open = false;
            WirePanelHitProxy(active: false);
            ClearAllSlots();

            if (panelRoot != null && panelRoot.activeSelf)
            {
                panelRoot.SetActive(false);
            }

            if (releaseDimmer && _dimmerHeld)
            {
                BattleUiDimmerOverlay.Release(DimmerAcquireReason);
                _dimmerHeld = false;
            }

            RestoreAvatarSlotBaseTransform();

            if (completeAwait && wasOpen)
            {
                _dismissTcs?.TrySetResult();
            }
        }

        private void RestoreAvatarSlotBaseTransform()
        {
            if (!_avatarSlotBaseCaptured || playerBodySlot == null)
            {
                return;
            }

            playerBodySlot.localPosition = _avatarSlotBaseLocalPos;
            playerBodySlot.localScale = _avatarSlotBaseLocalScale;
        }

        private void ApplyContent(NodeDeckOptions options)
        {
            WireAllSlots();
            ApplyRoomInfoText();

            var avatarId = string.IsNullOrWhiteSpace(avatarDefId) ? DefaultAvatarDefId : avatarDefId.Trim();
            if (playerBodySlotView != null)
            {
                playerBodySlotView.Bind(
                    avatarId,
                    CardPresentationKind.Avatar,
                    Mathf.Max(0.01f, avatarIconExternalScale));
                ApplyAvatarPortraitTransform();
                playerBodySlotView.RefreshHitRegistration();
            }

            var playerDefs = CollectPlayerItemDefIdsForPreview(options);
            FillSlots(
                playerItemSlots,
                playerDefs,
                ResolvePlayerKind,
                Mathf.Max(0.01f, playerItemIconExternalScale));

            var monsterDefs = CollectDefIds(
                options?.EnemyCards,
                kind => kind == CardKind.Monster);
            FillSlots(
                monsterSlots,
                monsterDefs,
                _ => CardPresentationKind.Monster,
                Mathf.Max(0.01f, monsterIconExternalScale));

            var envDefs = CollectDefIds(
                options?.EnemyCards,
                kind => kind == CardKind.Trap);
            if (previewLeaveTrap)
            {
                var leaveId = string.IsNullOrWhiteSpace(leaveTrapDefId)
                    ? DefaultLeaveTrapDefId
                    : leaveTrapDefId.Trim();
                if (!envDefs.Contains(leaveId))
                {
                    envDefs.Add(leaveId);
                }
            }

            FillSlots(
                environmentSlots,
                envDefs,
                _ => CardPresentationKind.Trap,
                Mathf.Max(0.01f, environmentIconExternalScale));
        }

        private void ApplyRoomInfoText()
        {
            if (roomInfoText == null)
            {
                return;
            }

            var arch = NineGridArchitecture.Current;
            var run = arch != null ? arch.GetModel<RunModel>() : null;
            var floor = run != null ? run.Floor.Value : 0;
            var nodeIndex = run != null ? run.NodeIndex.Value : 0;
            var displayNode = MapNodeProgression.ToDisplayNode(nodeIndex);
            if (displayNode < 1 || displayNode > RunModel.NodesPerFloor)
            {
                displayNode = ((nodeIndex % RunModel.NodesPerFloor) + RunModel.NodesPerFloor)
                    % RunModel.NodesPerFloor + 1;
            }

            var room = run != null ? run.Room.Value : RoomKind.None;
            var roomName = BoardBriefTipCopy.FormatRoomHint(room);
            if (string.IsNullOrEmpty(roomName))
            {
                roomName = room != RoomKind.None ? room.ToString() : "—";
            }

            var progress = displayNode + "/" + RunModel.NodesPerFloor;
            var template = ResolveRoomInfoTemplate(floor, displayNode);
            roomInfoText.text = template
                .Replace("{room}", roomName)
                .Replace("{floor}", floor.ToString())
                .Replace("{progress}", progress)
                .Replace("{displayNode}", displayNode.ToString())
                .Replace("{nodesPerFloor}", RunModel.NodesPerFloor.ToString());
        }

        private string ResolveRoomInfoTemplate(int floor, int displayNode)
        {
            var so = copyConfig != null
                ? copyConfig
                : Resources.Load<BattleInfoPreviewCopySO>(BattleInfoPreviewCopySO.ResourcePath);
            if (copyConfig == null && so != null)
            {
                copyConfig = so;
            }

            if (so != null && so.TryResolveTemplate(floor, displayNode, out var template)
                && !string.IsNullOrWhiteSpace(template))
            {
                return template;
            }

            return "房间类型：{room}\n楼层：{floor}\n进度：{progress}";
        }

        /// <summary>
        /// 玩家侧道具预览：含来源池随机 + 固定卡 + 房间开局注入（与 <see cref="RewardSystem.BuildNodeDeckOptions"/> 同序生成）。
        /// 房间注入 / 固定卡排在列表尾部，槽位不足时优先展示「开局即可知」的专属牌。
        /// </summary>
        private List<string> CollectPlayerItemDefIdsForPreview(NodeDeckOptions options)
        {
            var all = CollectDefIds(options?.PlayerCards, kind => true);
            if (all.Count == 0)
            {
                return all;
            }

            var arch = NineGridArchitecture.Current;
            var roomCandidates = CollectRoomOpeningInjectCandidateDefIds(arch);
            var fixedCards = arch != null ? arch.GetModel<PlayerModel>().FixedItemCardDefIds : null;

            var prioritized = new List<string>();
            for (var i = 0; i < all.Count; i++)
            {
                var defId = all[i];
                if (!IsKnownAtOpeningDefId(defId, roomCandidates, fixedCards))
                {
                    continue;
                }

                if (!prioritized.Contains(defId))
                {
                    prioritized.Add(defId);
                }
            }

            for (var i = 0; i < all.Count; i++)
            {
                var defId = all[i];
                if (!prioritized.Contains(defId))
                {
                    prioritized.Add(defId);
                }
            }

            return prioritized;
        }

        private static bool IsKnownAtOpeningDefId(
            string defId,
            HashSet<string> roomCandidates,
            IReadOnlyList<string> fixedCards)
        {
            if (string.IsNullOrEmpty(defId))
            {
                return false;
            }

            if (roomCandidates != null && roomCandidates.Contains(defId))
            {
                return true;
            }

            if (fixedCards == null)
            {
                return false;
            }

            for (var i = 0; i < fixedCards.Count; i++)
            {
                if (string.Equals(fixedCards[i], defId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>本关房间 <see cref="RunModel.Room"/> 的玩家侧开局注入候选 defId（固定 + 加权池选项）。</summary>
        private static HashSet<string> CollectRoomOpeningInjectCandidateDefIds(IArchitecture arch)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (arch == null)
            {
                return result;
            }

            var run = arch.GetModel<RunModel>();
            var roomKind = run != null ? run.Room.Value : RoomKind.None;
            if (roomKind == RoomKind.None)
            {
                return result;
            }

            var catalog = arch.GetSystem<IContentSystem>()?.Catalog;
            RoomDefinition room;
            if (catalog == null || !catalog.Rewards.TryGetRoom(roomKind, out room) || room == null)
            {
                return result;
            }

            var injects = room.OpeningInjects;
            for (var i = 0; i < injects.Count; i++)
            {
                var inject = injects[i];
                if (inject == null || inject.Side != RoomInjectSide.Player)
                {
                    continue;
                }

                if (inject.SourceKind == RoomInjectSourceKind.FixedCard
                    && !string.IsNullOrEmpty(inject.CardDefId))
                {
                    result.Add(inject.CardDefId);
                    continue;
                }

                if (inject.SourceKind != RoomInjectSourceKind.WeightedPool)
                {
                    continue;
                }

                var pool = inject.Pool;
                for (var j = 0; j < pool.Count; j++)
                {
                    var option = pool[j];
                    if (option != null && !string.IsNullOrEmpty(option.CardDefId))
                    {
                        result.Add(option.CardDefId);
                    }
                }
            }

            return result;
        }

        /// <summary>按 defId 去重：同种只占一槽（出现几个种类显示几个）。</summary>
        private static List<string> CollectDefIds(
            IReadOnlyList<CardDraft> drafts,
            Func<CardKind, bool> predicate)
        {
            var list = new List<string>();
            if (drafts == null)
            {
                return list;
            }

            for (var i = 0; i < drafts.Count; i++)
            {
                var draft = drafts[i];
                if (draft == null || string.IsNullOrEmpty(draft.DefId) || !predicate(draft.Kind))
                {
                    continue;
                }

                if (list.Contains(draft.DefId))
                {
                    continue;
                }

                list.Add(draft.DefId);
            }

            return list;
        }

        private static CardPresentationKind ResolvePlayerKind(string defId)
        {
            return CoreCardPresentationMapper.ResolvePresentationKindFromDefId(defId);
        }

        private void FillSlots(
            List<BattleInfoPreviewSlotView> slots,
            List<string> defIds,
            Func<string, CardPresentationKind> kindResolver,
            float iconExternalScale)
        {
            if (slots == null)
            {
                return;
            }

            var count = defIds != null ? defIds.Count : 0;
            if (count > slots.Count && slots.Count > 0)
            {
                Debug.LogWarning(
                    "[BattleInfoPreview] 槽位不足：需要 "
                    + count
                    + " 仅有 "
                    + slots.Count
                    + "，多余截断。");
            }

            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null)
                {
                    continue;
                }

                if (i < count)
                {
                    slot.Bind(defIds[i], kindResolver(defIds[i]), iconExternalScale);
                    slot.RefreshHitRegistration();
                }
                else
                {
                    slot.Clear();
                    slot.RefreshHitRegistration();
                }
            }
        }

        private void ClearAllSlots()
        {
            playerBodySlotView?.Clear();
            playerBodySlotView?.RefreshHitRegistration();
            ClearSlotList(playerItemSlots);
            ClearSlotList(monsterSlots);
            ClearSlotList(environmentSlots);
        }

        private static void ClearSlotList(List<BattleInfoPreviewSlotView> slots)
        {
            if (slots == null)
            {
                return;
            }

            for (var i = 0; i < slots.Count; i++)
            {
                slots[i]?.Clear();
                slots[i]?.RefreshHitRegistration();
            }
        }

        private void WireAllSlots()
        {
            WireSlot(playerBodySlotView);
            WireSlotList(playerItemSlots);
            WireSlotList(monsterSlots);
            WireSlotList(environmentSlots);
        }

        private void WireSlotList(List<BattleInfoPreviewSlotView> slots)
        {
            if (slots == null)
            {
                return;
            }

            for (var i = 0; i < slots.Count; i++)
            {
                WireSlot(slots[i]);
            }
        }

        private void WireSlot(BattleInfoPreviewSlotView slot)
        {
            slot?.EnsureWired(highlightColor, highlightSprite);
        }

        private void CaptureAvatarSlotBaseIfNeeded()
        {
            if (_avatarSlotBaseCaptured || playerBodySlot == null)
            {
                return;
            }

            // 场景里 localPose 往往已含 OnValidate 叠过的 offset/scale；
            // 必须先扣回，否则 Player（无 OnValidate）会再叠一次 → 往左下偏。
            CaptureAvatarSlotBaseFromCurrentTransform();
        }

        /// <summary>
        /// 从当前 Transform 反推「场景原位」基准。Editor / Player 共用，避免 #if UNITY_EDITOR 分叉。
        /// </summary>
        private void CaptureAvatarSlotBaseFromCurrentTransform()
        {
            if (playerBodySlot == null)
            {
                return;
            }

            _avatarSlotBaseLocalPos = playerBodySlot.localPosition
                - new Vector3(avatarIconOffset.x, avatarIconOffset.y, 0f);
            var scale = Mathf.Max(0.01f, avatarIconScale);
            _avatarSlotBaseLocalScale = new Vector3(
                playerBodySlot.localScale.x / scale,
                playerBodySlot.localScale.y / scale,
                playerBodySlot.localScale.z);
            if (_avatarSlotBaseLocalScale.sqrMagnitude < 0.0001f)
            {
                _avatarSlotBaseLocalScale = Vector3.one;
            }

            _avatarSlotBaseCaptured = true;
        }

        private void ApplyAvatarPortraitTransform()
        {
            if (playerBodySlot == null)
            {
                return;
            }

            CaptureAvatarSlotBaseIfNeeded();
            var scale = Mathf.Max(0.01f, avatarIconScale);
            playerBodySlot.localPosition = _avatarSlotBaseLocalPos
                + new Vector3(avatarIconOffset.x, avatarIconOffset.y, 0f);
            playerBodySlot.localScale = new Vector3(
                _avatarSlotBaseLocalScale.x * scale,
                _avatarSlotBaseLocalScale.y * scale,
                _avatarSlotBaseLocalScale.z);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (playerBodySlot == null && playerBodySlotView != null)
            {
                playerBodySlot = playerBodySlotView.transform;
            }

            if (playerBodySlot == null)
            {
                return;
            }

            // Edit 模式首次：把当前场景位当基准，再叠偏移，方便在 Inspector 里拖调。
            if (!_avatarSlotBaseCaptured)
            {
                CaptureAvatarSlotBaseFromCurrentTransform();
            }

            ApplyAvatarPortraitTransform();
        }
#endif

        private void EnsureBindings()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject.name == RootObjectName ? gameObject : FindPanelRoot();
            }

            if (panelRoot == null)
            {
                return;
            }

            if (copyConfig == null)
            {
                copyConfig = Resources.Load<BattleInfoPreviewCopySO>(BattleInfoPreviewCopySO.ResourcePath);
            }

            if (highlightSprite == null)
            {
                highlightSprite = ResolveDefaultHighlightSprite();
            }

            if (playerGroup == null)
            {
                playerGroup = panelRoot.transform.Find("玩家");
            }

            if (monsterGroup == null)
            {
                monsterGroup = panelRoot.transform.Find("怪物");
            }

            if (environmentGroup == null)
            {
                environmentGroup = panelRoot.transform.Find("环境");
            }

            if (roomInfoText == null)
            {
                roomInfoText = FindRoomInfoText(panelRoot.transform);
            }

            if (playerBodySlot == null && playerGroup != null)
            {
                playerBodySlot = playerGroup.Find("玩家本体占位");
            }

            if (playerBodySlot != null)
            {
                CaptureAvatarSlotBaseIfNeeded();
            }

            if (playerBodySlotView == null && playerBodySlot != null)
            {
                playerBodySlotView = EnsureSlotView(playerBodySlot.gameObject);
            }

            CollectPlaceholderSlots(playerGroup, playerItemSlots, excludeBody: true);
            CollectPlaceholderSlots(monsterGroup, monsterSlots, excludeBody: false);
            CollectPlaceholderSlots(environmentGroup, environmentSlots, excludeBody: false);

            EnsurePanelColliderAndProxy();
        }

        private void CollectPlaceholderSlots(
            Transform group,
            List<BattleInfoPreviewSlotView> into,
            bool excludeBody)
        {
            if (group == null || into == null)
            {
                return;
            }

            if (into.Count > 0)
            {
                // 已在 Inspector 绑过则尊重序列化列表，只补齐组件。
                for (var i = 0; i < into.Count; i++)
                {
                    if (into[i] == null)
                    {
                        continue;
                    }

                    WireSlot(into[i]);
                }

                return;
            }

            for (var i = 0; i < group.childCount; i++)
            {
                var child = group.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                var name = child.name;
                if (excludeBody && name == "玩家本体占位")
                {
                    continue;
                }

                if (!name.StartsWith("占位模板", StringComparison.Ordinal)
                    && name != "玩家本体占位")
                {
                    continue;
                }

                into.Add(EnsureSlotView(child.gameObject));
            }
        }

        private BattleInfoPreviewSlotView EnsureSlotView(GameObject go)
        {
            if (go == null)
            {
                return null;
            }

            var view = go.GetComponent<BattleInfoPreviewSlotView>();
            if (view == null)
            {
                view = go.AddComponent<BattleInfoPreviewSlotView>();
            }

            view.EnsureWired(highlightColor, highlightSprite);
            return view;
        }

        private void EnsurePanelColliderAndProxy()
        {
            if (panelRoot == null)
            {
                return;
            }

            var col = panelRoot.GetComponent<BoxCollider2D>();
            if (col == null)
            {
                col = panelRoot.GetComponent<Collider2D>() as BoxCollider2D;
            }

            if (col == null)
            {
                col = panelRoot.AddComponent<BoxCollider2D>();
                col.size = new Vector2(8f, 5f);
            }

            col.isTrigger = false;

            _panelHitProxy = panelRoot.GetComponent<UiOverlayHitProxy>();
            if (_panelHitProxy == null)
            {
                _panelHitProxy = panelRoot.AddComponent<UiOverlayHitProxy>();
            }

            _panelHitProxy.Configure(
                UiOverlayHitAction.Swallow,
                BattleUiDimmerOverlay.HitSort + 2,
                PointerHitSurfacePriorities.Overlay);
        }

        private void WirePanelHitProxy(bool active)
        {
            if (_panelHitProxy == null)
            {
                return;
            }

            _panelHitProxy.enabled = active;
        }

        private static Sprite ResolveDefaultHighlightSprite()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                BattleInfoPreviewHighlight.DefaultCornerFrameAssetPath);
#else
            return null;
#endif
        }

        private static TMP_Text FindRoomInfoText(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            var texts = root.GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < texts.Length; i++)
            {
                var t = texts[i];
                if (t == null)
                {
                    continue;
                }

                if (t.gameObject.name == "房间信息"
                    || (t.text != null && t.text.Contains("房间类型")))
                {
                    // 分组标签「玩家：」等也叫房间信息 (1)；优先无父级分组名的。
                    var parent = t.transform.parent;
                    if (parent != null
                        && (parent.name == "玩家" || parent.name == "怪物" || parent.name == "环境"))
                    {
                        continue;
                    }

                    return t;
                }
            }

            return null;
        }

        private static GameObject FindPanelRoot()
        {
            var ui = FindUiPanelRoot();
            if (ui != null)
            {
                var child = ui.transform.Find(RootObjectName);
                if (child != null)
                {
                    return child.gameObject;
                }
            }

            var all = Resources.FindObjectsOfTypeAll<Transform>();
            for (var i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null || t.name != RootObjectName)
                {
                    continue;
                }

                if (!t.gameObject.scene.IsValid())
                {
                    continue;
                }

                return t.gameObject;
            }

            return null;
        }

        private static GameObject FindUiPanelRoot()
        {
            var all = Resources.FindObjectsOfTypeAll<Transform>();
            for (var i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t != null && t.name == "UI面板" && t.gameObject.scene.IsValid())
                {
                    return t.gameObject;
                }
            }

            return null;
        }
    }
}

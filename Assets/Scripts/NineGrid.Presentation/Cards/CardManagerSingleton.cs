using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using NineGrid.Cards.Convergence;
using NineGrid.Cards.Presentation;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineGrid.Cards
{
    /// <summary>
    /// 卡牌表现状态模式，由卡牌管理器统一装配与切换。
    /// </summary>
    public enum CardDisplayMode
    {
        CardDeckMode = 0,
        HandCardMode = 1,
        GroundCardMode = 2,
        RemovedMode = 3,
        DragCardMode = 4,
    }

    /// <summary>
    /// 表现层卡牌句柄。Uid 与 Core CardInstance.Uid 共用，是跨层查找同一张卡的唯一键。
    /// </summary>
    public sealed class ManagedCard
    {
        internal ManagedCard(int uid, string defId, StandardCardView view)
        {
            Uid = uid;
            DefId = defId ?? string.Empty;
            View = view;
            DisplayMode = CardDisplayMode.HandCardMode;
        }

        /// <summary>与 Core CardInstance.Uid 相同。</summary>
        public int Uid { get; }

        /// <summary>用于选取预制体，逻辑数据以 Core 为准。</summary>
        public string DefId { get; }

        public StandardCardView View { get; }

        public GameObject GameObject => View != null ? View.gameObject : null;

        public Transform Transform => View != null ? View.transform : null;

        public CardDisplayMode DisplayMode { get; internal set; }

        /// <summary>场地交战即死后标记；Vacate 后应立刻离锚点，待死亡特效结束再 Release。</summary>
        public bool IsFieldDead { get; internal set; }

        /// <summary>由 Flow 映射层写入的 Core 卡牌种类，Cards 程序集不直接引用 Core。</summary>
        public CardPresentationKind CoreKind { get; set; }

        /// <summary>
        /// 主路径挂入 FacePivot 的卡面根；RegisterPrefab 特例路径为 null。
        /// </summary>
        public Transform MountedFaceRoot { get; internal set; }

        /// <summary>
        /// 已提交的卡牌表现投影（可见值真相层 2）。对账应对齐此快照，而非最新 Core。
        /// </summary>
        public CardPresentationSnapshot CommittedPresentation { get; private set; }

        /// <summary>
        /// 投影提交：写入已提交快照并分发给底盘宿主 → Kind Binder。
        /// 仅应由战斗表演编排串行主线 / 对等 Commit 出口触发。
        /// </summary>
        public void CommitPresentation(CardPresentationSnapshot snapshot)
        {
            if (snapshot == null || View == null)
            {
                return;
            }

            CommittedPresentation = snapshot;
            CoreKind = snapshot.Kind;
            View.ApplyPresentation(snapshot);
        }

        /// <summary>
        /// 只写入已提交镜像（不立刻 Apply 视觉）；供翻牌动画路径先锁 Commit 再播 Flip。
        /// </summary>
        public void StoreCommittedPresentation(CardPresentationSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            CommittedPresentation = snapshot;
            CoreKind = snapshot.Kind;
        }

        /// <summary>
        /// 全场对账：重放已提交投影到卡面；无已提交快照时 no-op。
        /// </summary>
        public void ReapplyCommittedPresentation()
        {
            if (CommittedPresentation == null || View == null)
            {
                return;
            }

            View.ApplyPresentation(CommittedPresentation);
            var presenter = GameObject != null
                ? GameObject.GetComponent<CardFaceFlipPresenter>()
                : null;
            presenter?.SnapVisualFace(CommittedPresentation.FaceUp);
        }
    }

    /// <summary>
    /// 卡牌管理器单例：以 Core 下发的 Uid 为键，管理表现视图与显示模式。
    /// V6 compat shell — 跨手牌/牌库解析优先 <see cref="CardEntityLifecycleHook"/>；待调用点切换后可收缩。
    /// </summary>
    public sealed class CardManagerSingleton : MonoBehaviour
    {
        public const int InvalidUid = 0;
        public const string StandardDefId = "standard";

        private const string CardChassisPrefabAssetPath = CardChassisPaths.ChassisPrefab;


        [SerializeField]
        [Tooltip("卡牌根节点。运行时自动查找/装配：留空时 Awake EnsureCardRoot 创建 Cards 子节点。")]
        private Transform cardRoot;

        [SerializeField]
        [Tooltip("卡牌底盘预制体（变换塔 / SortingGroup / HitProxy / Effect）。主 Spawn 路径 Instantiate 此底盘后再挂 Kind 卡面。留空时编辑器 Awake 尝试加载 Assets/Resources/Prefabs/老Standard Card.prefab；仍空则 Spawn 失败并打 Error。")]
        private GameObject standardCardPrefab;

        [SerializeField]
        [Tooltip("Avatar → 玩家卡面模板，挂入底盘 L4/FacePivot。留空时 Avatar Spawn 只出底盘、跳过挂面并 Warning。")]
        private GameObject avatarFacePrefab;

        [SerializeField]
        [Tooltip("Monster → 怪物卡面模板，挂入底盘 L4/FacePivot。留空时 Monster Spawn 只出底盘、跳过挂面并 Warning。")]
        private GameObject monsterFacePrefab;

        [SerializeField]
        [Tooltip("HelpCard / Item / PlayerCard → 道具卡面模板，挂入底盘 L4/FacePivot。留空时对应 Kind Spawn 只出底盘、跳过挂面并 Warning。")]
        private GameObject itemFacePrefab;

        [SerializeField]
        [Tooltip("Relic → 遗物卡面模板，挂入底盘 L4/FacePivot。留空时 Relic Spawn 只出底盘、跳过挂面并 Warning。")]
        private GameObject relicFacePrefab;

        [SerializeField]
        [Tooltip("Trap → 机关卡面模板，挂入底盘 L4/FacePivot。留空时 Trap Spawn 只出底盘、跳过挂面并 Warning。")]
        private GameObject trapFacePrefab;

        private readonly Dictionary<int, ManagedCard> _cardsByUid = new();
        private readonly Dictionary<string, GameObject> _defPrefabs = new(StringComparer.Ordinal);

        /// <summary>
        /// 无 Core 时的本地 Uid 兜底分配，规则与 CardRegistry.mNextUid 一致。
        /// 接入 Core 后应改为传入 CardRegistry.Create 得到的 Uid。
        /// </summary>
        private int _nextUid = 1;

        /// <summary>
        /// 纯表现卡（BounceFan 等）负 Uid 分配器：递减，永不与 Core 正整数 CardUid 撞号。
        /// </summary>
        private int _nextPresentationUid = -1;

        public IReadOnlyDictionary<int, ManagedCard> CardsByUid => _cardsByUid;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            EnsureCardRoot();
            BootstrapPrefabs();
        }

        private void OnDestroy()
        {
        }

        public void RegisterPrefab(string defId, GameObject prefab)
        {
            if (string.IsNullOrWhiteSpace(defId) || prefab == null)
            {
                return;
            }

            _defPrefabs[defId] = prefab;
        }

        /// <summary>
        /// 配置卡牌底盘与 Kind→卡面模板表（EditMode / 运行时装配）。
        /// </summary>
        public void ConfigureChassisAndFaces(
            GameObject chassis,
            GameObject avatarFace,
            GameObject monsterFace,
            GameObject itemFace,
            GameObject relicFace,
            GameObject trapFace = null)
        {
            standardCardPrefab = chassis;
            avatarFacePrefab = avatarFace;
            monsterFacePrefab = monsterFace;
            itemFacePrefab = itemFace;
            relicFacePrefab = relicFace;
            trapFacePrefab = trapFace;
        }

        /// <summary>
        /// 为指定 Uid 创建表现视图。Uid 应由 Core CardRegistry 分配。
        /// 主路径：Instantiate(底盘) → 按 Kind 挂 L4/FacePivot 卡面。
        /// <see cref="RegisterPrefab"/> 仅作 DefId 完整预制体特例覆盖。
        /// </summary>
        public ManagedCard SpawnView(
            int uid,
            string defId,
            Transform parent = null,
            CardDisplayMode initialMode = CardDisplayMode.HandCardMode,
            CardPresentationKind kind = CardPresentationKind.Unknown)
        {
            if (uid == InvalidUid)
            {
                Debug.LogError("[CardManagerSingleton] Uid 无效（不可为 0）。Core 卡用正整数；纯表现卡用负整数。");
                return null;
            }

            if (_cardsByUid.ContainsKey(uid))
            {
                Debug.LogError($"[CardManagerSingleton] Uid 已存在: {uid}");
                return null;
            }

            GameObject overridePrefab = null;
            var useOverride = !string.IsNullOrWhiteSpace(defId)
                              && _defPrefabs.TryGetValue(defId, out overridePrefab)
                              && overridePrefab != null;
            GameObject instance;
            if (useOverride)
            {
                TrackUid(uid);
                instance = Instantiate(overridePrefab, parent != null ? parent : cardRoot);
                instance.name = $"{overridePrefab.name} (#{uid})";
            }
            else
            {
                var chassis = standardCardPrefab;
                if (chassis == null)
                {
                    Debug.LogError(
                        $"[CardManagerSingleton] 未配置卡牌底盘，无法 Spawn DefId={defId}。路径回退：{CardChassisPrefabAssetPath}");
                    return null;
                }

                TrackUid(uid);
                instance = Instantiate(chassis, parent != null ? parent : cardRoot);
                instance.name = $"{chassis.name} (#{uid})";
            }

            var view = instance.GetComponent<StandardCardView>();
            if (view == null)
            {
                Debug.LogWarning($"[CardManagerSingleton] DefId {defId} 未挂载 StandardCardView。");
            }

            var card = new ManagedCard(uid, defId, view)
            {
                CoreKind = kind,
            };
            var countBefore = _cardsByUid.Count;
            _cardsByUid[uid] = card;
            CardPresentationProbe.RegistryDelta(
                uid,
                "add",
                countBefore,
                _cardsByUid.Count,
                "Card.Spawn",
                reason: "SpawnView",
                defId: card.DefId);

            EnsureTransformTower(instance);

            if (!useOverride)
            {
                MountFaceForKind(card, kind);
            }

            var driver = instance.GetComponent<CardVisualDriver>();
            if (driver == null)
            {
                driver = instance.AddComponent<CardVisualDriver>();
            }

            driver.Bind(card);
            driver.CaptureAuthoredBaseScale(instance.transform.localScale);

            if (instance.GetComponent<CardEffectManager>() == null)
            {
                instance.AddComponent<CardEffectManager>();
            }

            if (instance.GetComponent<CardFaceFlipPresenter>() == null)
            {
                instance.AddComponent<CardFaceFlipPresenter>();
            }

            var hitProxy = instance.GetComponent<GroundCardHitProxy>();
            if (hitProxy != null)
            {
                hitProxy.ApplyColliderSize();
            }

            var handHitProxy = instance.GetComponent<HandCardHitProxy>();
            if (handHitProxy != null)
            {
                handHitProxy.ApplyColliderSize();
            }

            ApplyDisplayMode(card, initialMode);
            CardPresentationProbe.Spawn(
                card.Uid,
                card.DefId,
                "Card.Spawn",
                parent: parent != null ? parent.name : (cardRoot != null ? cardRoot.name : string.Empty));
            return card;
        }

        /// <summary>
        /// 复杂域入口前置：强制补齐塔 + L2/L3 driver，消除中途 spawn 的 timing race。
        /// </summary>
        public void EnsureComplexDomainStack(ManagedCard card)
        {
            if (card?.GameObject == null)
            {
                return;
            }

            EnsureTransformTower(card.GameObject);
        }

        /// <summary>
        /// 本地测试用：自行分配正 Uid 并创建视图。正式流程请用 Core 创建逻辑卡后再 SpawnView。
        /// BounceFan / 无 Core 选项卡请用 <see cref="SpawnPresentationOnly"/>，避免占 Core 号段。
        /// </summary>
        public ManagedCard Spawn(
            string defId,
            Transform parent = null,
            CardDisplayMode initialMode = CardDisplayMode.HandCardMode,
            CardPresentationKind kind = CardPresentationKind.Unknown)
        {
            return SpawnView(_nextUid++, defId, parent, initialMode, kind);
        }

        /// <summary>
        /// 纯表现卡：分配负 Uid，不占用 Core CardUid 号段（防 BounceFan 与洗回 NewCard 撞号挂死）。
        /// </summary>
        public ManagedCard SpawnPresentationOnly(
            string defId,
            Transform parent = null,
            CardDisplayMode initialMode = CardDisplayMode.HandCardMode,
            CardPresentationKind kind = CardPresentationKind.Unknown)
        {
            return SpawnView(_nextPresentationUid--, defId, parent, initialMode, kind);
        }

        public List<ManagedCard> SpawnMany(
            string defId,
            int count,
            Transform parent = null,
            CardDisplayMode initialMode = CardDisplayMode.HandCardMode,
            CardPresentationKind kind = CardPresentationKind.Unknown)
        {
            var result = new List<ManagedCard>(Mathf.Max(0, count));
            for (var i = 0; i < count; i++)
            {
                var card = Spawn(defId, parent, initialMode, kind);
                if (card != null)
                {
                    result.Add(card);
                }
            }

            return result;
        }

        public bool TryGet(int uid, out ManagedCard card)
        {
            return _cardsByUid.TryGetValue(uid, out card);
        }

        /// <summary>
        /// 带 PerfLog 的 TryGet：失败时记 RegistryMiss，便于抓「占格有 uid 无视图」。
        /// </summary>
        public bool TryGetTracked(int uid, string site, out ManagedCard card, string detail = null)
        {
            if (_cardsByUid.TryGetValue(uid, out card))
            {
                return true;
            }

            CardPresentationProbe.RegistryMiss(uid, site ?? "Card.TryGetTracked", detail);
            return false;
        }

        /// <summary>
        /// 对比 CardManager 注册表与场地占格，输出 ghosts/orphans 到 PerfLog + CoreLog。
        /// 在旋转结束、Sync 后、或 RegistryMiss 时调用。
        /// </summary>
        public void AuditRegistryIntegrity(string triggerSite)
        {
            try
            {
                var field = GroundFieldGeometryHook.FieldOrNull();
                if (field == null)
                {
                    return;
                }

                var ghostSb = new StringBuilder(32);
                var orphanSb = new StringBuilder(32);
                var fieldCount = 0;

                var snap = field.GetSnapshot();
                for (var i = 0; i < snap.Slots.Length; i++)
                {
                    var occ = snap.Slots[i];
                    if (occ.IsEmpty || occ.IsAvatarReserved)
                    {
                        continue;
                    }

                    fieldCount++;
                    if (!_cardsByUid.ContainsKey(occ.Uid))
                    {
                        if (ghostSb.Length > 0)
                        {
                            ghostSb.Append(';');
                        }

                        ghostSb.Append(occ.Uid).Append('@').Append(occ.Slot);
                    }
                }

                foreach (var pair in _cardsByUid)
                {
                    var card = pair.Value;
                    if (card == null
                        || card.DisplayMode != CardDisplayMode.GroundCardMode
                        || card.IsFieldDead)
                    {
                        continue;
                    }

                    if (!field.TryGetSlotOf(pair.Key, out _))
                    {
                        if (orphanSb.Length > 0)
                        {
                            orphanSb.Append(';');
                        }

                        orphanSb.Append(pair.Key);
                    }
                }

                var ghosts = ghostSb.ToString();
                var orphans = orphanSb.ToString();
                var dualHold = BuildDualHandDeckHoldReport();
                CardPresentationProbe.RegistryAudit(
                    "Card.RegistryAudit",
                    _cardsByUid.Count,
                    fieldCount,
                    ghosts,
                    orphans,
                    trigger: triggerSite);

                if (!string.IsNullOrEmpty(dualHold))
                {
                    Debug.LogError(
                        $"[CardManager] 手牌与卡组双持有 uid={dualHold}（site={triggerSite}）。");
                }

                try
                {
                    FlowFieldTraceSink.RegistryAudit?.Invoke(
                        triggerSite ?? string.Empty,
                        _cardsByUid.Count,
                        fieldCount,
                        ghosts,
                        orphans);
                }
                catch
                {
                    // ignore
                }
            }
            catch
            {
                // swallow
            }
        }

        private string BuildDualHandDeckHoldReport()
        {
            var hand = CardEntityLifecycleHook.HandOrNull();
            var deck = CardEntityLifecycleHook.DeckOrNull();
            if (hand == null || deck == null)
            {
                return string.Empty;
            }

            var sb = new StringBuilder(16);
            foreach (var card in _cardsByUid.Values)
            {
                if (card == null || !hand.ContainsUid(card.Uid) || !deck.ContainsUid(card.Uid))
                {
                    continue;
                }

                if (sb.Length > 0)
                {
                    sb.Append(';');
                }

                sb.Append(card.Uid);
            }

            return sb.ToString();
        }

        public IEnumerable<ManagedCard> EnumerateCards()
        {
            return _cardsByUid.Values;
        }

        public bool TryResolveUid(Transform transform, out int uid)
        {
            uid = InvalidUid;
            if (transform == null)
            {
                return false;
            }

            foreach (var kv in _cardsByUid)
            {
                var card = kv.Value;
                if (card?.Transform == transform)
                {
                    uid = card.Uid;
                    return true;
                }
            }

            return false;
        }

        public ManagedCard Get(int uid)
        {
            if (!_cardsByUid.TryGetValue(uid, out var card))
            {
                throw new KeyNotFoundException("Card uid not found: " + uid);
            }

            return card;
        }

        public void Release(int uid, string reason, [CallerMemberName] string caller = "")
        {
            if (!_cardsByUid.TryGetValue(uid, out var card))
            {
                CardPresentationProbe.RegistryMiss(
                    uid,
                    "Card.Release",
                    "releaseOnMissing;reason=" + (reason ?? string.Empty));
                return;
            }

            TryVacateFieldOccupancyBeforeRelease(uid);

            var countBefore = _cardsByUid.Count;
            CardPresentationProbe.Despawn(uid, "Card.Release", reason: reason, caller: caller);

            if (card.View != null)
            {
                CardDeckTween.KillMotion(card.Transform);
                DestroyUnityObject(card.View.gameObject);
            }

            _cardsByUid.Remove(uid);
            CardPresentationProbe.RegistryDelta(
                uid,
                "remove",
                countBefore,
                _cardsByUid.Count,
                "Card.Release",
                reason: reason,
                caller: caller,
                defId: card.DefId);
            CardEntityLifecycleHook.RequestNotifyReleased(uid);
        }

        public void Release(ManagedCard card, string reason, [CallerMemberName] string caller = "")
        {
            if (card == null)
            {
                return;
            }

            if (!_cardsByUid.TryGetValue(card.Uid, out var registered) || !ReferenceEquals(registered, card))
            {
                var currentDefId = registered?.DefId ?? string.Empty;
                var requestedViewId = card.View != null ? card.View.GetInstanceID() : 0;
                var currentViewId = registered?.View != null ? registered.View.GetInstanceID() : 0;
                var detail = string.Format(
                    CultureInfo.InvariantCulture,
                    "reason={0};caller={1};requestedDefId={2};currentDefId={3};requestedViewId={4};currentViewId={5}",
                    reason ?? string.Empty,
                    caller ?? string.Empty,
                    card.DefId ?? string.Empty,
                    currentDefId,
                    requestedViewId,
                    currentViewId);
                CardPresentationProbe.RegistryMiss(card.Uid, "Card.Release.StaleHandle", detail);
                return;
            }

            Release(card.Uid, reason, caller);
        }

        /// <summary>
        /// Release 前卸场地占格，防止「CardManager 已 Despawn 但 _uidBySlot 仍登记」的缺卡幽灵格。
        /// </summary>
        private static void TryVacateFieldOccupancyBeforeRelease(int uid)
        {
            if (uid <= 0)
            {
                return;
            }

            try
            {
                GroundFieldGeometryHook.FieldOrNull()?.TryClearOccupancyForUid(
                    uid,
                    skipBusyGuard: true);
            }
            catch
            {
                // swallow
            }
        }

        public void ReleaseAll(string reason, [CallerMemberName] string caller = "")
        {
            var uids = new List<int>(_cardsByUid.Keys);
            for (var i = uids.Count - 1; i >= 0; i--)
            {
                Release(uids[i], reason, caller);
            }
        }

        public void SetDisplayMode(int uid, CardDisplayMode mode)
        {
            if (_cardsByUid.TryGetValue(uid, out var card))
            {
                SetDisplayMode(card, mode);
            }
        }

        public void SetDisplayMode(ManagedCard card, CardDisplayMode mode)
        {
            if (card?.View == null)
            {
                return;
            }

            ApplyDisplayMode(card, mode);
        }

        /// <summary>
        /// 按当前 DisplayMode 重新应用缩放与 sorting，用于动效结束后校正表现。
        /// </summary>
        public void RefreshDisplayMode(ManagedCard card)
        {
            if (card?.View == null)
            {
                return;
            }

            ApplyDisplayMode(card, card.DisplayMode);
        }

        public void MarkFieldDead(ManagedCard card)
        {
            if (card == null)
            {
                return;
            }

            // 仅标记场地死亡；可见血量归零由 KillCard 结算指令在表演锚点提交（ADR-0005）。
            card.IsFieldDead = true;
        }

        /// <summary>
        /// 击杀 Vacate 后立刻把尸体移出格锚点并切 RemovedMode，
        /// 避免 PostKill Drain hop 与尸体叠在同一世界坐标造成「凭空消失+出现」。
        /// 不 SetActive(false)，以便死亡特效与 FinalizeLethal 等待逻辑继续。
        /// </summary>
        public void StageFieldDeadCorpseOffAnchor(ManagedCard card)
        {
            if (card?.Transform == null || !card.IsFieldDead)
            {
                return;
            }

            CardDeckTween.KillMotion(card.Transform);

            // 先离锚再切模式，避免 RemovedMode 缩放在空槽上闪一帧。
            var t = card.Transform;
            t.position = new Vector3(t.position.x, t.position.y - 80f, t.position.z);
            SetDisplayMode(card, CardDisplayMode.RemovedMode);

            var go = card.GameObject;
            var sortingGroup = card.View?.GetComponent<SortingGroup>();
            CardPresentationProbe.VisChange(
                card.Uid,
                "Card.StageFieldDead",
                active: go != null && go.activeInHierarchy,
                mode: CardDisplayMode.RemovedMode.ToString(),
                renderOn: CardPresentationProbe.HasEnabledRenderer(go),
                sortOrder: sortingGroup != null ? sortingGroup.sortingOrder : (int?)null);
        }

        private void TrackUid(int uid)
        {
            if (uid > 0 && uid >= _nextUid)
            {
                _nextUid = uid + 1;
            }

            if (uid < 0 && uid <= _nextPresentationUid)
            {
                _nextPresentationUid = uid - 1;
            }
        }

        private void EnsureCardRoot()
        {
            if (cardRoot != null)
            {
                return;
            }

            var rootObject = new GameObject("Cards");
            rootObject.transform.SetParent(transform, false);
            cardRoot = rootObject.transform;
        }

        private static void EnsureTransformTower(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            var tower = instance.GetComponent<CardTransformTower>();
            if (tower == null)
            {
                tower = instance.AddComponent<CardTransformTower>();
            }

            tower.EnsureTower();

            LayerConvergenceDriver.Ensure(instance.transform, TowerLayer.SlotFrame);
            LayerConvergenceDriver.Ensure(instance.transform, TowerLayer.EffectFrame);

            // 运行时兜底：若预制体尚未迁入 L4，把非塔层直系子节点挂到 CardVisual 下，避免视觉与 L2 脱节。
            var visual = tower.CardVisual;
            if (visual == null)
            {
                return;
            }

            for (var i = instance.transform.childCount - 1; i >= 0; i--)
            {
                var child = instance.transform.GetChild(i);
                if (child == null || child.name == CardTransformTower.BoardFrameName)
                {
                    continue;
                }

                child.SetParent(visual, false);
            }
        }

        private void BootstrapPrefabs()
        {
            if (standardCardPrefab == null)
            {
#if UNITY_EDITOR
                standardCardPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(CardChassisPrefabAssetPath);
#endif
            }

            if (standardCardPrefab == null)
            {
                Debug.LogWarning(
                    $"[CardManagerSingleton] 未配置卡牌底盘，请赋值或通过路径 {CardChassisPrefabAssetPath} 提供。");
            }

#if UNITY_EDITOR
            if (trapFacePrefab == null)
            {
                trapFacePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(CardChassisPaths.TrapFacePrefab);
            }
#endif

            // RegisterPrefab(defId) 仅作特例覆盖；底盘不再注册为 StandardDefId 主回退。
        }

        /// <summary>
        /// Kind → 卡面模板显式表。不为 PlayerSkill / Unknown 开卡面。
        /// </summary>
        private GameObject ResolveFacePrefab(CardPresentationKind kind)
        {
            return kind switch
            {
                CardPresentationKind.Avatar => avatarFacePrefab,
                CardPresentationKind.Monster => monsterFacePrefab,
                CardPresentationKind.HelpCard => itemFacePrefab,
                CardPresentationKind.Item => itemFacePrefab,
                CardPresentationKind.PlayerCard => itemFacePrefab,
                CardPresentationKind.Relic => relicFacePrefab,
                CardPresentationKind.Trap => trapFacePrefab,
                _ => null,
            };
        }

        private void MountFaceForKind(ManagedCard card, CardPresentationKind kind)
        {
            if (card?.GameObject == null)
            {
                return;
            }

            var facePrefab = ResolveFacePrefab(kind);
            if (facePrefab == null)
            {
                if (kind != CardPresentationKind.Unknown)
                {
                    Debug.LogWarning(
                        $"[CardManagerSingleton] Kind={kind} 无卡面模板，跳过挂面 uid={card.Uid} defId={card.DefId}");
                }

                return;
            }

            var tower = card.GameObject.GetComponent<CardTransformTower>();
            if (tower == null)
            {
                Debug.LogError($"[CardManagerSingleton] 底盘缺少 CardTransformTower，无法挂面 uid={card.Uid}");
                return;
            }

            tower.EnsureTower();
            var pivot = tower.FacePivot;
            if (pivot == null)
            {
                Debug.LogError($"[CardManagerSingleton] FacePivot 缺失，无法挂面 uid={card.Uid}");
                return;
            }

            for (var i = pivot.childCount - 1; i >= 0; i--)
            {
                var child = pivot.GetChild(i);
                if (child != null)
                {
                    DestroyUnityObject(child.gameObject);
                }
            }

            var face = Instantiate(facePrefab, pivot);
            face.name = facePrefab.name;
            face.transform.localPosition = Vector3.zero;
            face.transform.localRotation = Quaternion.identity;
            face.transform.localScale = Vector3.one;

            var faceSortingGroup = face.GetComponent<SortingGroup>();
            if (faceSortingGroup != null)
            {
                Debug.LogWarning(
                    $"[CardManagerSingleton] 卡面模板自带 SortingGroup，已移除以免第二套卡级 SG：{facePrefab.name}",
                    face);
                DestroyUnityObject(faceSortingGroup);
            }

            card.MountedFaceRoot = face.transform;

            var binder = face.GetComponent<CardFacePresentationBinder>();
            if (binder == null)
            {
                binder = face.AddComponent<CardFacePresentationBinder>();
            }

            card.View?.AttachFaceBinder(binder);
        }

        private static void DestroyUnityObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private static void ApplyDisplayMode(ManagedCard card, CardDisplayMode mode)
        {
            if (card?.View == null)
            {
                return;
            }

            var modeChanged = card.DisplayMode != mode;
            card.DisplayMode = mode;

            if (modeChanged && IsSanctuaryDisplayMode(mode))
            {
                SlotFrameConvergence.SanitizeForSanctuary(card, "Card.DisplayMode." + mode);
            }

            var cardTransform = card.View.transform;
            var sortingGroup = card.View.GetComponent<SortingGroup>();
            var driver = card.View.GetComponent<CardVisualDriver>();
            var authored = driver != null ? driver.AuthoredBaseScale : Vector3.one;

            cardTransform.localScale = CardDisplayModeVisuals.GetBaseLocalScale(mode, authored);
            cardTransform.localRotation = Quaternion.identity;
            if (mode == CardDisplayMode.HandCardMode)
            {
                var hand = CardEntityLifecycleHook.HandOrNull();
                if (hand != null && hand.ContainsUid(card.Uid))
                {
                    hand.EnsureHandSorting(card);
                }
            }
            else if (mode == CardDisplayMode.CardDeckMode)
            {
                var deck = CardEntityLifecycleHook.DeckOrNull();
                if (deck != null && deck.ContainsUid(card.Uid))
                {
                    // 已入槽：走卡组权威序 + Propagate，禁止打回 DisplayMode 默认 -30。
                    deck.EnsureDeckSorting(card);
                }
                else if (!FlightSortingChannel.IsArmed(card.Uid))
                {
                    SetSortingOrder(sortingGroup, CardDisplayModeVisuals.GetSortingOrder(mode, card.CoreKind));
                }
            }
            else if (!FlightSortingChannel.IsArmed(card.Uid))
            {
                SetSortingOrder(sortingGroup, CardDisplayModeVisuals.GetSortingOrder(mode, card.CoreKind));
            }

            driver?.SnapToDisplayMode();
            SyncHitProxyForDisplayMode(card, mode);

            if (modeChanged)
            {
                var go = card.GameObject;
                var sortOrder = sortingGroup != null ? sortingGroup.sortingOrder : (int?)null;
                CardPresentationProbe.VisChange(
                    card.Uid,
                    "Card.DisplayMode",
                    active: go != null && go.activeInHierarchy,
                    mode: mode.ToString(),
                    renderOn: CardPresentationProbe.HasEnabledRenderer(go),
                    sortOrder: sortOrder);
            }
        }

        private static bool IsSanctuaryDisplayMode(CardDisplayMode mode) =>
            mode == CardDisplayMode.CardDeckMode
            || mode == CardDisplayMode.HandCardMode
            || mode == CardDisplayMode.RemovedMode;

        /// <summary>
        /// 场地卡不得保留 Hand 命中代理：其 sort 会压过 <see cref="GroundFieldHitSurface"/>，
        /// 吞掉房内货架/固定道具的格位点击（ADR-0023）。
        /// </summary>
        private static void SyncHitProxyForDisplayMode(ManagedCard card, CardDisplayMode mode)
        {
            if (card?.View == null)
            {
                return;
            }

            var go = card.View.gameObject;
            var handHit = go.GetComponent<HandCardHitProxy>();
            if (handHit != null)
            {
                handHit.enabled = mode == CardDisplayMode.HandCardMode
                                  || mode == CardDisplayMode.DragCardMode;
            }
        }

        private static void SetSortingOrder(SortingGroup sortingGroup, int sortingOrder)
        {
            if (sortingGroup != null)
            {
                sortingGroup.sortingOrder = sortingOrder;
            }
        }
    }
}

using System;
using System.Collections.Generic;
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

        /// <summary>场地交战即死后标记；仍占格直至外圈旋转或移除。</summary>
        public bool IsFieldDead { get; internal set; }

        /// <summary>由 Flow 映射层写入的 Core 卡牌种类，Cards 程序集不直接引用 Core。</summary>
        public CardPresentationKind CoreKind { get; set; }
    }

    /// <summary>
    /// 卡牌管理器单例：以 Core 下发的 Uid 为键，管理表现视图与显示模式。
    /// </summary>
    public sealed class CardManagerSingleton : MonoBehaviour
    {
        public const int InvalidUid = 0;
        public const string StandardDefId = "standard";

        private const string StandardCardPrefabAssetPath = "Assets/Prefabs/Standard Card.prefab";

        private static CardManagerSingleton _instance;

        [SerializeField] private Transform cardRoot;
        [SerializeField] private GameObject standardCardPrefab;

        private readonly Dictionary<int, ManagedCard> _cardsByUid = new();
        private readonly Dictionary<string, GameObject> _defPrefabs = new(StringComparer.Ordinal);

        /// <summary>
        /// 无 Core 时的本地 Uid 兜底分配，规则与 CardRegistry.mNextUid 一致。
        /// 接入 Core 后应改为传入 CardRegistry.Create 得到的 Uid。
        /// </summary>
        private int _nextUid = 1;

        public static CardManagerSingleton Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<CardManagerSingleton>();
                    if (_instance == null)
                    {
                        var go = new GameObject(nameof(CardManagerSingleton));
                        _instance = go.AddComponent<CardManagerSingleton>();
                    }
                }

                return _instance;
            }
        }

        /// <summary>
        /// 仅查找，不创建。销毁/卸载期释放卡视图时必须走此入口，避免 OnDestroy 里 Spawn 残留对象。
        /// </summary>
        public static CardManagerSingleton TryGetInstance()
        {
            if (_instance != null)
            {
                return _instance;
            }

            return FindFirstObjectByType<CardManagerSingleton>();
        }

        public IReadOnlyDictionary<int, ManagedCard> CardsByUid => _cardsByUid;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureCardRoot();
            BootstrapPrefabs();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
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
        /// 为指定 Uid 创建表现视图。Uid 应由 Core CardRegistry 分配。
        /// </summary>
        public ManagedCard SpawnView(
            int uid,
            string defId,
            Transform parent = null,
            CardDisplayMode initialMode = CardDisplayMode.HandCardMode)
        {
            if (uid <= InvalidUid)
            {
                Debug.LogError("[CardManagerSingleton] Uid 无效，须为正整数。");
                return null;
            }

            if (_cardsByUid.ContainsKey(uid))
            {
                Debug.LogError($"[CardManagerSingleton] Uid 已存在: {uid}");
                return null;
            }

            if (!_defPrefabs.TryGetValue(defId, out var prefab) || prefab == null)
            {
                if (!_defPrefabs.TryGetValue(StandardDefId, out prefab) || prefab == null)
                {
                    Debug.LogError($"[CardManagerSingleton] 未找到 DefId 对应预制体: {defId}");
                    return null;
                }

                Debug.LogWarning(
                    $"[CardManagerSingleton] DefId {defId} 无专用预制体，回退 Standard Card。");
            }

            TrackUid(uid);

            var instance = Instantiate(prefab, parent != null ? parent : cardRoot);
            instance.name = $"{prefab.name} (#{uid})";

            var view = instance.GetComponent<StandardCardView>();
            if (view == null)
            {
                Debug.LogWarning($"[CardManagerSingleton] DefId {defId} 未挂载 StandardCardView。");
            }

            var card = new ManagedCard(uid, defId, view);
            _cardsByUid[uid] = card;

            var driver = instance.GetComponent<CardVisualDriver>();
            if (driver == null)
            {
                driver = instance.AddComponent<CardVisualDriver>();
            }

            driver.Bind(card);

            if (instance.GetComponent<CardEffectManager>() == null)
            {
                instance.AddComponent<CardEffectManager>();
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
            return card;
        }

        /// <summary>
        /// 本地测试用：自行分配 Uid 并创建视图。正式流程请用 Core 创建逻辑卡后再 SpawnView。
        /// </summary>
        public ManagedCard Spawn(
            string defId,
            Transform parent = null,
            CardDisplayMode initialMode = CardDisplayMode.HandCardMode)
        {
            return SpawnView(_nextUid++, defId, parent, initialMode);
        }

        public List<ManagedCard> SpawnMany(
            string defId,
            int count,
            Transform parent = null,
            CardDisplayMode initialMode = CardDisplayMode.HandCardMode)
        {
            var result = new List<ManagedCard>(Mathf.Max(0, count));
            for (var i = 0; i < count; i++)
            {
                var card = Spawn(defId, parent, initialMode);
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

        public ManagedCard Get(int uid)
        {
            if (!_cardsByUid.TryGetValue(uid, out var card))
            {
                throw new KeyNotFoundException("Card uid not found: " + uid);
            }

            return card;
        }

        public void Release(int uid)
        {
            if (!_cardsByUid.TryGetValue(uid, out var card))
            {
                return;
            }

            if (card.View != null)
            {
                CardDeckTween.KillMotion(card.Transform);
                Destroy(card.View.gameObject);
            }

            _cardsByUid.Remove(uid);
        }

        public void Release(ManagedCard card)
        {
            if (card != null)
            {
                Release(card.Uid);
            }
        }

        public void ReleaseAll()
        {
            var uids = new List<int>(_cardsByUid.Keys);
            for (var i = uids.Count - 1; i >= 0; i--)
            {
                Release(uids[i]);
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

            card.IsFieldDead = true;
            if (card.View != null)
            {
                card.View.SetHealth(0, animate: false);
            }
        }

        private void TrackUid(int uid)
        {
            if (uid >= _nextUid)
            {
                _nextUid = uid + 1;
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

        private void BootstrapPrefabs()
        {
            if (standardCardPrefab == null)
            {
#if UNITY_EDITOR
                standardCardPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(StandardCardPrefabAssetPath);
#endif
            }

            if (standardCardPrefab != null)
            {
                RegisterPrefab(StandardDefId, standardCardPrefab);
            }
            else
            {
                Debug.LogWarning(
                    $"[CardManagerSingleton] 未配置 Standard Card 预制体，请赋值或通过路径 {StandardCardPrefabAssetPath} 提供。");
            }
        }

        private static void ApplyDisplayMode(ManagedCard card, CardDisplayMode mode)
        {
            if (card?.View == null)
            {
                return;
            }

            card.DisplayMode = mode;

            var cardTransform = card.View.transform;
            var sortingGroup = card.View.GetComponent<SortingGroup>();

            cardTransform.localScale = CardDisplayModeVisuals.GetBaseLocalScale(mode);
            cardTransform.localRotation = Quaternion.identity;
            SetSortingOrder(sortingGroup, CardDisplayModeVisuals.GetSortingOrder(mode, card.CoreKind));

            var driver = card.View.GetComponent<CardVisualDriver>();
            driver?.SnapToDisplayMode();
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

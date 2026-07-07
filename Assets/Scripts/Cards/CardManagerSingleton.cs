using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineGrid.Cards
{
    /// <summary>
    /// 与 Core <see cref="NineGrid.Core.CardKind"/> 数值对齐，便于后续桥接。
    /// </summary>
    public enum ManagedCardKind
    {
        Unknown = 0,
        Avatar = 1,
        Monster = 2,
        PlayerCard = 3,
        Relic = 4,
        HelpCard = 5,
        Item = 6,
    }

    /// <summary>
    /// 与 Core <see cref="NineGrid.Core.ZoneId"/> 数值对齐，便于后续桥接。
    /// </summary>
    public enum ManagedZoneId
    {
        None = 0,
        Avatar = 1,
        Board = 2,
        DrawPile = 3,
        PlayerCardPool = 4,
        EnemyCardPool = 5,
        ItemSlots = 6,
        Graveyard = 7,
        Removed = 8,
    }

    /// <summary>
    /// 卡牌表现状态模式，由卡牌管理器统一装配与切换。
    /// </summary>
    public enum CardDisplayMode
    {
        CardDeckMode = 0,
        HandCardMode = 1,
        GroundCardMode = 2,
        CardChoiseMode = 3,
    }

    /// <summary>
    /// 卡牌实例生命周期状态。
    /// </summary>
    public enum CardLifecycleState
    {
        Active = 0,
        Released = 1,
    }

    /// <summary>
    /// 表现层卡牌 Uid 约定，与 Core 一致：0 表示无效。
    /// </summary>
    public static class ManagedCardUid
    {
        public const int None = 0;
    }

    /// <summary>
    /// 槽位索引约定，与 Core <c>SlotId.Index</c> 对齐。
    /// </summary>
    public static class ManagedSlotIndex
    {
        public const int None = -1;
        public const int Avatar = 0;
        public const int MinBoard = 1;
        public const int MaxBoard = 9;
    }

    /// <summary>
    /// 由卡牌管理器托管的卡牌记录，对应 Core <see cref="NineGrid.Core.CardInstance"/> 的身份字段。
    /// </summary>
    public sealed class ManagedCard
    {
        internal ManagedCard(int uid, string defId, ManagedCardKind kind)
        {
            Uid = uid;
            DefId = defId ?? string.Empty;
            Kind = kind;
            Zone = ManagedZoneId.None;
            SlotIndex = ManagedSlotIndex.None;
            LifecycleState = CardLifecycleState.Active;
            DisplayMode = CardDisplayMode.HandCardMode;
        }

        /// <summary>运行时唯一卡牌 ID，与 Core CardInstance.Uid 同语义。</summary>
        public int Uid { get; }

        /// <summary>内容定义 ID，与 Core CardInstance.DefId 同语义。</summary>
        public string DefId { get; }

        /// <summary>卡牌种类，与 Core CardInstance.Kind 同语义。</summary>
        public ManagedCardKind Kind { get; internal set; }

        /// <summary>所在区域，与 Core CardInstance.Zone 同语义。</summary>
        public ManagedZoneId Zone { get; internal set; }

        /// <summary>所在槽位索引，与 Core SlotId.Index 同语义。</summary>
        public int SlotIndex { get; internal set; }

        public StandardCardView View { get; private set; }

        public GameObject GameObject => View != null ? View.gameObject : null;

        public Transform Transform => View != null ? View.transform : null;

        public CardLifecycleState LifecycleState { get; internal set; }

        public CardDisplayMode DisplayMode { get; internal set; }

        public bool HasView => View != null;

        public bool IsActive => LifecycleState == CardLifecycleState.Active;

        internal void BindView(StandardCardView view)
        {
            View = view;
        }

        internal void ClearView()
        {
            View = null;
        }
    }

    /// <summary>
    /// 表现层卡牌 Uid 注册表，对应 Core <see cref="NineGrid.Core.CardRegistry"/>。
    /// </summary>
    public sealed class ManagedCardRegistry
    {
        private readonly Dictionary<int, ManagedCard> _cards = new();
        private int _nextUid = 1;

        public int Version { get; private set; }

        public IReadOnlyDictionary<int, ManagedCard> Cards => _cards;

        public ManagedCard Create(string defId, ManagedCardKind kind)
        {
            var card = new ManagedCard(_nextUid++, defId, kind);
            _cards.Add(card.Uid, card);
            Touch();
            return card;
        }

        public bool TryRegister(int uid, string defId, ManagedCardKind kind, out ManagedCard card)
        {
            card = null;
            if (uid <= ManagedCardUid.None || _cards.ContainsKey(uid))
            {
                return false;
            }

            card = new ManagedCard(uid, defId, kind);
            _cards.Add(uid, card);
            if (uid >= _nextUid)
            {
                _nextUid = uid + 1;
            }

            Touch();
            return true;
        }

        public ManagedCard Get(int uid)
        {
            if (!_cards.TryGetValue(uid, out var card))
            {
                throw new KeyNotFoundException("Card uid not found: " + uid);
            }

            return card;
        }

        public bool TryGet(int uid, out ManagedCard card)
        {
            return _cards.TryGetValue(uid, out card);
        }

        public void MoveCard(int uid, ManagedZoneId zone, int slotIndex)
        {
            var card = Get(uid);
            card.Zone = zone;
            card.SlotIndex = slotIndex;
            Touch();
        }

        public bool Remove(int uid)
        {
            var removed = _cards.Remove(uid);
            if (removed)
            {
                Touch();
            }

            return removed;
        }

        public void Clear()
        {
            _cards.Clear();
            _nextUid = 1;
            Touch();
        }

        private void Touch()
        {
            Version++;
        }
    }

    /// <summary>
    /// 卡牌管理器单例：Uid 注册、视图生产、生命周期与表现状态管理。
    /// </summary>
    public sealed class CardManagerSingleton : MonoBehaviour
    {
        public const string StandardDefId = "standard";

        private const string StandardCardPrefabAssetPath = "Assets/Prefabs/Standard Card.prefab";

        private static CardManagerSingleton _instance;

        [SerializeField] private Transform cardRoot;
        [SerializeField] private GameObject standardCardPrefab;

        private readonly ManagedCardRegistry _registry = new();
        private readonly Dictionary<string, GameObject> _defPrefabs = new(StringComparer.Ordinal);

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

        public ManagedCardRegistry Registry => _registry;

        public int RegistryVersion => _registry.Version;

        public IReadOnlyDictionary<int, ManagedCard> Cards => _registry.Cards;

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

        public bool HasPrefab(string defId)
        {
            return _defPrefabs.ContainsKey(defId);
        }

        /// <summary>
        /// 仅登记 Uid 记录，不生成视图。供后续 Core 下发 Uid 时预注册。
        /// </summary>
        public bool TryRegisterCard(int uid, string defId, ManagedCardKind kind, out ManagedCard card)
        {
            return _registry.TryRegister(uid, defId, kind, out card);
        }

        /// <summary>
        /// 创建卡牌记录并生成视图。
        /// </summary>
        public ManagedCard Spawn(
            string defId,
            ManagedCardKind kind = ManagedCardKind.PlayerCard,
            Transform parent = null,
            CardDisplayMode initialMode = CardDisplayMode.HandCardMode,
            ManagedZoneId zone = ManagedZoneId.None,
            int slotIndex = ManagedSlotIndex.None)
        {
            var card = _registry.Create(defId, kind);
            if (zone != ManagedZoneId.None || slotIndex != ManagedSlotIndex.None)
            {
                _registry.MoveCard(card.Uid, zone, slotIndex);
            }

            if (!TrySpawnView(card, parent, initialMode))
            {
                _registry.Remove(card.Uid);
                return null;
            }

            return card;
        }

        /// <summary>
        /// 为已存在的 Uid 记录补齐视图。
        /// </summary>
        public bool EnsureView(
            int uid,
            Transform parent = null,
            CardDisplayMode initialMode = CardDisplayMode.HandCardMode)
        {
            if (!_registry.TryGet(uid, out var card) || !card.IsActive || card.HasView)
            {
                return card is { HasView: true };
            }

            return TrySpawnView(card, parent, initialMode);
        }

        public List<ManagedCard> SpawnMany(
            string defId,
            int count,
            ManagedCardKind kind = ManagedCardKind.PlayerCard,
            Transform parent = null,
            CardDisplayMode initialMode = CardDisplayMode.HandCardMode,
            ManagedZoneId zone = ManagedZoneId.None,
            int slotIndex = ManagedSlotIndex.None)
        {
            var result = new List<ManagedCard>(Mathf.Max(0, count));
            for (var i = 0; i < count; i++)
            {
                var card = Spawn(defId, kind, parent, initialMode, zone, slotIndex);
                if (card != null)
                {
                    result.Add(card);
                }
            }

            return result;
        }

        public void MoveCard(int uid, ManagedZoneId zone, int slotIndex)
        {
            _registry.MoveCard(uid, zone, slotIndex);
        }

        public ManagedCard Get(int uid)
        {
            return _registry.Get(uid);
        }

        public bool TryGet(int uid, out ManagedCard card)
        {
            return _registry.TryGet(uid, out card);
        }

        public bool IsValidUid(int uid)
        {
            return uid > ManagedCardUid.None && _registry.TryGet(uid, out var card) && card.IsActive;
        }

        public void Release(int uid)
        {
            if (!_registry.TryGet(uid, out var card) || !card.IsActive)
            {
                return;
            }

            Release(card);
        }

        public void Release(ManagedCard card)
        {
            if (card == null || !card.IsActive)
            {
                return;
            }

            card.LifecycleState = CardLifecycleState.Released;

            if (card.View != null)
            {
                Destroy(card.View.gameObject);
                card.ClearView();
            }

            _registry.Remove(card.Uid);
        }

        public void ReleaseAll()
        {
            var uids = new List<int>(_registry.Cards.Keys);
            for (var i = uids.Count - 1; i >= 0; i--)
            {
                Release(uids[i]);
            }
        }

        public void SetDisplayMode(int uid, CardDisplayMode mode)
        {
            if (_registry.TryGet(uid, out var card))
            {
                SetDisplayMode(card, mode);
            }
        }

        public void SetDisplayMode(ManagedCard card, CardDisplayMode mode)
        {
            if (card == null || !card.IsActive || !card.HasView)
            {
                return;
            }

            ApplyDisplayMode(card, mode);
        }

        public List<ManagedCard> GetCardsByDefId(string defId)
        {
            var result = new List<ManagedCard>();
            foreach (var pair in _registry.Cards)
            {
                var card = pair.Value;
                if (card.IsActive && string.Equals(card.DefId, defId, StringComparison.Ordinal))
                {
                    result.Add(card);
                }
            }

            return result;
        }

        public List<ManagedCard> GetCardsByKind(ManagedCardKind kind)
        {
            var result = new List<ManagedCard>();
            foreach (var pair in _registry.Cards)
            {
                var card = pair.Value;
                if (card.IsActive && card.Kind == kind)
                {
                    result.Add(card);
                }
            }

            return result;
        }

        public List<ManagedCard> GetCardsByZone(ManagedZoneId zone)
        {
            var result = new List<ManagedCard>();
            foreach (var pair in _registry.Cards)
            {
                var card = pair.Value;
                if (card.IsActive && card.Zone == zone)
                {
                    result.Add(card);
                }
            }

            return result;
        }

        public List<ManagedCard> GetCardsByDisplayMode(CardDisplayMode mode)
        {
            var result = new List<ManagedCard>();
            foreach (var pair in _registry.Cards)
            {
                var card = pair.Value;
                if (card.IsActive && card.HasView && card.DisplayMode == mode)
                {
                    result.Add(card);
                }
            }

            return result;
        }

        public List<ManagedCard> GetCardsWithView()
        {
            var result = new List<ManagedCard>();
            foreach (var pair in _registry.Cards)
            {
                var card = pair.Value;
                if (card.IsActive && card.HasView)
                {
                    result.Add(card);
                }
            }

            return result;
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

        private bool TrySpawnView(ManagedCard card, Transform parent, CardDisplayMode initialMode)
        {
            if (card == null || card.HasView)
            {
                return false;
            }

            if (!_defPrefabs.TryGetValue(card.DefId, out var prefab) || prefab == null)
            {
                Debug.LogError($"[CardManagerSingleton] 未找到 DefId 对应预制体: {card.DefId}");
                return false;
            }

            var instance = Instantiate(prefab, parent != null ? parent : cardRoot);
            instance.name = $"{prefab.name} (#{card.Uid})";

            var view = instance.GetComponent<StandardCardView>();
            if (view == null)
            {
                Debug.LogWarning($"[CardManagerSingleton] DefId {card.DefId} 未挂载 StandardCardView。");
            }

            card.BindView(view);
            ApplyDisplayMode(card, initialMode);
            return true;
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

            switch (mode)
            {
                case CardDisplayMode.CardDeckMode:
                    cardTransform.localScale = Vector3.one * 0.55f;
                    cardTransform.localRotation = Quaternion.identity;
                    SetSortingOrder(sortingGroup, -30);
                    break;

                case CardDisplayMode.HandCardMode:
                    cardTransform.localScale = Vector3.one;
                    cardTransform.localRotation = Quaternion.identity;
                    SetSortingOrder(sortingGroup, 0);
                    break;

                case CardDisplayMode.GroundCardMode:
                    cardTransform.localScale = Vector3.one * 0.92f;
                    cardTransform.localRotation = Quaternion.identity;
                    SetSortingOrder(sortingGroup, -10);
                    break;

                case CardDisplayMode.CardChoiseMode:
                    cardTransform.localScale = Vector3.one * 1.08f;
                    cardTransform.localRotation = Quaternion.identity;
                    SetSortingOrder(sortingGroup, 20);
                    break;
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

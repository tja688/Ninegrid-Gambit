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
    /// 由卡牌管理器托管的卡牌实例句柄。
    /// </summary>
    public sealed class ManagedCard
    {
        internal ManagedCard(int instanceId, string templateId, StandardCardView view)
        {
            InstanceId = instanceId;
            TemplateId = templateId;
            View = view;
            LifecycleState = CardLifecycleState.Active;
            DisplayMode = CardDisplayMode.HandCardMode;
        }

        public int InstanceId { get; }

        public string TemplateId { get; }

        public StandardCardView View { get; }

        public GameObject GameObject => View != null ? View.gameObject : null;

        public Transform Transform => View != null ? View.transform : null;

        public CardLifecycleState LifecycleState { get; internal set; }

        public CardDisplayMode DisplayMode { get; internal set; }

        public bool IsActive => LifecycleState == CardLifecycleState.Active && View != null;
    }

    /// <summary>
    /// 卡牌管理器单例：按模板生产卡牌、管理生命周期与表现状态，供外部查询与切换。
    /// </summary>
    public sealed class CardManagerSingleton : MonoBehaviour
    {
        public const string StandardTemplateId = "standard";

        private const string StandardCardPrefabAssetPath = "Assets/Prefabs/Standard Card.prefab";

        private static CardManagerSingleton _instance;

        [SerializeField] private Transform cardRoot;
        [SerializeField] private GameObject standardCardPrefab;

        private readonly Dictionary<string, GameObject> _templatePrefabs = new(StringComparer.Ordinal);
        private readonly Dictionary<int, ManagedCard> _activeCards = new();
        private readonly List<ManagedCard> _activeCardList = new();
        private int _nextInstanceId = 1;

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

        public IReadOnlyList<ManagedCard> ActiveCards => _activeCardList;

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
            BootstrapTemplates();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// 注册卡牌模板，后续可用 templateId 批量生产。
        /// </summary>
        public void RegisterTemplate(string templateId, GameObject prefab)
        {
            if (string.IsNullOrWhiteSpace(templateId) || prefab == null)
            {
                return;
            }

            _templatePrefabs[templateId] = prefab;
        }

        /// <summary>
        /// 生产一张指定模板的卡牌。
        /// </summary>
        public ManagedCard Spawn(
            string templateId,
            Transform parent = null,
            CardDisplayMode initialMode = CardDisplayMode.HandCardMode)
        {
            if (!_templatePrefabs.TryGetValue(templateId, out var prefab) || prefab == null)
            {
                Debug.LogError($"[CardManagerSingleton] 未找到模板: {templateId}");
                return null;
            }

            var instance = Instantiate(prefab, parent != null ? parent : cardRoot);
            instance.name = $"{prefab.name} ({_nextInstanceId})";

            var view = instance.GetComponent<StandardCardView>();
            if (view == null)
            {
                Debug.LogWarning($"[CardManagerSingleton] 模板 {templateId} 未挂载 StandardCardView。");
            }

            var card = new ManagedCard(_nextInstanceId++, templateId, view);
            _activeCards[card.InstanceId] = card;
            _activeCardList.Add(card);
            ApplyDisplayMode(card, initialMode);
            return card;
        }

        /// <summary>
        /// 批量生产指定模板的卡牌。
        /// </summary>
        public List<ManagedCard> SpawnMany(
            string templateId,
            int count,
            Transform parent = null,
            CardDisplayMode initialMode = CardDisplayMode.HandCardMode)
        {
            var result = new List<ManagedCard>(Mathf.Max(0, count));
            for (var i = 0; i < count; i++)
            {
                var card = Spawn(templateId, parent, initialMode);
                if (card != null)
                {
                    result.Add(card);
                }
            }

            return result;
        }

        /// <summary>
        /// 释放卡牌并销毁其 GameObject。
        /// </summary>
        public void Release(ManagedCard card)
        {
            if (card == null || card.LifecycleState == CardLifecycleState.Released)
            {
                return;
            }

            card.LifecycleState = CardLifecycleState.Released;
            _activeCards.Remove(card.InstanceId);
            _activeCardList.Remove(card);

            if (card.View != null)
            {
                Destroy(card.View.gameObject);
            }
        }

        /// <summary>
        /// 释放全部活跃卡牌。
        /// </summary>
        public void ReleaseAll()
        {
            for (var i = _activeCardList.Count - 1; i >= 0; i--)
            {
                Release(_activeCardList[i]);
            }
        }

        /// <summary>
        /// 切换卡牌表现状态模式。
        /// </summary>
        public void SetDisplayMode(ManagedCard card, CardDisplayMode mode)
        {
            if (card == null || !card.IsActive)
            {
                return;
            }

            ApplyDisplayMode(card, mode);
        }

        public bool TryGetCard(int instanceId, out ManagedCard card)
        {
            return _activeCards.TryGetValue(instanceId, out card);
        }

        public List<ManagedCard> GetCardsByTemplate(string templateId)
        {
            var result = new List<ManagedCard>();
            for (var i = 0; i < _activeCardList.Count; i++)
            {
                var card = _activeCardList[i];
                if (card.IsActive && string.Equals(card.TemplateId, templateId, StringComparison.Ordinal))
                {
                    result.Add(card);
                }
            }

            return result;
        }

        public List<ManagedCard> GetCardsByDisplayMode(CardDisplayMode mode)
        {
            var result = new List<ManagedCard>();
            for (var i = 0; i < _activeCardList.Count; i++)
            {
                var card = _activeCardList[i];
                if (card.IsActive && card.DisplayMode == mode)
                {
                    result.Add(card);
                }
            }

            return result;
        }

        public bool HasTemplate(string templateId)
        {
            return _templatePrefabs.ContainsKey(templateId);
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

        private void BootstrapTemplates()
        {
            if (standardCardPrefab == null)
            {
#if UNITY_EDITOR
                standardCardPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(StandardCardPrefabAssetPath);
#endif
            }

            if (standardCardPrefab != null)
            {
                RegisterTemplate(StandardTemplateId, standardCardPrefab);
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

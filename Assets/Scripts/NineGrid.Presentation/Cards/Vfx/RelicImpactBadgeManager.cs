using System;
using System.Collections.Generic;
using NineGrid.Content.CardPresentation;
using NineGrid.Flow;
using UnityEngine;

namespace NineGrid.Cards.Vfx
{
    /// <summary>
    /// 遗物影响场地目标时弹出徽章的统一管理器。
    /// 维护格位多遗物动态水平排版、对象池、Sprite 缓存与平滑滑动。
    /// 纯表现装饰层：不读写 Core 规则状态、不参与 Batch-ack（ADR-0001 / ADR-0051）。
    /// </summary>
    public sealed class RelicImpactBadgeManager : MonoBehaviour
    {
        public const string DefaultSortingLayerName = "Main";
        public const int DefaultSortingOrder = 12;
        public const float BaseHorizontalSpacing = 0.75f;
        public const float MaxLayoutWidth = 1.60f;

        private static RelicImpactBadgeManager sInstance;

        [Tooltip("徽章使用的 Sorting Layer 名称。")]
        [SerializeField] private string sortingLayerName = DefaultSortingLayerName;

        [Tooltip("徽章使用的 sortingOrder，需高于卡牌(-10)与飘字(5)。")]
        [SerializeField] private int sortingOrder = DefaultSortingOrder;

        private readonly Dictionary<string, Sprite> _spriteCache = new(StringComparer.Ordinal);
        private readonly Dictionary<int, List<RelicImpactBadge>> _slotBadges = new();
        private readonly List<RelicImpactBadge> _customPosBadges = new();
        private readonly Stack<RelicImpactBadge> _pool = new();
        private Transform _poolRoot;

        public static RelicImpactBadgeManager Instance => sInstance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            sInstance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            EnsureRunner();
        }

        public static RelicImpactBadgeManager EnsureRunner()
        {
            if (sInstance != null)
            {
                return sInstance;
            }

            var found = UnityEngine.Object.FindFirstObjectByType<RelicImpactBadgeManager>();
            if (found != null)
            {
                sInstance = found;
                return sInstance;
            }

            var go = new GameObject("[RelicImpactBadgeManager]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            sInstance = go.AddComponent<RelicImpactBadgeManager>();
            return sInstance;
        }

        private void Awake()
        {
            if (sInstance == null)
            {
                sInstance = this;
            }
            else if (sInstance != this)
            {
                Destroy(gameObject);
                return;
            }

            EnsurePoolRoot();
        }

        private void OnDestroy()
        {
            if (sInstance == this)
            {
                sInstance = null;
            }
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            // 1. 更新按 Slot 分组的徽章
            foreach (var kvp in _slotBadges)
            {
                var slot = kvp.Key;
                var list = kvp.Value;
                var removedAny = false;

                for (var i = list.Count - 1; i >= 0; i--)
                {
                    var badge = list[i];
                    if (badge == null || badge.IsFinished)
                    {
                        if (badge != null)
                        {
                            RecycleBadge(badge);
                        }

                        list.RemoveAt(i);
                        removedAny = true;
                        continue;
                    }

                    badge.Tick(dt);
                }

                if (removedAny && list.Count > 0)
                {
                    UpdateLayoutForSlot(slot);
                }
            }

            // 2. 更新位置型徽章（非标准格位兜底）
            for (var i = _customPosBadges.Count - 1; i >= 0; i--)
            {
                var badge = _customPosBadges[i];
                if (badge == null || badge.IsFinished)
                {
                    if (badge != null)
                    {
                        RecycleBadge(badge);
                    }

                    _customPosBadges.RemoveAt(i);
                    continue;
                }

                badge.Tick(dt);
            }
        }

        /// <summary>
        /// 在指定场地格（1~9）中心弹出遗物图标。
        /// 若格位上已有该遗物图标在显示，则刷新其存活时间；若有其它遗物，则自动水平排开。
        /// </summary>
        public void SpawnOrRefreshForSlot(int slotIndex, string relicDefId)
        {
            if (slotIndex < GroundSlotTopology.MinSlot || slotIndex > GroundSlotTopology.MaxSlot)
            {
                return;
            }

            var canonicalDefId = ExtractCanonicalRelicDefId(relicDefId);
            if (string.IsNullOrEmpty(canonicalDefId))
            {
                return;
            }

            var sprite = ResolveRelicSprite(canonicalDefId);
            if (sprite == null)
            {
                return;
            }

            var slotBaseWorldPos = ResolveSlotWorldCenter(slotIndex);

            if (!_slotBadges.TryGetValue(slotIndex, out var list))
            {
                list = new List<RelicImpactBadge>(4);
                _slotBadges[slotIndex] = list;
            }

            // 检查格位上是否已有同款遗物正在展示
            for (var i = 0; i < list.Count; i++)
            {
                var active = list[i];
                if (active != null && !active.IsFinished
                    && string.Equals(active.RelicDefId, canonicalDefId, StringComparison.Ordinal))
                {
                    active.RefreshLifetime();
                    return;
                }
            }

            // 新增遗物徽章
            var badge = GetOrCreateBadge();
            var targetOffset = Vector3.zero;
            badge.Initialize(
                canonicalDefId,
                sprite,
                slotBaseWorldPos,
                slotIndex,
                targetOffset,
                sortingLayerName,
                sortingOrder);

            list.Add(badge);
            UpdateLayoutForSlot(slotIndex);
        }

        /// <summary>
        /// 在指定世界坐标弹出遗物图标（非标准格位兜底）。
        /// </summary>
        public void SpawnOrRefreshAtPosition(Vector3 worldPosition, string relicDefId)
        {
            var canonicalDefId = ExtractCanonicalRelicDefId(relicDefId);
            if (string.IsNullOrEmpty(canonicalDefId))
            {
                return;
            }

            var sprite = ResolveRelicSprite(canonicalDefId);
            if (sprite == null)
            {
                return;
            }

            // 尝试对齐最近的场地格
            if (TryFindClosestSlot(worldPosition, out var closestSlot))
            {
                SpawnOrRefreshForSlot(closestSlot, canonicalDefId);
                return;
            }

            // 检查现有位置型徽章中是否有同款遗物且位置接近
            for (var i = 0; i < _customPosBadges.Count; i++)
            {
                var active = _customPosBadges[i];
                if (active != null && !active.IsFinished
                    && string.Equals(active.RelicDefId, canonicalDefId, StringComparison.Ordinal)
                    && (active.transform.position - worldPosition).sqrMagnitude < 0.5f)
                {
                    active.RefreshLifetime();
                    return;
                }
            }

            var badge = GetOrCreateBadge();
            badge.Initialize(
                canonicalDefId,
                sprite,
                worldPosition,
                0,
                Vector3.zero,
                sortingLayerName,
                sortingOrder);

            _customPosBadges.Add(badge);
        }

        /// <summary>
        /// 清理所有正在展示或缓存的遗物徽章（进房/切关时重置）。
        /// </summary>
        public void ClearAll()
        {
            foreach (var kvp in _slotBadges)
            {
                var list = kvp.Value;
                for (var i = 0; i < list.Count; i++)
                {
                    var badge = list[i];
                    if (badge != null)
                    {
                        RecycleBadge(badge);
                    }
                }

                list.Clear();
            }

            for (var i = 0; i < _customPosBadges.Count; i++)
            {
                var badge = _customPosBadges[i];
                if (badge != null)
                {
                    RecycleBadge(badge);
                }
            }

            _customPosBadges.Clear();
        }

        private void UpdateLayoutForSlot(int slotIndex)
        {
            if (!_slotBadges.TryGetValue(slotIndex, out var list) || list.Count == 0)
            {
                return;
            }

            var count = list.Count;
            if (count == 1)
            {
                list[0].TargetLocalOffset = Vector3.zero;
                return;
            }

            // 动态水平等距排版：总跨度不超过 MaxLayoutWidth
            var spacing = Mathf.Min(BaseHorizontalSpacing, MaxLayoutWidth / Mathf.Max(1, count - 1));
            var halfSpan = (count - 1) * 0.5f;

            for (var i = 0; i < count; i++)
            {
                var offsetX = (i - halfSpan) * spacing;
                list[i].TargetLocalOffset = new Vector3(offsetX, 0f, 0f);
            }
        }

        private Vector3 ResolveSlotWorldCenter(int slotIndex)
        {
            var field = GroundFieldGeometryHook.FieldOrNull();
            if (field != null)
            {
                var anchor = field.GetGroundAnchor(slotIndex);
                if (anchor != null)
                {
                    return anchor.position;
                }
            }

            var slotPos = PresentationOutputProjector.ResolveBoardSlotWorldPosition(slotIndex);
            if (slotPos.HasValue)
            {
                return slotPos.Value;
            }

            return Vector3.zero;
        }

        private static bool TryFindClosestSlot(Vector3 worldPos, out int closestSlot)
        {
            closestSlot = 0;
            var field = GroundFieldGeometryHook.FieldOrNull();
            if (field == null)
            {
                return false;
            }

            var minDistanceSqr = 1.5f * 1.5f; // 1.5 格阈值内认领为该格
            var found = false;

            for (var s = GroundSlotTopology.MinSlot; s <= GroundSlotTopology.MaxSlot; s++)
            {
                var anchor = field.GetGroundAnchor(s);
                if (anchor == null)
                {
                    continue;
                }

                var distSqr = (anchor.position - worldPos).sqrMagnitude;
                if (distSqr < minDistanceSqr)
                {
                    minDistanceSqr = distSqr;
                    closestSlot = s;
                    found = true;
                }
            }

            return found;
        }

        private Sprite ResolveRelicSprite(string canonicalDefId)
        {
            if (string.IsNullOrEmpty(canonicalDefId))
            {
                return null;
            }

            if (_spriteCache.TryGetValue(canonicalDefId, out var cached) && cached != null)
            {
                return cached;
            }

            Sprite loaded = null;

            if (CardPresentationConfigCatalog.TryGet(canonicalDefId, out var dto)
                && dto?.sprites != null
                && !string.IsNullOrWhiteSpace(dto.sprites.mainIcon))
            {
                loaded = CardPresentationSpritePath.LoadSprite(dto.sprites.mainIcon);
            }

            if (loaded == null)
            {
                loaded = ContentIconSlotBinder.TryLoadLegacyRelicIconPublic(canonicalDefId);
            }

            if (loaded != null)
            {
                _spriteCache[canonicalDefId] = loaded;
            }

            return loaded;
        }

        /// <summary>
        /// 从可能带有子装配或前缀的 ID 中提取纯遗物主键（如 relic.rotten_cleave_axe.cleave -> relic.rotten_cleave_axe）。
        /// </summary>
        public static string ExtractCanonicalRelicDefId(string rawDefId)
        {
            if (string.IsNullOrEmpty(rawDefId))
            {
                return string.Empty;
            }

            var trimmed = rawDefId.Trim();
            if (!trimmed.StartsWith("relic.", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            // 检查完整 defId 是否在 Presentation Catalog 中
            if (CardPresentationConfigCatalog.TryGet(trimmed, out _))
            {
                return trimmed;
            }

            // 否则按 '.' 拆分，提取前两段（relic.xxx）
            var parts = trimmed.Split('.');
            if (parts.Length >= 2)
            {
                var canonical = parts[0] + "." + parts[1];
                return canonical;
            }

            return trimmed;
        }

        private RelicImpactBadge GetOrCreateBadge()
        {
            EnsurePoolRoot();

            while (_pool.Count > 0)
            {
                var pooled = _pool.Pop();
                if (pooled != null)
                {
                    return pooled;
                }
            }

            var go = new GameObject("RelicImpactBadge");
            go.transform.SetParent(_poolRoot, false);
            var badge = go.AddComponent<RelicImpactBadge>();
            return badge;
        }

        private void RecycleBadge(RelicImpactBadge badge)
        {
            if (badge == null)
            {
                return;
            }

            badge.HideImmediate();
            if (_poolRoot != null && badge.transform.parent != _poolRoot)
            {
                badge.transform.SetParent(_poolRoot, false);
            }

            _pool.Push(badge);
        }

        private void EnsurePoolRoot()
        {
            if (_poolRoot == null)
            {
                var rootGo = new GameObject("__RelicBadgePool");
                rootGo.transform.SetParent(transform, false);
                _poolRoot = rootGo.transform;
            }
        }
    }
}

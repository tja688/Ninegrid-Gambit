using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Systems;
using NineGrid.Presentation;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineGrid.Flow
{
    /// <summary>
    /// 局内遗物栏表现单例：从 Core PlayerModel.RelicDefIds 读写，刷到 RelicPanelAnchors 子槽。
    /// 显示：槽下挂「标准遗物图标模板」（图标+计数）；命中 Collider/HitProxy 仍在锚点（ADR-0027）。
    /// 计数：只消费 Settled 已提交剩余（ADR-0035）；禁止 View 直读 Core 计数器。
    /// </summary>
    public sealed class RelicManagerSingleton : MonoBehaviour
    {
        public const string DefaultAnchorsName = "RelicPanelAnchors";
        private const float DragAlpha = 0.55f;
        private const int DragSortingBoost = 200;
        private const string DragGhostName = "__RelicDragGhost";

        [Tooltip("遗物栏锚点根；留空则运行时按名查找 RelicPanelAnchors。")]
        [SerializeField] private Transform panelAnchors;

        [Tooltip("遗物栏图标显示壳；留空则按 CardChassisPaths.RelicHudIconPrefab 加载。")]
        [SerializeField] private GameObject iconPrefab;

        private RelicIconSlotView[] _slots = System.Array.Empty<RelicIconSlotView>();
        private readonly List<string> _displayedDefIds = new();
        private readonly Dictionary<string, Dictionary<string, string>> _committedRemaining
            = new(System.StringComparer.Ordinal);
        private readonly List<RelicCountdownProjection.Entry> _projectionScratch = new(4);
        private CancellationTokenSource _dragCts;
        private int _dragSlotIndex = -1;
        private string _dragDefId;
        private GameObject _dragGhost;
        private int _dragSavedSortingOrder;
        private bool _dragHadSortingGroup;
        private Color _dragSavedColor = Color.white;

        public IReadOnlyList<string> DisplayedDefIds => _displayedDefIds;

        public bool IsDragging => _dragSlotIndex >= 0;

        private void Awake()
        {
            EnsureBindings();
        }

        private void OnDisable()
        {
            CancelDragImmediate(restoreSlot: true);
        }

        /// <summary>
        /// 从内核 PlayerModel 同步遗物栏图标。
        /// </summary>
        public void SyncFromCore()
        {
            EnsureBindings();
            var arch = NineGridArchitecture.Current;
            if (arch == null)
            {
                Clear();
                return;
            }

            ApplyDefIds(arch.GetModel<PlayerModel>().RelicDefIds);
        }

        public void ApplyDefIds(IReadOnlyList<string> defIds)
        {
            EnsureBindings();
            _displayedDefIds.Clear();
            if (defIds != null)
            {
                for (var i = 0; i < defIds.Count; i++)
                {
                    if (!string.IsNullOrEmpty(defIds[i]))
                    {
                        _displayedDefIds.Add(defIds[i]);
                    }
                }
            }

            // 卸下的遗物清掉已提交剩余，避免重装时脏值。
            PruneCommittedToDisplayed();

            for (var i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                if (slot == null)
                {
                    continue;
                }

                if (i >= _displayedDefIds.Count)
                {
                    slot.ClearVisual();
                    continue;
                }

                var defId = _displayedDefIds[i];
                var sprite = ResolveSprite(defId);
                if (sprite == null)
                {
                    slot.ClearVisual();
                    continue;
                }

                slot.ApplySprite(sprite);
                slot.BindDefId(defId);
                RefreshCounterForSlot(slot, defId);
            }

            // 拖动中栏位须保持隐藏，且不改布局（ADR-0027）。
            if (_dragSlotIndex >= 0)
            {
                HideSlotVisual(_dragSlotIndex);
            }
        }

        public void Clear()
        {
            CancelDragImmediate(restoreSlot: false);
            EnsureBindings();
            _displayedDefIds.Clear();
            _committedRemaining.Clear();
            for (var i = 0; i < _slots.Length; i++)
            {
                _slots[i]?.ClearVisual();
            }
        }

        /// <summary>
        /// Settled 提交遗物倒计时剩余（ADR-0035）：SourceDefId=relic.*，键为完整装配id.键。
        /// </summary>
        public void CommitCountdownRemaining(string relicDefId, string projectKey, string remainingText)
        {
            if (string.IsNullOrEmpty(relicDefId)
                || string.IsNullOrEmpty(projectKey)
                || !relicDefId.StartsWith("relic.", System.StringComparison.Ordinal))
            {
                return;
            }

            if (!_committedRemaining.TryGetValue(relicDefId, out var map) || map == null)
            {
                map = new Dictionary<string, string>(System.StringComparer.Ordinal);
                _committedRemaining[relicDefId] = map;
            }

            map[projectKey] = remainingText ?? "0";
            RefreshCounterForDefId(relicDefId);
        }

        /// <summary>
        /// Settled 清除遗物倒计时投影键；回退装配周期初值（若仍装备）。
        /// </summary>
        public void ClearCountdownRemaining(string relicDefId, string projectKey)
        {
            if (string.IsNullOrEmpty(relicDefId) || string.IsNullOrEmpty(projectKey))
            {
                return;
            }

            if (_committedRemaining.TryGetValue(relicDefId, out var map) && map != null)
            {
                map.Remove(projectKey);
                if (map.Count == 0)
                {
                    _committedRemaining.Remove(relicDefId);
                }
            }

            RefreshCounterForDefId(relicDefId);
        }

        /// <summary>
        /// 按当前遗物栏占位顺序解析发牌视觉起点（与图标槽 index 一致）。
        /// </summary>
        public bool TryGetDealOrigin(string relicDefId, out Transform anchor)
        {
            anchor = null;
            EnsureBindings();
            if (panelAnchors == null || string.IsNullOrEmpty(relicDefId))
            {
                return false;
            }

            for (var i = 0; i < _displayedDefIds.Count && i < _slots.Length; i++)
            {
                if (_displayedDefIds[i] != relicDefId)
                {
                    continue;
                }

                anchor = _slots[i]?.SlotRoot;
                return anchor != null;
            }

            return false;
        }

        /// <summary>
        /// 指针下有已装备遗物时开始拖动（可穿透半黑屏；与道具卡格共享回收区）。
        /// </summary>
        public bool TryBeginDragUnderPointer(Camera camera, Vector2 screen)
        {
            if (IsDragging
                || CardInspectOverlayPresenter.IsOpen
                || camera == null)
            {
                return false;
            }

            var hand = CardEntityLifecycleHook.HandOrNull();
            if (hand != null && hand.IsDragging)
            {
                return false;
            }

            if (!TryResolveRelicSlotUnderPointer(camera, screen, out var slotIndex, out var defId, out var slotView)
                || slotView == null
                || slotView.IconRenderer == null
                || string.IsNullOrEmpty(defId))
            {
                return false;
            }

            _dragSlotIndex = slotIndex;
            _dragDefId = defId;
            HideSlotVisual(slotIndex);
            SpawnDragGhost(slotView.IconRenderer, camera, screen);
            hand?.SetRecycleZonePresentationActive(true, ResolveDiscardRelicGold());

            _dragCts?.Cancel();
            _dragCts?.Dispose();
            _dragCts = new CancellationTokenSource();
            RunDragLoopAsync(camera, _dragCts.Token).Forget();
            return true;
        }

        private async UniTaskVoid RunDragLoopAsync(Camera camera, CancellationToken cancellationToken)
        {
            var hand = CardEntityLifecycleHook.HandOrNull();
            try
            {
                while (WorldPointerUtility.IsPrimaryHeld())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (camera == null || _dragGhost == null)
                    {
                        break;
                    }

                    if (!WorldPointerUtility.TryGetPointerScreen(out var dragScreen))
                    {
                        await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                        continue;
                    }

                    var z = _dragGhost.transform.position.z;
                    var world = ScreenToWorldOnPlane(dragScreen, camera, z);
                    _dragGhost.transform.position = world;
                    hand?.UpdateRecycleValueHover(world);
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                var recycled = false;
                if (_dragGhost != null
                    && WorldPointerUtility.TryGetPointerScreen(out var releaseScreen)
                    && camera != null)
                {
                    var releaseWorld = ScreenToWorldOnPlane(
                        releaseScreen,
                        camera,
                        _dragGhost.transform.position.z);
                    if (hand != null && hand.ContainsWorldPointInRecycleZone(releaseWorld))
                    {
                        recycled = RelicHudHook.TryDiscardRelic != null
                            && RelicHudHook.TryDiscardRelic(_dragDefId);
                    }
                }

                if (recycled)
                {
                    FinishDragAfterRecycle();
                }
                else
                {
                    CancelDragImmediate(restoreSlot: true);
                }
            }
            catch (System.OperationCanceledException)
            {
                CancelDragImmediate(restoreSlot: true);
            }
            finally
            {
                hand?.SetRecycleZonePresentationActive(false);
            }
        }

        private void FinishDragAfterRecycle()
        {
            DestroyDragGhost();
            _dragSlotIndex = -1;
            _dragDefId = null;
            _dragCts?.Dispose();
            _dragCts = null;
            SyncFromCore();
        }

        private void CancelDragImmediate(bool restoreSlot)
        {
            _dragCts?.Cancel();
            _dragCts?.Dispose();
            _dragCts = null;
            DestroyDragGhost();
            var slot = _dragSlotIndex;
            _dragSlotIndex = -1;
            _dragDefId = null;
            if (restoreSlot && slot >= 0)
            {
                ShowSlotVisual(slot);
            }
        }

        private void SpawnDragGhost(SpriteRenderer slotRenderer, Camera camera, Vector2 screen)
        {
            DestroyDragGhost();
            var ghost = new GameObject(DragGhostName);
            var sr = ghost.AddComponent<SpriteRenderer>();
            sr.sprite = slotRenderer.sprite;
            sr.sortingLayerID = slotRenderer.sortingLayerID;
            _dragSavedColor = slotRenderer.color;
            sr.color = new Color(_dragSavedColor.r, _dragSavedColor.g, _dragSavedColor.b, DragAlpha);

            var slotGroup = slotRenderer.GetComponent<SortingGroup>();
            _dragHadSortingGroup = slotGroup != null;
            _dragSavedSortingOrder = slotGroup != null
                ? slotGroup.sortingOrder
                : slotRenderer.sortingOrder;

            var ghostGroup = ghost.AddComponent<SortingGroup>();
            ghostGroup.sortingLayerName = slotGroup != null
                ? slotGroup.sortingLayerName
                : slotRenderer.sortingLayerName;
            ghostGroup.sortingOrder = _dragSavedSortingOrder + DragSortingBoost;

            var z = slotRenderer.transform.position.z;
            ghost.transform.position = ScreenToWorldOnPlane(screen, camera, z);
            ghost.transform.rotation = slotRenderer.transform.rotation;
            ghost.transform.localScale = slotRenderer.transform.lossyScale;
            _dragGhost = ghost;
        }

        private void DestroyDragGhost()
        {
            if (_dragGhost != null)
            {
                Object.Destroy(_dragGhost);
                _dragGhost = null;
            }
        }

        private void HideSlotVisual(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Length)
            {
                return;
            }

            _slots[slotIndex]?.SetDisplayVisible(false);
        }

        private void ShowSlotVisual(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Length || slotIndex >= _displayedDefIds.Count)
            {
                return;
            }

            var slot = _slots[slotIndex];
            if (slot?.IconRenderer != null && slot.IconRenderer.sprite != null)
            {
                slot.SetDisplayVisible(true);
            }
        }

        private bool TryResolveRelicSlotUnderPointer(
            Camera camera,
            Vector2 screen,
            out int slotIndex,
            out string defId,
            out RelicIconSlotView slotView)
        {
            slotIndex = -1;
            defId = null;
            slotView = null;
            EnsureBindings();

            ContentIconSlotHitProxy bestRelic = null;
            var bestSort = int.MinValue;
            var targets = PointerHitRegistry.All;
            for (var i = 0; i < targets.Count; i++)
            {
                var proxy = targets[i] as ContentIconSlotHitProxy;
                if (proxy == null
                    || string.IsNullOrEmpty(proxy.DefId)
                    || !proxy.DefId.StartsWith("relic.", System.StringComparison.Ordinal))
                {
                    continue;
                }

                var collider = proxy.HitCollider;
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var planeZ = collider.transform.position.z;
                if (!TryScreenToWorldOnPlane(camera, screen, planeZ, out var world)
                    || !collider.OverlapPoint(world))
                {
                    continue;
                }

                var sort = proxy.HitSortOrder;
                if (bestRelic == null || sort > bestSort)
                {
                    bestRelic = proxy;
                    bestSort = sort;
                }
            }

            if (bestRelic == null)
            {
                return false;
            }

            defId = bestRelic.DefId;
            for (var i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                if (slot == null || slot.HitProxy != bestRelic)
                {
                    continue;
                }

                slotIndex = i;
                slotView = slot;
                return i < _displayedDefIds.Count && _displayedDefIds[i] == defId;
            }

            return false;
        }

        private void RefreshCounterForDefId(string defId)
        {
            EnsureBindings();
            for (var i = 0; i < _displayedDefIds.Count && i < _slots.Length; i++)
            {
                if (_displayedDefIds[i] != defId)
                {
                    continue;
                }

                RefreshCounterForSlot(_slots[i], defId);
                return;
            }
        }

        private void RefreshCounterForSlot(RelicIconSlotView slot, string defId)
        {
            if (slot == null || string.IsNullOrEmpty(defId))
            {
                return;
            }

            if (!RelicCountdownProjection.TryGetEntries(defId, _projectionScratch))
            {
                slot.SetCounter(null, visible: false);
                return;
            }

            // 图标只显示一个裸数字：取首个 period>1 的投影键。
            var entry = _projectionScratch[0];
            string text;
            if (_committedRemaining.TryGetValue(defId, out var map)
                && map != null
                && map.TryGetValue(entry.ProjectKey, out var committed)
                && !string.IsNullOrEmpty(committed))
            {
                text = committed;
            }
            else
            {
                text = entry.Period.ToString();
            }

            slot.SetCounter(text, visible: true);
        }

        private void PruneCommittedToDisplayed()
        {
            if (_committedRemaining.Count == 0)
            {
                return;
            }

            var keep = new HashSet<string>(_displayedDefIds, System.StringComparer.Ordinal);
            var remove = new List<string>();
            foreach (var pair in _committedRemaining)
            {
                if (!keep.Contains(pair.Key))
                {
                    remove.Add(pair.Key);
                }
            }

            for (var i = 0; i < remove.Count; i++)
            {
                _committedRemaining.Remove(remove[i]);
            }
        }

        private static Sprite ResolveSprite(string defId)
        {
            if (CardPresentationConfigCatalog.TryGet(defId, out var dto)
                && dto?.sprites != null
                && !string.IsNullOrWhiteSpace(dto.sprites.mainIcon))
            {
                var fromJson = CardPresentationSpritePath.LoadSprite(dto.sprites.mainIcon);
                if (fromJson != null)
                {
                    return fromJson;
                }
            }

            return ContentIconSlotBinder.TryLoadLegacyRelicIconPublic(defId);
        }

        private static Vector3 ScreenToWorldOnPlane(Vector2 screen, Camera camera, float planeZ)
        {
            TryScreenToWorldOnPlane(camera, screen, planeZ, out var world);
            return world;
        }

        private static bool TryScreenToWorldOnPlane(
            Camera camera,
            Vector2 screen,
            float planeZ,
            out Vector3 world)
        {
            world = default;
            if (camera == null)
            {
                return false;
            }

            var depth = planeZ - camera.transform.position.z;
            world = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
            world.z = planeZ;
            return true;
        }

        private static int ResolveDiscardRelicGold()
        {
            var catalog = NineGridArchitecture.Current?.GetSystem<IContentSystem>()?.Catalog;
            var gold = catalog?.Economy?.DiscardRelicGold ?? 20;
            return Mathf.Max(0, gold);
        }

        /// <summary>
        /// 按视觉阅读序占槽：先上后下、同行从左到右（不改锚点世界坐标）。
        /// </summary>
        private static void SortSlotsByGridReadingOrder(List<RelicIconSlotView> slots)
        {
            slots.Sort(static (a, b) =>
            {
                var aRoot = a?.SlotRoot;
                var bRoot = b?.SlotRoot;
                if (aRoot == null && bRoot == null)
                {
                    return 0;
                }

                if (aRoot == null)
                {
                    return 1;
                }

                if (bRoot == null)
                {
                    return -1;
                }

                var pa = aRoot.localPosition;
                var pb = bRoot.localPosition;
                var yCmp = pb.y.CompareTo(pa.y);
                if (yCmp != 0)
                {
                    return yCmp;
                }

                return pa.x.CompareTo(pb.x);
            });
        }

        private void EnsureBindings()
        {
            if (panelAnchors == null)
            {
                var found = GameObject.Find(DefaultAnchorsName);
                if (found != null)
                {
                    panelAnchors = found.transform;
                }
            }

            if (iconPrefab == null)
            {
                iconPrefab = CardChassisPaths.LoadGameObject(CardChassisPaths.RelicHudIconPrefab);
            }

            if ((_slots == null || _slots.Length == 0) && panelAnchors != null)
            {
                var list = new List<RelicIconSlotView>(panelAnchors.childCount);
                for (var i = 0; i < panelAnchors.childCount; i++)
                {
                    var child = panelAnchors.GetChild(i);
                    if (child == null)
                    {
                        continue;
                    }

                    var view = new RelicIconSlotView(child);
                    view.EnsureDisplay(iconPrefab);
                    list.Add(view);
                }

                SortSlotsByGridReadingOrder(list);
                _slots = list.ToArray();
            }
            else if (_slots != null && iconPrefab != null)
            {
                for (var i = 0; i < _slots.Length; i++)
                {
                    _slots[i]?.EnsureDisplay(iconPrefab);
                }
            }
        }
    }
}

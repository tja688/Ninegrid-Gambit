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
    /// 局内遗物栏表现单例：从 Core PlayerModel.RelicDefIds 读写，刷到 RelicPanelAnchors 子槽图标。
    /// 图标权威：一卡一文件 JSON sprites.mainIcon（#69）；JSON 缺省时回退 RelicVisualCatalog bootstrap。
    /// ADR-0027：左键拖入共享回收区丢弃；栏位布局不因拖动重排。
    /// </summary>
    public sealed class RelicManagerSingleton : MonoBehaviour
    {
        public const string DefaultAnchorsName = "RelicPanelAnchors";
        private const float DragAlpha = 0.55f;
        private const int DragSortingBoost = 200;
        private const string DragGhostName = "__RelicDragGhost";

        [Tooltip("遗物栏锚点根；留空则运行时按名查找 RelicPanelAnchors。")]
        [SerializeField] private Transform panelAnchors;

        private SpriteRenderer[] _slotRenderers = System.Array.Empty<SpriteRenderer>();
        private readonly List<string> _displayedDefIds = new();
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

            ContentIconSlotBinder.ApplyFromPresentationJson(_slotRenderers, _displayedDefIds);
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
            ContentIconSlotBinder.ClearAll(_slotRenderers);
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

            return ContentIconSlotBinder.TryGetSlotTransformByDefId(
                panelAnchors,
                _displayedDefIds,
                relicDefId,
                out anchor);
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

            if (!TryResolveRelicSlotUnderPointer(camera, screen, out var slotIndex, out var defId, out var slotRenderer)
                || slotRenderer == null
                || string.IsNullOrEmpty(defId))
            {
                return false;
            }

            _dragSlotIndex = slotIndex;
            _dragDefId = defId;
            HideSlotVisual(slotIndex);
            SpawnDragGhost(slotRenderer, camera, screen);
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
            // Core 已丢弃；同步栏位触发布局重算。
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
            if (slotIndex < 0 || slotIndex >= _slotRenderers.Length)
            {
                return;
            }

            var sr = _slotRenderers[slotIndex];
            if (sr != null)
            {
                sr.enabled = false;
            }
        }

        private void ShowSlotVisual(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slotRenderers.Length)
            {
                return;
            }

            var sr = _slotRenderers[slotIndex];
            if (sr != null && sr.sprite != null)
            {
                sr.enabled = true;
            }
        }

        private bool TryResolveRelicSlotUnderPointer(
            Camera camera,
            Vector2 screen,
            out int slotIndex,
            out string defId,
            out SpriteRenderer slotRenderer)
        {
            slotIndex = -1;
            defId = null;
            slotRenderer = null;
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
            slotRenderer = bestRelic.GetComponent<SpriteRenderer>();
            if (slotRenderer == null)
            {
                return false;
            }

            for (var i = 0; i < _slotRenderers.Length; i++)
            {
                if (_slotRenderers[i] != slotRenderer)
                {
                    continue;
                }

                slotIndex = i;
                return i < _displayedDefIds.Count && _displayedDefIds[i] == defId;
            }

            return false;
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

            if ((_slotRenderers == null || _slotRenderers.Length == 0) && panelAnchors != null)
            {
                _slotRenderers = ContentIconSlotBinder.CollectChildRenderers(panelAnchors);
            }
        }
    }
}

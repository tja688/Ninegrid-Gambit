using NineGrid.Cards;
using NineGrid.Presentation;
using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 每帧轮询指针命中：合成 Enter / Exit / Down，替代 legacy OnMouse*。
    /// 与 ADR-0006 配套——RIDEV_NOLEGACY 后 OnMouse* 不再可用。
    /// 手牌拖拽优先走本帧已刷新的 hover 槽位带（Hand 在 -50 先于本路由），
    /// 避免 Overlap 与 band 不一致导致「能悬停不能拖」。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-40)]
    public sealed class PointerHitRouter : MonoBehaviour
    {
        private IPointerHitTarget _hovered;
        private Camera _camera;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (FindFirstObjectByType<PointerHitRouter>() != null)
            {
                return;
            }

            var go = new GameObject(nameof(PointerHitRouter));
            DontDestroyOnLoad(go);
            go.AddComponent<PointerHitRouter>();
        }

        private void Update()
        {
            Tick();
        }

        /// <summary>EditMode / 测试可直接驱动一帧。</summary>
        public void Tick()
        {
            var cam = ResolveCamera();
            if (cam == null || !WorldPointerUtility.TryGetPointerScreen(out var screen))
            {
                ClearHover();
                return;
            }

            var best = ResolveBestTarget(cam, screen);
            if (!ReferenceEquals(best, _hovered))
            {
                if (_hovered != null)
                {
                    _hovered.HandlePointerExit();
                }

                _hovered = best;
                _hovered?.HandlePointerEnter();
            }
            else if (_hovered is IMultiColliderPointerHitTarget multiStay)
            {
                // 同一场地面上跨格 / 认领变更时表面身份不变，须刷新子目标悬停。
                multiStay.RefreshPointerHover();
            }

            if (WorldPointerUtility.WasSecondaryPressedThisFrame())
            {
                // 半黑屏 HitSort 盖住遗物栏时仍须能右键丢弃（满栏 Bounce 腾空，#98）。
                if (TryDiscardEquippedRelicUnderPointer(cam, screen))
                {
                    return;
                }

                TryOpenCardInspect(best);
                return;
            }

            if (!WorldPointerUtility.WasPrimaryPressedThisFrame())
            {
                return;
            }

            // 局内 UI 叠层（半黑屏）期间不拖手牌。
            // 覆层不再靠 HitSort/TypePriority 抢几何（ADR-0023）；场地可胜出后由门禁拒。
            // 详述关闭：若胜出者不是覆层表面，显式关面板（旧 Swallow 抢点职责）。
            if (BattleUiDimmerOverlay.IsActive || CardInspectOverlayPresenter.IsOpen)
            {
                best?.HandlePointerDown();
                if (CardInspectOverlayPresenter.IsOpen
                    && (best == null
                        || best.HitTypePriority != PointerHitSurfacePriorities.Overlay))
                {
                    CardInspectOverlayPresenter.CloseIfOpen();
                }

                return;
            }

            // 手牌：按下时拖当前 hover 卡（上一帧槽位带结果），优先于 collider Down。
            var hand = CardEntityLifecycleHook.HandOrNull();
            if (hand != null && hand.TryBeginDragFromHoveredCard())
            {
                return;
            }

            best?.HandlePointerDown();
        }

        /// <summary>
        /// 在指针下找遗物槽（不看半黑屏最高优先级），供满栏 Bounce 期间右键丢弃。
        /// </summary>
        private static bool TryDiscardEquippedRelicUnderPointer(Camera camera, Vector2 screen)
        {
            if (RelicHudHook.TryDiscardRelic == null)
            {
                RelicHudHook.RequestWire();
            }

            if (RelicHudHook.TryDiscardRelic == null)
            {
                return false;
            }

            ContentIconSlotHitProxy bestRelic = null;
            var bestSort = int.MinValue;
            var targets = PointerHitRegistry.All;
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                var proxy = target as ContentIconSlotHitProxy;
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

            return bestRelic != null && RelicHudHook.TryDiscardRelic(bestRelic.DefId);
        }

        private static void TryOpenCardInspect(IPointerHitTarget hovered)
        {
            if (CardInspectOverlayPresenter.IsOpen)
            {
                // 面板已开时右键再点：关面板（半黑屏上也可关）。
                CardInspectOverlayPresenter.CloseIfOpen();
                return;
            }

            if (PresentationInputGates.ChoiceOverlayActive
                || PresentationInputGates.OpeningPresentationActive)
            {
                return;
            }

            var card = ResolveManagedCard(hovered);
            if (card == null)
            {
                var hand = CardEntityLifecycleHook.HandOrNull();
                // 手牌槽位带 hover 可能与 collider 不一致时兜底。
                card = hand != null ? hand.TryPeekHoveredCardForInspect() : null;
            }

            if (card == null)
            {
                return;
            }

            CardInspectOverlayPresenter.TryOpen(card);
        }

        private static ManagedCard ResolveManagedCard(IPointerHitTarget target)
        {
            if (target == null)
            {
                return null;
            }

            // ADR-0023：场卡无自带 collider，命中面是 GroundFieldHitSurface；
            // 须从当前格认领者取 ManagedCard，不能再指望 HitCollider 上挂 CardVisualDriver。
            if (target is GroundFieldHitSurface fieldSurface
                && fieldSurface.TryResolveInspectCard(out var claimed))
            {
                return claimed;
            }

            if (target.HitCollider == null)
            {
                return null;
            }

            var driver = target.HitCollider.GetComponent<CardVisualDriver>()
                ?? target.HitCollider.GetComponentInParent<CardVisualDriver>();
            return driver != null ? driver.BoundCard : null;
        }

        /// <summary>EditMode：强制清 hover 状态。</summary>
        public void ResetHoverStateForTests()
        {
            ClearHover();
        }

        private void ClearHover()
        {
            if (_hovered == null)
            {
                return;
            }

            _hovered.HandlePointerExit();
            _hovered = null;
        }

        private Camera ResolveCamera()
        {
            if (_camera != null)
            {
                return _camera;
            }

            _camera = Camera.main;
            return _camera;
        }

        private static IPointerHitTarget ResolveBestTarget(Camera camera, Vector2 screen)
        {
            IPointerHitTarget best = null;
            var bestSort = int.MinValue;
            var bestType = int.MinValue;
            var tieWarned = false;
            var targets = PointerHitRegistry.All;
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null || !TryOverlapTarget(target, camera, screen))
                {
                    continue;
                }

                var sort = target.HitSortOrder;
                var type = target.HitTypePriority;
                if (best == null
                    || sort > bestSort
                    || (sort == bestSort && type > bestType))
                {
                    best = target;
                    bestSort = sort;
                    bestType = type;
                    continue;
                }

                // ADR-0023：同分属装配错误，不得静默按注册顺序决胜。
                if (!tieWarned && sort == bestSort && type == bestType)
                {
                    tieWarned = true;
                    Debug.LogError(
                        "[PointerHitRouter] 表面优先级同分（装配错误）：sort="
                        + sort
                        + " type="
                        + type
                        + " — "
                        + DescribeTarget(best)
                        + " ↔ "
                        + DescribeTarget(target));
                }
            }

            return best;
        }

        private static bool TryOverlapTarget(IPointerHitTarget target, Camera camera, Vector2 screen)
        {
            if (target is IMultiColliderPointerHitTarget multi)
            {
                return multi.TryOverlapScreenPoint(camera, screen, out _);
            }

            var collider = target.HitCollider;
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
            {
                return false;
            }

            // 按目标所在平面还原世界坐标，避免与手牌/场地 Z 不一致导致 Overlap 漏检。
            var planeZ = collider.transform.position.z;
            if (!TryScreenToWorldOnPlane(camera, screen, planeZ, out var world))
            {
                return false;
            }

            return collider.OverlapPoint(world);
        }

        private static string DescribeTarget(IPointerHitTarget target)
        {
            if (target is Object obj && obj != null)
            {
                return obj.name;
            }

            return target != null ? target.GetType().Name : "null";
        }

        private static bool TryScreenToWorldOnPlane(
            Camera camera,
            Vector2 screen,
            float planeZ,
            out Vector3 world)
        {
            var depth = camera.WorldToScreenPoint(new Vector3(0f, 0f, planeZ)).z;
            world = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
            world.z = planeZ;
            return true;
        }
    }
}

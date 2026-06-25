using System.Collections.Generic;
using DG.Tweening;
using NineGrid.Core;
using NineGrid.Core.Systems;
using NineGrid.Presentation.Performance;
using NineGrid.Presentation.Visuals;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Registry
{
    /// <summary>
    /// 统一演员工厂：按 CardUid 从对象池租用演员、绑定 defId 视觉与槽位数值，并管理归还。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TableNineActorFactory : MonoBehaviour, IController
    {
        [Header("Registry")]
        [SerializeField] private TableNineViewRegistry viewRegistry;

        [Header("Prefab")]
        [SerializeField] private GameObject cardActorPrefab;

        [Header("Pool")]
        [SerializeField, Min(0)] private int prewarmCount = 4;

        private readonly Stack<Transform> mPool = new();

        public TableNineViewRegistry ViewRegistry => viewRegistry;

        public IArchitecture GetArchitecture()
        {
            return NineGridArchitecture.Interface;
        }

        private void Awake()
        {
            EnsureReferences();
            PrewarmPool();
        }

        /// <summary>
        /// Boot 后确保化身演员存在（化身不走 Spawn/Deal 流水线）。
        /// </summary>
        public void EnsureAvatarActor(IArchitecture architecture)
        {
            if (architecture == null)
            {
                return;
            }

            EnsureReferences();
            ContentCatalogRuntimeBootstrap.EnsureLoaded(architecture);

            var board = architecture.GetModel<BoardModel>();
            int avatarUid = board.AvatarUid.Value;
            if (avatarUid <= 0)
            {
                return;
            }

            CoreViewSnapshot snapshot = CoreViewSnapshotFactory.Capture(architecture);
            Transform actor = Acquire(avatarUid, ViewActorZone.Board, architecture, snapshot);
            AlignActorToSlot(actor, snapshot.AvatarSlot);
        }

        /// <summary>
        /// 获取或创建绑定 <paramref name="cardUid"/> 的演员。
        /// </summary>
        public Transform Acquire(
            int cardUid,
            ViewActorZone zone,
            IArchitecture architecture,
            CoreViewSnapshot snapshot = null)
        {
            if (cardUid <= 0)
            {
                return null;
            }

            EnsureReferences();

            Transform actor;
            if (viewRegistry.TryGetActor(cardUid, out actor) && actor != null)
            {
                EnsurePickCollider(actor);
                viewRegistry.RegisterActor(cardUid, actor, zone);
                return actor;
            }

            actor = RentInstance(cardUid);
            actor.gameObject.SetActive(true);

            BindVisuals(actor, cardUid, architecture);
            BindStats(actor, cardUid, architecture, snapshot);
            EnsurePickCollider(actor);

            viewRegistry.RegisterActor(cardUid, actor, zone);
            return actor;
        }

        /// <summary>
        /// 击杀/移除后归还对象池。
        /// </summary>
        public void Release(int cardUid)
        {
            if (cardUid <= 0)
            {
                return;
            }

            EnsureReferences();

            Transform actor;
            if (!viewRegistry.TryGetActor(cardUid, out actor) || actor == null)
            {
                viewRegistry.UnregisterActor(cardUid);
                return;
            }

            viewRegistry.UnregisterActor(cardUid);
            ReturnToPool(actor);
        }

        public void AlignActorToSlot(Transform actor, SlotId slot)
        {
            if (actor == null || viewRegistry == null)
            {
                return;
            }

            Transform anchor;
            if (!viewRegistry.TryGetSlotAnchor(slot, out anchor) || anchor == null)
            {
                return;
            }

            actor.position = anchor.position;
            actor.rotation = anchor.rotation;
        }

        private void PrewarmPool()
        {
            if (cardActorPrefab == null || viewRegistry == null)
            {
                return;
            }

            for (var i = mPool.Count; i < prewarmCount; i++)
            {
                Transform instance = CreateFreshInstance(0);
                ReturnToPool(instance);
            }
        }

        private Transform RentInstance(int cardUid)
        {
            while (mPool.Count > 0)
            {
                Transform pooled = mPool.Pop();
                if (pooled != null)
                {
                    pooled.name = BuildActorName(cardUid);
                    ResetVisualState(pooled);
                    return pooled;
                }
            }

            return CreateFreshInstance(cardUid);
        }

        private Transform CreateFreshInstance(int cardUid)
        {
            Transform parent = viewRegistry.ActorLayer;
            if (cardActorPrefab != null)
            {
                var instance = Instantiate(cardActorPrefab, parent);
                instance.name = BuildActorName(cardUid);
                EnsureSortingProfile(instance.transform);
                return instance.transform;
            }

            var stub = new GameObject(BuildActorName(cardUid));
            stub.transform.SetParent(parent, false);
            stub.AddComponent<SpriteRenderer>();
            return stub.transform;
        }

        private void ReturnToPool(Transform actor)
        {
            if (actor == null)
            {
                return;
            }

            actor.DOKill(true);
            ResetVisualState(actor);
            actor.SetParent(viewRegistry.ActorLayer, false);
            actor.gameObject.SetActive(false);
            mPool.Push(actor);
        }

        private static void ResetVisualState(Transform actor)
        {
            SpriteRenderer[] renderers = actor.GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Color color = renderer.color;
                color.a = 1f;
                renderer.color = color;
            }

            CardSortingLayerProfile profile = actor.GetComponent<CardSortingLayerProfile>();
            if (profile != null)
            {
                profile.CaptureIfNeeded();
                profile.ApplyBaseOrder(0);
            }
        }

        private static void EnsureSortingProfile(Transform actor)
        {
            if (actor == null)
            {
                return;
            }

            CardSortingLayerProfile profile = actor.GetComponent<CardSortingLayerProfile>();
            if (profile == null)
            {
                profile = actor.gameObject.AddComponent<CardSortingLayerProfile>();
            }

            profile.CaptureIfNeeded();
        }

        private void BindVisuals(Transform actor, int cardUid, IArchitecture architecture)
        {
            if (architecture == null)
            {
                return;
            }

            var registry = architecture.GetModel<CardRegistry>();
            CardInstance card;
            if (!registry.TryGet(cardUid, out card) || string.IsNullOrEmpty(card.DefId))
            {
                return;
            }

            TableNineCardVisualBinder.TryApplyForDefId(architecture, actor, card.DefId);
        }

        private void BindStats(
            Transform actor,
            int cardUid,
            IArchitecture architecture,
            CoreViewSnapshot snapshot)
        {
            BoardSlotView slot = ResolveSlot(cardUid, architecture, snapshot);
            if (slot == null)
            {
                return;
            }

            TableNineCardStatusView statusView = actor.GetComponentInChildren<TableNineCardStatusView>(true);
            statusView?.SnapFromSlot(slot);
        }

        private static BoardSlotView ResolveSlot(
            int cardUid,
            IArchitecture architecture,
            CoreViewSnapshot snapshot)
        {
            if (snapshot != null)
            {
                for (var i = 0; i < snapshot.BoardSlots.Count; i++)
                {
                    BoardSlotView slot = snapshot.BoardSlots[i];
                    if (slot.CardUid == cardUid)
                    {
                        return slot;
                    }
                }
            }

            if (architecture == null)
            {
                return null;
            }

            var board = architecture.GetModel<BoardModel>();
            var registry = architecture.GetModel<CardRegistry>();
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slotId = SlotId.Board(i);
                if (board.GetCardUid(slotId) != cardUid)
                {
                    continue;
                }

                CardInstance card;
                if (!registry.TryGet(cardUid, out card))
                {
                    return null;
                }

                return new BoardSlotView(
                    slotId,
                    cardUid,
                    card.DefId,
                    card.Kind,
                    (int)card.Stats.GetBase(StatId.Hp),
                    (int)card.Stats.GetBase(StatId.Hp),
                    (int)card.Stats.GetBase(StatId.MaxHp),
                    (int)card.Stats.GetBase(StatId.MaxHp),
                    (int)card.Stats.GetBase(StatId.Armor),
                    (int)card.Stats.GetBase(StatId.Armor),
                    (int)card.Stats.GetBase(StatId.Attack),
                    (int)card.Stats.GetBase(StatId.Attack),
                    board.IsBlessed(slotId));
            }

            return null;
        }

        private static string BuildActorName(int cardUid)
        {
            return cardUid > 0 ? $"CardActor_{cardUid}" : "CardActor_Pooled";
        }

        private static void EnsurePickCollider(Transform actor)
        {
            if (actor == null || actor.GetComponent<Collider2D>() != null)
            {
                return;
            }

            SpriteRenderer spriteRenderer = actor.GetComponentInChildren<SpriteRenderer>(true);
            var box = actor.gameObject.AddComponent<BoxCollider2D>();
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                box.size = spriteRenderer.sprite.bounds.size;
            }
            else
            {
                box.size = new Vector2(1.625f, 2.0625f);
            }
        }

        private void EnsureReferences()
        {
            if (viewRegistry == null)
            {
                viewRegistry = GetComponent<TableNineViewRegistry>();
                if (viewRegistry == null)
                {
                    viewRegistry = GetComponentInParent<TableNineViewRegistry>();
                }
            }
        }

        private void OnDisable()
        {
            DOTween.Kill(this);
        }
    }
}

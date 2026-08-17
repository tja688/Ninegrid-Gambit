using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Flow.Tutorial
{
    /// <summary>
    /// 教学波次清场：移除场上全部机关与怪物（Avatar 与道具卡不动），
    /// reason=clearResidualBoard。
    /// </summary>
    public sealed class TutorialClearWaveCommand : AbstractCommand<CoreCommandResult>
    {
        protected override CoreCommandResult OnExecute()
        {
            var pipeline = this.GetSystem<IActionPipelineSystem>();
            var board = this.GetModel<BoardModel>();
            var registry = this.GetModel<CardRegistry>();
            var avatarUid = board.AvatarUid.Value;

            var removed = 0;
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var uid = board.GetCardUid(SlotId.Board(i));
                if (uid <= 0
                    || uid == avatarUid
                    || !registry.TryGet(uid, out var card)
                    || card == null)
                {
                    continue;
                }

                if (card.Kind != CardKind.Trap && card.Kind != CardKind.Monster && card.Kind != CardKind.HelpCard)
                {
                    continue;
                }

                pipeline.Enqueue(new RemoveCardAction(uid, ZoneId.Removed, "clearResidualBoard"));
                removed++;
            }

            if (removed > 0)
            {
                pipeline.RunToCompletion();
            }

            return CoreCommandResult.Accept(removed);
        }
    }

    /// <summary>清空抽牌堆并移除场上残留教学道具（阶段重开用手牌回滚）。</summary>
    public sealed class TutorialClearDeckAndItemsCommand : AbstractCommand<CoreCommandResult>
    {
        private readonly bool mClearItemSlots;

        public TutorialClearDeckAndItemsCommand(bool clearItemSlots)
        {
            mClearItemSlots = clearItemSlots;
        }

        protected override CoreCommandResult OnExecute()
        {
            var pipeline = this.GetSystem<IActionPipelineSystem>();
            var deck = this.GetModel<DeckModel>();
            var registry = this.GetModel<CardRegistry>();
            var cleared = 0;

            var drawPile = new List<int>(deck.DrawPileUids);
            for (var i = 0; i < drawPile.Count; i++)
            {
                pipeline.Enqueue(new RemoveCardAction(drawPile[i], ZoneId.Removed, "tutorialReset"));
                cleared++;
            }

            if (mClearItemSlots)
            {
                var items = new List<int>(deck.ItemSlotUids);
                for (var i = 0; i < items.Count; i++)
                {
                    var uid = items[i];
                    if (uid <= 0 || !registry.TryGet(uid, out var card) || card == null)
                    {
                        continue;
                    }

                    var defId = card.DefId ?? string.Empty;
                    if (defId != TutorialContentIds.KnifeDefId && defId != TutorialContentIds.PotionDefId)
                    {
                        continue;
                    }

                    pipeline.Enqueue(new RemoveCardAction(uid, ZoneId.Removed, "tutorialReset"));
                    cleared++;
                }
            }

            if (cleared > 0)
            {
                pipeline.RunToCompletion();
            }

            return CoreCommandResult.Accept(cleared);
        }
    }

    /// <summary>按阶段布局直摆场面 + 洗入抽牌堆（指定格，不走 FillOrder）。</summary>
    public sealed class TutorialSetupPhaseCommand : AbstractCommand<CoreCommandResult>
    {
        private readonly int mPhase;

        public TutorialSetupPhaseCommand(int phase)
        {
            mPhase = phase;
        }

        protected override CoreCommandResult OnExecute()
        {
            var pipeline = this.GetSystem<IActionPipelineSystem>();
            var spawned = 0;

            var placements = TutorialPhaseLayouts.GetBoardPlacements(mPhase);
            for (var i = 0; i < placements.Count; i++)
            {
                var p = placements[i];
                pipeline.Enqueue(new SpawnCardAction(
                    p.DefId,
                    p.Kind,
                    ZoneId.Board,
                    SlotId.Board(p.Slot),
                    1,
                    cause: "tutorialPhase" + mPhase));
                spawned++;
            }

            var pile = TutorialPhaseLayouts.GetDrawPileDefIds(mPhase);
            for (var i = pile.Count - 1; i >= 0; i--)
            {
                var defId = pile[i];
                var kind = ResolveKind(defId);
                pipeline.Enqueue(new ShuffleIntoDrawPileAction(defId, kind, 1, top: true, cause: "tutorialPhase" + mPhase));
                spawned++;
            }

            if (spawned > 0)
            {
                pipeline.RunToCompletion();
            }

            return CoreCommandResult.Accept(spawned);
        }

        private static CardKind ResolveKind(string defId)
        {
            if (defId == TutorialContentIds.DummyTrapDefId
                || defId == TutorialContentIds.ActionDummyDefId
                || defId == TutorialContentIds.MoveDummyDefId)
            {
                return CardKind.Trap;
            }

            if (defId.StartsWith("monster.", System.StringComparison.Ordinal))
            {
                return CardKind.Monster;
            }

            return CardKind.HelpCard;
        }
    }

    /// <summary>阶段2：把抽牌堆顶的教学假人补到刚死那一格（真补牌，不另造第三张）。</summary>
    public sealed class TutorialRefillDummyToSlotCommand : AbstractCommand<CoreCommandResult>
    {
        private readonly int mSlot;

        public TutorialRefillDummyToSlotCommand(int slot)
        {
            mSlot = slot;
        }

        protected override CoreCommandResult OnExecute()
        {
            var pipeline = this.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new TutorialDealDrawPileToSlotAction(
                mSlot,
                TutorialContentIds.DummyTrapDefId,
                "tutorialPhase2Refill"));
            pipeline.RunToCompletion();

            var board = this.GetModel<BoardModel>();
            if (board.IsEmpty(SlotId.Board(mSlot)))
            {
                pipeline.Enqueue(new SpawnCardAction(
                    TutorialContentIds.DummyTrapDefId,
                    CardKind.Trap,
                    ZoneId.Board,
                    SlotId.Board(mSlot),
                    1,
                    cause: "tutorialPhase2Refill"));
                pipeline.RunToCompletion();
            }

            return CoreCommandResult.Accept(1);
        }
    }

    /// <summary>将抽牌堆顶指定 defId 放到指定格，供教学阶段2补牌。</summary>
    public sealed class TutorialDealDrawPileToSlotAction : GameAction
    {
        private static readonly TriggerPoint[] sPostTriggers =
        {
            TriggerPoint.AfterAction,
            TriggerPoint.OnDeal,
            TriggerPoint.OnEnter
        };

        public TutorialDealDrawPileToSlotAction(int slot, string expectedDefId, string cause)
        {
            Slot = SlotId.Board(slot);
            ExpectedDefId = expectedDefId ?? string.Empty;
            Cause = cause ?? string.Empty;
        }

        public SlotId Slot { get; private set; }
        public string ExpectedDefId { get; private set; }
        public string Cause { get; private set; }
        public override string ActionName { get { return "TutorialDealDrawPileToSlot"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var deck = context.GetModel<DeckModel>();
            var board = context.GetModel<BoardModel>();
            var registry = context.GetModel<CardRegistry>();
            if (!Slot.IsBoardSlot || !board.IsEmpty(Slot) || !deck.TryPeekDrawPile(out var uid))
            {
                return GameActionResult.Empty;
            }

            CardInstance card;
            if (!registry.TryGet(uid, out card)
                || card == null
                || (!string.IsNullOrEmpty(ExpectedDefId) && card.DefId != ExpectedDefId))
            {
                return GameActionResult.Empty;
            }

            deck.RemoveUid(uid);
            board.PlaceCard(card, Slot);
            return new GameActionResult()
                .AddWithFaceAbsolutes(
                    context,
                    card,
                    new CoreGameEvent(CoreEventType.CardDealt, context.ActionId, ActionName)
                        .WithCard(uid)
                        .WithSlots(SlotId.None, Slot)
                        .WithSource(card.DefId, Cause));
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(
            GameActionContext context,
            IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
        }
    }

    /// <summary>导演强制顺时针转一次。</summary>
    public sealed class TutorialForceRotateCommand : AbstractCommand<CoreCommandResult>
    {
        protected override CoreCommandResult OnExecute()
        {
            var pipeline = this.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new RotateBoardClockwiseAction());
            pipeline.RunToCompletion();
            return CoreCommandResult.Accept(1);
        }
    }

    /// <summary>阶段死亡重开：回 InteractionLoop、回满血，再摆场。</summary>
    public sealed class TutorialRestartPhaseCommand : AbstractCommand<CoreCommandResult>
    {
        private readonly int mPhase;
        private readonly bool mClearItemSlots;

        public TutorialRestartPhaseCommand(int phase, bool clearItemSlots)
        {
            mPhase = phase;
            mClearItemSlots = clearItemSlots;
        }

        protected override CoreCommandResult OnExecute()
        {
            var pipeline = this.GetSystem<IActionPipelineSystem>();
            var run = this.GetModel<RunModel>();
            if (run.Phase.Value == GamePhase.Defeat)
            {
                pipeline.Enqueue(new ChangePhaseAction(GamePhase.InteractionLoop));
                pipeline.RunToCompletion();
            }

            var board = this.GetModel<BoardModel>();
            var registry = this.GetModel<CardRegistry>();
            var avatarUid = board.AvatarUid.Value;
            if (avatarUid > 0 && registry.TryGet(avatarUid, out var avatar) && avatar != null)
            {
                var maxHp = (int)System.Math.Round(avatar.Stats.GetBase(StatId.MaxHp));
                avatar.Stats.SetBase(StatId.Hp, maxHp);
                pipeline.Enqueue(new HealAction(avatarUid, avatarUid, maxHp, cause: "tutorialRestart"));
                pipeline.RunToCompletion();
            }

            this.SendCommand(new TutorialClearWaveCommand());
            this.SendCommand(new TutorialClearDeckAndItemsCommand(mClearItemSlots));
            this.SendCommand(new TutorialSetupPhaseCommand(mPhase));
            return CoreCommandResult.Accept(mPhase);
        }
    }
}

using System.Collections.Generic;
using QFramework;

namespace NineGrid.Core.Systems
{
    public interface IDeckSystem : ISystem
    {
        int SetupNode(NodeDeckOptions options);
        bool HasEnemyOnBoard();
        bool HasEnemyInDrawPile();
        bool HasPendingEnemyCards();
        bool IsNodeCleared();

        /// <summary>
        /// 重绑系统级 Trigger（InitialGameFactory 若 Clear 了 TriggerSystem 后必须调用）。
        /// </summary>
        void RebindSystemTriggers();
    }

    public sealed class DeckSystem : AbstractSystem, IDeckSystem
    {
        private const string LeaveTrapDefId = "trap.leave";

        private IUnRegister mLeaveTrapInsertUnregister;

        protected override void OnInit()
        {
            RebindSystemTriggers();
        }

        public void RebindSystemTriggers()
        {
            if (mLeaveTrapInsertUnregister != null)
            {
                mLeaveTrapInsertUnregister.UnRegister();
                mLeaveTrapInsertUnregister = null;
            }

            mLeaveTrapInsertUnregister = this.GetSystem<ITriggerSystem>().Register(
                TriggerPoint.OnKill,
                TriggerTiming.Post,
                new DelegateTriggerReaction("deck.leaveTrapInsert", ReactToTrueMonsterKillForLeaveTrap));
        }

        public int SetupNode(NodeDeckOptions options)
        {
            var pipeline = this.GetSystem<IActionPipelineSystem>();
            options = options ?? NodeDeckOptions.CreateDefaultBattle();
            pipeline.Enqueue(new SetupNodeDeckAction(options));
            pipeline.Enqueue(new OpeningDealAction(options));
            pipeline.Enqueue(new FillEmptySlotsAction());
            return pipeline.RunToCompletion();
        }

        public bool HasEnemyOnBoard()
        {
            var registry = this.GetModel<CardRegistry>();
            var board = this.GetModel<BoardModel>();
            foreach (var uid in board.BoardCardUids())
            {
                CardInstance card;
                if (registry.TryGet(uid, out card) && CardCombatRules.IsTrueMonster(card.Kind))
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasEnemyInDrawPile()
        {
            var registry = this.GetModel<CardRegistry>();
            var deck = this.GetModel<DeckModel>();
            for (var i = 0; i < deck.DrawPileUids.Count; i++)
            {
                var card = registry.Get(deck.DrawPileUids[i]);
                if (CardCombatRules.IsTrueMonster(card.Kind))
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasPendingEnemyCards()
        {
            // ADR-0017：敌池/抽牌/场上残留 Trap 不算「还有敌」。
            return HasEnemyInEnemyCardPool() || HasEnemyInDrawPile() || HasEnemyOnBoard();
        }

        public bool IsNodeCleared()
        {
            // ADR-0026 / #113：唯一清关条件是离开机关已被击破（或等价清关标志）。
            return this.GetModel<BattleContextModel>().IsLeaveTrapBroken;
        }

        private IEnumerable<GameAction> ReactToTrueMonsterKillForLeaveTrap(TriggerContext context)
        {
            if (context.Events == null)
            {
                return null;
            }

            var battle = this.GetModel<BattleContextModel>();
            var bossRoom = this.GetModel<RunModel>().Room.Value == RoomKind.Boss;
            var recorded = false;
            for (var i = 0; i < context.Events.Count; i++)
            {
                var evt = context.Events[i];
                if (evt.Type != CoreEventType.CardKilled || evt.CardUid == 0)
                {
                    continue;
                }

                // 开局编入真怪 UID 才计入进度（局中新生怪不加速出门）。
                // 层主房：只认开局层主击破；其它战斗房：⌈N/2⌉（ADR-0026）。
                if (bossRoom)
                {
                    if (battle.TryRecordOpeningBossDefeat(evt.CardUid))
                    {
                        recorded = true;
                    }
                }
                else if (battle.TryRecordOpeningTrueMonsterDefeat(evt.CardUid))
                {
                    recorded = true;
                }
            }

            if (!recorded || !battle.ShouldInsertLeaveTrap(bossRoom))
            {
                return null;
            }

            battle.MarkLeaveTrapInserted();
            return new GameAction[]
            {
                new ShuffleIntoDrawPileAction(LeaveTrapDefId, CardKind.Trap, 1, false, "leaveTrap.insert")
            };
        }

        private bool HasEnemyInEnemyCardPool()
        {
            var registry = this.GetModel<CardRegistry>();
            var deck = this.GetModel<DeckModel>();
            for (var i = 0; i < deck.EnemyCardPoolUids.Count; i++)
            {
                var card = registry.Get(deck.EnemyCardPoolUids[i]);
                if (CardCombatRules.IsTrueMonster(card.Kind))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

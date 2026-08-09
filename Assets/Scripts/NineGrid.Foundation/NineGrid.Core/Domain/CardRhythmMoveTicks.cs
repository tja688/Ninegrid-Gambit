using System.Collections.Generic;

namespace NineGrid.Core
{
    /// <summary>
    /// 移动计数通道推进：本卡盘面→盘面换格时 −1 共享倒计时（ADR-0038）。
    /// </summary>
    public static class CardRhythmMoveTicks
    {
        public static void AppendFromMovedEvents(
            GameActionResult result,
            GameActionContext context,
            IReadOnlyList<CoreGameEvent> events)
        {
            if (result == null || context == null || events == null || events.Count == 0)
            {
                return;
            }

            var registry = context.GetModel<CardRegistry>();
            if (registry == null)
            {
                return;
            }

            for (var i = 0; i < events.Count; i++)
            {
                var gameEvent = events[i];
                if (gameEvent.Type != CoreEventType.CardMoved
                    || !gameEvent.FromSlot.IsBoardSlot
                    || !gameEvent.ToSlot.IsBoardSlot
                    || gameEvent.CardUid <= 0)
                {
                    continue;
                }

                CardInstance card;
                if (!registry.TryGet(gameEvent.CardUid, out card)
                    || card == null
                    || !card.FaceUp
                    || !CardRhythmRules.ShouldTickOnBoardMove(card))
                {
                    continue;
                }

                var previous = card.Counters.Get(CoreCounterKeys.AttackPatternCountdown);
                var remaining = previous;
                if (remaining <= 0)
                {
                    // 已在开火窗：保持 0，等待报名收走。
                    remaining = 0;
                }
                else
                {
                    remaining -= 1;
                }

                card.Counters.Set(CoreCounterKeys.AttackPatternCountdown, remaining);
                result.AddEvent(new CoreGameEvent(
                        CoreEventType.ActionCountdownChanged,
                        context.ActionId,
                        "CardRhythmMoveTick")
                    .WithCard(card.Uid)
                    .WithDelta(remaining - previous)
                    .WithResultValue(remaining));
            }
        }
    }
}

using System.Collections.Generic;
using NineGrid.Core.Effects;
using NineGrid.Core.Systems;

namespace NineGrid.Core
{
    /// <summary>
    /// `[翻面]`：切换牌面朝向；发 <see cref="CoreEventType.CardFaceChanged"/> 并 Post 触发 <see cref="TriggerPoint.OnFlip"/>。
    /// </summary>
    public sealed class FlipCardAction : GameAction
    {
        private static readonly TriggerPoint[] sPostTriggers =
        {
            TriggerPoint.AfterAction,
            TriggerPoint.OnFlip
        };

        public FlipCardAction(int cardUid)
            : this(cardUid, null, null)
        {
        }

        public FlipCardAction(int cardUid, string sourceDefId, string cause)
        {
            CardUid = cardUid;
            SourceDefId = sourceDefId ?? string.Empty;
            Cause = cause ?? string.Empty;
        }

        public int CardUid { get; private set; }
        public string SourceDefId { get; private set; }
        public string Cause { get; private set; }
        public override string ActionName { get { return "FlipCard"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            CardInstance card;
            if (!context.GetModel<CardRegistry>().TryGet(CardUid, out card) || card == null)
            {
                return GameActionResult.Empty;
            }

            card.FaceUp = !card.FaceUp;
            context.GetSystem<IEffectSystem>().SyncOwnerFaceSuppression(CardUid);

            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.CardFaceChanged, context.ActionId, ActionName)
                    .WithCard(CardUid)
                    .WithResultValue(card.FaceUp ? 1 : 0)
                    .WithSource(SourceDefId, Cause));
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(
            GameActionContext context,
            IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
        }
    }

    /// <summary>
    /// 主动翻开：仅背面→正面；已正面则 no-op。
    /// </summary>
    public sealed class RevealFaceAction : GameAction
    {
        private static readonly TriggerPoint[] sPostTriggers =
        {
            TriggerPoint.AfterAction,
            TriggerPoint.OnFlip
        };

        public RevealFaceAction(int cardUid)
            : this(cardUid, null, null)
        {
        }

        public RevealFaceAction(int cardUid, string sourceDefId, string cause)
        {
            CardUid = cardUid;
            SourceDefId = sourceDefId ?? string.Empty;
            Cause = cause ?? string.Empty;
        }

        public int CardUid { get; private set; }
        public string SourceDefId { get; private set; }
        public string Cause { get; private set; }
        public override string ActionName { get { return "RevealFace"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            CardInstance card;
            if (!context.GetModel<CardRegistry>().TryGet(CardUid, out card) || card == null)
            {
                return GameActionResult.Empty;
            }

            if (card.FaceUp)
            {
                return GameActionResult.Empty;
            }

            card.FaceUp = true;
            context.GetSystem<IEffectSystem>().SyncOwnerFaceSuppression(CardUid);

            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.CardFaceChanged, context.ActionId, ActionName)
                    .WithCard(CardUid)
                    .WithResultValue(1)
                    .WithSource(SourceDefId, Cause));
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(
            GameActionContext context,
            IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
        }
    }
}

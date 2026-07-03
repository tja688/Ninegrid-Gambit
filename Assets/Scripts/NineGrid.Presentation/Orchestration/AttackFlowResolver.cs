using NineGrid.Core;

namespace NineGrid.Presentation.Orchestration
{
    /// <summary>
    /// 将 <see cref="CoreGameEvent"/> 伤害语义映射为玩家攻击或怪物反击 Flow。
    /// </summary>
    public static class AttackFlowResolver
    {
        public static FlowId ResolveDamageFlow(FlowPayload payload, CoreViewSnapshot snapshot)
        {
            if (payload == null || snapshot == null)
            {
                return FlowId.CardAttack;
            }

            if (IsAvatar(payload.TargetUid, snapshot) && IsMonster(payload.ActorUid, snapshot))
            {
                return FlowId.Counterattack;
            }

            return FlowId.CardAttack;
        }

        public static FlowId ResolveKillFlow(FlowPayload payload, CoreViewSnapshot snapshot)
        {
            if (payload == null || snapshot == null)
            {
                return FlowId.CardKill;
            }

            if (IsAvatar(payload.TargetUid, snapshot) || IsAvatar(payload.CardUid, snapshot))
            {
                return FlowId.CounterattackKill;
            }

            return FlowId.CardKill;
        }

        private static bool IsAvatar(int cardUid, CoreViewSnapshot snapshot)
        {
            CardView card;
            return cardUid > 0
                   && snapshot.TryGetCard(cardUid, out card)
                   && card.Kind == CardKind.Avatar;
        }

        private static bool IsMonster(int cardUid, CoreViewSnapshot snapshot)
        {
            CardView card;
            return cardUid > 0
                   && snapshot.TryGetCard(cardUid, out card)
                   && card.Kind == CardKind.Monster;
        }
    }
}

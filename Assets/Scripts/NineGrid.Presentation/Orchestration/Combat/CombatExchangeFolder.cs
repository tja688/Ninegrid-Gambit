using System.Collections.Generic;
using NineGrid.Core;

namespace NineGrid.Presentation.Orchestration.Combat
{
    internal readonly struct RoutedCombatInstruction
    {
        public RoutedCombatInstruction(
            PresentationInstruction instruction,
            InstructionRoute route,
            SourceRef source)
        {
            Instruction = instruction;
            Route = route;
            Source = source;
        }

        public PresentationInstruction Instruction { get; }
        public InstructionRoute Route { get; }
        public SourceRef Source { get; }
    }

    /// <summary>
    /// 从有序 Presentation 指令折叠 CombatExchange / StrikeStep。
    /// </summary>
    internal static class CombatExchangeFolder
    {
        public static bool IsCombatInstruction(
            PresentationInstruction instruction,
            InstructionRoute route,
            CoreViewSnapshot snapshot)
        {
            if (instruction == null || route == null)
            {
                return false;
            }

            if (instruction.Kind == PresentationInstructionKind.KillCard)
            {
                return true;
            }

            if (instruction.Kind != PresentationInstructionKind.ShowDamage
                || !InstructionKindFlowRouter.ShouldSynthesizeAttackFlow(instruction.Event))
            {
                return false;
            }

            var resolved = ResolvePlaybackFlow(route.FlowId, route.Payload, snapshot);
            return resolved == FlowId.CardAttack || resolved == FlowId.Counterattack;
        }

        public static CombatExchange Fold(
            IReadOnlyList<RoutedCombatInstruction> segment,
            CoreViewSnapshot snapshot)
        {
            var strikes = new List<StrikeStep>();
            if (segment == null || segment.Count == 0)
            {
                return new CombatExchange(strikes);
            }

            for (var i = 0; i < segment.Count; i++)
            {
                RoutedCombatInstruction item = segment[i];
                if (item.Instruction.Kind == PresentationInstructionKind.ShowDamage)
                {
                    AppendDamageStrike(strikes, item, snapshot);
                    continue;
                }

                if (item.Instruction.Kind == PresentationInstructionKind.KillCard)
                {
                    AttachOrCreateKillStrike(strikes, item, snapshot);
                }
            }

            return new CombatExchange(strikes);
        }

        private static void AppendDamageStrike(
            List<StrikeStep> strikes,
            RoutedCombatInstruction item,
            CoreViewSnapshot snapshot)
        {
            FlowPayload payload = item.Route.Payload;
            if (payload == null)
            {
                return;
            }

            var role = AttackFlowResolver.ResolveDamageFlow(payload, snapshot) == FlowId.Counterattack
                ? StrikeRole.CounterAttack
                : StrikeRole.PrimaryAttack;

            strikes.Add(new StrikeStep
            {
                Role = role,
                AttackerUid = payload.ActorUid,
                TargetUid = payload.TargetUid,
                DamagePayload = payload,
                ActionId = item.Instruction.Event.ActionId,
                DamageSource = item.Source,
            });
        }

        private static void AttachOrCreateKillStrike(
            List<StrikeStep> strikes,
            RoutedCombatInstruction item,
            CoreViewSnapshot snapshot)
        {
            FlowPayload payload = item.Route.Payload;
            if (payload == null)
            {
                return;
            }

            int victimUid = payload.CardUid > 0 ? payload.CardUid : payload.TargetUid;
            for (var strikeIndex = strikes.Count - 1; strikeIndex >= 0; strikeIndex--)
            {
                StrikeStep strike = strikes[strikeIndex];
                if (strike.TargetUid == victimUid && !strike.TargetKilled)
                {
                    strike.TargetKilled = true;
                    strike.KillPayload = payload;
                    strike.KillSource = item.Source;
                    return;
                }
            }

            var role = AttackFlowResolver.ResolveKillFlow(payload, snapshot) == FlowId.CounterattackKill
                ? StrikeRole.CounterAttack
                : StrikeRole.PrimaryAttack;

            strikes.Add(new StrikeStep
            {
                Role = role,
                AttackerUid = payload.ActorUid,
                TargetUid = victimUid,
                TargetKilled = true,
                KillPayload = payload,
                ActionId = item.Instruction.Event.ActionId,
                KillSource = item.Source,
            });
        }

        private static FlowId ResolvePlaybackFlow(FlowId flowId, FlowPayload payload, CoreViewSnapshot snapshot)
        {
            switch (flowId)
            {
                case FlowId.CardAttack:
                    return AttackFlowResolver.ResolveDamageFlow(payload, snapshot);
                case FlowId.CardKill:
                    return AttackFlowResolver.ResolveKillFlow(payload, snapshot);
                default:
                    return flowId;
            }
        }
    }
}

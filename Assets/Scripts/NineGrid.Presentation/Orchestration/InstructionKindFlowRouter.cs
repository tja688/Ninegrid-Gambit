using NineGrid.Core;

namespace NineGrid.Presentation.Orchestration
{
    public enum InstructionRouteKind
    {
        None,
        Flow,
    }

    public sealed class InstructionRoute
    {
        public InstructionRouteKind Kind { get; set; } = InstructionRouteKind.None;
        public FlowId FlowId { get; set; }
        public FlowPayload Payload { get; set; }
    }

    /// <summary>
    /// 将 <see cref="PresentationInstructionKind"/> 语义路由为 Flow，并从 <see cref="CoreGameEvent"/> 抽取 payload。
    /// 暂无专属动效的事件路由到 <see cref="FlowId.SnapshotAlign"/>，由批末 Reconcile 对齐终态。
    /// </summary>
    public static class InstructionKindFlowRouter
    {
        public static bool TryRoute(PresentationInstruction instruction, out InstructionRoute route)
        {
            route = null;
            if (instruction == null || instruction.Event == null)
            {
                return false;
            }

            var payload = FlowPayload.FromEvent(instruction.Event);

            switch (instruction.Kind)
            {
                case PresentationInstructionKind.ShowDamage:
                    route = FlowRoute(
                        ShouldSynthesizeAttackFlow(instruction.Event) ? FlowId.CardAttack : FlowId.SnapshotAlign,
                        payload);
                    return true;

                case PresentationInstructionKind.KillCard:
                    route = FlowRoute(FlowId.CardKill, payload);
                    return true;

                case PresentationInstructionKind.RotateBoard:
                    route = FlowRoute(FlowId.BoardRotate, payload);
                    return true;

                case PresentationInstructionKind.MoveCard:
                    route = FlowRoute(FlowId.MoveCard, payload);
                    return true;

                case PresentationInstructionKind.DealCard:
                case PresentationInstructionKind.SpawnCard:
                    route = FlowRoute(FlowId.CardDeal, payload);
                    return true;

                case PresentationInstructionKind.FillSlots:
                    route = FlowRoute(FlowId.FillSlots, payload);
                    return true;

                case PresentationInstructionKind.UseItem:
                    route = FlowRoute(FlowId.UseItem, payload);
                    return true;

                case PresentationInstructionKind.ShowRejectedIntent:
                case PresentationInstructionKind.UpdateHp:
                case PresentationInstructionKind.UpdateArmor:
                case PresentationInstructionKind.UpdateGold:
                case PresentationInstructionKind.RemoveCard:
                case PresentationInstructionKind.SwapCards:
                case PresentationInstructionKind.ShowDrawPileExhausted:
                case PresentationInstructionKind.UpdateInteractionCount:
                case PresentationInstructionKind.ChangePhase:
                case PresentationInstructionKind.StartNode:
                case PresentationInstructionKind.CompleteNode:
                case PresentationInstructionKind.PickItem:
                case PresentationInstructionKind.ClickEmpty:
                case PresentationInstructionKind.TriggerEffect:
                case PresentationInstructionKind.ApplyModifier:
                case PresentationInstructionKind.DeactivateEffect:
                case PresentationInstructionKind.GrantSkill:
                case PresentationInstructionKind.ModifyBaseStat:
                case PresentationInstructionKind.GrantRelic:
                case PresentationInstructionKind.OfferReward:
                case PresentationInstructionKind.SelectReward:
                case PresentationInstructionKind.SkipReward:
                case PresentationInstructionKind.OfferRooms:
                case PresentationInstructionKind.SelectRoom:
                case PresentationInstructionKind.ResolveRoom:
                case PresentationInstructionKind.AdvanceNode:
                case PresentationInstructionKind.MarkBoard:
                case PresentationInstructionKind.LoadContent:
                    route = FlowRoute(FlowId.SnapshotAlign, payload);
                    return true;

                default:
                    return false;
            }
        }

        private static InstructionRoute FlowRoute(FlowId flowId, FlowPayload payload)
        {
            return new InstructionRoute
            {
                Kind = InstructionRouteKind.Flow,
                FlowId = flowId,
                Payload = payload,
            };
        }

        private static bool ShouldSynthesizeAttackFlow(CoreGameEvent gameEvent)
        {
            return gameEvent.ActorUid > 0
                   && gameEvent.TargetUid > 0
                   && gameEvent.ActorUid != gameEvent.TargetUid;
        }
    }

    public static class FlowMarkers
    {
        public const string Impact = "Impact";
    }
}

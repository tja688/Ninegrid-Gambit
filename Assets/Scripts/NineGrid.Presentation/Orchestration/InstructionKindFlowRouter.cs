using NineGrid.Core;

namespace NineGrid.Presentation.Orchestration
{
    public enum InstructionRouteKind
    {
        None,
        Flow,
        Reaction,
    }

    public sealed class InstructionRoute
    {
        public InstructionRouteKind Kind { get; set; } = InstructionRouteKind.None;
        public FlowId FlowId { get; set; }
        public ReactionId ReactionId { get; set; }
        public FlowPayload Payload { get; set; }
        public ReactionAnchor SuggestedAnchor { get; set; }
        public bool SynthesizeAttackFlow { get; set; }
    }

    /// <summary>
    /// 将 <see cref="PresentationInstructionKind"/> 语义路由为 Flow / Reaction，并从 <see cref="CoreGameEvent"/> 抽取 payload。
    /// 首版覆盖攻击击杀旋转补位 / 开局发牌 / 帮助卡道具参考场景。
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
            var source = new SourceRef(instruction.Sequence, instruction.Event.ActionId, instruction.Kind);

            switch (instruction.Kind)
            {
                case PresentationInstructionKind.ShowDamage:
                    route = new InstructionRoute
                    {
                        Kind = InstructionRouteKind.Reaction,
                        ReactionId = ReactionId.ShowDamage,
                        Payload = payload,
                        SuggestedAnchor = new ReactionAnchor
                        {
                            Kind = ReactionAnchorKind.StepMarker,
                            Marker = FlowMarkers.Impact,
                        },
                        SynthesizeAttackFlow = ShouldSynthesizeAttackFlow(instruction.Event),
                    };
                    return true;

                case PresentationInstructionKind.KillCard:
                    route = new InstructionRoute
                    {
                        Kind = InstructionRouteKind.Flow,
                        FlowId = FlowId.CardKill,
                        Payload = payload,
                    };
                    return true;

                case PresentationInstructionKind.RotateBoard:
                    route = new InstructionRoute
                    {
                        Kind = InstructionRouteKind.Flow,
                        FlowId = FlowId.BoardRotate,
                        Payload = payload,
                    };
                    return true;

                case PresentationInstructionKind.MoveCard:
                    route = new InstructionRoute
                    {
                        Kind = InstructionRouteKind.Flow,
                        FlowId = FlowId.MoveCard,
                        Payload = payload,
                    };
                    return true;

                case PresentationInstructionKind.DealCard:
                    route = new InstructionRoute
                    {
                        Kind = InstructionRouteKind.Flow,
                        FlowId = FlowId.CardDeal,
                        Payload = payload,
                    };
                    return true;

                case PresentationInstructionKind.FillSlots:
                    route = new InstructionRoute
                    {
                        Kind = InstructionRouteKind.Flow,
                        FlowId = FlowId.FillSlots,
                        Payload = payload,
                    };
                    return true;

                case PresentationInstructionKind.UseItem:
                    route = new InstructionRoute
                    {
                        Kind = InstructionRouteKind.Flow,
                        FlowId = FlowId.UseItem,
                        Payload = payload,
                    };
                    return true;

                case PresentationInstructionKind.TriggerEffect:
                    route = new InstructionRoute
                    {
                        Kind = InstructionRouteKind.Reaction,
                        ReactionId = ReactionId.TriggerEffect,
                        Payload = payload,
                        SuggestedAnchor = new ReactionAnchor { Kind = ReactionAnchorKind.StepEnd },
                    };
                    return true;

                case PresentationInstructionKind.ApplyModifier:
                    route = new InstructionRoute
                    {
                        Kind = InstructionRouteKind.Reaction,
                        ReactionId = ReactionId.ApplyModifier,
                        Payload = payload,
                        SuggestedAnchor = new ReactionAnchor { Kind = ReactionAnchorKind.Immediate },
                    };
                    return true;

                case PresentationInstructionKind.UpdateHp:
                    route = new InstructionRoute
                    {
                        Kind = InstructionRouteKind.Reaction,
                        ReactionId = ReactionId.UpdateHp,
                        Payload = payload,
                        SuggestedAnchor = new ReactionAnchor { Kind = ReactionAnchorKind.BatchEnd },
                    };
                    return true;

                case PresentationInstructionKind.UpdateArmor:
                    route = new InstructionRoute
                    {
                        Kind = InstructionRouteKind.Reaction,
                        ReactionId = ReactionId.UpdateArmor,
                        Payload = payload,
                        SuggestedAnchor = new ReactionAnchor { Kind = ReactionAnchorKind.BatchEnd },
                    };
                    return true;

                default:
                    return false;
            }
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

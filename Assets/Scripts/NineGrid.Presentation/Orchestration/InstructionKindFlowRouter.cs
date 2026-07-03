using NineGrid.Core;
using NineGrid.Presentation.Visuals;

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
    /// 暂无专属动效的事件路由到 <see cref="FlowId.SnapshotAlign"/>（no-op 占位，终态由专属 Flow 或数值投影承担）。
    /// 结构性缺口见 <see cref="StructuralFlowGaps"/>。
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
                    route = FlowRoute(ResolveDealFlowId(payload), payload);
                    return true;

                case PresentationInstructionKind.FillSlots:
                    route = FlowRoute(FlowId.FillSlots, payload);
                    return true;

                case PresentationInstructionKind.UseItem:
                    route = FlowRoute(FlowId.UseItem, payload);
                    return true;

                case PresentationInstructionKind.PickItem:
                    route = FlowRoute(FlowId.CardAcquisition, payload);
                    return true;

                case PresentationInstructionKind.ChangePhase:
                    route = FlowRoute(ResolvePhaseChangeFlowId(payload), payload);
                    return true;

                case PresentationInstructionKind.OfferRooms:
                    route = FlowRoute(FlowId.RoomChoiceIn, payload);
                    return true;

                case PresentationInstructionKind.SelectRoom:
                    route = FlowRoute(FlowId.RoomChoiceOut, payload);
                    return true;

                case PresentationInstructionKind.ShowRejectedIntent:
                case PresentationInstructionKind.UpdateHp:
                case PresentationInstructionKind.UpdateArmor:
                case PresentationInstructionKind.UpdateGold:
                case PresentationInstructionKind.RemoveCard:
                case PresentationInstructionKind.SwapCards:
                case PresentationInstructionKind.ShowDrawPileExhausted:
                case PresentationInstructionKind.UpdateInteractionCount:
                case PresentationInstructionKind.StartNode:
                case PresentationInstructionKind.CompleteNode:
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

        private static FlowId ResolveDealFlowId(FlowPayload payload)
        {
            if (payload != null
                && payload.CardUid <= 0
                && payload.Amount > 0
                && IsOpeningDeal(payload))
            {
                return FlowId.CardDeckEntry;
            }

            return FlowId.CardDeal;
        }

        private static FlowId ResolvePhaseChangeFlowId(FlowPayload payload)
        {
            if (payload == null)
            {
                return FlowId.SnapshotAlign;
            }

            var next = (GamePhase)payload.Amount;
            var previous = (GamePhase)payload.Delta;
            bool showNext = InGameHudPhasePolicy.ShouldShowGameplayHud(next);
            bool showPrevious = InGameHudPhasePolicy.ShouldShowGameplayHud(previous);
            if (showNext && !showPrevious)
            {
                return FlowId.InGameUiEntrance;
            }

            if (!showNext && showPrevious)
            {
                return FlowId.InGameUiExit;
            }

            return FlowId.SnapshotAlign;
        }

        private static bool IsOpeningDeal(FlowPayload payload)
        {
            return string.Equals(payload.Message, "opening", System.StringComparison.Ordinal)
                || string.Equals(payload.Cause, "opening", System.StringComparison.Ordinal);
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

        internal static bool ShouldSynthesizeAttackFlow(CoreGameEvent gameEvent)
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

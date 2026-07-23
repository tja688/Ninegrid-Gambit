using System;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Systems;
using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Commands
{
    /// <summary>
    /// 场地拾取写 Core：ApplyPickupItem → 盘面投影摘要；拾取后表现由手牌侧承接。
    /// </summary>
    public sealed class ApplyPickupItemCommand : AbstractCommand<PickupItemPresentationResult>
    {
        private readonly int mGroundSlot;

        public ApplyPickupItemCommand(int groundSlot)
        {
            mGroundSlot = groundSlot;
        }

        protected override PickupItemPresentationResult OnExecute()
        {
            var arch = NineGridArchitecture.Interface;
            if (arch == null)
            {
                this.SendEvent(new PickupItemRejectedEvent
                {
                    GroundSlot = mGroundSlot,
                    Reason = "noArchitecture"
                });
                return default;
            }

            var pipeline = arch.GetSystem<IActionPipelineSystem>();
            var startIndex = pipeline.EventLog.Entries.Count;
            var result = arch.GetSystem<IPhaseSystem>().ApplyPickupItem(SlotId.Board(mGroundSlot));
            var summary = new PickupItemPresentationResult
            {
                Accepted = result.Accepted,
                Reason = result.Reason ?? string.Empty,
            };
            if (!result.Accepted)
            {
                this.SendEvent(new PickupItemRejectedEvent
                {
                    GroundSlot = mGroundSlot,
                    Reason = result.Reason ?? "rejected"
                });
                Debug.LogWarning("[ApplyPickupItemCommand] PickupItem 被拒: " + result.Reason);
                summary.Moves = Array.Empty<PostKillCardMove>();
                summary.Deals = Array.Empty<PostKillCardDeal>();
                summary.RemovedUids = Array.Empty<int>();
                return summary;
            }

            var projection = BoardPresentationStepProjector.Project(
                pipeline.EventLog.Entries,
                startIndex,
                arch.GetModel<CardRegistry>());
            var pickedUid = projection.PickedUid;
            summary.CardUid = pickedUid;
            summary.Steps = projection.Steps ?? Array.Empty<BoardPresentationStep>();
            summary.Moves = projection.LegacyMoves ?? Array.Empty<PostKillCardMove>();
            summary.Deals = projection.LegacyDeals ?? Array.Empty<PostKillCardDeal>();
            summary.RemovedUids = projection.LegacyRemovedUids ?? Array.Empty<int>();

            if (pickedUid > 0
                && arch.GetModel<CardRegistry>().TryGet(pickedUid, out var card))
            {
                summary.AcquiredToHand = card.Zone.Value == ZoneId.ItemSlots;
                summary.RemovedWithoutHand = card.Zone.Value == ZoneId.Removed;
            }

            var phase = arch.GetSystem<IPhaseSystem>().CurrentPhase;
            summary.NodeClearedOrRewardPhase =
                phase == GamePhase.RewardItemChoice
                || phase == GamePhase.ClearCheck
                || phase == GamePhase.NodeCompleted
                || arch.GetSystem<IDeckSystem>().IsNodeCleared();

            BattleSessionController.PresentPickupPostApplyEffects(startIndex, pickedUid);
            return summary;
        }
    }
}

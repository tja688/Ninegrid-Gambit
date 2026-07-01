using NineGrid.Core;
using UnityEngine;

namespace NineGrid.Presentation.Orchestration
{
    public sealed class FlowPayload
    {
        public int ActorUid { get; set; }
        public int TargetUid { get; set; }
        public int CardUid { get; set; }
        public SlotId FromSlot { get; set; } = SlotId.None;
        public SlotId ToSlot { get; set; } = SlotId.None;
        public int Amount { get; set; }
        public int Delta { get; set; }
        public int Index { get; set; }
        public int Total { get; set; }
        public Vector2 Direction { get; set; } = Vector2.right;
        public string SourceDefId { get; set; } = string.Empty;
        public string Cause { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int RemainingHp { get; set; } = -1;
        public int RemainingArmor { get; set; } = -1;

        public static FlowPayload FromEvent(CoreGameEvent gameEvent)
        {
            if (gameEvent == null)
            {
                return new FlowPayload();
            }

            var payload = new FlowPayload
            {
                ActorUid = gameEvent.ActorUid,
                TargetUid = gameEvent.TargetUid,
                CardUid = gameEvent.CardUid,
                FromSlot = gameEvent.FromSlot,
                ToSlot = gameEvent.ToSlot,
                Amount = gameEvent.Amount,
                Delta = gameEvent.Delta,
                RemainingHp = gameEvent.RemainingHp,
                RemainingArmor = gameEvent.RemainingArmor,
                SourceDefId = gameEvent.SourceDefId,
                Cause = gameEvent.Cause,
                Message = gameEvent.Message,
            };

            if (gameEvent.Type == CoreEventType.BoardRotated)
            {
                payload.Direction = gameEvent.Amount >= 0 ? Vector2.right : Vector2.left;
            }

            return payload;
        }
    }
}

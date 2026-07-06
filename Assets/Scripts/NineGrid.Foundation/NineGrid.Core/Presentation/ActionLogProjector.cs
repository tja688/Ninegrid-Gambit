using System.Collections.Generic;

namespace NineGrid.Core
{
    public sealed class ActionLogRow
    {
        public ActionLogRow(CoreGameEvent gameEvent, PresentationEventMapEntry mapEntry)
        {
            Sequence = gameEvent.Sequence;
            ActionId = gameEvent.ActionId;
            ActionName = gameEvent.ActionName;
            EventType = gameEvent.Type;
            InstructionKind = mapEntry.InstructionKind;
            Category = mapEntry.Category;
            CardUid = gameEvent.CardUid;
            ActorUid = gameEvent.ActorUid;
            TargetUid = gameEvent.TargetUid;
            FromSlot = gameEvent.FromSlot;
            ToSlot = gameEvent.ToSlot;
            Amount = gameEvent.Amount;
            Delta = gameEvent.Delta;
            RemainingHp = gameEvent.RemainingHp;
            RemainingArmor = gameEvent.RemainingArmor;
            RemovedAttack = gameEvent.RemovedAttack;
            RemovedArmor = gameEvent.RemovedArmor;
            SourceDefId = gameEvent.SourceDefId;
            Cause = gameEvent.Cause;
            Message = gameEvent.Message;
            Summary = BuildSummary(gameEvent, mapEntry);
        }

        public long Sequence { get; private set; }
        public int ActionId { get; private set; }
        public string ActionName { get; private set; }
        public CoreEventType EventType { get; private set; }
        public PresentationInstructionKind InstructionKind { get; private set; }
        public PresentationEventCategory Category { get; private set; }
        public int CardUid { get; private set; }
        public int ActorUid { get; private set; }
        public int TargetUid { get; private set; }
        public SlotId FromSlot { get; private set; }
        public SlotId ToSlot { get; private set; }
        public int Amount { get; private set; }
        public int Delta { get; private set; }
        public int RemainingHp { get; private set; }
        public int RemainingArmor { get; private set; }
        public int RemovedAttack { get; private set; }
        public int RemovedArmor { get; private set; }
        public string SourceDefId { get; private set; }
        public string Cause { get; private set; }
        public string Message { get; private set; }
        public string Summary { get; private set; }

        private static string BuildSummary(CoreGameEvent gameEvent, PresentationEventMapEntry mapEntry)
        {
            var summary = "#" + gameEvent.Sequence + " " + mapEntry.Label;
            if (gameEvent.CardUid != 0)
            {
                summary += " card=" + gameEvent.CardUid;
            }

            if (!gameEvent.FromSlot.IsNone || !gameEvent.ToSlot.IsNone)
            {
                summary += " " + gameEvent.FromSlot + "->" + gameEvent.ToSlot;
            }

            if (gameEvent.Amount != 0 || gameEvent.Delta != 0)
            {
                summary += " amount=" + gameEvent.Amount + " delta=" + gameEvent.Delta;
            }

            if (!string.IsNullOrEmpty(gameEvent.SourceDefId) || !string.IsNullOrEmpty(gameEvent.Cause))
            {
                summary += " source=" + gameEvent.SourceDefId + " cause=" + gameEvent.Cause;
            }

            if (!string.IsNullOrEmpty(gameEvent.Message))
            {
                summary += " " + gameEvent.Message;
            }

            return summary;
        }
    }

    public static class ActionLogProjector
    {
        public static List<ActionLogRow> FromEventLog(EventLog eventLog)
        {
            var rows = new List<ActionLogRow>();
            if (eventLog == null)
            {
                return rows;
            }

            var entries = eventLog.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                rows.Add(new ActionLogRow(entries[i], PresentationEventMap.Get(entries[i].Type)));
            }

            return rows;
        }
    }
}

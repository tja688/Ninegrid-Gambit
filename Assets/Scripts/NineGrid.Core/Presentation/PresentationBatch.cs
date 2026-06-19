using System.Collections.Generic;

namespace NineGrid.Core
{
    public sealed class PresentationInstruction
    {
        public PresentationInstruction(CoreGameEvent gameEvent, PresentationEventMapEntry mapEntry)
        {
            Event = gameEvent;
            MapEntry = mapEntry;
        }

        public CoreGameEvent Event { get; private set; }
        public PresentationEventMapEntry MapEntry { get; private set; }

        public long Sequence
        {
            get { return Event != null ? Event.Sequence : -1L; }
        }

        public PresentationInstructionKind Kind
        {
            get { return MapEntry != null ? MapEntry.InstructionKind : PresentationInstructionKind.None; }
        }
    }

    public sealed class PresentationBatch
    {
        public PresentationBatch(int batchId, IReadOnlyList<PresentationInstruction> instructions, CoreViewSnapshot snapshot)
        {
            BatchId = batchId;
            Instructions = instructions ?? new PresentationInstruction[0];
            Snapshot = snapshot;
            FromSequence = Instructions.Count > 0 ? Instructions[0].Sequence : -1L;
            ToSequence = Instructions.Count > 0 ? Instructions[Instructions.Count - 1].Sequence : -1L;
            RequiresAcknowledgement = HasBlockingInstruction(Instructions);
        }

        public int BatchId { get; private set; }
        public long FromSequence { get; private set; }
        public long ToSequence { get; private set; }
        public IReadOnlyList<PresentationInstruction> Instructions { get; private set; }
        public CoreViewSnapshot Snapshot { get; private set; }
        public bool RequiresAcknowledgement { get; private set; }

        private static bool HasBlockingInstruction(IReadOnlyList<PresentationInstruction> instructions)
        {
            for (var i = 0; i < instructions.Count; i++)
            {
                if (instructions[i].MapEntry != null && instructions[i].MapEntry.LocksInput)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public static class PresentationBatchFactory
    {
        public static PresentationBatch FromEventLog(EventLog eventLog, int startIndex, int batchId, CoreViewSnapshot snapshot)
        {
            var instructions = new List<PresentationInstruction>();
            if (eventLog != null)
            {
                var entries = eventLog.Entries;
                for (var i = startIndex; i < entries.Count; i++)
                {
                    var map = PresentationEventMap.Get(entries[i].Type);
                    if (map.RequiresPlayback)
                    {
                        instructions.Add(new PresentationInstruction(entries[i], map));
                    }
                }
            }

            return new PresentationBatch(batchId, instructions, snapshot);
        }
    }
}

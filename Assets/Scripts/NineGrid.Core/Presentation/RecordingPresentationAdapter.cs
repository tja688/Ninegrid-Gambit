using System.Collections.Generic;

namespace NineGrid.Core
{
    public sealed class PresentationPlaybackResult
    {
        public PresentationPlaybackResult(int batchId, int instructionCount)
        {
            BatchId = batchId;
            InstructionCount = instructionCount;
        }

        public int BatchId { get; private set; }
        public int InstructionCount { get; private set; }
    }

    public interface IPresentationAdapter
    {
        PresentationPlaybackResult Play(PresentationBatch batch);
    }

    public sealed class RecordingPresentationAdapter : IPresentationAdapter
    {
        private readonly List<string> mLines = new List<string>();
        private readonly Dictionary<int, SlotId> mCardSlots = new Dictionary<int, SlotId>();

        public IReadOnlyList<string> Lines
        {
            get { return mLines; }
        }

        public IReadOnlyDictionary<int, SlotId> CardSlots
        {
            get { return mCardSlots; }
        }

        public PresentationPlaybackResult Play(PresentationBatch batch)
        {
            if (batch == null)
            {
                return new PresentationPlaybackResult(0, 0);
            }

            mLines.Add("batch " + batch.BatchId + " " + batch.FromSequence + "-" + batch.ToSequence);
            for (var i = 0; i < batch.Instructions.Count; i++)
            {
                Apply(batch.Instructions[i]);
            }

            return new PresentationPlaybackResult(batch.BatchId, batch.Instructions.Count);
        }

        private void Apply(PresentationInstruction instruction)
        {
            var evt = instruction.Event;
            mLines.Add(evt.Sequence + " " + instruction.Kind + " " + evt);

            if (evt.CardUid == 0)
            {
                return;
            }

            switch (instruction.Kind)
            {
                case PresentationInstructionKind.MoveCard:
                case PresentationInstructionKind.DealCard:
                case PresentationInstructionKind.SpawnCard:
                    mCardSlots[evt.CardUid] = evt.ToSlot;
                    break;
                case PresentationInstructionKind.RemoveCard:
                case PresentationInstructionKind.KillCard:
                case PresentationInstructionKind.PickItem:
                    mCardSlots[evt.CardUid] = SlotId.None;
                    break;
            }
        }
    }
}

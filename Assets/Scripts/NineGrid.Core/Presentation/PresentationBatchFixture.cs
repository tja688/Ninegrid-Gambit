using System.Collections.Generic;

namespace NineGrid.Core
{
    /// <summary>
    /// 测试/调试用手搓真批次：对 <see cref="CoreGameEvent"/> 赋序并套用 <see cref="PresentationEventMap"/>，
    /// 产出与 <see cref="PresentationBatchFactory"/> 同契约的 <see cref="PresentationBatch"/>。
    /// </summary>
    public static class PresentationBatchFixture
    {
        public static PresentationBatch Create(
            int batchId,
            IReadOnlyList<CoreGameEvent> events,
            CoreViewSnapshot snapshot,
            long sequenceStart = 0L)
        {
            var instructions = new List<PresentationInstruction>();
            if (events != null)
            {
                var sequence = sequenceStart;
                for (var i = 0; i < events.Count; i++)
                {
                    var gameEvent = events[i];
                    if (gameEvent == null)
                    {
                        continue;
                    }

                    gameEvent.AssignSequence(sequence++);
                    var map = PresentationEventMap.Get(gameEvent.Type);
                    if (map.RequiresPlayback)
                    {
                        instructions.Add(new PresentationInstruction(gameEvent, map));
                    }
                }
            }

            return new PresentationBatch(batchId, instructions, snapshot);
        }
    }
}

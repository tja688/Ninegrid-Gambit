using System.Threading;
using Cysharp.Threading.Tasks;

namespace NineGrid.Cards
{
    /// <summary>
    /// 执行层入口：消费带 commitment 的位移类 Step（Rotate/Swap/Move）。
    /// 编排层（Drain）只供给有序 Step[]，不再直调 Field.Rotate/Hop/Move。
    /// </summary>
    public static class BoardMotionStepScheduler
    {
        public static async UniTask ExecuteMotionStepAsync(
            GroundFieldManagerSingleton field,
            BoardPresentationStep step,
            CancellationToken cancellationToken)
        {
            if (field == null)
            {
                return;
            }

            switch (step.Kind)
            {
                case BoardPresentationStepKind.Rotate:
                    await field.RotateOuterRingWhileBusyAsync(step.Clockwise, cancellationToken);
                    break;

                case BoardPresentationStepKind.Swap:
                case BoardPresentationStepKind.Move:
                    if (step.Moves == null || step.Moves.Length == 0)
                    {
                        return;
                    }

                    await field.ApplyBoardMovesAndHopAsync(
                        step.Moves,
                        cancellationToken,
                        skipBusyGuard: true,
                        commitment: step.Commitment);
                    break;
            }
        }

        public static bool IsMotionStep(BoardPresentationStepKind kind)
        {
            return kind == BoardPresentationStepKind.Rotate
                   || kind == BoardPresentationStepKind.Swap
                   || kind == BoardPresentationStepKind.Move;
        }
    }
}

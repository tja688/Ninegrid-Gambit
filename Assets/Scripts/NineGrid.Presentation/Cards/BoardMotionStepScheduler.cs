using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Core;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using QFramework;

namespace NineGrid.Cards
{
    /// <summary>
    /// 执行层入口：消费带 commitment 的位移类 Step（Rotate/Swap/Move）。
    /// 编排层（Drain）只供给有序 Step[]；经 GroundPresentation / System 执行。
    /// </summary>
    public static class BoardMotionStepScheduler
    {
        public static async UniTask ExecuteMotionStepAsync(
            GroundFieldView field,
            BoardPresentationStep step,
            CancellationToken cancellationToken)
        {
            CardLifecycleAudioCues.PulseMotion(step.Kind, "BoardMotionStepScheduler.ExecuteMotionStepAsync");

            var presentation = TryGetPresentation();
            if (presentation != null)
            {
                await ExecuteViaPresentationAsync(presentation, step, cancellationToken);
                return;
            }

            if (field == null)
            {
                return;
            }

            // System 尚未 Bind 时回退到 View forwarder。
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

        private static async UniTask ExecuteViaPresentationAsync(
            GroundPresentation presentation,
            BoardPresentationStep step,
            CancellationToken cancellationToken)
        {
            switch (step.Kind)
            {
                case BoardPresentationStepKind.Rotate:
                    await presentation.RotateOuterRingAsync(step.Clockwise, cancellationToken);
                    break;

                case BoardPresentationStepKind.Swap:
                case BoardPresentationStepKind.Move:
                    if (step.Moves == null || step.Moves.Length == 0)
                    {
                        return;
                    }

                    await presentation.ApplyBoardMovesAndHopAsync(
                        step.Moves,
                        cancellationToken,
                        skipBusyGuard: true,
                        commitment: step.Commitment);
                    break;
            }
        }

        private static GroundPresentation TryGetPresentation()
        {
            var system = NineGridArchitecture.Interface?.GetSystem<IGroundFieldGeometrySystem>()
                         as GroundFieldGeometrySystem;
            return system != null && system.IsBound ? system.Presentation : null;
        }

        public static bool IsMotionStep(BoardPresentationStepKind kind)
        {
            return kind == BoardPresentationStepKind.Rotate
                   || kind == BoardPresentationStepKind.Swap
                   || kind == BoardPresentationStepKind.Move;
        }
    }
}

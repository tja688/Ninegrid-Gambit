using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Core;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 效果打击编排桥（ADR-0050）：场上卡造成的伤害/破坏统一走「攻击动作 + 受击反馈」串行表演。
    /// 委托由组合根注入（EffectStrikeChoreographer）；未装配时各调用点按旧冲刷路径降级，不丢指令。
    /// </summary>
    public static class EffectStrikeHook
    {
        /// <summary>开批重建打击计划（含把效果伤害指令移入排期器打击暂扣区）。</summary>
        public static Action<PresentationBatch> OnBatchOpened;

        /// <summary>播完当批所有未消费打击组（PresentStep 触发脉冲节拍之后、FlushBeats 之前）。</summary>
        public static Func<CancellationToken, UniTask> PlayAllPendingStrikes;

        /// <summary>
        /// 播涉及指定卡的打击组（该卡作为打击者或受击者）。
        /// 在该卡即将退场/被移除呈现之前调用，保证打击与飘字先于消失可见。
        /// </summary>
        public static Func<int, CancellationToken, UniTask> PlayStrikesInvolving;

        public static void Reset()
        {
            OnBatchOpened = null;
            PlayAllPendingStrikes = null;
            PlayStrikesInvolving = null;
        }

        public static void NotifyBatchOpened(PresentationBatch batch)
        {
            OnBatchOpened?.Invoke(batch);
        }

        public static UniTask NotifyPlayAllPendingStrikesAsync(CancellationToken cancellationToken)
        {
            var handler = PlayAllPendingStrikes;
            return handler != null ? handler(cancellationToken) : UniTask.CompletedTask;
        }

        public static UniTask NotifyPlayStrikesInvolvingAsync(int cardUid, CancellationToken cancellationToken)
        {
            var handler = PlayStrikesInvolving;
            return handler != null ? handler(cardUid, cancellationToken) : UniTask.CompletedTask;
        }
    }
}

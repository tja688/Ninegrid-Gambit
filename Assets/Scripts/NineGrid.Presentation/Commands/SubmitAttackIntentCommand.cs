using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Commands
{
    /// <summary>
    /// 玩家攻击意图：Core 合法性裁决后经 <see cref="IPresentationRuntimeSystem"/> 提交导演。
    /// </summary>
    public sealed class SubmitAttackIntentCommand : AbstractCommand<bool>
    {
        private readonly int mGroundSlot;

        public SubmitAttackIntentCommand(int groundSlot)
        {
            mGroundSlot = groundSlot;
        }

        protected override bool OnExecute()
        {
            var architecture = NineGridArchitecture.Interface;
            if (IsOrphanMidBattleRewardPending(architecture))
            {
                this.SendEvent(new AttackIntentRejectedEvent
                {
                    GroundSlot = mGroundSlot,
                    Reason = "orphanMidBattleReward"
                });
                Debug.LogWarning(
                    "[SubmitAttackIntentCommand] Attack 被孤儿中局奖励门禁拒绝 slot=" + mGroundSlot);
                return false;
            }

            string legalityReject;
            if (!BoardIntentLegality.TryExplainAttack(architecture, mGroundSlot, out legalityReject))
            {
                this.SendEvent(new AttackIntentRejectedEvent
                {
                    GroundSlot = mGroundSlot,
                    Reason = legalityReject
                });
                Debug.LogWarning(
                    "[SubmitAttackIntentCommand] Attack 被 Core 合法性拒绝 slot="
                    + mGroundSlot + ": " + legalityReject);
                return false;
            }

            var runtime = this.GetSystem<IPresentationRuntimeSystem>();
            if (runtime == null || !runtime.IsStarted)
            {
                Debug.LogWarning("[SubmitAttackIntentCommand] 表现意图运行时未启动。");
                return false;
            }

            bool preview;
            return runtime.TrySubmitIntent(
                new InputIntent(InputIntentKinds.Attack, mGroundSlot),
                out preview);
        }

        private static bool IsOrphanMidBattleRewardPending(IArchitecture architecture)
        {
            if (CombatHitSink.ChoiceOverlayActive || architecture == null)
            {
                return false;
            }

            var pending = architecture.GetModel<PendingChoiceModel>();
            if (pending.Kind.Value != PendingChoiceKind.Reward
                || pending.RewardOptions == null
                || pending.RewardOptions.Count == 0)
            {
                return false;
            }

            var phase = architecture.GetSystem<NineGrid.Core.Systems.IPhaseSystem>().CurrentPhase;
            return phase == GamePhase.InteractionLoop || phase == GamePhase.RewardItemChoice;
        }
    }
}

using NineGrid.Presentation.Debugging;
using NineGrid.Core;
using NineGrid.Presentation.Reactions;
using NineGrid.Presentation.Visuals;
using UnityEngine;

namespace NineGrid.Presentation.Orchestration.Bindings
{
    public sealed class ShowDamageReactionBinding : IReactionBinding
    {
        private readonly DamageNumbersReaction mReaction;

        public ShowDamageReactionBinding(DamageNumbersReaction reaction)
        {
            mReaction = reaction;
        }

        public ReactionId Id => ReactionId.ShowDamage;

        public void Play(IViewRegistry registry, FlowPayload payload)
        {
            if (mReaction == null || payload == null)
            {
                return;
            }

            int targetUid = payload.TargetUid > 0 ? payload.TargetUid : payload.CardUid;
            Transform target = registry?.ResolveActor(targetUid);
            if (target == null)
            {
                return;
            }

            mReaction.Play(target, payload.Amount, DamagePopupKind.Damage);
        }

        public void Stop()
        {
            mReaction?.StopAndRestore();
        }
    }

    public sealed class TriggerEffectReactionBinding : IReactionBinding
    {
        private readonly EffectTriggerReaction mReaction;

        public TriggerEffectReactionBinding(EffectTriggerReaction reaction)
        {
            mReaction = reaction;
        }

        public ReactionId Id => ReactionId.TriggerEffect;

        public void Play(IViewRegistry registry, FlowPayload payload)
        {
            if (mReaction == null || payload == null)
            {
                return;
            }

            mReaction.Play(
                payload.CardUid,
                payload.Message,
                payload.SourceDefId,
                payload.Cause);
        }

        public void Stop()
        {
            mReaction?.StopAndRestore();
        }
    }

    public sealed class ApplyModifierReactionBinding : IReactionBinding
    {
        private readonly ModifierApplyReaction mReaction;

        public ApplyModifierReactionBinding(ModifierApplyReaction reaction)
        {
            mReaction = reaction;
        }

        public ReactionId Id => ReactionId.ApplyModifier;

        public void Play(IViewRegistry registry, FlowPayload payload)
        {
            if (mReaction == null || payload == null)
            {
                return;
            }

            mReaction.Play(
                payload.CardUid,
                payload.Amount,
                payload.Delta,
                payload.Message);
        }

        public void Stop()
        {
            mReaction?.StopAndRestore();
        }
    }

    public sealed class StatusTickReactionBinding : IReactionBinding
    {
        private readonly StatusTickReaction mReaction;

        public StatusTickReactionBinding(StatusTickReaction reaction)
        {
            mReaction = reaction;
        }

        public ReactionId Id => ReactionId.StatusTick;

        public void Play(IViewRegistry registry, FlowPayload payload)
        {
            // 批内状态跳变由 CardStatus 视图在后续迭代接线；批末 Reconcile 兜底。
        }

        public void Stop()
        {
            mReaction?.StopAndRestore();
        }
    }

    public sealed class UpdateHpReactionBinding : IReactionBinding
    {
        private readonly TableNineStatusPanelView mStatusPanel;

        public UpdateHpReactionBinding(TableNineStatusPanelView statusPanel)
        {
            mStatusPanel = statusPanel;
        }

        public ReactionId Id => ReactionId.UpdateHp;

        public void Play(IViewRegistry registry, FlowPayload payload)
        {
            if (mStatusPanel == null || payload == null)
            {
                return;
            }

            if (payload.RemainingHp >= 0)
            {
                mStatusPanel.SetHp(payload.RemainingHp);
            }
            else if (payload.Delta != 0)
            {
                // BatchEnd 前增量更新占位；完整对齐由 Reconcile 负责。
            }
        }

        public void Stop()
        {
        }
    }

    public sealed class UpdateArmorReactionBinding : IReactionBinding
    {
        private readonly TableNineStatusPanelView mStatusPanel;

        public UpdateArmorReactionBinding(TableNineStatusPanelView statusPanel)
        {
            mStatusPanel = statusPanel;
        }

        public ReactionId Id => ReactionId.UpdateArmor;

        public void Play(IViewRegistry registry, FlowPayload payload)
        {
            if (mStatusPanel == null || payload == null)
            {
                return;
            }

            if (payload.RemainingArmor >= 0)
            {
                mStatusPanel.SetArmor(payload.RemainingArmor);
            }
        }

        public void Stop()
        {
        }
    }

    public sealed class StatusPanelReconcilable : IReconcilable
    {
        private readonly TableNineStatusPanelView mStatusPanel;

        public StatusPanelReconcilable(TableNineStatusPanelView statusPanel)
        {
            mStatusPanel = statusPanel;
        }

        public void ApplySnapshot(CoreViewSnapshot snapshot)
        {
            mStatusPanel?.ApplySnapshot(snapshot);
        }
    }
}

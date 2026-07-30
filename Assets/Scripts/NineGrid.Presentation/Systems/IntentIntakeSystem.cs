using System;
using NineGrid.Core;
using NineGrid.Flow;
using NineGrid.Flow.Diagnostics;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// #50 IntentIntake：薄前置，不自持缓冲/互斥可写真相。
    /// </summary>
    public sealed class IntentIntakeSystem : AbstractSystem, IIntentIntake
    {
        private IAccelerationSink mAcceleration = NoOpAccelerationSink.Instance;
        private Func<InputIntent, bool> mLegalityOverride;

        public void SetAccelerationSink(IAccelerationSink sink)
        {
            mAcceleration = sink ?? NoOpAccelerationSink.Instance;
        }

        /// <summary>EditMode：注入恒真/录制假体，跳过 Core 棋盘合法性。</summary>
        public void SetLegalityOverride(Func<InputIntent, bool> legalityOverride)
        {
            mLegalityOverride = legalityOverride;
        }

        public IntentDisposition Submit(
            InputIntent intent,
            InputOwner targetSurface,
            out bool uiPickPreview)
        {
            uiPickPreview = false;

            var input = this.GetSystem<IPresentationInputStateSystem>();
            var owner = input != null ? input.CurrentOwner : InputOwner.ProtectedField;
            var mainlineBusy = input != null && input.MainlineBusy;

            if (targetSurface != owner)
            {
                if (mainlineBusy)
                {
                    mAcceleration.Tap(intent);
                }

                Reject(
                    intent,
                    "ownerMismatch owner=" + owner + " target=" + targetSurface,
                    mainlineBusy);
                return IntentDisposition.Reject;
            }

            // 主线忙时：对当前所有者的每次点击均发冲动轻点（含随后 Buffer/Reject/非法）。
            if (mainlineBusy)
            {
                mAcceleration.Tap(intent);
            }

            if (InputIntentKinds.IsBoardAction(intent.Kind))
            {
                return SubmitBoardAction(intent, mainlineBusy, out uiPickPreview);
            }

            if (InputIntentKinds.IsModeOrModal(intent.Kind))
            {
                // 棋盘选牌模式：主线忙时禁止切入。
                if (string.Equals(intent.Kind, InputIntentKinds.BoardSelectBegin, StringComparison.Ordinal))
                {
                    if (mainlineBusy)
                    {
                        Reject(intent, "modeOrModalWhileMainlineBusy", mainlineBusy: true);
                        return IntentDisposition.Reject;
                    }

                    return IntentDisposition.Allow;
                }

                // 奖励/房间模态：Bounce 可能挂在 UseItem Present 主线上等待点选
                // （局内宝箱）。主线忙时仍必须放行，否则 SelectReward 被拒 → 孤儿 Pending →
                // 二次弹窗；未 Ack 批次还会粘住 IsInputLocked，跨关 BootstrapRun 清掉遗物。
                if (mainlineBusy
                    && (owner != InputOwner.ChoiceOverlay
                        || !PresentationInputGates.ChoiceOverlayActive))
                {
                    Reject(intent, "modeOrModalWhileMainlineBusy", mainlineBusy: true);
                    return IntentDisposition.Reject;
                }

                return IntentDisposition.Allow;
            }

            Reject(intent, "unknownKind", mainlineBusy);
            return IntentDisposition.Reject;
        }

        private IntentDisposition SubmitBoardAction(
            InputIntent intent,
            bool mainlineBusy,
            out bool uiPickPreview)
        {
            uiPickPreview = false;

            if (ShouldRouteToBoardSelect(intent))
            {
                if (mainlineBusy)
                {
                    Reject(intent, "routeBoardSelectWhileMainlineBusy", mainlineBusy: true);
                    return IntentDisposition.Reject;
                }

                return IntentDisposition.RouteToBoardSelect;
            }

            if (string.Equals(intent.Kind, InputIntentKinds.Attack, StringComparison.Ordinal)
                && IsOrphanMidBattleRewardPending())
            {
                Reject(intent, "orphanMidBattleReward", mainlineBusy);
                return IntentDisposition.Reject;
            }

            string legalityReject;
            if (!TryExplainBoardLegality(intent, out legalityReject))
            {
                Reject(
                    intent,
                    string.IsNullOrEmpty(legalityReject) ? "illegal" : legalityReject,
                    mainlineBusy);
                return IntentDisposition.Reject;
            }

            // Pickup：idle 时 Allow，由调用方 ExternalHold→Apply；busy 时 Director latest-wins 缓冲。
            if (string.Equals(intent.Kind, InputIntentKinds.Pickup, StringComparison.Ordinal))
            {
                if (!mainlineBusy)
                {
                    return IntentDisposition.Allow;
                }

                var pickupRuntime = this.GetSystem<IPresentationRuntimeSystem>();
                if (pickupRuntime == null || !pickupRuntime.IsStarted)
                {
                    Reject(intent, "runtimeNotStarted(pickupBuffer)", mainlineBusy: true);
                    return IntentDisposition.Reject;
                }

                pickupRuntime.TrySubmitIntent(intent, out uiPickPreview);
                return IntentDisposition.BufferToDirector;
            }

            var runtime = this.GetSystem<IPresentationRuntimeSystem>();
            if (runtime == null || !runtime.IsStarted)
            {
                Reject(intent, "runtimeNotStarted", mainlineBusy);
                return IntentDisposition.Reject;
            }

            if (mainlineBusy)
            {
                runtime.TrySubmitIntent(intent, out uiPickPreview);
                return IntentDisposition.BufferToDirector;
            }

            runtime.TrySubmitIntent(intent, out uiPickPreview);
            return IntentDisposition.Allow;
        }

        private static bool IsOrphanMidBattleRewardPending()
        {
            var architecture = NineGridArchitecture.Interface;
            if (PresentationInputGates.ChoiceOverlayActive || architecture == null)
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

        private bool ShouldRouteToBoardSelect(InputIntent intent)
        {
            if (!string.Equals(intent.Kind, InputIntentKinds.UseItem, StringComparison.Ordinal))
            {
                return false;
            }

            if (intent.SelectedCardUids != null && intent.SelectedCardUids.Length > 0)
            {
                return false;
            }

            var arch = NineGridArchitecture.Interface;
            if (arch == null || intent.TargetId <= 0)
            {
                return false;
            }

            var registry = arch.GetModel<CardRegistry>();
            if (registry == null || !registry.TryGet(intent.TargetId, out var card))
            {
                return false;
            }

            HelpCardPlayKind playKind;
            HelpCardSelectedCardsSpec spec;
            if (!HelpCardBoardSelectResolver.TryGetPlayKind(card.DefId, out playKind, out spec))
            {
                return false;
            }

            return playKind == HelpCardPlayKind.MultiBoardSelect;
        }

        private bool TryExplainBoardLegality(InputIntent intent, out string rejectReason)
        {
            rejectReason = null;
            if (mLegalityOverride != null)
            {
                return mLegalityOverride(intent);
            }

            var arch = NineGridArchitecture.Interface;

            if (string.Equals(intent.Kind, InputIntentKinds.Explore, StringComparison.Ordinal))
            {
                return BoardIntentLegality.TryExplainExplore(arch, intent.TargetId, out rejectReason);
            }

            if (string.Equals(intent.Kind, InputIntentKinds.Attack, StringComparison.Ordinal))
            {
                return BoardIntentLegality.TryExplainAttack(arch, intent.TargetId, out rejectReason);
            }

            if (string.Equals(intent.Kind, InputIntentKinds.UseItem, StringComparison.Ordinal))
            {
                return BoardIntentLegality.TryExplainUseItem(
                    arch,
                    intent.TargetId,
                    intent.SelectedCardUids,
                    intent.SelectedOption,
                    out rejectReason);
            }

            if (string.Equals(intent.Kind, InputIntentKinds.Pickup, StringComparison.Ordinal))
            {
                return BoardIntentLegality.TryExplainPickup(arch, intent.TargetId, out rejectReason);
            }

            if (string.Equals(intent.Kind, InputIntentKinds.RevealFace, StringComparison.Ordinal))
            {
                return BoardIntentLegality.TryExplainRevealFace(arch, intent.TargetId, out rejectReason);
            }

            rejectReason = "unknownKind";
            return false;
        }

        private static void Reject(InputIntent intent, string reason, bool mainlineBusy)
        {
            DirectorTrace.IntentRejected(intent.Kind, intent.TargetId, reason);
            Debug.LogWarning(
                "[IntentIntake] Reject kind=" + intent.Kind
                + " target=" + intent.TargetId
                + " reason=" + reason
                + " mainlineBusy=" + mainlineBusy
                + " owner=" + PresentationInputGates.CurrentOwner);
        }

        public static IIntentIntake EnsureRegistered(
            IArchitecture architecture = null,
            IAccelerationSink acceleration = null,
            Func<InputIntent, bool> legalityOverride = null)
        {
            var arch = architecture ?? NineGridArchitecture.Interface;
            if (arch == null)
            {
                throw new InvalidOperationException("Architecture is not available for IntentIntake.");
            }

            PresentationInputStateSystem.EnsureRegistered(arch);

            var existing = arch.GetSystem<IIntentIntake>();
            if (existing != null)
            {
                var asSystem = existing as IntentIntakeSystem;
                if (asSystem != null)
                {
                    if (acceleration != null)
                    {
                        asSystem.SetAccelerationSink(acceleration);
                    }

                    if (legalityOverride != null)
                    {
                        asSystem.SetLegalityOverride(legalityOverride);
                    }
                }

                return existing;
            }

            var created = new IntentIntakeSystem();
            if (acceleration != null)
            {
                created.SetAccelerationSink(acceleration);
            }

            if (legalityOverride != null)
            {
                created.SetLegalityOverride(legalityOverride);
            }

            arch.RegisterSystem<IIntentIntake>(created);
            return created;
        }

        protected override void OnInit()
        {
        }
    }
}

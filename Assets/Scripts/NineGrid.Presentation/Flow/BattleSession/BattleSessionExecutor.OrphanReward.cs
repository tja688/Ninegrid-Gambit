using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Flow.Diagnostics;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Flow
{
    internal sealed partial class BattleSessionExecutor
    {
        private static bool HasOrphanMidBattleRewardPending()
        {
            if (PresentationInputGates.ChoiceOverlayActive)
            {
                return false;
            }

            var arch = NineGridArchitecture.Current;
            if (arch == null)
            {
                return false;
            }

            var pending = arch.GetModel<PendingChoiceModel>();
            if (pending.Kind.Value != PendingChoiceKind.Reward
                || pending.RewardOptions == null
                || pending.RewardOptions.Count == 0)
            {
                return false;
            }

            var phase = arch.GetSystem<IPhaseSystem>().CurrentPhase;
            return phase == GamePhase.InteractionLoop || phase == GamePhase.RewardItemChoice;
        }

        private void TryRecoverOrphanMidBattleRewardUi(string context)
        {
            if (_recoveringRewardUi)
            {
                return;
            }

            if (!HasOrphanMidBattleRewardPending())
            {
                return;
            }

            Debug.LogWarning(
                "[BattleSession] 孤儿 PendingReward（无覆盖层），重开 Bounce。context="
                + (context ?? string.Empty));
            _recoveringRewardUi = true;
            RecoverPendingRewardUiAsync().Forget();
        }

        private async UniTaskVoid RecoverPendingRewardUiAsync()
        {
            try
            {
                await PresentRewardChoiceFromCoreAsync(hoverOnNotice: false);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _recoveringRewardUi = false;
            }
        }
    }
}

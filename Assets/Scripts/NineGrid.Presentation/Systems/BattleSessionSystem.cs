using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using QFramework;

namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// 局内会话权威所有者：委托 <see cref="BattleSessionExecutor"/>。
    /// </summary>
    public sealed class BattleSessionSystem : AbstractSystem, IBattleSessionSystem
    {
        private readonly BattleSessionExecutor mExecutor = new();

        protected override void OnInit()
        {
        }

        public bool IsBound => mExecutor.IsBound;

        public bool IsBusy => mExecutor.IsBusy;

        public event Action OnNodeSettlementReady
        {
            add => mExecutor.OnNodeSettlementReady += value;
            remove => mExecutor.OnNodeSettlementReady -= value;
        }

        public void Bind(IBattleSessionView view)
        {
            mExecutor.Bind(view);
        }

        public void Unbind()
        {
            mExecutor.Unbind();
        }

        public void UnbindIfView(IBattleSessionView view)
        {
            mExecutor.UnbindIfView(view);
        }

        public void BindPresentChannels(
            QueuedBoardPresentChannel explore,
            CombatAttackPresentChannel attackHit,
            CombatCounterPresentChannel attackCounter,
            QueuedBoardPresentChannel attackBoard,
            UseItemPresentChannel useItem,
            QueuedBoardPresentChannel useItemBoard)
        {
            mExecutor.BindPresentChannels(
                explore,
                attackHit,
                attackCounter,
                attackBoard,
                useItem,
                useItemBoard);
        }

        public void ClearPresentChannels()
        {
            mExecutor.ClearPresentChannels();
        }

        public CancellationToken EnsurePresentationToken()
        {
            return mExecutor.EnsurePresentationToken();
        }

        public InitialGameSnapshot BootstrapRun(InitialGameOptions options = null)
        {
            return mExecutor.BootstrapRun(options);
        }

        public UniTask StartBattleNodeAsync(
            NodeDeckOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return mExecutor.StartBattleNodeAsync(options, cancellationToken);
        }

        public bool TryEnterNodeSettlement()
        {
            return mExecutor.TryEnterNodeSettlement();
        }

        public void NotifyPresentationBoardMayBeClear()
        {
            mExecutor.NotifyPresentationBoardMayBeClear();
        }

        public void ClearPresentationSurface()
        {
            mExecutor.ClearPresentationSurface();
        }

        public void ClearCardPresentationSurface()
        {
            mExecutor.ClearCardPresentationSurface();
        }

        public void RefreshPersistentInBattleUi(bool animate = false)
        {
            mExecutor.RefreshPersistentInBattleUi(animate);
        }

        public UniTask PresentRewardChoiceFromCoreAsync(bool hoverOnNotice = false)
        {
            return mExecutor.PresentRewardChoiceFromCoreAsync(hoverOnNotice);
        }

        public UniTask PresentUnusedHelpCardSettlementFromEventLogAsync(
            int startIndex,
            CancellationToken cancellationToken = default)
        {
            return mExecutor.PresentUnusedHelpCardSettlementFromEventLogAsync(startIndex, cancellationToken);
        }

        public void PresentShuffleIntoDeckFromEventLog(int startIndex)
        {
            mExecutor.PresentShuffleIntoDeckFromEventLog(startIndex);
        }

        public UniTask DrainPostKillBoardAsync(
            PostKillBoardPresentationResult result,
            CancellationToken cancellationToken = default)
        {
            return mExecutor.DrainPostKillBoardAsync(result, cancellationToken);
        }

        public UniTask FlushPendingShuffleIntoPresentationAsync(
            CancellationToken cancellationToken = default)
        {
            return mExecutor.FlushPendingShuffleIntoPresentationAsync(cancellationToken);
        }

        public CombatHitPresentationResult ApplyCombatHit(int attackerUid, int targetUid)
        {
            return mExecutor.ApplyCombatHit(attackerUid, targetUid);
        }

        public PostKillBoardPresentationResult ResolvePostKillBoard()
        {
            return mExecutor.ResolvePostKillBoard();
        }

        public void OnExploreBatchProjected(
            int startIndex,
            int boardSlot,
            PostKillBoardPresentationResult result)
        {
            mExecutor.OnExploreBatchProjected(startIndex, boardSlot, result);
        }

        public void OnAttackHitBatchProjected(
            int startIndex,
            int boardSlot,
            int resolvedCombatUid,
            PostKillBoardPresentationResult result)
        {
            mExecutor.OnAttackHitBatchProjected(startIndex, boardSlot, resolvedCombatUid, result);
        }

        public void OnAttackBoardBatchProjected(
            int startIndex,
            int boardSlot,
            PostKillBoardPresentationResult result)
        {
            mExecutor.OnAttackBoardBatchProjected(startIndex, boardSlot, result);
        }

        public void OnAttackCounterBatchProjected(
            int startIndex,
            int attackerBoardSlot,
            int attackerUid,
            PostKillBoardPresentationResult result)
        {
            mExecutor.OnAttackCounterBatchProjected(startIndex, attackerBoardSlot, attackerUid, result);
        }

        public void OnUseItemBatchProjected(
            int startIndex,
            int boardSlot,
            PostKillBoardPresentationResult result)
        {
            mExecutor.OnUseItemBatchProjected(startIndex, boardSlot, result);
        }

        public void OnUseItemBoardBatchProjected(
            int startIndex,
            int boardSlot,
            PostKillBoardPresentationResult result)
        {
            mExecutor.OnUseItemBoardBatchProjected(startIndex, boardSlot, result);
        }

        public void OnUseItemResolvedWithoutKill()
        {
            mExecutor.OnUseItemResolvedWithoutKill();
        }

        public UniTask PlayDirectorUseItemPresentAsync(
            PostKillBoardPresentationResult boardResult,
            CancellationToken token)
        {
            return mExecutor.PlayDirectorUseItemPresentAsync(boardResult, token);
        }

        public void RaiseBattleEnded(bool victory)
        {
            mExecutor.RaiseBattleEnded(victory);
        }

        public void RegisterPresentationIntentHandlers()
        {
            mExecutor.RegisterPresentationIntentHandlers();
        }

        public void UnregisterPresentationIntentHandlers()
        {
            mExecutor.UnregisterPresentationIntentHandlers();
        }

        public void RequestSyncBoardFromCore()
        {
            BattleSessionExecutor.AssertOccupancySyncForbidden("requestSyncBoardFromCore", "soft");
        }

        public void CancelPresentationWork()
        {
            mExecutor.CancelPresentationWork();
        }

        public UniTask<bool> ValidateHandDragApplyAsync(ManagedCard card, int? targetGroundSlot)
        {
            return mExecutor.ValidateHandDragApplyAsync(card, targetGroundSlot);
        }

        public void AbortBoardSelectIfActive(string reason)
        {
            mExecutor.AbortBoardSelectIfActive(reason);
        }

        public UniTask OnBoardSelectionCompletedAsync(int itemUid, int[] selectedUids)
        {
            return mExecutor.OnBoardSelectionCompletedAsync(itemUid, selectedUids);
        }

        public UniTask OnBoardSelectionAbortedAsync(int itemUid, string defId, string reason)
        {
            return mExecutor.OnBoardSelectionAbortedAsync(itemUid, defId, reason);
        }

        public static IBattleSessionSystem EnsureRegistered(IArchitecture architecture = null)
        {
            architecture ??= NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            if (architecture == null)
            {
                throw new InvalidOperationException("NineGridArchitecture 未就绪，无法注册 BattleSessionSystem。");
            }

            var existing = architecture.GetSystem<IBattleSessionSystem>();
            if (existing != null)
            {
                return existing;
            }

            var created = new BattleSessionSystem();
            architecture.RegisterSystem<IBattleSessionSystem>(created);
            return created;
        }
    }
}

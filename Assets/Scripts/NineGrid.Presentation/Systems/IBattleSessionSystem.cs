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
    /// 局内会话单一 QF 所有者：节点启动 / Opening / 结算门 / 批次投影 / 盘面 Present。
    /// 场景宿主仅作 View；Cheat 不进入本 interface。
    /// </summary>
    public interface IBattleSessionSystem : ISystem
    {
        bool IsBound { get; }

        bool IsBusy { get; }

        event Action OnNodeSettlementReady;

        void Bind(IBattleSessionView view);

        void Unbind();

        void UnbindIfView(IBattleSessionView view);

        void BindPresentChannels(
            QueuedBoardPresentChannel explore,
            CombatAttackPresentChannel attackHit,
            CombatCounterPresentChannel attackCounter,
            QueuedBoardPresentChannel attackBoard,
            UseItemPresentChannel useItem,
            QueuedBoardPresentChannel useItemBoard,
            QueuedBoardPresentChannel pickupBoard);

        void ClearPresentChannels();

        CancellationToken EnsurePresentationToken();

        InitialGameSnapshot BootstrapRun(
            InitialGameOptions options = null,
            bool preserveRunInventory = false);

        UniTask StartBattleNodeAsync(
            NodeDeckOptions options = null,
            CancellationToken cancellationToken = default);

        bool TryEnterNodeSettlement();

        void NotifyPresentationBoardMayBeClear();

        void ClearPresentationSurface();

        void ClearCardPresentationSurface();

        void RefreshPersistentInBattleUi(bool animate = false);

        UniTask PresentRewardChoiceFromCoreAsync(bool hoverOnNotice = false);

        UniTask PresentUnusedHelpCardSettlementFromEventLogAsync(
            int startIndex,
            CancellationToken cancellationToken = default);

        void PresentShuffleIntoDeckFromEventLog(int startIndex);

        UniTask DrainPostKillBoardAsync(
            PostKillBoardPresentationResult result,
            CancellationToken cancellationToken = default,
            int[] occupancyPendingVacateUids = null);

        UniTask FlushPendingShuffleIntoPresentationAsync(CancellationToken cancellationToken = default);

        void AssertHitPresentOccupancySync();

        CombatHitPresentationResult ApplyCombatHit(int attackerUid, int targetUid);

        PostKillBoardPresentationResult ResolvePostKillBoard();

        void OnExploreBatchProjected(
            int startIndex,
            int boardSlot,
            PostKillBoardPresentationResult result);

        void OnAttackHitBatchProjected(
            int startIndex,
            int boardSlot,
            int resolvedCombatUid,
            PostKillBoardPresentationResult result);

        void OnAttackBoardBatchProjected(
            int startIndex,
            int boardSlot,
            PostKillBoardPresentationResult result);

        void OnAttackCounterBatchProjected(
            int startIndex,
            int attackerBoardSlot,
            int attackerUid,
            PostKillBoardPresentationResult result);

        void OnUseItemBatchProjected(
            int startIndex,
            int boardSlot,
            PostKillBoardPresentationResult result);

        void OnUseItemBoardBatchProjected(
            int startIndex,
            int boardSlot,
            PostKillBoardPresentationResult result);

        void OnPickupBoardBatchProjected(
            int startIndex,
            int boardSlot,
            PostKillBoardPresentationResult result);

        void OnUseItemResolvedWithoutKill();

        UniTask PlayDirectorUseItemPresentAsync(
            PostKillBoardPresentationResult boardResult,
            CancellationToken token);

        void RaiseBattleEnded(bool victory);

        /// <summary>
        /// 投影 / Core 已 AvatarDefeated 时武装战败收口（幂等）。
        /// 导演链仍在演时只武装，等主线空闲后再 Raise，避免 HardClear 跳过致死表演。
        /// </summary>
        void EnsureBattleEndedIfAvatarDefeated(
            PostKillBoardPresentationResult result,
            CancellationToken cancellationToken = default);

        void RegisterPresentationIntentHandlers();

        void UnregisterPresentationIntentHandlers();

        void RequestSyncBoardFromCore();

        void CancelPresentationWork();

        /// <summary>硬关停表现 Runtime（离开局内生命周期 / Bootstrap / 清场）。</summary>
        void TeardownPresentationRuntime(IntentClearReason reason = IntentClearReason.LayerChange);

        UniTask<bool> ValidateHandDragApplyAsync(ManagedCard card, int? targetGroundSlot);

        void AbortBoardSelectIfActive(string reason);

        UniTask OnBoardSelectionCompletedAsync(int itemUid, int[] selectedUids);

        UniTask OnBoardSelectionAbortedAsync(int itemUid, string defId, string reason);
    }
}

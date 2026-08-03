using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Flow.BoardBriefTip;
using NineGrid.Flow.Diagnostics;
using NineGrid.Flow.Presentation;
using NineGrid.Flow.RewardBoard;
using NineGrid.Flow.RoomIcons;
using NineGrid.Flow.ShopBoard;
using NineGrid.Flow.TavernBoard;
using NineGrid.Presentation;
using NineGrid.Presentation.Commands;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 流程编排深接口：节点循环 Battle → Reward → RoomChoice → RoomEvent → Victory/Defeat。
    /// 由 <see cref="GameFlowShellSystem"/> 持有；相位写入经 <see cref="SetGameFlowShellStateCommand"/>。
    /// </summary>
    internal sealed class GameFlowOrchestrator
    {
        private readonly GameFlowShellSystem mShell;

        private CancellationTokenSource mLoopCts;
        private CancellationTokenSource mBattleEndCts;
        private UniTaskCompletionSource mSettlementTcs;

        public GameFlowOrchestrator(GameFlowShellSystem shell)
        {
            mShell = shell ?? throw new ArgumentNullException(nameof(shell));
        }

        public void Start(GameFlowRunOptions options)
        {
            // 必须先取消胜负 Notice 的延迟回菜单，否则 Delay 结束后 EnterMainMenu
            // 会 ClearPresentationSurface，把已重开的 Opening/StartNode 清成空场。
            CancelBattleEndWork();

            if (mShell.IsBusy && mShell.State.Value != GameFlowShellState.MainMenu)
            {
                Debug.LogWarning("[GameFlow] 当前循环仍在进行，忽略 BeginRun。");
                return;
            }

            var view = mShell.View;
            view?.EnsureViewBindings();
            CancelLoopWork();
            mLoopCts = new CancellationTokenSource();
            PresentationInputGates.Reset("BeginRun");
            NineGridArchitecture.Interface?.GetSystem<IFieldBattlePresentationSystem>()?.CancelBattleWork();
            ResolveSession()?.ClearCardPresentationSurface();

            var testMode = options == null || options.TestMode;
            var quickTestMode = options != null && options.QuickTestMode;
            var quickTestOptions = options?.QuickTest;

            mShell.ApplyRunMode(testMode, quickTestMode);
            mShell.ResetNodeProgress();
            // ResetNodeProgress 清 QuickTest 字段；PrepareQuickTest 须在其后。
            mShell.BumpGeneration();

            if (quickTestMode)
            {
                var qt = quickTestOptions ?? new QuickTestRunOptions();
                mShell.PrepareQuickTest(qt);
                DiagTraceShared.SetRunTag(
                    DiagTraceShared.QuickTestRunTag,
                    BuildQuickTestRunTagNote(
                        mShell.QuickTestNodeOrderMode,
                        mShell.PinnedFirstBattleDeckId,
                        mShell.QuickTestSkillIds));
            }
            else
            {
                mShell.ClearQuickTest();
                DiagTraceShared.ClearRunTag();
            }

            ApplyQuickTestTimeScale();
            view?.HideNotice();
            view?.ShowInRunShell(inBattle: true);
            try
            {
                BattleTraceRecorder.RotateSessionForNewRun();
                FlowTraceRecorder.Record(
                    FlowTraceCategory.UI,
                    FlowTraceNames.StartRun,
                    new Dictionary<string, string>
                    {
                        { "testMode", testMode ? "true" : "false" },
                        { "quickTestMode", quickTestMode ? "true" : "false" },
                        { "runTag", DiagTraceShared.RunTag },
                        { "runTagNote", DiagTraceShared.RunTagNote },
                        { "quickTestNodeOrder", quickTestMode
                            ? QuickTestRunPlanner.FormatNodeOrder(mShell.QuickTestContentNodeQueue)
                            : string.Empty },
                        { "quickTestNodeOrderMode", quickTestMode
                            ? mShell.QuickTestNodeOrderMode.ToString()
                            : string.Empty },
                        { "quickTestPinnedFirstDeck", quickTestMode
                            && !string.IsNullOrEmpty(mShell.PinnedFirstBattleDeckId)
                            ? mShell.PinnedFirstBattleDeckId
                            : string.Empty },
                    },
                    loopState: mShell.State.Value.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GameFlow] FlowTrace StartRun: " + ex.Message);
            }

            if (quickTestMode)
            {
                Debug.Log(
                    $"[GameFlow] 快速测试模式：全局速度 x1，玩家 HP {GameFlowShellSystem.QuickTestAvatarHp} / ATK {GameFlowShellSystem.QuickTestAvatarAttack} 每关重置，"
                    + $"节点顺序 {mShell.QuickTestNodeOrderMode}，队列 {QuickTestRunPlanner.FormatNodeOrder(mShell.QuickTestContentNodeQueue)}"
                    + (string.IsNullOrEmpty(mShell.PinnedFirstBattleDeckId)
                        ? string.Empty
                        : $"，首关牌组 {mShell.PinnedFirstBattleDeckId}")
                    + FormatSkillIdsNote(mShell.QuickTestSkillIds));
            }

            RunNodeCycleAsync(mLoopCts.Token).Forget();
        }

        public void Signal(GameFlowSignal signal)
        {
            switch (signal.Kind)
            {
                case GameFlowSignalKind.SettlementReady:
                    mSettlementTcs?.TrySetResult();
                    break;
                case GameFlowSignalKind.BattleEnded:
                    ShowBattleEndAndReturnAsync(signal.Victory).Forget();
                    break;
            }
        }

        public void Stop()
        {
            CancelLoopWork();
            CancelBattleEndWork();
            NineGridArchitecture.Interface?.GetSystem<IAvatarWalkSystem>()?.SetEnabled(false);
            NineGridArchitecture.Interface?.GetSystem<IAvatarWalkSystem>()?.Cancel();

            try
            {
                FlowTraceRecorder.Record(
                    FlowTraceCategory.UI,
                    FlowTraceNames.ReturnMainMenu,
                    loopState: mShell.State.Value.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GameFlow] FlowTrace ReturnMainMenu: " + ex.Message);
            }

            EnterMainMenuImmediate();
        }

        public void Dispose()
        {
            CancelLoopWork();
            CancelBattleEndWork();
        }

        private async UniTaskVoid RunNodeCycleAsync(CancellationToken ct)
        {
            mShell.SetBusy(true);
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    mShell.IncrementNodeIndex();
                    var arch = NineGridArchitecture.Current;
                    var coreNodeIndex = arch.GetModel<RunModel>().NodeIndex.Value;
                    var entersBattle = MapNodeProgression.EntersInteractionLoop(coreNodeIndex);

                    if (entersBattle)
                    {
                        var battleStarted = await PlayRealBattleAsync(ct);
                        if (ct.IsCancellationRequested)
                        {
                            return;
                        }

                        if (!battleStarted)
                        {
                            Debug.LogError("[GameFlow] 局内会话未就绪，终止节点循环。");
                            return;
                        }

                        await PlayRewardChoiceAsync(ct);
                        if (ct.IsCancellationRequested)
                        {
                            return;
                        }
                    }
                    else
                    {
                        var nonCombatOk = await PlayNonCombatNodeAsync(ct);
                        if (ct.IsCancellationRequested)
                        {
                            return;
                        }

                        if (!nonCombatOk)
                        {
                            Debug.LogError("[GameFlow] 非战斗节点未就绪，终止节点循环。");
                            return;
                        }
                    }

                    await PlayRoomIconChoiceAsync(ct);
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }

                    var phase = NineGridArchitecture.Current.GetSystem<IPhaseSystem>().CurrentPhase;
                    if (phase == GamePhase.Victory)
                    {
                        await ShowVictoryAndReturnAsync(ct);
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                mShell.SetBusy(false);
            }
        }

        private async UniTask<bool> PlayRealBattleAsync(CancellationToken ct)
        {
            RequestSetState(GameFlowShellState.BattleStub);
            var view = mShell.View;
            view?.EnsureViewBindings();
            view?.ShowInRunShell(inBattle: true);

            var session = ResolveSession();
            if (session == null || !session.IsBound)
            {
                Debug.LogError("[GameFlow] 未绑定 IBattleSessionSystem，无法入场。");
                return false;
            }

            mSettlementTcs = new UniTaskCompletionSource();

            var arch = NineGridArchitecture.Current;
            var phase = arch.GetSystem<IPhaseSystem>();
            EnsureBattleNodeBootstrap(session, phase);

            CoreCardPresentationMapper.EnsureContentCatalogLoaded();

            var catalog = arch.GetSystem<IContentSystem>()?.Catalog;
            string monsterDeckId = null;
            int contentNodeIndex;
            if (mShell.TryConsumePinnedFirstBattle(out var pinnedDeckId))
            {
                monsterDeckId = pinnedDeckId;
                contentNodeIndex = QuickTestDeckCatalog.GetDefaultNodeIndexForDeckId(catalog, pinnedDeckId);
            }
            else
            {
                contentNodeIndex = mShell.ResolveBattleContentNodeIndex();
            }

            var options = arch.GetSystem<IRewardSystem>().BuildNodeDeckOptions(contentNodeIndex, monsterDeckId);
            if (options == null)
            {
                options = NodeDeckOptions.CreateDefaultBattle();
            }

            ApplyQuickTestTrapCardsIfNeeded(options);

            Debug.Log(mShell.IsQuickTestMode
                ? $"[GameFlow] 循环节点 {mShell.NodeIndex} 快速测试内容节点 {contentNodeIndex}"
                  + (string.IsNullOrEmpty(monsterDeckId) ? string.Empty : $" 固定牌组 {monsterDeckId}")
                  + " 真实局内入场"
                : $"[GameFlow] 节点 {mShell.NodeIndex} 真实局内入场");
            await session.StartBattleNodeAsync(options, ct);
            if (ct.IsCancellationRequested)
            {
                return false;
            }

            ApplyQuickTestAvatarCheatsIfNeeded();
            ApplyQuickTestSkillMountsIfNeeded();
            ApplyQuickTestTimeScale();

            session.TryEnterNodeSettlement();

            Debug.Log($"[GameFlow] 节点 {mShell.NodeIndex} 已入场，等待节点结算");
            await mSettlementTcs.Task.AttachExternalCancellation(ct);
            Debug.Log($"[GameFlow] 节点 {mShell.NodeIndex} 结算就绪，进入奖励");
            return true;
        }

        private async UniTask PlayRewardChoiceAsync(CancellationToken ct)
        {
            RequestSetState(GameFlowShellState.RewardChoice);
            var view = mShell.View;
            view?.EnsureViewBindings();

            var arch = NineGridArchitecture.Current;
            var phase = arch.GetSystem<IPhaseSystem>().CurrentPhase;
            var pending = arch.GetModel<PendingChoiceModel>();
            if (phase != GamePhase.RewardItemChoice
                || pending.Kind.Value != PendingChoiceKind.Reward
                || pending.RewardOptions == null
                || pending.RewardOptions.Count == 0)
            {
                Debug.LogWarning(
                    $"[GameFlow] 跳过通关奖励 phase={phase} pending={pending.Kind.Value}");
                try
                {
                    FlowTraceRecorder.Record(
                        FlowTraceCategory.CoreGate,
                        FlowTraceNames.RewardPresented,
                        new Dictionary<string, string>
                        {
                            { "skipped", "true" },
                            { "phase", phase.ToString() },
                            { "pending", pending.Kind.Value.ToString() },
                            { "nodeIndex", mShell.NodeIndex.ToString() },
                        },
                        loopState: mShell.State.Value.ToString(),
                        phaseBefore: phase.ToString(),
                        accepted: false);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[GameFlow] FlowTrace RewardSkipped: " + ex.Message);
                }

                return;
            }

            try
            {
                var optionIds = new List<string>(pending.RewardOptions.Count);
                for (var i = 0; i < pending.RewardOptions.Count; i++)
                {
                    optionIds.Add(pending.RewardOptions[i].DefId ?? string.Empty);
                }

                FlowTraceRecorder.Record(
                    FlowTraceCategory.CoreGate,
                    FlowTraceNames.RewardPresented,
                    new Dictionary<string, string>
                    {
                        { "optionCount", pending.RewardOptions.Count.ToString() },
                        { "options", string.Join(",", optionIds) },
                        { "nodeIndex", mShell.NodeIndex.ToString() },
                        { "source", "nodeClear" },
                    },
                    loopState: mShell.State.Value.ToString(),
                    phaseBefore: phase.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GameFlow] FlowTrace RewardPresented: " + ex.Message);
            }

            view?.ShowRewardOverlay();
            BoardCardSelectModeController.RequestAbort("mainloop-reward-overlay");
            PresentationInputGates.SetChoiceOverlay(true);
            try
            {
                var session = ResolveSession();
                if (session != null)
                {
                    await session.PresentRewardChoiceFromCoreAsync(hoverOnNotice: true);
                }
            }
            finally
            {
                PresentationInputGates.SetChoiceOverlay(false);
                view?.HideAllOverlays();
            }

            await UniTask.Yield(cancellationToken: ct);
        }

        /// <summary>节点 4/7：StartNode → 非战斗 RoomChoice（离开图标），不进 InteractionLoop。</summary>
        private async UniTask<bool> PlayNonCombatNodeAsync(CancellationToken ct)
        {
            RequestSetState(GameFlowShellState.RoomChoice);
            var view = mShell.View;
            view?.EnsureViewBindings();
            view?.ShowInRunShell(inBattle: true);

            var session = ResolveSession();
            if (session == null || !session.IsBound)
            {
                Debug.LogError("[GameFlow] 非战斗节点：未绑定 IBattleSessionSystem。");
                return false;
            }

            var arch = NineGridArchitecture.Current;
            var phase = arch.GetSystem<IPhaseSystem>();
            EnsureBattleNodeBootstrap(session, phase);

            if (!phase.CanExecute(GameCommandKind.StartNode))
            {
                Debug.LogError("[GameFlow] 非战斗节点：StartNode 非法 phase=" + phase.CurrentPhase);
                return false;
            }

            var start = phase.StartNode(NodeDeckOptions.CreateDefaultBattle());
            if (start == null || !start.Accepted)
            {
                Debug.LogError("[GameFlow] 非战斗 StartNode 被拒: " + start?.Reason);
                return false;
            }

            ApplyQuickTestAvatarCheatsIfNeeded();
            ApplyQuickTestTimeScale();
            await UniTask.Yield(ct);
            return !ct.IsCancellationRequested;
        }

        /// <summary>
        /// 场地图标选房 / 导航：Spawn → BoardWalk → 驻留 1s → Select+Enter；进房硬切。
        /// 消费/特殊房进入 <see cref="GamePhase.RewardItemChoice"/> 后接场地板；导航/战斗房进房则直接推进节点。
        /// </summary>
        private async UniTask PlayRoomIconChoiceAsync(CancellationToken ct)
        {
            RequestSetState(GameFlowShellState.RoomChoice);
            var view = mShell.View;
            view?.EnsureViewBindings();

            var arch = NineGridArchitecture.Current;
            var phaseSystem = arch.GetSystem<IPhaseSystem>();
            var pending = arch.GetModel<PendingChoiceModel>();
            var kind = pending.Kind.Value;
            if (phaseSystem.CurrentPhase != GamePhase.RoomChoice
                || (kind != PendingChoiceKind.Room && kind != PendingChoiceKind.Navigation))
            {
                Debug.LogWarning(
                    $"[GameFlow] 跳过房间图标 phase={phaseSystem.CurrentPhase} pending={kind}");
                return;
            }

            if (kind == PendingChoiceKind.Room
                && (pending.RoomOptions == null || pending.RoomOptions.Count == 0))
            {
                Debug.LogWarning("[GameFlow] 跳过房间图标：RoomOptions 为空");
                return;
            }

            var walk = AvatarWalkSystem.EnsureRegistered(NineGridArchitecture.Interface);
            walk?.SetEnabled(true);
            BoardCardSelectModeController.RequestAbort("mainloop-room-icons");

            var presenter = RoomIconBoardPresenter.Current;
            presenter.Bind(arch);
            if (!presenter.TrySpawnFromPending(arch))
            {
                Debug.LogError("[GameFlow] 房间图标 Spawn 失败（旧浮层已退役，无回退路径）");
                walk?.SetEnabled(false);
                return;
            }

            try
            {
                FlowTraceRecorder.Record(
                    FlowTraceCategory.CoreGate,
                    FlowTraceNames.RoomPresented,
                    new Dictionary<string, string>
                    {
                        { "mode", "boardIcons" },
                        { "pending", kind.ToString() },
                        { "nodeIndex", mShell.NodeIndex.ToString() },
                    },
                    loopState: mShell.State.Value.ToString(),
                    phaseBefore: phaseSystem.CurrentPhase.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GameFlow] FlowTrace RoomPresented icons: " + ex.Message);
            }

            try
            {
                // 驻留提交已 Select+Enter：导航/战斗房 → NodeCompleted；商店/卡店/特殊房 → RewardItemChoice。
                await UniTask.WaitUntil(
                    () =>
                    {
                        if (ct.IsCancellationRequested)
                        {
                            return true;
                        }

                        var p = phaseSystem.CurrentPhase;
                        if (p == GamePhase.NodeCompleted
                            || p == GamePhase.Victory
                            || p == GamePhase.Defeat
                            || phaseSystem.CanExecute(GameCommandKind.StartNode))
                        {
                            return true;
                        }

                        return IsAwaitingInRoomBoard(phaseSystem, pending);
                    },
                    cancellationToken: ct);
            }
            finally
            {
                walk?.SetEnabled(false);
                walk?.Cancel();
                if (RoomIconOccupancy.Current.HasAny)
                {
                    presenter.DespawnAll();
                }
            }

            if (ct.IsCancellationRequested)
            {
                return;
            }

            // 图标路径已 EnterRoom；此处只刷房内场地板（旧 PlayRoomEventAsync 壳层再 Enter 已退役）。
            if (IsAwaitingInRoomBoard(phaseSystem, pending))
            {
                await PresentInRoomSessionAfterEnterAsync(ct);
            }
        }

        /// <summary>
        /// 图标驻留已完成 EnterRoom 后：按 Pending 刷商店 / 卡店 / 特殊奖励场地板，直到离开。
        /// </summary>
        private async UniTask PresentInRoomSessionAfterEnterAsync(CancellationToken ct)
        {
            RequestSetState(GameFlowShellState.RoomEvent);
            var view = mShell.View;
            view?.EnsureViewBindings();

            var arch = NineGridArchitecture.Current;
            var phaseSystem = arch.GetSystem<IPhaseSystem>();
            var pending = arch.GetModel<PendingChoiceModel>();
            var selectedRoom = pending.SelectedRoom.Value;
            view?.ShowRoomEventOverlay();
            ResolveSession()?.RefreshPersistentInBattleUi(animate: false);

            if (pending.Kind.Value == PendingChoiceKind.Reward
                && PendingChoiceModel.IsConsumerBoardPool(pending.PoolId.Value))
            {
                try
                {
                    var optionIds = new List<string>(
                        pending.RewardOptions != null ? pending.RewardOptions.Count : 0);
                    if (pending.RewardOptions != null)
                    {
                        for (var i = 0; i < pending.RewardOptions.Count; i++)
                        {
                            optionIds.Add(pending.RewardOptions[i].DefId ?? string.Empty);
                        }
                    }

                    FlowTraceRecorder.Record(
                        FlowTraceCategory.CoreGate,
                        FlowTraceNames.RewardPresented,
                        new Dictionary<string, string>
                        {
                            { "optionCount", optionIds.Count.ToString() },
                            { "options", string.Join(",", optionIds) },
                            { "nodeIndex", mShell.NodeIndex.ToString() },
                            { "source", "inRoomBoard" },
                            { "room", selectedRoom.ToString() },
                            { "pool", pending.PoolId.Value ?? string.Empty },
                        },
                        loopState: mShell.State.Value.ToString(),
                        phaseBefore: phaseSystem.CurrentPhase.ToString());
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[GameFlow] FlowTrace RoomRewardPresented: " + ex.Message);
                }

                if (PendingChoiceModel.IsShopPool(pending.PoolId.Value))
                {
                    await PresentShopBoardAsync(ct);
                }
                else if (PendingChoiceModel.IsTavernPool(pending.PoolId.Value)
                         || PendingChoiceModel.IsTavernFixItemPool(pending.PoolId.Value))
                {
                    await PresentTavernBoardAsync(ct);
                }
                else if (PendingChoiceModel.IsSpecialRewardPool(pending.PoolId.Value))
                {
                    await PresentRewardBoardAsync(ct);
                }
            }
            else
            {
                var message = BuildRoomResolvedNotice(selectedRoom);
                if (!string.IsNullOrEmpty(message) && view != null)
                {
                    view.ShowNotice(message);
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(Mathf.Max(0.05f, view.RoomEventStubSeconds)),
                        cancellationToken: ct);
                    view.HideNotice();
                }
            }

            view?.HideAllOverlays();
            ResolveSession()?.RefreshPersistentInBattleUi(animate: false);
        }

        /// <summary>图标 EnterRoom 后进入消费/特殊房场地板会话。</summary>
        private static bool IsAwaitingInRoomBoard(IPhaseSystem phase, PendingChoiceModel pending)
        {
            return phase != null
                   && pending != null
                   && phase.CurrentPhase == GamePhase.RewardItemChoice
                   && pending.Kind.Value == PendingChoiceKind.Reward
                   && PendingChoiceModel.IsConsumerBoardPool(pending.PoolId.Value);
        }

        private async UniTask PresentShopBoardAsync(CancellationToken ct)
        {
            var arch = NineGridArchitecture.Current;
            var phaseSystem = arch.GetSystem<IPhaseSystem>();
            var walk = AvatarWalkSystem.EnsureRegistered(NineGridArchitecture.Interface);
            walk?.SetEnabled(true);
            BoardCardSelectModeController.RequestAbort("mainloop-shop-board");

            var shop = ShopBoardPresenter.Current;
            shop.Bind(arch);
            RoomIconBoardPresenter.Current.HardCutAfterEnter(arch);
            if (!shop.TrySpawnFromPending(arch))
            {
                Debug.LogError("[GameFlow] 商店货架 Spawn 失败");
                walk?.SetEnabled(false);
                return;
            }

            PresentationInputGates.SetChoiceOverlay(true);
            try
            {
                await UniTask.WaitUntil(
                    () =>
                    {
                        if (ct.IsCancellationRequested)
                        {
                            return true;
                        }

                        var p = phaseSystem.CurrentPhase;
                        return p == GamePhase.NodeCompleted
                               || p == GamePhase.Victory
                               || p == GamePhase.Defeat
                               || phaseSystem.CanExecute(GameCommandKind.StartNode);
                    },
                    cancellationToken: ct);
            }
            finally
            {
                PresentationInputGates.SetChoiceOverlay(false);
                walk?.SetEnabled(false);
                walk?.Cancel();
                if (shop.IsActive)
                {
                    shop.DespawnAll();
                }
            }
        }

        private async UniTask PresentTavernBoardAsync(CancellationToken ct)
        {
            var arch = NineGridArchitecture.Current;
            var phaseSystem = arch.GetSystem<IPhaseSystem>();
            var walk = AvatarWalkSystem.EnsureRegistered(NineGridArchitecture.Interface);
            walk?.SetEnabled(true);
            BoardCardSelectModeController.RequestAbort("mainloop-tavern-board");

            var tavern = TavernBoardPresenter.Current;
            tavern.Bind(arch);
            RoomIconBoardPresenter.Current.HardCutAfterEnter(arch);
            if (!tavern.TrySpawnFromPending(arch))
            {
                Debug.LogError("[GameFlow] 卡店服务 Spawn 失败");
                walk?.SetEnabled(false);
                return;
            }

            PresentationInputGates.SetChoiceOverlay(true);
            try
            {
                await UniTask.WaitUntil(
                    () =>
                    {
                        if (ct.IsCancellationRequested)
                        {
                            return true;
                        }

                        var p = phaseSystem.CurrentPhase;
                        return p == GamePhase.NodeCompleted
                               || p == GamePhase.Victory
                               || p == GamePhase.Defeat
                               || phaseSystem.CanExecute(GameCommandKind.StartNode);
                    },
                    cancellationToken: ct);
            }
            finally
            {
                PresentationInputGates.SetChoiceOverlay(false);
                walk?.SetEnabled(false);
                walk?.Cancel();
                if (tavern.IsActive)
                {
                    tavern.DespawnAll();
                }
            }
        }

        private async UniTask PresentRewardBoardAsync(CancellationToken ct)
        {
            var arch = NineGridArchitecture.Current;
            var phaseSystem = arch.GetSystem<IPhaseSystem>();
            var walk = AvatarWalkSystem.EnsureRegistered(NineGridArchitecture.Interface);
            walk?.SetEnabled(true);
            BoardCardSelectModeController.RequestAbort("mainloop-reward-board");

            var reward = RewardBoardPresenter.Current;
            reward.Bind(arch);
            RoomIconBoardPresenter.Current.HardCutAfterEnter(arch);
            if (!reward.TrySpawnFromPending(arch))
            {
                Debug.LogError("[GameFlow] 特殊奖励房 Spawn 失败");
                walk?.SetEnabled(false);
                return;
            }

            PresentationInputGates.SetChoiceOverlay(true);
            try
            {
                await UniTask.WaitUntil(
                    () =>
                    {
                        if (ct.IsCancellationRequested)
                        {
                            return true;
                        }

                        var p = phaseSystem.CurrentPhase;
                        return p == GamePhase.NodeCompleted
                               || p == GamePhase.Victory
                               || p == GamePhase.Defeat
                               || phaseSystem.CanExecute(GameCommandKind.StartNode);
                    },
                    cancellationToken: ct);
            }
            finally
            {
                PresentationInputGates.SetChoiceOverlay(false);
                walk?.SetEnabled(false);
                walk?.Cancel();
                if (reward.IsActive)
                {
                    reward.DespawnAll();
                }
            }
        }

        private static string BuildRoomResolvedNotice(RoomKind room)
        {
            CoreCardPresentationMapper.EnsureContentCatalogLoaded();
            var arch = NineGridArchitecture.Current;
            if (arch != null)
            {
                var content = arch.GetSystem<IContentSystem>();
                if (content != null
                    && content.HasCatalog
                    && content.Catalog.Rewards.TryGetRoom(room, out var def))
                {
                    var tip = BoardBriefTipCopy.ForRoom(def);
                    if (!string.IsNullOrEmpty(tip))
                    {
                        return tip;
                    }
                }
            }

            return room == RoomKind.None ? string.Empty : room.ToString();
        }

        private async UniTask ShowVictoryAndReturnAsync(CancellationToken ct)
        {
            // 不可把节点循环 ct 传给胜负回菜单：ShowBattleEnd 开头 CancelLoopWork 会立刻取消该 ct，
            // Delay 抛取消后 EnterMainMenuImmediate 走不到，DisableDomainReload 下会残留非 MainMenu。
            if (ct.IsCancellationRequested)
            {
                return;
            }

            CancelBattleEndWork();
            mBattleEndCts = new CancellationTokenSource();
            await ShowBattleEndAndReturnAsync(victory: true, mBattleEndCts.Token);
        }

        private async UniTaskVoid ShowBattleEndAndReturnAsync(bool victory)
        {
            CancelBattleEndWork();
            mBattleEndCts = new CancellationTokenSource();
            try
            {
                await ShowBattleEndAndReturnAsync(victory, mBattleEndCts.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async UniTask ShowBattleEndAndReturnAsync(bool victory, CancellationToken ct)
        {
            CancelLoopWork();
            ResolveSession()?.ClearCardPresentationSurface();
            ResolveSession()?.RefreshPersistentInBattleUi(animate: false);
            NineGridArchitecture.Interface?.GetSystem<IFieldBattlePresentationSystem>()?.CancelBattleWork();

            RequestSetState(victory ? GameFlowShellState.VictoryNotice : GameFlowShellState.DefeatNotice);
            var view = mShell.View;
            view?.EnsureViewBindings();
            view?.ShowInRunShell();
            var message = victory
                ? (view == null || string.IsNullOrWhiteSpace(view.VictoryMessage) ? "胜利" : view.VictoryMessage)
                : (view == null || string.IsNullOrWhiteSpace(view.DefeatMessage) ? "失败" : view.DefeatMessage);
            view?.ShowNotice(message);
            try
            {
                FlowTraceRecorder.Record(
                    FlowTraceCategory.Loop,
                    victory ? FlowTraceNames.Victory : FlowTraceNames.Defeat,
                    new Dictionary<string, string>
                    {
                        { "message", message },
                        { "nodeIndex", mShell.NodeIndex.ToString() },
                    },
                    loopState: mShell.State.Value.ToString());
                BattleTraceRecorder.ExportBothNow(silentIfEmpty: true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GameFlow] FlowTrace Victory/Defeat: " + ex.Message);
            }

            Debug.Log(victory
                ? "[GameFlow] 整局胜利，准备回主菜单。"
                : "[GameFlow] 战斗失败，准备回主菜单。");
            var seconds = victory
                ? Mathf.Max(0.2f, view?.VictoryNoticeSeconds ?? 1f)
                : Mathf.Max(0.2f, view?.DefeatNoticeSeconds ?? 1f);
            await UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: ct);
            EnterMainMenuImmediate();
        }

        private void EnterMainMenuImmediate()
        {
            var view = mShell.View;
            view?.EnsureViewBindings();
            view?.HideNotice();
            PresentationInputGates.Reset("EnterMainMenu");
            NineGridArchitecture.Interface?.GetSystem<IFieldBattlePresentationSystem>()?.CancelBattleWork();
            ResolveSession()?.ClearPresentationSurface();
            view?.ShowMainMenuPanels();
            RequestSetState(GameFlowShellState.MainMenu);
            try
            {
                FlowTraceRecorder.BeginSessionIfNeeded();
                FlowTraceRecorder.Record(
                    FlowTraceCategory.Loop,
                    FlowTraceNames.EnterMainMenu,
                    loopState: GameFlowShellState.MainMenu.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GameFlow] FlowTrace EnterMainMenu: " + ex.Message);
            }

            mShell.ClearRunSession();
            ResetQuickTestTimeScale();
            mSettlementTcs = null;
        }

        private void ApplyQuickTestTimeScale()
        {
            if (!mShell.IsQuickTestMode)
            {
                return;
            }

            Time.timeScale = GameFlowShellSystem.QuickTestTimeScale;
        }

        private static void ResetQuickTestTimeScale()
        {
            Time.timeScale = 1f;
        }

        private void ApplyQuickTestTrapCardsIfNeeded(NodeDeckOptions options)
        {
            if (!mShell.IsQuickTestMode || options == null)
            {
                return;
            }

            var trapIds = mShell.QuickTestTrapContentIds;
            if (trapIds == null || trapIds.Count == 0)
            {
                return;
            }

            var content = NineGridArchitecture.Current.GetSystem<IContentSystem>();
            if (content == null || !content.HasCatalog)
            {
                Debug.LogWarning("[GameFlow] 快速测试注入机关失败：ContentSystem 未就绪");
                return;
            }

            var injected = 0;
            for (var i = 0; i < trapIds.Count; i++)
            {
                var defId = trapIds[i];
                if (string.IsNullOrEmpty(defId))
                {
                    continue;
                }

                var draft = content.CreateDraft(defId);
                if (draft == null || draft.Kind != CardKind.Trap)
                {
                    Debug.LogWarning($"[GameFlow] 快速测试机关注入跳过：defId={defId} kind={draft?.Kind}");
                    continue;
                }

                options.AddEnemyCard(draft);
                injected++;
            }

            if (injected > 0)
            {
                Debug.Log($"[GameFlow] 快速测试机关注入 count={injected} ids={string.Join(",", trapIds)}");
            }
        }

        private void ApplyQuickTestAvatarCheatsIfNeeded()
        {
            if (!mShell.IsQuickTestMode)
            {
                return;
            }

            if (!BattleSessionCheat.TrySetAvatarHp(GameFlowShellSystem.QuickTestAvatarHp))
            {
                Debug.LogWarning(
                    $"[GameFlow] 快速测试改血失败：目标 {GameFlowShellSystem.QuickTestAvatarHp}，请确认 Avatar 已入场。");
            }

            if (!BattleSessionCheat.TrySetAvatarAttack(GameFlowShellSystem.QuickTestAvatarAttack))
            {
                Debug.LogWarning(
                    $"[GameFlow] 快速测试改攻失败：目标 {GameFlowShellSystem.QuickTestAvatarAttack}，请确认 Avatar 已入场。");
            }
        }

        private void ApplyQuickTestSkillMountsIfNeeded()
        {
            if (!mShell.IsQuickTestMode)
            {
                return;
            }

            var skillIds = mShell.QuickTestSkillIds;
            if (skillIds == null || skillIds.Count == 0)
            {
                return;
            }

            var hosts = BattleSessionCheat.TryAttachSkillsToBoardMonsters(skillIds);
            if (hosts <= 0)
            {
                Debug.LogWarning(
                    "[GameFlow] 快速测试挂技能：场上无怪物宿主，skillIds=" + skillIds.Count);
            }

            BattleSessionCheat.EnsureQuickTestFlamePropsIfNeeded(skillIds);
        }

        private static string BuildQuickTestRunTagNote(
            QuickTestNodeOrderMode orderMode,
            string pinnedFirstBattleDeckId,
            IReadOnlyList<string> skillIds)
        {
            var note = "快速测试：全局速度x1，玩家HP99/ATK5每关重置，节点顺序"
                + (orderMode == QuickTestNodeOrderMode.Sequential ? "正式" : "乱序");
            if (!string.IsNullOrEmpty(pinnedFirstBattleDeckId))
            {
                note += "，首关固定牌组=" + pinnedFirstBattleDeckId;
            }

            if (skillIds != null && skillIds.Count > 0)
            {
                note += "，技能=" + string.Join(",", skillIds);
            }

            return note;
        }

        private static string FormatSkillIdsNote(IReadOnlyList<string> skillIds)
        {
            if (skillIds == null || skillIds.Count == 0)
            {
                return string.Empty;
            }

            return "，技能 " + string.Join(",", skillIds);
        }

        private void RequestSetState(GameFlowShellState next)
        {
            var from = mShell.State.Value;
            if (from == next)
            {
                return;
            }

            var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            arch?.SendCommand(new SetGameFlowShellStateCommand(next));

            try
            {
                FlowTraceRecorder.Record(
                    FlowTraceCategory.Loop,
                    FlowTraceNames.SetState,
                    new Dictionary<string, string>
                    {
                        { "from", from.ToString() },
                        { "to", next.ToString() },
                        { "nodeIndex", mShell.NodeIndex.ToString() },
                    },
                    loopState: next.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GameFlow] FlowTrace SetState: " + ex.Message);
            }
        }

        private void EnsureBattleNodeBootstrap(IBattleSessionSystem session, IPhaseSystem phase)
        {
            if (mShell.NodeIndex <= 1)
            {
                session.BootstrapRun();
                return;
            }

            if (phase.CanExecute(GameCommandKind.StartNode))
            {
                return;
            }

            var arch = NineGridArchitecture.Current;
            var sync = arch.GetSystem<IPresentationSyncSystem>();
            var player = arch.GetModel<PlayerModel>();
            var phaseBefore = phase.CurrentPhase.ToString();
            var relicsBefore = FormatRelicIds(player);
            var locked = sync != null && sync.IsInputLocked;

            // 粘连 Present 锁会使 StartNode 非法；先清锁再决定是否必须 BootstrapRun。
            sync?.Clear();
            PresentationInputGates.ForceEndExternalHold("EnsureBattleNodeBootstrap.unlock");

            if (phase.CanExecute(GameCommandKind.StartNode))
            {
                RecordBootstrapDecision(
                    "avoided_clearedPresentationLock",
                    phaseBefore,
                    locked,
                    relicsBefore,
                    bootstrapped: false);
                return;
            }

            if (TryRecoverStuckRoomTransition(phase)
                && phase.CanExecute(GameCommandKind.StartNode))
            {
                RecordBootstrapDecision(
                    "avoided_recoveredRoomTransition",
                    phaseBefore,
                    locked,
                    relicsBefore,
                    bootstrapped: false);
                return;
            }

            // 末路：BootstrapRun 会 player.Reset；跨关必须带回遗物/金币/帮助卡。
            RecordBootstrapDecision(
                "forced_preserveRunInventory",
                phaseBefore,
                locked,
                relicsBefore,
                bootstrapped: true);
            session.BootstrapRun(preserveRunInventory: true);
        }

        private static bool TryRecoverStuckRoomTransition(IPhaseSystem phase)
        {
            if (phase.CurrentPhase == GamePhase.RoomChoice
                && phase.CanExecute(GameCommandKind.SelectRoom))
            {
                PresentationInputGates.SetChoiceOverlay(true);
                try
                {
                    var select = SubmitSelectRoom(phase, 0);
                    if (!select.Accepted)
                    {
                        return false;
                    }
                }
                finally
                {
                    PresentationInputGates.SetChoiceOverlay(false);
                }
            }

            if (phase.CurrentPhase != GamePhase.RoomEvent
                || !phase.CanExecute(GameCommandKind.EnterRoom))
            {
                return phase.CanExecute(GameCommandKind.StartNode);
            }

            PresentationInputGates.SetChoiceOverlay(true);
            try
            {
                return SubmitEnterRoom(phase).Accepted;
            }
            finally
            {
                PresentationInputGates.SetChoiceOverlay(false);
            }
        }

        private void RecordBootstrapDecision(
            string decision,
            string phaseBefore,
            bool wasInputLocked,
            string relicsBefore,
            bool bootstrapped)
        {
            try
            {
                var player = NineGridArchitecture.Current.GetModel<PlayerModel>();
                FlowTraceRecorder.Record(
                    FlowTraceCategory.CoreGate,
                    FlowTraceNames.BootstrapRun,
                    new Dictionary<string, string>
                    {
                        { "decision", decision },
                        { "nodeIndex", mShell.NodeIndex.ToString() },
                        { "wasInputLocked", wasInputLocked ? "true" : "false" },
                        { "relicsBefore", relicsBefore ?? string.Empty },
                        { "relicsAfter", FormatRelicIds(player) },
                        { "bootstrapped", bootstrapped ? "true" : "false" },
                    },
                    loopState: mShell.State.Value.ToString(),
                    phaseBefore: phaseBefore,
                    phaseAfter: NineGridArchitecture.Current.GetSystem<IPhaseSystem>().CurrentPhase.ToString(),
                    accepted: !bootstrapped || !string.IsNullOrEmpty(FormatRelicIds(player)));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GameFlow] FlowTrace BootstrapRun: " + ex.Message);
            }
        }

        private static string FormatRelicIds(PlayerModel player)
        {
            if (player == null || player.RelicDefIds == null || player.RelicDefIds.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(",", player.RelicDefIds);
        }

        private static CoreCommandResult SubmitSelectRoom(IPhaseSystem phaseSystem, int optionIndex)
        {
            EnsureRoomChoiceHooksWired();
            if (RoomChoiceCoreHook.SelectRoom != null)
            {
                return RoomChoiceCoreHook.SelectRoom(optionIndex);
            }

            return phaseSystem.SelectRoom(optionIndex);
        }

        private static CoreCommandResult SubmitEnterRoom(IPhaseSystem phaseSystem)
        {
            EnsureRoomChoiceHooksWired();
            if (RoomChoiceCoreHook.EnterRoom != null)
            {
                return RoomChoiceCoreHook.EnterRoom();
            }

            return phaseSystem.EnterRoom();
        }

        private static void EnsureRoomChoiceHooksWired()
        {
            if (RoomChoiceCoreHook.SelectRoom == null || RoomChoiceCoreHook.EnterRoom == null)
            {
                RoomChoiceCoreHook.RequestWire();
            }
        }

        private void CancelBattleEndWork()
        {
            if (mBattleEndCts == null)
            {
                return;
            }

            mBattleEndCts.Cancel();
            mBattleEndCts.Dispose();
            mBattleEndCts = null;
        }

        private void CancelLoopWork()
        {
            mSettlementTcs?.TrySetCanceled();
            mSettlementTcs = null;

            if (mLoopCts == null)
            {
                return;
            }

            mLoopCts.Cancel();
            mLoopCts.Dispose();
            mLoopCts = null;
        }

        private static IBattleSessionSystem ResolveSession()
        {
            var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            return arch?.GetSystem<IBattleSessionSystem>();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Flow.BoardBriefTip;
using NineGrid.Flow.AttributeBoard;
using NineGrid.Flow.Diagnostics;
using NineGrid.Flow.Presentation;
using NineGrid.Flow.RewardBoard;
using NineGrid.Flow.RoomIcons;
using NineGrid.Flow.ShopBoard;
using NineGrid.Flow.TavernBoard;
using NineGrid.Flow.BattleInfoPreview;
using NineGrid.Flow.Transitions;
using NineGrid.Flow.Tutorial;
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
        /// <summary>读档恢复：Start 已完成恢复版 Bootstrap，首个战斗节点不得再 BootstrapRun 覆盖。</summary>
        private bool mRestoredBootstrapPending;

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
            // 结算统计（时长/击杀/损血/道具使用）随新 run 清零重开（只读旁路）。
            RunRecapTracker.HandleRunStarted();
            NineGridArchitecture.Interface?.GetSystem<IFieldBattlePresentationSystem>()?.CancelBattleWork();
            ResolveSession()?.ClearCardPresentationSurface();

            var quickTestMode = options != null && options.QuickTestMode;
            var quickTestOptions = options?.QuickTest;
            var tutorialMode = options != null && options.TutorialMode;

            mShell.ApplyRunMode(quickTestMode);
            mShell.ApplyTutorialMode(tutorialMode);
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

            // 读档恢复：先做恢复版 Bootstrap（Create + 快照覆盖 + RNG 还原），
            // 再启动节点循环，首迭代 Increment 后正好落在快照捕获的全局节点上。
            mRestoredBootstrapPending = false;
            if (options != null && options.RestoreMode)
            {
                if (!TryBootstrapRestoredRun(options.RestoreSnapshot))
                {
                    Debug.LogError("[GameFlow] 读档恢复失败，回主菜单。");
                    EnterMainMenuImmediate();
                    return;
                }
            }

            // 教学模式：单场受控教学战斗，不进节点循环（不影响正式流程）。
            if (tutorialMode)
            {
                RunTutorialAsync(options.Tutorial, mLoopCts.Token).Forget();
                return;
            }

            RunNodeCycleAsync(mLoopCts.Token).Forget();
        }

        /// <summary>
        /// 教学关卡（独立于节点循环）：BootstrapRun → 受控开局发牌 → 教学导演推进四波 →
        /// 击破离开机关走 Core 正常清关 → 结算就绪即教学完成。
        /// 完成后按载荷转正式开局或回主菜单；战败走常规 BattleEnded 收口（不标记完成）。
        /// </summary>
        private async UniTaskVoid RunTutorialAsync(TutorialRunOptions tutorial, CancellationToken ct)
        {
            mShell.SetBusy(true);
            TutorialBattleDirector director = null;
            var completed = false;
            try
            {
                mShell.IncrementNodeIndex();
                RequestSetState(GameFlowShellState.BattleStub);
                var view = mShell.View;
                view?.EnsureViewBindings();
                view?.HideNotice();
                view?.ShowInRunShell(inBattle: true);
                BoardBriefTipPresenter.InstanceOrNull()?.HardClear();

                var session = ResolveSession();
                if (session == null || !session.IsBound)
                {
                    Debug.LogError("[Tutorial] 未绑定 IBattleSessionSystem，教学开局失败。");
                    return;
                }

                mSettlementTcs = new UniTaskCompletionSource();
                session.BootstrapRun();
                CoreCardPresentationMapper.EnsureContentCatalogLoaded();

                var content = NineGridArchitecture.Current.GetSystem<IContentSystem>();
                if (content == null || !content.HasCatalog)
                {
                    Debug.LogError("[Tutorial] ContentSystem 未就绪，教学开局失败。");
                    return;
                }

                Debug.Log("[Tutorial] 教学关卡入场");
                await session.StartBattleNodeAsync(TutorialDeckPlan.BuildOpeningOptions(content), ct);
                if (ct.IsCancellationRequested)
                {
                    return;
                }

                director = TutorialBattleDirector.StartNew(ct);
                session.TryEnterNodeSettlement();
                await mSettlementTcs.Task.AttachExternalCancellation(ct);
                completed = true;
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                director?.Stop();
                mShell.SetBusy(false);
            }

            if (!completed)
            {
                return;
            }

            TutorialProgressStore.MarkCompleted();
            var continueToFormal = tutorial != null && tutorial.ContinueToFormalRun;
            Debug.Log("[Tutorial] 教学完成" + (continueToFormal ? "，转入正式开局。" : "，返回主菜单。"));
            FinishTutorialAndContinueAsync(continueToFormal).Forget();
        }

        /// <summary>
        /// 教学收口：短暂完成提示 →（转正式时）跨层洞形转场盖住 →
        /// 遮罩下回主菜单收干净并开正式局 → 揭开；主菜单帧藏在遮罩下不闪帧。
        /// 仅回主菜单路径保持原样。
        /// </summary>
        private async UniTaskVoid FinishTutorialAndContinueAsync(bool continueToFormal)
        {
            var view = mShell.View;
            view?.ShowNotice(continueToFormal
                ? NineGrid.Core.Localization.L10n.Tr("notice.tutorial_complete_continue", "教学完成！准备开始冒险…")
                : NineGrid.Core.Localization.L10n.Tr("notice.tutorial_complete", "教学完成！"));
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(1.2f), cancellationToken: CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
            }

            if (!continueToFormal)
            {
                EnterMainMenuImmediate();
                return;
            }

            // 与楼层切换同一转场（洞形 iris）：收场清理与正式开局的硬切全部在遮罩下完成。
            var transition = RunSceneTransitionService.InstanceOrNull;
            var covered = false;
            if (transition != null && transition.IsEnabled)
            {
                try
                {
                    await transition.BeginCoverAsync(crossFloor: true, CancellationToken.None);
                    covered = transition.IsCoverHeld;
                }
                catch (OperationCanceledException)
                {
                    transition.ForceClearFaders();
                }
            }

            try
            {
                EnterMainMenuImmediate();
                await UniTask.Yield();
                mShell.BeginRun(GameFlowRunOptions.CreateFormal());
                await UniTask.Yield();
            }
            finally
            {
                if (covered)
                {
                    await RevealHeldTransitionIfAnyAsync();
                }
            }
        }

        /// <summary>
        /// 读档恢复 Bootstrap：以快照种子重建初始局 → Core 覆盖恢复（含 RNG 状态）→
        /// 壳层全局节点序号对齐到目标节点前一格 → 刷新跑图持久 HUD（遗物栏 / 玩家信息）。
        /// </summary>
        private bool TryBootstrapRestoredRun(RunSaveSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return false;
            }

            var session = ResolveSession();
            if (session == null || !session.IsBound)
            {
                Debug.LogError("[GameFlow] 读档恢复：未绑定 IBattleSessionSystem。");
                return false;
            }

            try
            {
                session.BootstrapRun(new InitialGameOptions
                {
                    Seed = snapshot.SeedValue,
                    DifficultyId = snapshot.difficultyId,
                });
                RunSaveGame.RestoreAfterCreate(NineGridArchitecture.Current, snapshot);
                mShell.SetNodeProgressBeforeRestoredNode(snapshot.shellGlobalNodeIndex);
                mRestoredBootstrapPending = true;
                session.RefreshPersistentInBattleUi(animate: false);
                PlayerInfoHudPresenter.TryGetInstance()?.SyncFromCore(animate: false);
                Debug.Log(
                    $"[GameFlow] 读档恢复完成 层{snapshot.floor} 节点{snapshot.DisplayNode}"
                    + $"（全局{snapshot.shellGlobalNodeIndex}） seed={snapshot.seed}"
                    + $" 金币={snapshot.coins} HP={snapshot.metaHp}/{snapshot.metaMaxHp}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError("[GameFlow] 读档恢复异常：" + ex);
                return false;
            }
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
            view?.HideNotice();
            view?.ShowInRunShell(inBattle: true);
            BoardBriefTipPresenter.InstanceOrNull()?.HardClear();

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

            // 存档检查点：正式局在 BuildNodeDeckOptions 消耗 RNG 之前捕获，
            // 恢复时以同一 RNG 状态重跑发牌即可复现「本场对战开始」。
            if (!mShell.IsQuickTestMode)
            {
                RunSaveService.CaptureCheckpoint(mShell.NodeIndex);
            }

            CoreCardPresentationMapper.EnsureContentCatalogLoaded();

            var catalog = arch.GetSystem<IContentSystem>()?.Catalog;
            string monsterDeckId = null;
            int contentNodeIndex;
            if (mShell.TryConsumePinnedFirstBattle(out var pinnedDeckId))
            {
                monsterDeckId = pinnedDeckId;
                contentNodeIndex = QuickTestDeckCatalog.GetDefaultNodeIndexForDeckId(catalog, pinnedDeckId);
            }
            else if (mShell.IsQuickTestMode)
            {
                contentNodeIndex = mShell.ResolveBattleContentNodeIndex();
            }
            else
            {
                contentNodeIndex = GameFlowShellSystem.ResolveFormalBattleContentNodeIndex(
                    arch.GetModel<RunModel>());
            }

            var options = arch.GetSystem<IRewardSystem>().BuildNodeDeckOptions(contentNodeIndex, monsterDeckId);
            if (options == null)
            {
                options = NodeDeckOptions.CreateDefaultBattle();
            }

            ApplyQuickTestTrapCardsIfNeeded(options);

            LogMonsterDeckWiringGuard(catalog, options, monsterDeckId);

            Debug.Log(mShell.IsQuickTestMode
                ? $"[GameFlow] 循环节点 {mShell.NodeIndex} 快速测试内容节点 {contentNodeIndex}"
                  + (string.IsNullOrEmpty(monsterDeckId) ? string.Empty : $" 固定牌组 {monsterDeckId}")
                  + " 真实局内入场"
                : $"[GameFlow] 节点 {mShell.NodeIndex} 真实局内入场");

            // 正式 Run：战斗信息预览硬阻塞；未关闭前不 StartBattleNodeAsync（不入场/不发牌）。
            if (!mShell.IsQuickTestMode)
            {
                await BattleInfoPreviewPresenter.ShowAndWaitAsync(options, ct);
                if (ct.IsCancellationRequested)
                {
                    return false;
                }
            }

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

        /// <summary>节点 4/7：StartNode → 非战斗 RoomChoice（4=离开 / 7=层主房图标），不进 InteractionLoop。</summary>
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

            if ((pending.Kind.Value == PendingChoiceKind.Reward
                 && PendingChoiceModel.IsConsumerBoardPool(pending.PoolId.Value))
                || (pending.Kind.Value == PendingChoiceKind.AttributePick
                    && PendingChoiceModel.IsAttributePickPool(pending.PoolId.Value)))
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
                else if (PendingChoiceModel.IsAttributePickPool(pending.PoolId.Value))
                {
                    await PresentAttributeBoardAsync(ct);
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

            // 若 Cover 仍挂起（未进 Present*Board / Spawn 失败已 ForceClear），此处兜底揭开。
            var held = RunSceneTransitionService.InstanceOrNull;
            if (held != null && held.IsCoverHeld)
            {
                await held.CompleteRevealAsync(CancellationToken.None);
            }

            view?.HideAllOverlays();
            ResolveSession()?.RefreshPersistentInBattleUi(animate: false);
        }

        /// <summary>图标 EnterRoom 后进入消费/特殊/属性房场地板会话。</summary>
        private static bool IsAwaitingInRoomBoard(IPhaseSystem phase, PendingChoiceModel pending)
        {
            if (phase == null || pending == null)
            {
                return false;
            }

            if (phase.CurrentPhase != GamePhase.RewardItemChoice)
            {
                return false;
            }

            if (pending.Kind.Value == PendingChoiceKind.AttributePick)
            {
                return PendingChoiceModel.IsAttributePickPool(pending.PoolId.Value);
            }

            return pending.Kind.Value == PendingChoiceKind.Reward
                   && PendingChoiceModel.IsConsumerBoardPool(pending.PoolId.Value);
        }

        private static async UniTask RevealHeldTransitionIfAnyAsync()
        {
            var transition = RunSceneTransitionService.InstanceOrNull;
            if (transition == null || !transition.IsCoverHeld)
            {
                return;
            }

            await transition.CompleteRevealAsync(CancellationToken.None);
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
                RunSceneTransitionService.InstanceOrNull?.ForceClearFaders();
                walk?.SetEnabled(false);
                return;
            }

            await RevealHeldTransitionIfAnyAsync();

            // ADR-0020：房内场地板=受保护场地，勿整段持有 ChoiceOverlay，否则 BoardWalk
            // 以 ProtectedField 提交会 ownerMismatch，离开/空格全点不动。
            // 购买/刷新/离开走 RewardChoice + 当前 CurrentOwner（店内为 ProtectedField）。
            Debug.Log(
                "[InRoom] ShopBoard session begin walkEnabled=true choiceOverlay=false phase="
                + phaseSystem.CurrentPhase);
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
                Debug.Log(
                    "[InRoom] ShopBoard session end phase=" + phaseSystem.CurrentPhase
                    + " shopActive=" + shop.IsActive);
                BoardIntentGateDiagnostics.LogConsole(
                    "ShopBoardEnd",
                    "walkEnabled=" + (walk != null && walk.IsEnabled ? "1" : "0")
                    + " shopActive=" + (shop.IsActive ? "1" : "0"),
                    arch,
                    GameCommandKind.MoveAvatar);
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
                RunSceneTransitionService.InstanceOrNull?.ForceClearFaders();
                walk?.SetEnabled(false);
                return;
            }

            await RevealHeldTransitionIfAnyAsync();

            // 同商店：场地即交互面，勿整段 ChoiceOverlay（ADR-0020）。
            Debug.Log(
                "[InRoom] TavernBoard session begin walkEnabled=true choiceOverlay=false phase="
                + phaseSystem.CurrentPhase);
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
                Debug.Log(
                    "[InRoom] TavernBoard session end phase=" + phaseSystem.CurrentPhase
                    + " tavernActive=" + tavern.IsActive);
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
                RunSceneTransitionService.InstanceOrNull?.ForceClearFaders();
                walk?.SetEnabled(false);
                return;
            }

            await RevealHeldTransitionIfAnyAsync();

            // 同商店：场地即交互面，勿整段 ChoiceOverlay（ADR-0020）。
            Debug.Log(
                "[InRoom] RewardBoard session begin walkEnabled=true choiceOverlay=false phase="
                + phaseSystem.CurrentPhase);
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
                Debug.Log(
                    "[InRoom] RewardBoard session end phase=" + phaseSystem.CurrentPhase
                    + " rewardActive=" + reward.IsActive);
                walk?.SetEnabled(false);
                walk?.Cancel();
                if (reward.IsActive)
                {
                    reward.DespawnAll();
                }
            }
        }

        private async UniTask PresentAttributeBoardAsync(CancellationToken ct)
        {
            var arch = NineGridArchitecture.Current;
            var phaseSystem = arch.GetSystem<IPhaseSystem>();
            var walk = AvatarWalkSystem.EnsureRegistered(NineGridArchitecture.Interface);
            walk?.SetEnabled(true);
            BoardCardSelectModeController.RequestAbort("mainloop-attribute-board");

            var attribute = AttributeBoardPresenter.Current;
            attribute.Bind(arch);
            RoomIconBoardPresenter.Current.HardCutAfterEnter(arch);
            if (!attribute.TrySpawnFromPending(arch))
            {
                Debug.LogError("[GameFlow] 属性房三选二 Spawn 失败");
                RunSceneTransitionService.InstanceOrNull?.ForceClearFaders();
                walk?.SetEnabled(false);
                return;
            }

            await RevealHeldTransitionIfAnyAsync();

            // 同商店：场地即交互面，勿整段 ChoiceOverlay（ADR-0020）。
            Debug.Log(
                "[InRoom] AttributeBoard session begin walkEnabled=true choiceOverlay=false phase="
                + phaseSystem.CurrentPhase);
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
                Debug.Log(
                    "[InRoom] AttributeBoard session end phase=" + phaseSystem.CurrentPhase
                    + " attributeActive=" + attribute.IsActive);
                walk?.SetEnabled(false);
                walk?.Cancel();
                if (attribute.IsActive)
                {
                    attribute.DespawnAll();
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
            // run 终局：清内存检查点并删除自动存档（手动槽保留）。
            // 教学局不产生检查点，也不得误删玩家早前正式局的自动存档。
            if (!mShell.IsTutorialMode)
            {
                RunSaveService.HandleRunEnded();
                if (victory)
                {
                    TutorialProgressStore.MarkScarletUnlocked();
                }
                else
                {
                    TutorialProgressStore.MarkSteps1To9Completed();
                }
            }
            CancelLoopWork();
            ResolveSession()?.ClearCardPresentationSurface();
            ResolveSession()?.RefreshPersistentInBattleUi(animate: false);
            NineGridArchitecture.Interface?.GetSystem<IFieldBattlePresentationSystem>()?.CancelBattleWork();

            RequestSetState(victory ? GameFlowShellState.VictoryNotice : GameFlowShellState.DefeatNotice);
            FlowRoomEconomyAudioCues.Pulse(
                victory ? FlowRoomEconomyAudioCues.Victory : FlowRoomEconomyAudioCues.Defeat,
                "GameFlowOrchestrator.ShowBattleEndAndReturnAsync");
            FlowBattleEndVfxCues.PulseBattleEnd(
                victory,
                "GameFlowOrchestrator.ShowBattleEndAndReturnAsync");
            var view = mShell.View;
            view?.EnsureViewBindings();
            view?.ShowInRunShell();
            var message = victory
                ? (view == null || string.IsNullOrWhiteSpace(view.VictoryMessage)
                    ? NineGrid.Core.Localization.L10n.Tr("notice.victory", "胜利")
                    : view.VictoryMessage)
                : (view == null || string.IsNullOrWhiteSpace(view.DefeatMessage)
                    ? NineGrid.Core.Localization.L10n.Tr("notice.defeat", "失败")
                    : view.DefeatMessage);
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
                ? "[GameFlow] 整局胜利，展示结算面板后回主菜单。"
                : "[GameFlow] 战斗失败，展示结算面板后回主菜单。");

            // 结算面板：只读展示本局数据（ClearRunSession 之前快照），等玩家确认返回。
            // 场景缺预置时回退旧 Notice + 延时路径，不阻断流程。
            var summaryShown = await NineGrid.Presentation.Ui.RunSummaryPanel
                .TryShowAndWaitAsync(victory, ct);
            if (!summaryShown)
            {
                view?.ShowNotice(message);
                var seconds = victory
                    ? Mathf.Max(0.2f, view?.VictoryNoticeSeconds ?? 1f)
                    : Mathf.Max(0.2f, view?.DefeatNoticeSeconds ?? 1f);
                await UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: ct);
            }

            EnterMainMenuImmediate();
        }

        private void EnterMainMenuImmediate()
        {
            // 强退（Stop）路径可能未走结算面板 finally；兜底收起，避免面板挂在主菜单上。
            NineGrid.Presentation.Ui.RunSummaryPanel.CloseIfOpen();
            var view = mShell.View;
            view?.EnsureViewBindings();
            view?.HideNotice();
            PresentationInputGates.Reset("EnterMainMenu");
            NineGridArchitecture.Interface?.GetSystem<IFieldBattlePresentationSystem>()?.CancelBattleWork();
            ResolveSession()?.ClearPresentationSurface();
            view?.ShowMainMenuPanels();
            FlowRoomEconomyAudioCues.Pulse(
                FlowRoomEconomyAudioCues.ReturnMainMenu,
                "GameFlowOrchestrator.EnterMainMenuImmediate");
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

        /// <summary>
        /// 主题卡组接线守卫（非正式卡组泄露追查）：每场战斗记录本场绑定的主题卡组
        /// （id / kind / 显示名 / 来源）。正式局绑定到非正式档位（Reserve / Unknown），或
        /// 开局卡池出现归档 / AI 拓展 / 过渡内容时打 Error，复现时可直接从 ConsoleLog 归因。
        /// </summary>
        private void LogMonsterDeckWiringGuard(GameContentCatalog catalog, NodeDeckOptions options, string pinnedDeckId)
        {
            if (catalog == null || options == null)
            {
                return;
            }

            var run = NineGridArchitecture.Current.GetModel<RunModel>();
            var deckId = !string.IsNullOrEmpty(pinnedDeckId)
                ? pinnedDeckId
                : (run != null ? run.FloorMonsterDeckId.Value : string.Empty);
            if (!string.IsNullOrEmpty(deckId) && catalog.MonsterDecks.TryGetValue(deckId, out var deck) && deck != null)
            {
                var playable = MonsterDeckFloorPool.IsPlayableDifficulty(deck.Kind);
                Debug.Log(
                    $"[GameFlow] 主题卡组绑定 deck={deck.Id} kind={deck.Kind} display={deck.DisplayName}"
                    + $" pinned={!string.IsNullOrEmpty(pinnedDeckId)} playable={playable} node={mShell.NodeIndex}");
                if (!playable)
                {
                    Debug.LogError(
                        $"[GameFlow] 绑定到非正式档位主题卡组 deck={deck.Id} kind={deck.Kind}——"
                        + "疑似内容接线泄露，请核对 monster_decks.json 与跑图存档。");
                }
            }
            else if (!string.IsNullOrEmpty(deckId))
            {
                Debug.LogWarning($"[GameFlow] 主题卡组绑定未命中 catalog：deck={deckId} node={mShell.NodeIndex}");
            }

            var leaked = new List<string>();
            CollectUnofficialOpeningLeaks(catalog, options.EnemyCards, leaked);
            CollectUnofficialOpeningLeaks(catalog, options.PlayerCards, leaked);

            if (leaked.Count > 0)
            {
                Debug.LogError(
                    "[GameFlow] 开局卡池出现非正式接线内容（归档/AI拓展/过渡）："
                    + string.Join(",", leaked)
                    + " node=" + mShell.NodeIndex
                    + "——内容泄露，请保留本次 BattleLog/CoreLog 用于归因。");
            }
        }

        private static void CollectUnofficialOpeningLeaks(
            GameContentCatalog catalog,
            IReadOnlyList<CardDraft> drafts,
            List<string> leaked)
        {
            if (drafts == null)
            {
                return;
            }

            for (var i = 0; i < drafts.Count; i++)
            {
                var draft = drafts[i];
                if (draft == null || string.IsNullOrEmpty(draft.DefId))
                {
                    continue;
                }

                if (catalog.TryGetCard(draft.DefId, out var card)
                    && card != null
                    && FormalContentWiring.IsUnofficialDeck(card.DeckId)
                    && !leaked.Contains(draft.DefId))
                {
                    leaked.Add(draft.DefId);
                }
            }
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
            // 读档恢复：Start 已完成恢复版 Bootstrap（NodeCompleted 相位下 StartNode 已合法），
            // 首个战斗节点若再 BootstrapRun 会把恢复状态清成新局。
            if (mRestoredBootstrapPending)
            {
                mRestoredBootstrapPending = false;
                if (phase.CanExecute(GameCommandKind.StartNode))
                {
                    return;
                }

                Debug.LogWarning(
                    "[GameFlow] 读档恢复后 StartNode 仍非法 phase=" + phase.CurrentPhase + "，走常规兜底。");
            }

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

            if (locked)
            {
                BoardIntentGateDiagnostics.LogConsole(
                    "BattleBootstrap",
                    "StartNode-blocked-before-clear phaseBefore=" + phaseBefore,
                    arch,
                    GameCommandKind.StartNode);
            }

            // 粘连 Present 锁会使 StartNode 非法；先清锁再决定是否必须 BootstrapRun。
            sync?.Clear();
            PresentationInputGates.ForceEndExternalHold("EnsureBattleNodeBootstrap.unlock");

            if (phase.CanExecute(GameCommandKind.StartNode))
            {
                if (locked)
                {
                    BoardIntentGateDiagnostics.LogConsole(
                        "BattleBootstrap",
                        "StartNode-ok-after-clear phase=" + phase.CurrentPhase,
                        arch,
                        GameCommandKind.StartNode);
                }

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
            BoardIntentGateDiagnostics.LogConsole(
                "BattleBootstrap",
                "forced-bootstrap phaseBefore=" + phaseBefore + " locked=" + (locked ? "1" : "0"),
                arch,
                GameCommandKind.StartNode);
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

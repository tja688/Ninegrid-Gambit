using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Flow.Diagnostics;
using NineGrid.Flow.Presentation;
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
            mShell.BumpGeneration();

            if (quickTestMode)
            {
                var qt = quickTestOptions ?? new QuickTestRunOptions();
                mShell.PrepareQuickTest(qt);
                DiagTraceShared.SetRunTag(
                    DiagTraceShared.QuickTestRunTag,
                    BuildQuickTestRunTagNote(mShell.QuickTestNodeOrderMode, mShell.PinnedFirstBattleDeckId));
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
                    $"[GameFlow] 快速测试模式：全局速度 x1（局内 \\2 可加速），玩家 HP {GameFlowShellSystem.QuickTestAvatarHp} / ATK {GameFlowShellSystem.QuickTestAvatarAttack} 每关重置，"
                    + $"节点顺序 {mShell.QuickTestNodeOrderMode}，队列 {QuickTestRunPlanner.FormatNodeOrder(mShell.QuickTestContentNodeQueue)}"
                    + (string.IsNullOrEmpty(mShell.PinnedFirstBattleDeckId)
                        ? string.Empty
                        : $"，首关牌组 {mShell.PinnedFirstBattleDeckId}"));
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
            var view = mShell.View;
            if (view != null && view.IsRoomChoiceActive)
            {
                view.HideRoomChoice();
            }

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

                    await PlayRoomChoiceAsync(ct);
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }

                    await PlayRoomEventAsync(ct);
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

        private async UniTask PlayRoomChoiceAsync(CancellationToken ct)
        {
            RequestSetState(GameFlowShellState.RoomChoice);
            var view = mShell.View;
            view?.EnsureViewBindings();

            var arch = NineGridArchitecture.Current;
            var phaseSystem = arch.GetSystem<IPhaseSystem>();
            var pending = arch.GetModel<PendingChoiceModel>();
            if (phaseSystem.CurrentPhase != GamePhase.RoomChoice
                || pending.Kind.Value != PendingChoiceKind.Room
                || pending.RoomOptions == null
                || pending.RoomOptions.Count < 2)
            {
                Debug.LogWarning(
                    $"[GameFlow] 跳过房间选择 phase={phaseSystem.CurrentPhase} pending={pending.Kind.Value}");
                try
                {
                    FlowTraceRecorder.Record(
                        FlowTraceCategory.CoreGate,
                        FlowTraceNames.RoomPresented,
                        new Dictionary<string, string>
                        {
                            { "skipped", "true" },
                            { "phase", phaseSystem.CurrentPhase.ToString() },
                            { "pending", pending.Kind.Value.ToString() },
                            { "nodeIndex", mShell.NodeIndex.ToString() },
                        },
                        loopState: mShell.State.Value.ToString(),
                        phaseBefore: phaseSystem.CurrentPhase.ToString(),
                        accepted: false);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[GameFlow] FlowTrace RoomSkipped: " + ex.Message);
                }

                return;
            }

            var left = pending.RoomOptions[0];
            var right = pending.RoomOptions[1];
            try
            {
                FlowTraceRecorder.Record(
                    FlowTraceCategory.CoreGate,
                    FlowTraceNames.RoomPresented,
                    new Dictionary<string, string>
                    {
                        { "left", left.ToString() },
                        { "right", right.ToString() },
                        { "nodeIndex", mShell.NodeIndex.ToString() },
                    },
                    loopState: mShell.State.Value.ToString(),
                    phaseBefore: phaseSystem.CurrentPhase.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GameFlow] FlowTrace RoomPresented: " + ex.Message);
            }

            view?.ShowRoomChoiceOverlay();

            var pickedIndex = -1;
            var pickedId = string.Empty;
            var finished = false;
            BoardCardSelectModeController.RequestAbort("mainloop-room-choice");
            // SelectRoom 必须在 ChoiceOverlay 仍持有时提交：结算 Drain 可能仍 mainlineBusy，
            // 先关 overlay 再 Submit 会被 IntentIntake 拒成 intentIntakeReject，
            // phase 卡在 RoomChoice → 下一节点 BootstrapRun 清空中途遗物。
            PresentationInputGates.SetChoiceOverlay(true);
            CoreCommandResult result = CoreCommandResult.Reject("roomChoiceNotSubmitted");
            var phaseBeforeSelect = phaseSystem.CurrentPhase.ToString();
            var goldEventStart = 0;
            try
            {
                if (view != null)
                {
                    view.BeginRoomChoice(
                        left.ToString(),
                        right.ToString(),
                        (index, optionId) =>
                        {
                            pickedIndex = index;
                            pickedId = optionId ?? string.Empty;
                            Debug.Log($"[GameFlow] 房间已选 index={index} id={optionId}");
                        },
                        () => { finished = true; },
                        hoverOnNotice: true);

                    await UniTask.WaitUntil(() => finished || ct.IsCancellationRequested, cancellationToken: ct);
                }

                if (ct.IsCancellationRequested)
                {
                    return;
                }

                if (pickedIndex < 0)
                {
                    pickedIndex = 0;
                }

                phaseBeforeSelect = phaseSystem.CurrentPhase.ToString();
                var pipeline = arch.GetSystem<IActionPipelineSystem>();
                goldEventStart = pipeline.EventLog.Entries.Count;
                result = SubmitSelectRoom(phaseSystem, pickedIndex);
                if (!result.Accepted)
                {
                    Debug.LogWarning($"[GameFlow] SelectRoom 被拒: {result.Reason}");
                }
            }
            finally
            {
                PresentationInputGates.SetChoiceOverlay(false);
                view?.HideAllOverlays();
            }

            if (ct.IsCancellationRequested)
            {
                return;
            }

            try
            {
                FlowTraceRecorder.Record(
                    FlowTraceCategory.CoreGate,
                    FlowTraceNames.RoomChosen,
                    new Dictionary<string, string>
                    {
                        { "index", pickedIndex.ToString() },
                        { "optionId", string.IsNullOrEmpty(pickedId) ? pickedIndex.ToString() : pickedId },
                        { "reason", result.Reason ?? string.Empty },
                        { "nodeIndex", mShell.NodeIndex.ToString() },
                        { "choiceOverlayHeld", "true" },
                    },
                    loopState: mShell.State.Value.ToString(),
                    phaseBefore: phaseBeforeSelect,
                    phaseAfter: phaseSystem.CurrentPhase.ToString(),
                    accepted: result.Accepted);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GameFlow] FlowTrace RoomChosen: " + ex.Message);
            }

            if (result.Accepted)
            {
                BattleBeatFlush.PresentEventLogSlice(NineGridArchitecture.Current, goldEventStart);
                ResolveSession()?.RefreshPersistentInBattleUi(animate: false);
            }
        }

        private async UniTask PlayRoomEventAsync(CancellationToken ct)
        {
            RequestSetState(GameFlowShellState.RoomEvent);
            var view = mShell.View;
            view?.EnsureViewBindings();

            var arch = NineGridArchitecture.Current;
            var phaseSystem = arch.GetSystem<IPhaseSystem>();
            if (phaseSystem.CurrentPhase != GamePhase.RoomEvent)
            {
                Debug.LogWarning($"[GameFlow] 跳过房间事件 phase={phaseSystem.CurrentPhase}");
                return;
            }

            var selectedRoom = arch.GetModel<PendingChoiceModel>().SelectedRoom.Value;
            view?.ShowRoomEventOverlay();

            var phaseBeforeEnter = phaseSystem.CurrentPhase.ToString();
            var pipeline = arch.GetSystem<IActionPipelineSystem>();
            var goldEventStart = pipeline.EventLog.Entries.Count;
            // 与 SelectRoom 同理：壳层自动 EnterRoom 也须持有 ChoiceOverlay，
            // 否则结算 Drain 未尽时会被 IntentIntake 拒掉，卡死跨关。
            PresentationInputGates.SetChoiceOverlay(true);
            CoreCommandResult enter;
            try
            {
                enter = SubmitEnterRoom(phaseSystem);
            }
            finally
            {
                PresentationInputGates.SetChoiceOverlay(false);
            }

            if (!enter.Accepted)
            {
                Debug.LogWarning($"[GameFlow] EnterRoom 被拒: {enter.Reason}");
                try
                {
                    FlowTraceRecorder.Record(
                        FlowTraceCategory.CoreGate,
                        FlowTraceNames.EnterRoom,
                        new Dictionary<string, string>
                        {
                            { "room", selectedRoom.ToString() },
                            { "reason", enter.Reason ?? string.Empty },
                            { "nodeIndex", mShell.NodeIndex.ToString() },
                        },
                        loopState: mShell.State.Value.ToString(),
                        phaseBefore: phaseBeforeEnter,
                        phaseAfter: phaseSystem.CurrentPhase.ToString(),
                        accepted: false);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[GameFlow] FlowTrace EnterRoom reject: " + ex.Message);
                }

                view?.HideAllOverlays();
                return;
            }

            try
            {
                FlowTraceRecorder.Record(
                    FlowTraceCategory.CoreGate,
                    FlowTraceNames.EnterRoom,
                    new Dictionary<string, string>
                    {
                        { "room", selectedRoom.ToString() },
                        { "nodeIndex", mShell.NodeIndex.ToString() },
                    },
                    loopState: mShell.State.Value.ToString(),
                    phaseBefore: phaseBeforeEnter,
                    phaseAfter: phaseSystem.CurrentPhase.ToString(),
                    accepted: true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GameFlow] FlowTrace EnterRoom: " + ex.Message);
            }

            BattleBeatFlush.PresentEventLogSlice(NineGridArchitecture.Current, goldEventStart);
            ResolveSession()?.RefreshPersistentInBattleUi(animate: false);

            var pending = arch.GetModel<PendingChoiceModel>();
            if (pending.Kind.Value == PendingChoiceKind.Reward
                && pending.RewardOptions != null
                && pending.RewardOptions.Count > 0)
            {
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
                            { "source", "roomEvent" },
                            { "room", selectedRoom.ToString() },
                        },
                        loopState: mShell.State.Value.ToString(),
                        phaseBefore: phaseSystem.CurrentPhase.ToString());
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[GameFlow] FlowTrace RoomRewardPresented: " + ex.Message);
                }

                view?.ShowRewardOverlay();
                BoardCardSelectModeController.RequestAbort("mainloop-room-reward-overlay");
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

        private static string BuildRoomResolvedNotice(RoomKind room)
        {
            CoreCardPresentationMapper.EnsureContentCatalogLoaded();
            var arch = NineGridArchitecture.Current;
            if (arch != null)
            {
                var content = arch.GetSystem<IContentSystem>();
                if (content != null
                    && content.HasCatalog
                    && content.Catalog.Rewards.TryGetRoom(room, out var def)
                    && !string.IsNullOrWhiteSpace(def.DisplayName))
                {
                    if (def.GoldDelta != 0)
                    {
                        return $"{def.DisplayName}：金币{(def.GoldDelta > 0 ? "+" : string.Empty)}{def.GoldDelta}";
                    }

                    if (def.MaxHpDelta != 0 || def.HealToFull)
                    {
                        return def.HealToFull
                            ? $"{def.DisplayName}：血量上限+{def.MaxHpDelta}，已回满"
                            : $"{def.DisplayName}：血量上限+{def.MaxHpDelta}";
                    }

                    return def.DisplayName;
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

        private static string BuildQuickTestRunTagNote(
            QuickTestNodeOrderMode orderMode,
            string pinnedFirstBattleDeckId)
        {
            var note = "快速测试：全局速度x1，玩家HP99/ATK5每关重置，节点顺序"
                + (orderMode == QuickTestNodeOrderMode.Sequential ? "正式" : "乱序");
            if (!string.IsNullOrEmpty(pinnedFirstBattleDeckId))
            {
                note += "，首关固定牌组=" + pinnedFirstBattleDeckId;
            }

            return note;
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

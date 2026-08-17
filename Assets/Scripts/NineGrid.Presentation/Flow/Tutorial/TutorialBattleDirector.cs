using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.Cards.Vfx;
using NineGrid.Core;
using NineGrid.Core.Systems;
using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Flow.Tutorial
{
    /// <summary>
    /// 教学关卡导演：五阶段受控剧本。换阶段清场→直摆→登记；
    /// 阶段2补牌+强转；阶段死亡重开当前阶段；阶段5击杀钢铁莱姆通关。
    /// </summary>
    public sealed class TutorialBattleDirector
    {
        private const int PhaseOne = 1;

        private readonly IArchitecture mArchitecture;
        private readonly IBattleSessionSystem mSession;
        private readonly CoreCommandDispatcher mDispatcher;
        private readonly QueuedBoardPresentChannel mBoardChannel;
        private readonly TutorialLegality mLegality;
        private readonly TutorialBattleGlowHost mGlowHost;

        private int mPhase = PhaseOne;
        private bool mPhaseReady;
        private bool mTransitionQueued;
        private bool mRestartQueued;
        private bool mInputHeld;
        private bool mSuppressDefeatEnd;
        private bool mAvatarDefeatPending;

        private int mPhase2KillCount;
        private int mPhase2RefillSlot;
        private int mPhase2PendingRotate;

        private int mDummyUid;
        private int mActionDummyUid;
        private int mMoveDummyUid;
        private int mSteelSlimeUid;
        private int mKnifeBoardUid;
        private int mPotionBoardUid;

        private readonly HashSet<int> mTrackedUids = new HashSet<int>();
        private CancellationTokenSource mCts;
        private IntentIntakeSystem mIntentIntake;

        private TutorialBattleDirector(IArchitecture architecture)
        {
            mArchitecture = architecture;
            mSession = BattleSessionSystem.EnsureRegistered(architecture);
            mDispatcher = new CoreCommandDispatcher(architecture, 700_000);
            mBoardChannel = new QueuedBoardPresentChannel(
                (result, token) => mSession.DrainPostKillBoardAsync(result, token),
                mSession.EnsurePresentationToken);
            mLegality = new TutorialLegality(architecture);
            mGlowHost = TutorialBattleGlowHost.Ensure();
            mIntentIntake = IntentIntakeSystem.EnsureRegistered(architecture) as IntentIntakeSystem;
            mIntentIntake?.SetLegalityOverride(mLegality.TryExplain);
            mSuppressDefeatEnd = true;
            TutorialBattleSessionHook.Bind(
                () => mSuppressDefeatEnd,
                () => mAvatarDefeatPending = true);
        }

        public static TutorialBattleDirector StartNew(CancellationToken ct)
        {
            var architecture = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            var director = new TutorialBattleDirector(architecture);
            director.mCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            director.RunAsync(director.mCts.Token).Forget();
            return director;
        }

        public void Stop()
        {
            ReleasePlayerInput();
            SetAvatarRangePinned(false);
            TutorialBattleSessionHook.Clear();
            mIntentIntake?.SetLegalityOverride(null);
            TutorialBattleGlowHost.DestroyIfExists();
            if (mCts == null)
            {
                return;
            }

            mCts.Cancel();
            mCts.Dispose();
            mCts = null;
        }

        private async UniTaskVoid RunAsync(CancellationToken ct)
        {
            try
            {
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                EnqueueInitialPhaseSetup();
                while (!ct.IsCancellationRequested)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    Tick();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogError("[Tutorial] 教学导演异常：" + ex);
            }
        }

        private void EnqueueInitialPhaseSetup()
        {
            mTransitionQueued = true;
            HoldPlayerInput();
            var runtime = mArchitecture.GetSystem<IPresentationRuntimeSystem>();
            runtime?.MutateMainline(timeline =>
            {
                var sync = mArchitecture.GetSystem<IPresentationSyncSystem>();
                var gate = PresentationSyncBatchGate.FromSync(
                    sync,
                    () => ResolveAndProject(() => mDispatcher.Send(new TutorialSetupPhaseCommand(PhaseOne))),
                    slice: "TutorialPhaseSetup");
                timeline.Enqueue(new ResolveBatchStep(gate));
                timeline.Enqueue(new PresentStep(gate, mBoardChannel, channelName: "TutorialPhaseSetup"));
                timeline.Enqueue(new TutorialCallbackStep(() =>
                {
                    mTransitionQueued = false;
                    ScanPhase();
                    ReleasePlayerInput();
                }));
            });
        }

        private void Tick()
        {
            if (mAvatarDefeatPending && !mRestartQueued && !mTransitionQueued)
            {
                mAvatarDefeatPending = false;
                EnqueuePhaseRestart();
                return;
            }

            var phase = mArchitecture.GetSystem<IPhaseSystem>();
            if (phase == null || phase.CurrentPhase != GamePhase.InteractionLoop)
            {
                if (mSuppressDefeatEnd && phase != null && phase.CurrentPhase == GamePhase.Defeat)
                {
                    return;
                }

                ReleasePlayerInput();
                return;
            }

            if (!mSuppressDefeatEnd)
            {
                ReleasePlayerInput();
                return;
            }

            var runtime = mArchitecture.GetSystem<IPresentationRuntimeSystem>();
            var sync = mArchitecture.GetSystem<IPresentationSyncSystem>();
            if (runtime == null || sync == null || mTransitionQueued || mRestartQueued)
            {
                return;
            }

            if (mPhaseReady)
            {
                if (mPhase == 2 && mPhase2PendingRotate > 0 && !runtime.MainlineBusy.Value && sync.ActiveBatchId == 0)
                {
                    EnqueuePhase2Rotate();
                    return;
                }

                var nextPhase = NextPhaseIfCompleted();
                if (nextPhase > 0)
                {
                    if (nextPhase > TutorialDeckPlan.PhaseCount)
                    {
                        CompleteTutorial();
                        return;
                    }

                    HoldPlayerInput();
                    if (!runtime.MainlineBusy.Value && sync.ActiveBatchId == 0)
                    {
                        EnqueuePhaseTransition(nextPhase);
                    }

                    return;
                }
            }

            if (runtime.MainlineBusy.Value || sync.ActiveBatchId > 0)
            {
                return;
            }

            if (!mPhaseReady)
            {
                ScanPhase();
                ReleasePlayerInput();
            }
        }

        private int NextPhaseIfCompleted()
        {
            switch (mPhase)
            {
                case 1:
                    return IsDeadOrGone(mDummyUid) ? 2 : 0;
                case 2:
                    if (mPhase2KillCount < 1)
                    {
                        if (IsDeadOrGone(mDummyUid))
                        {
                            mPhase2RefillSlot = ResolveLastDummyDeathSlot();
                            mPhase2KillCount = 1;
                            mPhase2PendingRotate = 1;
                            mLegality.Phase2KillCount = 1;
                            EnqueuePhase2Refill();
                            return 0;
                        }

                        return 0;
                    }

                    return IsDeadOrGone(mDummyUid) ? 3 : 0;
                case 3:
                    return IsDeadOrGone(mActionDummyUid) && IsDeadOrGone(mMoveDummyUid) ? 4 : 0;
                case 4:
                    return ArePhase4ItemsPickedUp() ? 5 : 0;
                case 5:
                    return IsDeadOrGone(mSteelSlimeUid) ? TutorialDeckPlan.PhaseCount + 1 : 0;
                default:
                    return 0;
            }
        }

        private void EnqueuePhase2Refill()
        {
            if (mPhase2RefillSlot <= 0)
            {
                mPhase2RefillSlot = 9;
            }

            mTransitionQueued = true;
            HoldPlayerInput();
            var runtime = mArchitecture.GetSystem<IPresentationRuntimeSystem>();
            runtime?.MutateMainline(timeline =>
            {
                var sync = mArchitecture.GetSystem<IPresentationSyncSystem>();
                var gate = PresentationSyncBatchGate.FromSync(
                    sync,
                    () => ResolveAndProject(() =>
                        mDispatcher.Send(new TutorialRefillDummyToSlotCommand(mPhase2RefillSlot))),
                    slice: "TutorialPhase2Refill");
                timeline.Enqueue(new ResolveBatchStep(gate));
                timeline.Enqueue(new PresentStep(gate, mBoardChannel, channelName: "TutorialPhase2Refill"));
                timeline.Enqueue(new TutorialCallbackStep(() =>
                {
                    mTransitionQueued = false;
                    ScanPhase();
                }));
            });
        }

        private void EnqueuePhase2Rotate()
        {
            mPhase2PendingRotate = 0;
            mTransitionQueued = true;
            HoldPlayerInput();
            var runtime = mArchitecture.GetSystem<IPresentationRuntimeSystem>();
            runtime?.MutateMainline(timeline =>
            {
                var sync = mArchitecture.GetSystem<IPresentationSyncSystem>();
                var gate = PresentationSyncBatchGate.FromSync(
                    sync,
                    () => ResolveAndProject(() => mDispatcher.Send(new TutorialForceRotateCommand())),
                    slice: "TutorialPhase2Rotate");
                timeline.Enqueue(new ResolveBatchStep(gate));
                timeline.Enqueue(new PresentStep(gate, mBoardChannel, channelName: "TutorialPhase2Rotate"));
                timeline.Enqueue(new TutorialCallbackStep(() =>
                {
                    mTransitionQueued = false;
                    ScanPhase();
                    ReleasePlayerInput();
                }));
            });
        }

        private void EnqueuePhaseRestart()
        {
            mRestartQueued = true;
            HoldPlayerInput();
            var clearItems = mPhase >= 4;
            var runtime = mArchitecture.GetSystem<IPresentationRuntimeSystem>();
            runtime?.MutateMainline(timeline =>
            {
                var sync = mArchitecture.GetSystem<IPresentationSyncSystem>();
                var gate = PresentationSyncBatchGate.FromSync(
                    sync,
                    () => ResolveAndProject(() =>
                        mDispatcher.Send(new TutorialRestartPhaseCommand(mPhase, clearItems))),
                    slice: "TutorialPhaseRestart");
                timeline.Enqueue(new ResolveBatchStep(gate));
                timeline.Enqueue(new PresentStep(gate, mBoardChannel, channelName: "TutorialPhaseRestart"));
                timeline.Enqueue(new TutorialCallbackStep(() =>
                {
                    mRestartQueued = false;
                    mPhase2KillCount = 0;
                    mPhase2PendingRotate = 0;
                    mLegality.Phase2KillCount = 0;
                    ScanPhase();
                    ReleasePlayerInput();
                }));
            });
        }

        private void EnqueuePhaseTransition(int nextPhase)
        {
            mTransitionQueued = true;
            mPhase = nextPhase;
            mPhaseReady = false;
            mPhase2KillCount = 0;
            mPhase2PendingRotate = 0;
            mLegality.Phase2KillCount = 0;
            SetAvatarRangePinned(mPhase == 2);
            Debug.Log($"[Tutorial] 换阶段 → 第{mPhase}阶段");

            var runtime = mArchitecture.GetSystem<IPresentationRuntimeSystem>();
            runtime?.MutateMainline(timeline =>
            {
                var sync = mArchitecture.GetSystem<IPresentationSyncSystem>();
                var clearGate = PresentationSyncBatchGate.FromSync(
                    sync,
                    () => ResolveAndProject(() =>
                    {
                        mDispatcher.Send(new TutorialClearDeckAndItemsCommand(clearItemSlots: false));
                        return mDispatcher.Send(new TutorialClearWaveCommand());
                    }),
                    slice: "TutorialPhaseClear");
                timeline.Enqueue(new ResolveBatchStep(clearGate));
                timeline.Enqueue(new PresentStep(clearGate, mBoardChannel, channelName: "TutorialPhaseClear"));

                var setupGate = PresentationSyncBatchGate.FromSync(
                    sync,
                    () => ResolveAndProject(() => mDispatcher.Send(new TutorialSetupPhaseCommand(mPhase))),
                    slice: "TutorialPhaseSetup");
                timeline.Enqueue(new ResolveBatchStep(setupGate));
                timeline.Enqueue(new PresentStep(setupGate, mBoardChannel, channelName: "TutorialPhaseSetup"));
                timeline.Enqueue(new TutorialCallbackStep(() =>
                {
                    mTransitionQueued = false;
                    ScanPhase();
                    ReleasePlayerInput();
                }));
            });
        }

        private void CompleteTutorial()
        {
            mSuppressDefeatEnd = false;
            SetAvatarRangePinned(false);
            Debug.Log("[Tutorial] 第五阶段通关：钢铁莱姆已击败。");
            mArchitecture.SendEvent(new BattleSessionSettlementReadyEvent());
        }

        private void ScanPhase()
        {
            mTrackedUids.Clear();
            mDummyUid = 0;
            mActionDummyUid = 0;
            mMoveDummyUid = 0;
            mSteelSlimeUid = 0;
            mKnifeBoardUid = 0;
            mPotionBoardUid = 0;

            var board = mArchitecture.GetModel<BoardModel>();
            var deck = mArchitecture.GetModel<DeckModel>();
            var registry = mArchitecture.GetModel<CardRegistry>();

            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                RegisterCard(registry, board.GetCardUid(SlotId.Board(i)));
            }

            var pile = deck.DrawPileUids;
            for (var i = 0; i < pile.Count; i++)
            {
                RegisterCard(registry, pile[i]);
            }

            var items = deck.ItemSlotUids;
            for (var i = 0; i < items.Count; i++)
            {
                RegisterCard(registry, items[i]);
            }

            mLegality.ApplyPhase(mPhase);
            mPhaseReady = true;
            Debug.Log($"[Tutorial] 第{mPhase}阶段就位");
        }

        private void RegisterCard(CardRegistry registry, int uid)
        {
            if (uid <= 0 || !registry.TryGet(uid, out var card) || card == null)
            {
                return;
            }

            mTrackedUids.Add(uid);
            var defId = card.DefId ?? string.Empty;
            if (defId == TutorialContentIds.DummyTrapDefId && !IsDeadOrGone(uid))
            {
                mDummyUid = uid;
                return;
            }

            if (defId == TutorialContentIds.ActionDummyDefId && !IsDeadOrGone(uid))
            {
                mActionDummyUid = uid;
                return;
            }

            if (defId == TutorialContentIds.MoveDummyDefId && !IsDeadOrGone(uid))
            {
                mMoveDummyUid = uid;
                return;
            }

            if (defId == TutorialContentIds.SteelSlimeDefId && !IsDeadOrGone(uid))
            {
                mSteelSlimeUid = uid;
                return;
            }

            if (defId == TutorialContentIds.KnifeDefId && card.Zone.Value == ZoneId.Board)
            {
                mKnifeBoardUid = uid;
                return;
            }

            if (defId == TutorialContentIds.PotionDefId && card.Zone.Value == ZoneId.Board)
            {
                mPotionBoardUid = uid;
            }
        }

        private bool ArePhase4ItemsPickedUp()
        {
            return HasItemInSlots(TutorialContentIds.KnifeDefId)
                   && HasItemInSlots(TutorialContentIds.PotionDefId);
        }

        private bool HasItemInSlots(string defId)
        {
            var deck = mArchitecture.GetModel<DeckModel>();
            var registry = mArchitecture.GetModel<CardRegistry>();
            var items = deck.ItemSlotUids;
            for (var i = 0; i < items.Count; i++)
            {
                var uid = items[i];
                if (uid > 0 && registry.TryGet(uid, out var card) && card != null && card.DefId == defId)
                {
                    return true;
                }
            }

            return false;
        }

        private int ResolveLastDummyDeathSlot()
        {
            var board = mArchitecture.GetModel<BoardModel>();
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                if (board.IsEmpty(SlotId.Board(i)))
                {
                    return i;
                }
            }

            return 9;
        }

        private bool IsDeadOrGone(int uid)
        {
            if (uid <= 0)
            {
                return true;
            }

            var registry = mArchitecture.GetModel<CardRegistry>();
            if (!registry.TryGet(uid, out var card) || card == null)
            {
                return true;
            }

            var zone = card.Zone.Value;
            return zone == ZoneId.Graveyard || zone == ZoneId.Removed;
        }

        private void HoldPlayerInput()
        {
            if (!mInputHeld)
            {
                mInputHeld = true;
            }

            if (!PresentationInputGates.OpeningPresentationActive)
            {
                PresentationInputGates.SetOpening(true);
            }
        }

        private void ReleasePlayerInput()
        {
            if (!mInputHeld)
            {
                return;
            }

            mInputHeld = false;
            if (PresentationInputGates.OpeningPresentationActive)
            {
                PresentationInputGates.SetOpening(false);
            }
        }

        private void SetAvatarRangePinned(bool pinned)
        {
            BoardRangeGlowFx.SetAvatarRangePinned(mGlowHost, pinned);
        }

        private CoreCommandDispatchResult ResolveAndProject(Func<CoreCommandDispatchResult> resolve)
        {
            var pipeline = mArchitecture.GetSystem<IActionPipelineSystem>();
            var startIndex = pipeline.EventLog.Entries.Count;
            var dispatch = resolve();
            if (dispatch == null || !dispatch.Accepted)
            {
                return dispatch;
            }

            OnTutorialBatchProjected(
                startIndex,
                ResolveAvatarSlotIndex(),
                IntentBatchProjection.Build(mArchitecture, pipeline, startIndex));
            return dispatch;
        }

        private void OnTutorialBatchProjected(
            int startIndex,
            int boardSlot,
            PostKillBoardPresentationResult result)
        {
            mBoardChannel.Enqueue(result);
            mSession.PresentShuffleIntoDeckFromEventLog(startIndex);
        }

        private int ResolveAvatarSlotIndex()
        {
            var board = mArchitecture.GetModel<BoardModel>();
            var slot = board.AvatarSlot.Value;
            return slot.IsBoardSlot ? slot.Index : 5;
        }
    }
}

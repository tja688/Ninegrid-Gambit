using System;
using System.Collections.Generic;
using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NineGrid.Flow.Diagnostics;
using NineGrid.Flow.Presentation;
using NUnit.Framework;
using QFramework;
using NineGrid.Flow;
using NineGrid.Cards;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// #7 融合/合成：Rotate Present 后伴随补牌升格为独立 Resolve/Present 批次，禁止 in-flight FillEmptySlots。
    /// </summary>
    public sealed class FusionVerticalSliceTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);
        private static readonly SlotId sFarCornerSlot = SlotId.Board(1);
        private static readonly SlotId sHeadlessSlot = SlotId.Board(4);
        private static readonly SlotId sSkullSlot = SlotId.Board(7);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;
        private IPresentationSyncSystem mSync;
        private CoreCommandDispatcher mDispatcher;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            DirectorTrace.Reset();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(
                ContentConfigKeys.DefaultCatalog,
                ContentCatalogBootstrap.Load());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 19UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
            mSync = mArch.GetSystem<IPresentationSyncSystem>();
            mDispatcher = new CoreCommandDispatcher(mArch);
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void ExploreIntent_RotateFusion_Lockstep_FusionRefillIsSeparateBatchAfterRotatePresent()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 99, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sFarCornerSlot);
            SpawnOnBoard("monster.headless_skeleton", sHeadlessSlot);
            SpawnOnBoard("monster.skull_head", sSkullSlot);
            // Click 后 Fill 会吃掉若干张；多备牌堆确保融合后仍有非结果候选。
            SeedDrawPileFiller("monster.headless_skeleton", count: 16);
            Assert.IsTrue(mArch.GetModel<BoardModel>().IsEmpty(sAdjacentSlot));

            var boardPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var factory = new ExploreIntentScriptFactory(mArch, mDispatcher, boardPresent);
            var director = new PresentationDirector(factory);

            bool preview;
            Assert.IsTrue(director.TrySubmitIntent(
                new InputIntent(InputIntentKinds.Explore, sAdjacentSlot.Index),
                out preview));
            Assert.IsFalse(preview);
            Assert.IsTrue(director.IsMainlineBusy);
            var chainId = DirectorTrace.CurrentChainId;
            Assert.Greater(chainId, 0, "Explore intent 应分配 chainId");

            // ClickEmpty resolve + present
            director.Tick(0.016f);
            Assert.AreEqual(1, mSync.ActiveBatchId, "ClickEmpty 应打开第 1 批");
            Assert.AreEqual(chainId, DirectorTrace.CurrentChainId);
            director.Tick(0.016f);
            Assert.AreEqual(0, mSync.ActiveBatchId);
            Assert.AreEqual(1, boardPresent.BeginCount);

            // Fill resolve + present
            director.Tick(0.016f);
            Assert.AreEqual(2, mSync.ActiveBatchId);
            director.Tick(0.016f);
            Assert.AreEqual(0, mSync.ActiveBatchId);
            Assert.AreEqual(2, boardPresent.BeginCount);

            // Rotate resolve：应含融合，但不得 FillEmptySlots
            var rotateStart = mPipeline.EventLog.Entries.Count;
            director.Tick(0.016f);
            Assert.AreEqual(3, mSync.ActiveBatchId);
            Assert.AreEqual(chainId, DirectorTrace.CurrentChainId, "融合 batch 应继承同一 chainId");
            Assert.IsTrue(HasFusionSince(rotateStart), "Rotate 批应触发骷髅融合");
            Assert.IsFalse(ContainsTypeSince(rotateStart, CoreEventType.SlotsFilled),
                "融合伴随补牌不得在 Rotate 解算批内 in-flight 写入");

            // Rotate present ack
            director.Tick(0.016f);
            Assert.AreEqual(0, mSync.ActiveBatchId);
            Assert.AreEqual(3, boardPresent.BeginCount);

            // Branch 入队融合补牌（本 Tick 只入队）
            director.Tick(0.016f);
            Assert.AreEqual(0, mSync.ActiveBatchId);
            Assert.AreEqual(3, boardPresent.BeginCount);
            Assert.AreEqual(chainId, DirectorTrace.CurrentChainId, "defer 入队后 chainId 不得因新 batch 重分配");

            var deck = mArch.GetModel<DeckModel>();
            var board = mArch.GetModel<BoardModel>();
            Assert.IsTrue(FusionRefillPlanner.HasEmptyBoardSlot(board), "融合后盘面应有空位");
            Assert.IsTrue(
                FusionRefillPlanner.HasRefillCandidateExcluding(deck, CollectFusionResultUidsSince(rotateStart)),
                "牌堆应有非合体结果的补牌候选");

            // Fusion refill resolve：离散 SlotsFilled 批次
            var refillStart = mPipeline.EventLog.Entries.Count;
            director.Tick(0.016f);
            Assert.AreEqual(4, mSync.ActiveBatchId);
            Assert.AreEqual(chainId, DirectorTrace.CurrentChainId,
                "融合 batch 与 defer 补牌 batch 须带同一 chainId");
            Assert.IsTrue(ContainsTypeSince(refillStart, CoreEventType.SlotsFilled),
                "融合补牌须在独立 Resolve 批次写入 Core");

            // Fusion refill present ack → idle
            director.Tick(0.016f);
            Assert.AreEqual(0, mSync.ActiveBatchId);
            Assert.AreEqual(4, boardPresent.BeginCount);
            Assert.IsFalse(director.IsMainlineBusy);
            Assert.AreEqual(0, DirectorTrace.CurrentChainId, "脚本跑空后 chainId 归零");
        }

        [Test]
        public void ExploreIntent_NoFusion_DoesNotEnqueueFusionRefill()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 99, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sFarCornerSlot);
            Assert.IsTrue(mArch.GetModel<BoardModel>().IsEmpty(sAdjacentSlot));

            var boardPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var factory = new ExploreIntentScriptFactory(mArch, mDispatcher, boardPresent);
            var director = new PresentationDirector(factory);

            bool preview;
            Assert.IsTrue(director.TrySubmitIntent(
                new InputIntent(InputIntentKinds.Explore, sAdjacentSlot.Index),
                out preview));

            for (var i = 0; i < 12; i++)
            {
                director.Tick(0.016f);
                if (!director.IsMainlineBusy)
                {
                    break;
                }
            }

            Assert.IsFalse(director.IsMainlineBusy);
            Assert.AreEqual(3, boardPresent.BeginCount, "无融合时仅 Click/Fill/Rotate 三次 Present");
        }

        [Test]
        public void ResolveAndProject_WhenNoEmptySlots_SkipFillAccepted()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 99, attack: 0)).Accepted);
            FillAllEmptyBoardSlots("monster.headless_skeleton");
            SeedDrawPileFiller("monster.headless_skeleton", count: 4);
            Assert.IsFalse(FusionRefillPlanner.HasEmptyBoardSlot(mArch.GetModel<BoardModel>()));

            var scheduler = new FusionRefillScheduler();
            var before = mPipeline.EventLog.Entries.Count;
            var dispatch = scheduler.ResolveAndProject(
                mArch,
                mDispatcher,
                boardSlot: 2,
                excludeResultUids: new List<int>(),
                onBoardBatchProjected: null);
            Assert.IsNotNull(dispatch);
            Assert.IsTrue(dispatch.Accepted);
            Assert.IsFalse(ContainsTypeSince(before, CoreEventType.SlotsFilled));
        }

        [Test]
        public void ResolveAndProject_WhenEmptyAndOnlyExcludedCandidates_FillsWithFallback()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 99, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sFarCornerSlot);
            Assert.IsTrue(mArch.GetModel<BoardModel>().IsEmpty(sAdjacentSlot));

            // 牌堆只放一张，并将其列入 exclude → 旧逻辑会 skipFill；新逻辑回退补牌。
            SeedDrawPileFiller("monster.headless_skeleton", count: 1);
            var deck = mArch.GetModel<DeckModel>();
            Assert.AreEqual(1, deck.DrawPileUids.Count);
            var onlyUid = deck.DrawPileUids[0];
            var exclude = new List<int> { onlyUid };
            Assert.IsFalse(FusionRefillPlanner.HasRefillCandidateExcluding(deck, exclude));
            Assert.IsTrue(FusionRefillPlanner.HasEmptyBoardSlot(mArch.GetModel<BoardModel>()));

            var scheduler = new FusionRefillScheduler();
            var before = mPipeline.EventLog.Entries.Count;
            var projected = false;
            var dispatch = scheduler.ResolveAndProject(
                mArch,
                mDispatcher,
                boardSlot: sAdjacentSlot.Index,
                excludeResultUids: exclude,
                onBoardBatchProjected: (start, slot, result) => projected = result.Accepted);
            Assert.IsNotNull(dispatch);
            Assert.IsTrue(dispatch.Accepted);
            Assert.IsTrue(ContainsTypeSince(before, CoreEventType.SlotsFilled),
                "有空槽且牌堆非空时不得因 exclude 静默 skipFill");
            Assert.IsTrue(projected);
        }

        [Test]
        public void Aftermath_TrySchedule_WhenGateNotArmed_EnqueuesFusionRefillOnMainline()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 99, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sFarCornerSlot);
            Assert.IsTrue(mArch.GetModel<BoardModel>().IsEmpty(sAdjacentSlot));
            SeedDrawPileFiller("monster.headless_skeleton", count: 8);

            var boardPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var noopFactory = new NoopIntentScriptFactory();
            var director = new PresentationDirector(noopFactory);
            DirectorTrace.BeginChain();
            var scheduler = new FusionRefillScheduler();

            FusionRefillAftermath.Register(exclude =>
            {
                director.MutateMainline(timeline =>
                {
                    scheduler.EnqueueRefillBatches(
                        timeline,
                        mArch,
                        mDispatcher,
                        boardPresent,
                        sAdjacentSlot.Index,
                        exclude,
                        onBoardBatchProjected: null);
                    FusionRefillScheduler.DisarmRefillGate();
                });
            });

            try
            {
                Assert.IsFalse(FusionRefillScheduler.IsRefillGateArmed);
                Assert.IsTrue(FusionRefillAftermath.TrySchedule(Array.Empty<int>()));
                Assert.IsTrue(FusionRefillScheduler.IsRefillScheduled);
                Assert.IsFalse(FusionRefillScheduler.IsRefillGateArmed);
                Assert.IsTrue(director.IsMainlineBusy);

                var refillStart = mPipeline.EventLog.Entries.Count;
                director.Tick(0.016f);
                Assert.AreEqual(1, mSync.ActiveBatchId);
                Assert.IsTrue(ContainsTypeSince(refillStart, CoreEventType.SlotsFilled));
                director.Tick(0.016f);
                Assert.AreEqual(1, boardPresent.BeginCount);
                Assert.IsFalse(director.IsMainlineBusy);
            }
            finally
            {
                FusionRefillAftermath.Unregister();
            }
        }

        [Test]
        public void Aftermath_TrySchedule_WhenGateArmedButNotScheduled_EnqueuesOnce()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 99, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sFarCornerSlot);
            Assert.IsTrue(mArch.GetModel<BoardModel>().IsEmpty(sAdjacentSlot));
            SeedDrawPileFiller("monster.headless_skeleton", count: 8);

            var boardPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var director = new PresentationDirector(new NoopIntentScriptFactory());
            DirectorTrace.BeginChain();
            var scheduler = new FusionRefillScheduler();

            FusionRefillAftermath.Register(exclude =>
            {
                director.MutateMainline(timeline =>
                {
                    scheduler.EnqueueRefillBatches(
                        timeline,
                        mArch,
                        mDispatcher,
                        boardPresent,
                        sAdjacentSlot.Index,
                        exclude,
                        onBoardBatchProjected: null);
                    FusionRefillScheduler.DisarmRefillGate();
                });
            });

            try
            {
                FusionRefillScheduler.ArmRefillGate();
                Assert.IsTrue(FusionRefillScheduler.IsRefillGateArmed);
                Assert.IsFalse(FusionRefillScheduler.IsRefillScheduled);

                Assert.IsTrue(FusionRefillAftermath.TrySchedule(Array.Empty<int>()));
                Assert.IsTrue(FusionRefillScheduler.IsRefillScheduled);
                Assert.IsFalse(FusionRefillScheduler.IsRefillGateArmed);
                Assert.IsFalse(
                    FusionRefillAftermath.TrySchedule(Array.Empty<int>()),
                    "已 MarkRefillScheduled 后不得二次入队");

                director.Tick(0.016f);
                director.Tick(0.016f);
                Assert.AreEqual(1, boardPresent.BeginCount);
            }
            finally
            {
                FusionRefillAftermath.Unregister();
            }
        }

        [Test]
        public void AppendAfter_WhenAlreadyScheduled_DoesNotDoubleEnqueue()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 99, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sFarCornerSlot);
            Assert.IsTrue(mArch.GetModel<BoardModel>().IsEmpty(sAdjacentSlot));
            SeedDrawPileFiller("monster.headless_skeleton", count: 8);

            var boardPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var director = new PresentationDirector(new NoopIntentScriptFactory());
            DirectorTrace.BeginChain();
            var scheduler = new FusionRefillScheduler();
            var appendEnqueueCalls = 0;

            director.MutateMainline(timeline =>
            {
                scheduler.EnqueueRefillBatches(
                    timeline,
                    mArch,
                    mDispatcher,
                    boardPresent,
                    sAdjacentSlot.Index,
                    Array.Empty<int>(),
                    onBoardBatchProjected: null);
                FusionRefillScheduler.DisarmRefillGate();
            });
            Assert.IsTrue(FusionRefillScheduler.IsRefillScheduled);

            director.MutateMainline(timeline =>
            {
                scheduler.AppendAfterRotatePresent(
                    timeline,
                    () => true,
                    t =>
                    {
                        appendEnqueueCalls++;
                        scheduler.EnqueueRefillBatches(
                            t,
                            mArch,
                            mDispatcher,
                            boardPresent,
                            sAdjacentSlot.Index,
                            Array.Empty<int>(),
                            onBoardBatchProjected: null);
                    });
            });

            // Resolve + Present FusionRefill，再跑 AppendAfter 分支
            director.Tick(0.016f);
            director.Tick(0.016f);
            director.Tick(0.016f);
            Assert.AreEqual(0, appendEnqueueCalls, "已 scheduled 时 AppendAfter 不得再 enqueue");
            Assert.AreEqual(1, boardPresent.BeginCount);
            Assert.IsFalse(director.IsMainlineBusy);
        }

        [Test]
        public void TryCollectResultUids_ClearIntoFalse_PreservesPriorOnMiss()
        {
            var into = new List<int> { 42 };
            Assert.IsFalse(FusionRefillPlanner.TryCollectResultUids(
                mPipeline.EventLog.Entries,
                startIndex: 0,
                into,
                clearInto: false));
            Assert.AreEqual(1, into.Count);
            Assert.AreEqual(42, into[0]);

            Assert.IsFalse(FusionRefillPlanner.TryCollectResultUids(
                mPipeline.EventLog.Entries,
                startIndex: 0,
                into,
                clearInto: true));
            Assert.AreEqual(0, into.Count);
        }

        private sealed class NoopIntentScriptFactory : IIntentScriptFactory
        {
            public void BuildScript(InputIntent intent, BattleTimeline timeline)
            {
            }
        }

        private bool HasFusionSince(int startIndex)
        {
            var fusions = SkeletonFusionPresentationScanner.Collect(mPipeline.EventLog.Entries, startIndex);
            return fusions.Count > 0;
        }

        private List<int> CollectFusionResultUidsSince(int startIndex)
        {
            var uids = new List<int>(2);
            FusionRefillPlanner.TryCollectResultUids(mPipeline.EventLog.Entries, startIndex, uids);
            return uids;
        }

        private bool ContainsTypeSince(int startIndex, CoreEventType type)
        {
            var entries = mPipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                if (entries[i].Type == type)
                {
                    return true;
                }
            }

            return false;
        }

        private static NodeDeckOptions CreateSingleMonsterNode(int hp, int attack)
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1
            }.AddEnemyCard(new CardDraft("monster.test", CardKind.Monster) { MaxHp = hp, Attack = attack });
        }

        private void PlaceSoleBoardCardAt(SlotId targetSlot)
        {
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            CardInstance sole = null;
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                if (slot == board.AvatarSlot.Value)
                {
                    continue;
                }

                var uid = board.GetCardUid(slot);
                if (uid == 0)
                {
                    continue;
                }

                Assert.IsNull(sole, "Expected at most one non-avatar board card for relocate helper.");
                sole = registry.Get(uid);
            }

            Assert.IsNotNull(sole, "No board card to relocate.");
            if (sole.Slot.Value == targetSlot)
            {
                return;
            }

            board.ClearSlot(sole.Slot.Value);
            board.PlaceCard(sole, targetSlot);
        }

        private void SpawnOnBoard(string defId, SlotId slot)
        {
            Assert.IsTrue(mArch.GetModel<BoardModel>().IsEmpty(slot), "Spawn 目标格应为空: " + slot);
            mPipeline.Enqueue(new SpawnCardAction(defId, CardKind.Monster, ZoneId.Board, slot, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0, "应成功生成 " + defId);
        }

        private void FillAllEmptyBoardSlots(string defId)
        {
            var board = mArch.GetModel<BoardModel>();
            var avatarSlot = board.AvatarSlot.Value;
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                if (slot == avatarSlot || !board.IsEmpty(slot))
                {
                    continue;
                }

                SpawnOnBoard(defId, slot);
            }
        }

        private void SeedDrawPileFiller(string defId, int count)
        {
            for (var i = 0; i < count; i++)
            {
                mPipeline.Enqueue(new SpawnCardAction(defId, CardKind.Monster, ZoneId.DrawPile, SlotId.None, 1, "test"));
                Assert.Greater(mPipeline.RunToCompletion(), 0);
            }
        }

        private sealed class RecordingPresentChannel : IPresentChannel
        {
            private readonly int mTicksUntilComplete;
            private int mTicks;
            private bool mBegan;

            public RecordingPresentChannel(int ticksUntilComplete)
            {
                mTicksUntilComplete = ticksUntilComplete;
            }

            public int BeginCount { get; private set; }
            public readonly List<int> PresentedBatchIds = new List<int>();
            public int ActiveBatchId { get; private set; }

            public bool IsComplete
            {
                get { return mBegan && mTicks >= mTicksUntilComplete; }
            }

            public void Begin(int batchId)
            {
                mBegan = true;
                mTicks = 0;
                ActiveBatchId = batchId;
                BeginCount++;
                PresentedBatchIds.Add(batchId);
            }

            public void Tick(float deltaTime)
            {
                if (mBegan)
                {
                    mTicks++;
                }
            }
        }
    }
}

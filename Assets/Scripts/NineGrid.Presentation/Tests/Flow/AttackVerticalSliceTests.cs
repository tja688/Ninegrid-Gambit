using System.Collections.Generic;
using NineGrid.Cards;
using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NineGrid.Flow.Presentation;
using NUnit.Framework;
using QFramework;
using NineGrid.Flow;
using NineGrid.Cards;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// #5 攻击→击杀补牌旋转：CombatHit → Present → Fill → Present → Rotate → Present 批次锁步。
    /// </summary>
    public sealed class AttackVerticalSliceTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;
        private IPresentationSyncSystem mSync;
        private CoreCommandDispatcher mDispatcher;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 42UL });
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
        public void AttackIntent_Kill_Lockstep_HitThenStabilizeThenRotate_AcksBetweenBatches()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);

            var hitPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var boardPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var factory = new AttackIntentScriptFactory(mArch, mDispatcher, hitPresent, boardPresent);
            var director = new PresentationDirector(factory);

            bool preview;
            Assert.IsTrue(director.TrySubmitIntent(
                new InputIntent(InputIntentKinds.Attack, sAdjacentSlot.Index),
                out preview));
            Assert.IsFalse(preview);
            Assert.IsTrue(director.IsMainlineBusy);

            // Resolve CombatHit
            var hitStart = mPipeline.EventLog.Entries.Count;
            director.Tick(0.016f);
            Assert.AreEqual(1, mSync.ActiveBatchId);
            Assert.AreEqual(0, hitPresent.BeginCount);
            Assert.IsFalse(ContainsTypeSince(hitStart, CoreEventType.BoardRotated));
            Assert.IsTrue(ContainsTypeSince(hitStart, CoreEventType.CardKilled));

            // Present hit ack
            director.Tick(0.016f);
            Assert.AreEqual(0, mSync.ActiveBatchId);
            Assert.AreEqual(1, hitPresent.BeginCount);

            // Branch enqueues aftermath（本 Tick 只入队，不推进互动/Fill）
            director.Tick(0.016f);
            Assert.AreEqual(0, mSync.ActiveBatchId);
            Assert.AreEqual(0, boardPresent.BeginCount);

            // Resolve interaction advance（击杀路径须独立开批，OnInteract 效果才进 PresentationBatch）
            var interactStart = mPipeline.EventLog.Entries.Count;
            director.Tick(0.016f);
            Assert.AreEqual(2, mSync.ActiveBatchId);
            Assert.IsTrue(ContainsTypeSince(interactStart, CoreEventType.InteractionChanged));

            // Present interaction ack
            director.Tick(0.016f);
            Assert.AreEqual(0, mSync.ActiveBatchId);
            Assert.AreEqual(1, boardPresent.BeginCount);

            // Stable-state check schedules the first refill slice without resolving ahead.
            var fillStart = mPipeline.EventLog.Entries.Count;
            director.Tick(0.016f);
            Assert.AreEqual(0, mSync.ActiveBatchId);
            director.Tick(0.016f);
            Assert.AreEqual(3, mSync.ActiveBatchId);
            Assert.IsTrue(ContainsTypeSince(fillStart, CoreEventType.SlotsFilled));

            // Present fill ack
            director.Tick(0.016f);
            Assert.AreEqual(0, mSync.ActiveBatchId);
            Assert.AreEqual(2, boardPresent.BeginCount);

            // Stable check closes pre-rotate stabilization, then Rotate resolves.
            var rotateStart = mPipeline.EventLog.Entries.Count;
            director.Tick(0.016f);
            Assert.AreEqual(0, mSync.ActiveBatchId);
            director.Tick(0.016f);
            Assert.AreEqual(4, mSync.ActiveBatchId);
            Assert.IsTrue(ContainsTypeSince(rotateStart, CoreEventType.BoardRotated));

            // Present rotate ack
            director.Tick(0.016f);
            Assert.AreEqual(0, mSync.ActiveBatchId);
            Assert.AreEqual(3, boardPresent.BeginCount);

            for (var i = 0; i < 4 && director.IsMainlineBusy; i++)
            {
                director.Tick(0.016f);
            }
            Assert.IsFalse(director.IsMainlineBusy);
        }

        [Test]
        public void AdvanceInteractionCount_DispatcherBatch_IncludesAmbushEffectTriggered()
        {
            mArch.GetUtility<IConfigUtility>().Set(
                ContentConfigKeys.DefaultCatalog,
                ContentCatalogBootstrap.Load());
            mArch.GetSystem<IContentSystem>().Load(ContentCatalogBootstrap.Load());

            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 99, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var hostUid = board.GetCardUid(sAdjacentSlot);
            var host = registry.Get(hostUid);
            host.Stats.SetBase(StatId.Attack, 3);
            Assert.Greater(
                mArch.GetSystem<IContentSystem>().ActivateSkillsOnCard(host, new[] { "skill.ambush_melee" }).Count,
                0);
            if (mPipeline.PendingCount > 0)
            {
                Assert.Greater(mPipeline.RunToCompletion(), 0);
            }

            var avatar = registry.Get(board.AvatarUid.Value);
            avatar.Stats.SetBase(StatId.Hp, 99);

            for (var i = 0; i < 4; i++)
            {
                Assert.IsTrue(mPhase.AdvanceInteractionCount().Accepted);
            }

            var dispatch = mDispatcher.Send(new AdvanceInteractionCountCommand());
            Assert.IsTrue(dispatch.Accepted);
            Assert.IsTrue(dispatch.BatchOpened);
            Assert.IsTrue(
                BatchContainsEventType(dispatch.Batch, CoreEventType.EffectTriggered),
                "潜伏近战触发批应含 EffectTriggered（效果触发脉冲）");
            Assert.IsTrue(
                BatchContainsEventType(dispatch.Batch, CoreEventType.DamageDealt),
                "潜伏近战触发批应含 DamageDealt");
        }

        [Test]
        public void AttackIntent_NonKill_Lockstep_CounterOnMainline_NoFillRotate()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 99, attack: 3)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);
            // 压低 Avatar 攻击，确保本拍不击杀。
            var board = mArch.GetModel<BoardModel>();
            var avatar = mArch.GetModel<CardRegistry>().Get(board.AvatarUid.Value);
            avatar.Stats.SetBase(StatId.Attack, 1);

            var counterProjected = false;
            var hitPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var boardPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var counterPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var factory = new AttackIntentScriptFactory(
                mArch,
                mDispatcher,
                hitPresent,
                boardPresent,
                counterPresent,
                onCounterBatchProjected: (start, slot, attackerUid, result) =>
                {
                    counterProjected = true;
                    Assert.IsTrue(result.Accepted);
                    Assert.AreEqual(board.GetCardUid(sAdjacentSlot), attackerUid);
                });
            var director = new PresentationDirector(factory);

            bool preview;
            Assert.IsTrue(director.TrySubmitIntent(
                new InputIntent(InputIntentKinds.Attack, sAdjacentSlot.Index),
                out preview));

            var hitStart = mPipeline.EventLog.Entries.Count;
            director.Tick(0.016f); // resolve hit
            director.Tick(0.016f); // present hit
            director.Tick(0.016f); // branch → enqueue counter
            Assert.IsTrue(director.IsMainlineBusy);
            Assert.AreEqual(0, boardPresent.BeginCount);
            Assert.IsFalse(ContainsTypeSince(hitStart, CoreEventType.BoardRotated));
            Assert.IsFalse(ContainsTypeSince(hitStart, CoreEventType.SlotsFilled));

            var counterStart = mPipeline.EventLog.Entries.Count;
            director.Tick(0.016f); // resolve counter CombatHit
            Assert.IsTrue(counterProjected);
            Assert.AreEqual(2, mSync.ActiveBatchId);
            Assert.IsTrue(ContainsTypeSince(counterStart, CoreEventType.DamageDealt));
            Assert.AreEqual(0, counterPresent.BeginCount);

            director.Tick(0.016f); // present counter ack
            Assert.AreEqual(0, mSync.ActiveBatchId);
            Assert.AreEqual(1, counterPresent.BeginCount);
            director.Tick(0.016f); // branch after counter → enqueue interaction advance
            for (var i = 0; i < 6 && director.IsMainlineBusy; i++)
            {
                director.Tick(0.016f);
            }
            Assert.IsFalse(director.IsMainlineBusy);
            Assert.IsFalse(ContainsTypeSince(hitStart, CoreEventType.SlotsFilled));
            Assert.IsFalse(ContainsTypeSince(hitStart, CoreEventType.BoardRotated));
        }

        [Test]
        public void AttackIntent_RangedWeaponMonster_NoCounterBatch()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 99, attack: 5)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var avatar = registry.Get(board.AvatarUid.Value);
            avatar.Stats.SetBase(StatId.Attack, 1);

            var monster = registry.Get(board.GetCardUid(sAdjacentSlot));
            var avatarHpBefore = (int)avatar.Stats.GetBase(StatId.Hp);
            mArch.GetSystem<IContentSystem>().Load(ContentCatalogBootstrap.Load());
            Assert.Greater(
                mArch.GetSystem<IContentSystem>().ActivateSkillsOnCard(monster, new[] { "skill.ranged_weapon" }).Count,
                0);
            if (mPipeline.PendingCount > 0)
            {
                Assert.Greater(mPipeline.RunToCompletion(), 0);
            }

            var counterProjected = false;
            var hitPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var boardPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var counterPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var factory = new AttackIntentScriptFactory(
                mArch,
                mDispatcher,
                hitPresent,
                boardPresent,
                counterPresent,
                onCounterBatchProjected: (start, slot, attackerUid, result) =>
                {
                    counterProjected = true;
                });
            var director = new PresentationDirector(factory);

            bool preview;
            Assert.IsTrue(director.TrySubmitIntent(
                new InputIntent(InputIntentKinds.Attack, sAdjacentSlot.Index),
                out preview));

            director.Tick(0.016f); // resolve hit
            director.Tick(0.016f); // present hit
            director.Tick(0.016f); // branch → 远程武器跳过反击批，直接入队互动推进
            Assert.IsFalse(counterProjected, "远程武器怪不得入队反击批");
            Assert.AreEqual(0, counterPresent.BeginCount);
            for (var i = 0; i < 6 && director.IsMainlineBusy; i++)
            {
                director.Tick(0.016f);
            }
            Assert.IsFalse(director.IsMainlineBusy);
            Assert.AreEqual(1, mArch.GetModel<PlayerModel>().InteractionCount.Value);
            Assert.AreEqual(avatarHpBefore, (int)avatar.Stats.GetBase(StatId.Hp), "远程武器怪反击段不得伤玩家");
        }

        [Test]
        public void AttackIntent_NonKill_BufferedIntent_DoesNotFlushUntilCounterPresentDone()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 99, attack: 1)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);
            var board = mArch.GetModel<BoardModel>();
            var avatar = mArch.GetModel<CardRegistry>().Get(board.AvatarUid.Value);
            avatar.Stats.SetBase(StatId.Attack, 1);

            var hitPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var boardPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var counterPresent = new RecordingPresentChannel(ticksUntilComplete: 2);
            var factory = new AttackIntentScriptFactory(
                mArch,
                mDispatcher,
                hitPresent,
                boardPresent,
                counterPresent);
            var director = new PresentationDirector(factory);

            bool preview;
            Assert.IsTrue(director.TrySubmitIntent(
                new InputIntent(InputIntentKinds.Attack, sAdjacentSlot.Index),
                out preview));

            director.Tick(0.016f); // resolve hit
            director.Tick(0.016f); // present hit
            // 反击 Present 前缓冲另一意图
            Assert.IsTrue(director.TrySubmitIntent(
                new InputIntent(InputIntentKinds.Explore, sAdjacentSlot.Index),
                out preview));
            Assert.IsTrue(preview);
            Assert.IsTrue(director.HasBufferedIntent);

            director.Tick(0.016f); // branch → counter
            director.Tick(0.016f); // resolve counter
            director.Tick(0.016f); // present counter begin (needs 2 ticks)
            Assert.IsTrue(director.IsMainlineBusy);
            Assert.IsTrue(director.HasBufferedIntent);
            Assert.AreEqual(1, counterPresent.BeginCount);

            director.Tick(0.016f); // counter present complete
            Assert.IsTrue(director.IsMainlineBusy, "分支步仍占主线，缓冲尚未冲刷");
            Assert.IsTrue(director.HasBufferedIntent);
            director.Tick(0.016f); // branch after counter → enqueue advance
            Assert.IsTrue(director.IsMainlineBusy, "计数推进仍占主线，缓冲尚未冲刷");
            Assert.IsTrue(director.HasBufferedIntent);
            director.Tick(0.016f); // resolve interaction count
            Assert.IsTrue(director.HasBufferedIntent, "互动计数批尚未 Present，不得提前 flush");
            director.Tick(0.016f); // present interaction count
            Assert.IsTrue(director.HasBufferedIntent, "稳定化检查尚在主线，不得提前 flush");
            for (var i = 0; i < 5 && director.HasBufferedIntent; i++)
            {
                director.Tick(0.016f);
            }
            Assert.IsFalse(director.HasBufferedIntent);
        }

        [Test]
        public void AttackScriptFactory_IgnoresNonAttackIntent()
        {
            var hitPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var boardPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var factory = new AttackIntentScriptFactory(mArch, mDispatcher, hitPresent, boardPresent);
            var timeline = new BattleTimeline();
            factory.BuildScript(new InputIntent(InputIntentKinds.Explore, 2), timeline);
            Assert.IsFalse(timeline.IsBusy);
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

        private static bool BatchContainsEventType(PresentationBatch batch, CoreEventType type)
        {
            if (batch?.Instructions == null)
            {
                return false;
            }

            for (var i = 0; i < batch.Instructions.Count; i++)
            {
                var instruction = batch.Instructions[i];
                if (instruction?.Event != null && instruction.Event.Type == type)
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

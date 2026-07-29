using System.Collections.Generic;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Queries;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 导演分拍攻击路径的先攻接线：怪物先出手时 Counter 批在前，玩家 Hit 批在后。
    /// </summary>
    public sealed class FirstStrikeAttackIntentTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);
        private static readonly SlotId sSlot6 = SlotId.Board(6);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;
        private IPresentationSyncSystem mSync;
        private IStatSystem mStats;
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
            mStats = mArch.GetSystem<IStatSystem>();
            mDispatcher = new CoreCommandDispatcher(mArch);
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void MonsterStrikesFirstQuery_DelegatesToPhase()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 10, attack: 1)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);

            var board = mArch.GetModel<BoardModel>();
            var avatarUid = board.AvatarUid.Value;
            var monsterUid = board.GetCardUid(sAdjacentSlot);
            GrantFirstStrike(monsterUid);

            Assert.AreEqual(
                mPhase.MonsterStrikesFirst(avatarUid, monsterUid),
                mArch.SendQuery(new MonsterStrikesFirstQuery(avatarUid, monsterUid)));
        }

        [Test]
        public void AttackIntent_MonsterFirstStrike_CounterThenPlayerHit_Order()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 99, attack: 2)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);

            var board = mArch.GetModel<BoardModel>();
            var avatarUid = board.AvatarUid.Value;
            var monsterUid = board.GetCardUid(sAdjacentSlot);
            mArch.GetModel<CardRegistry>().Get(avatarUid).Stats.SetBase(StatId.Attack, 1);
            GrantFirstStrike(monsterUid);

            var counterProjected = false;
            var hitProjected = false;
            var hitPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var boardPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var counterPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var factory = new AttackIntentScriptFactory(
                mArch,
                mDispatcher,
                hitPresent,
                boardPresent,
                counterPresent,
                onHitBatchProjected: (start, slot, uid, result) =>
                {
                    hitProjected = true;
                    Assert.IsTrue(counterProjected, "先攻时应先投影反击批");
                    Assert.AreEqual(monsterUid, uid);
                },
                onCounterBatchProjected: (start, slot, attackerUid, result) =>
                {
                    counterProjected = true;
                    Assert.IsFalse(hitProjected, "先攻首段应为怪物反击批");
                    Assert.AreEqual(monsterUid, attackerUid);
                    Assert.IsTrue(result.Accepted);
                });
            var director = new PresentationDirector(factory);

            bool preview;
            Assert.IsTrue(director.TrySubmitIntent(
                new InputIntent(InputIntentKinds.Attack, sAdjacentSlot.Index),
                out preview));

            var startIndex = mPipeline.EventLog.Entries.Count;
            director.Tick(0.016f); // resolve first-strike counter
            Assert.IsTrue(counterProjected);
            Assert.IsFalse(hitProjected);
            var actorsAfterFirst = CollectDamageActors(startIndex);
            Assert.AreEqual(1, actorsAfterFirst.Count);
            Assert.AreEqual(monsterUid, actorsAfterFirst[0]);

            director.Tick(0.016f); // present counter
            director.Tick(0.016f); // branch → enqueue player hit
            var replyStart = mPipeline.EventLog.Entries.Count;
            director.Tick(0.016f); // resolve player hit
            Assert.IsTrue(hitProjected);
            var replyActors = CollectDamageActors(replyStart);
            Assert.AreEqual(1, replyActors.Count);
            Assert.AreEqual(avatarUid, replyActors[0]);

            director.Tick(0.016f); // present hit
            director.Tick(0.016f); // post-hit branch (no kill) → enqueue interaction advance
            director.Tick(0.016f); // silent AdvanceInteractionCount
            Assert.IsFalse(director.IsMainlineBusy);
            Assert.AreEqual(1, mArch.GetModel<PlayerModel>().InteractionCount.Value);
            Assert.AreEqual(1, counterPresent.BeginCount);
            Assert.AreEqual(1, hitPresent.BeginCount);
            Assert.AreEqual(0, boardPresent.BeginCount);
        }

        [Test]
        public void AttackIntent_MonsterFirstStrikeLethal_NoPlayerReply()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 99, attack: 99)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var avatarUid = board.AvatarUid.Value;
            var monsterUid = board.GetCardUid(sAdjacentSlot);
            var avatar = registry.Get(avatarUid);
            avatar.Stats.SetBase(StatId.MaxHp, 3);
            avatar.Stats.SetBase(StatId.Hp, 3);
            avatar.Stats.SetBase(StatId.Armor, 0);
            GrantFirstStrike(monsterUid);

            var hitProjected = false;
            var hitPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var boardPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var counterPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var factory = new AttackIntentScriptFactory(
                mArch,
                mDispatcher,
                hitPresent,
                boardPresent,
                counterPresent,
                onHitBatchProjected: (start, slot, uid, result) => { hitProjected = true; });
            var director = new PresentationDirector(factory);

            bool preview;
            Assert.IsTrue(director.TrySubmitIntent(
                new InputIntent(InputIntentKinds.Attack, sAdjacentSlot.Index),
                out preview));

            var startIndex = mPipeline.EventLog.Entries.Count;
            director.Tick(0.016f); // resolve first-strike
            director.Tick(0.016f); // present counter
            director.Tick(0.016f); // branch → avatar defeated, enqueue interaction advance
            director.Tick(0.016f); // silent AdvanceInteractionCount
            Assert.IsFalse(hitProjected);
            Assert.IsFalse(director.IsMainlineBusy);
            Assert.AreEqual(1, mArch.GetModel<PlayerModel>().InteractionCount.Value);

            var actors = CollectDamageActors(startIndex);
            Assert.AreEqual(1, actors.Count);
            Assert.AreEqual(monsterUid, actors[0]);
            Assert.AreEqual(0, hitPresent.BeginCount);
            Assert.AreEqual(1, counterPresent.BeginCount);
        }

        [Test]
        public void AttackIntent_Slot6ConditionalFirstStrike_ThenLeavesSlot_PlayerFirst()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 99, attack: 2)).Accepted);
            PlaceSoleBoardCardAt(sSlot6);

            var board = mArch.GetModel<BoardModel>();
            var avatarUid = board.AvatarUid.Value;
            var monsterUid = board.GetCardUid(sSlot6);
            mArch.GetModel<CardRegistry>().Get(avatarUid).Stats.SetBase(StatId.Attack, 1);
            GrantConditionalFirstStrikeAtSlot(sSlot6);

            Assert.IsTrue(mPhase.MonsterStrikesFirst(avatarUid, monsterUid));

            PlaceSoleBoardCardAt(sAdjacentSlot);
            monsterUid = board.GetCardUid(sAdjacentSlot);
            Assert.IsFalse(mPhase.MonsterStrikesFirst(avatarUid, monsterUid));

            var counterFirst = false;
            var hitPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var boardPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var counterPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var factory = new AttackIntentScriptFactory(
                mArch,
                mDispatcher,
                hitPresent,
                boardPresent,
                counterPresent,
                onHitBatchProjected: (start, slot, uid, result) =>
                {
                    Assert.IsFalse(counterFirst, "离开格6后应玩家先打");
                },
                onCounterBatchProjected: (start, slot, attackerUid, result) =>
                {
                    counterFirst = true;
                });
            var director = new PresentationDirector(factory);

            bool preview;
            Assert.IsTrue(director.TrySubmitIntent(
                new InputIntent(InputIntentKinds.Attack, sAdjacentSlot.Index),
                out preview));

            var startIndex = mPipeline.EventLog.Entries.Count;
            director.Tick(0.016f); // resolve player hit
            var actors = CollectDamageActors(startIndex);
            Assert.AreEqual(1, actors.Count);
            Assert.AreEqual(avatarUid, actors[0]);
            Assert.IsFalse(counterFirst);

            director.Tick(0.016f); // present hit
            director.Tick(0.016f); // branch counter
            director.Tick(0.016f); // resolve counter
            Assert.IsTrue(counterFirst);
            director.Tick(0.016f); // present counter
            director.Tick(0.016f); // silent AdvanceInteractionCount
            Assert.IsFalse(director.IsMainlineBusy);
            Assert.AreEqual(1, mArch.GetModel<PlayerModel>().InteractionCount.Value);
        }

        private void GrantFirstStrike(int cardUid)
        {
            mStats.RuleModifiers.Add(new RuleModifier(
                RuleId.FirstStrike,
                ModifierOp.Override,
                1f,
                ModifierLayer.Persistent,
                new ModifierSource("test:first_strike:" + cardUid),
                ModifierScope.Permanent,
                new TargetUidCondition(cardUid)));
        }

        private void GrantConditionalFirstStrikeAtSlot(SlotId slot)
        {
            mStats.RuleModifiers.Add(new RuleModifier(
                RuleId.FirstStrike,
                ModifierOp.Override,
                1f,
                ModifierLayer.Conditional,
                new ModifierSource("test:first_strike_slot:" + slot.Index),
                ModifierScope.Permanent,
                new AtSlotCondition(slot)));
        }

        private List<int> CollectDamageActors(int startIndex)
        {
            var actors = new List<int>();
            var entries = mPipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                if (entries[i].Type == CoreEventType.DamageDealt && entries[i].Amount > 0)
                {
                    actors.Add(entries[i].ActorUid);
                }
            }

            return actors;
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

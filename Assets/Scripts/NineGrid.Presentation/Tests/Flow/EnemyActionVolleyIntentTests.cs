using System.Collections.Generic;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Systems;
using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// #81：敌方行动阶段导演分拍——单向打击走 Counter；倒计时变更进事件日志。
    /// </summary>
    public sealed class EnemyActionVolleyIntentTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;
        private CoreCommandDispatcher mDispatcher;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 81UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
            mDispatcher = new CoreCommandDispatcher(mArch);
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void Register_Emits_ActionCountdownChanged_With_Remaining()
        {
            Assert.IsTrue(mPhase.StartNode(CreateLivingNode()).Accepted);
            var monsterUid = SpawnOrthogonalMelee(sAdjacentSlot, hp: 5, attack: 2, countdown: 2);
            var startIndex = mPipeline.EventLog.Entries.Count;

            Assert.IsTrue(mPhase.RegisterEnemyActionPhase().Accepted);

            var changed = FindLastCountdown(startIndex, monsterUid);
            Assert.IsNotNull(changed, "报名应产出 ActionCountdownChanged");
            Assert.AreEqual(1, changed.ResultValue);
            Assert.AreEqual(1, Countdown(monsterUid));
        }

        [Test]
        public void ResolveNext_Strike_Emits_CountdownReset_And_Damage()
        {
            Assert.IsTrue(mPhase.StartNode(CreateLivingNode()).Accepted);
            PrepareAvatar(hp: 20, armor: 0, attack: 0);
            var monsterUid = SpawnOrthogonalMelee(sAdjacentSlot, hp: 5, attack: 3, countdown: 1);
            var hpBefore = AvatarHp();

            Assert.IsTrue(mPhase.RegisterEnemyActionPhase().Accepted);
            var startIndex = mPipeline.EventLog.Entries.Count;
            Assert.IsTrue(mPhase.ResolveNextEnemyAction().Accepted);

            Assert.AreEqual(hpBefore - 3, AvatarHp());
            Assert.AreEqual(3, Countdown(monsterUid));
            Assert.IsTrue(ContainsTypeSince(startIndex, CoreEventType.DamageDealt));
            var reset = FindLastCountdown(startIndex, monsterUid);
            Assert.IsNotNull(reset);
            Assert.AreEqual(3, reset.ResultValue);
        }

        [Test]
        public void AttackIntent_EnemyActionStrike_UsesCounterPresentChannel()
        {
            Assert.IsTrue(mPhase.StartNode(CreateLivingNode()).Accepted);
            PrepareAvatar(hp: 20, armor: 0, attack: 1);
            var monsterUid = SpawnOrthogonalMelee(sAdjacentSlot, hp: 99, attack: 2, countdown: 1);
            mArch.GetModel<CardRegistry>().Get(mArch.GetModel<BoardModel>().AvatarUid.Value)
                .Stats.SetBase(StatId.Attack, 1);

            var counterProjected = 0;
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
                    if (attackerUid == monsterUid && ContainsTypeSince(start, CoreEventType.DamageDealt))
                    {
                        // 交战回击与敌方单向打击都可能投影 Counter；只计含 DamageDealt 的批。
                        counterProjected++;
                    }
                });
            var director = new PresentationDirector(factory);

            bool preview;
            Assert.IsTrue(director.TrySubmitIntent(
                new InputIntent(InputIntentKinds.Attack, sAdjacentSlot.Index),
                out preview));

            var guard = 0;
            while (director.IsMainlineBusy && guard++ < 80)
            {
                director.Tick(0.016f);
            }

            Assert.IsFalse(director.IsMainlineBusy, "敌方行动分拍应能跑完");
            Assert.GreaterOrEqual(counterPresent.BeginCount, 2, "交战回击 + 单向打击均应 Present Counter");
            Assert.GreaterOrEqual(counterProjected, 2);
            Assert.AreEqual(3, Countdown(monsterUid), "开火后倒计时应重置为频率");
        }

        private int SpawnOrthogonalMelee(SlotId slot, int hp, int attack, int countdown)
        {
            var draft = new CardDraft("monster.test.volley", CardKind.Monster)
            {
                MaxHp = hp,
                Attack = attack,
                AttackPattern = AttackPattern.OrthogonalMelee,
                ActionFrequency = 3
            };
            var card = draft.Create(mArch.GetModel<CardRegistry>());
            card.Counters.Set(CoreCounterKeys.AttackPatternCountdown, countdown);
            mArch.GetModel<BoardModel>().PlaceCard(card, slot);
            return card.Uid;
        }

        private void PrepareAvatar(int hp, int armor, int attack)
        {
            var avatar = mArch.GetModel<CardRegistry>().Get(mArch.GetModel<BoardModel>().AvatarUid.Value);
            avatar.Stats.SetBase(StatId.MaxHp, hp);
            avatar.Stats.SetBase(StatId.Hp, hp);
            avatar.Stats.SetBase(StatId.Armor, armor);
            avatar.Stats.SetBase(StatId.CurrentArmor, armor);
            avatar.Stats.SetBase(StatId.Attack, attack);
        }

        private int Countdown(int uid)
        {
            return mArch.GetModel<CardRegistry>().Get(uid).Counters.Get(CoreCounterKeys.AttackPatternCountdown);
        }

        private int AvatarHp()
        {
            return (int)mArch.GetModel<CardRegistry>()
                .Get(mArch.GetModel<BoardModel>().AvatarUid.Value)
                .Stats.GetBase(StatId.Hp);
        }

        private CoreGameEvent FindLastCountdown(int startIndex, int cardUid)
        {
            CoreGameEvent last = null;
            var entries = mPipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                if (entries[i].Type == CoreEventType.ActionCountdownChanged
                    && entries[i].CardUid == cardUid)
                {
                    last = entries[i];
                }
            }

            return last;
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

        private static NodeDeckOptions CreateLivingNode()
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1
            }.AddEnemyCard(new CardDraft("monster.test.anchor", CardKind.Monster)
            {
                MaxHp = 99,
                Attack = 0,
                AttackPattern = AttackPattern.None
            });
        }

        private sealed class RecordingPresentChannel : IPresentChannel
        {
            private readonly int mTicksUntilComplete;
            private bool mBegan;
            private int mTicks;

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

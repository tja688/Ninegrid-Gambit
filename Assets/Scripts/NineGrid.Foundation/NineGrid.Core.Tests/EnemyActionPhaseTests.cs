using System.Collections.Generic;
using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// #79 / #80 / ADR-0011–0012：敌方行动阶段 + 四开火模式位置×频率 + 「无」。
    /// Seam：IPhaseSystem 互动命令面（见 #74 Testing Decisions）。
    /// </summary>
    public sealed class EnemyActionPhaseTests
    {
        private const string ThornsReflectJson =
            "{\"id\":\"relic.enemy_action.thorns\",\"typeTag\":\"【类型遗物】\",\"containerType\":\"Relic\","
            + "\"kind\":\"Triggered\","
            + "\"requires\":[\"NoOwnerEntity\"],"
            + "\"trigger\":{\"atom\":\"OnDamageTaken\"},"
            + "\"conditions\":[{\"atom\":\"EventFilter\",\"eventType\":\"HpChanged\",\"targetKind\":\"Avatar\",\"maxDelta\":-1}],"
            + "\"target\":{\"atom\":\"FilteredCards\",\"include\":[\"Actor\"]},"
            + "\"action\":{\"atom\":\"DealDamage\",\"value\":99,\"actor\":\"Player\"}}";

        private const string BareOnBattleRelicJson =
            "{\"id\":\"relic.enemy_action.on_battle\",\"typeTag\":\"【类型遗物】\",\"containerType\":\"Relic\","
            + "\"kind\":\"Triggered\","
            + "\"requires\":[\"NoOwnerEntity\"],"
            + "\"trigger\":{\"atom\":\"OnBattle\"},"
            + "\"target\":{\"atom\":\"Player\"},"
            + "\"action\":{\"atom\":\"AddModifier\",\"stat\":\"Attack\",\"op\":\"Add\",\"value\":1,\"layer\":\"Temporary\",\"scope\":\"UntilBattleEnds\",\"source\":\"relic.enemy_action.on_battle\"}}";

        /// <summary>相对中心 Avatar(5) 的正交邻格。</summary>
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);
        /// <summary>相对中心 Avatar(5) 的对角邻格（对普通近战不合格）。</summary>
        private static readonly SlotId sDiagonalSlot = SlotId.Board(1);
        /// <summary>相对中心 Avatar(5) 的另一正交邻格。</summary>
        private static readonly SlotId sOtherAdjacentSlot = SlotId.Board(4);
        /// <summary>相对中心 Avatar(5) 的另一对角邻格。</summary>
        private static readonly SlotId sOtherDiagonalSlot = SlotId.Board(9);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;
        private IEffectSystem mEffects;
        private IStatSystem mStats;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(
                ContentConfigKeys.DefaultCatalog,
                ContentCatalogBootstrap.Load());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 79UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
            mEffects = mArch.GetSystem<IEffectSystem>();
            mStats = mArch.GetSystem<IStatSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void Register_TicksNonNone_AndFreezesRosterByUidAscending()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyNode()).Accepted);
            var lowUid = SpawnOrthogonalMelee(sAdjacentSlot, hp: 5, attack: 1, countdown: 1);
            var highUid = SpawnOrthogonalMelee(sOtherAdjacentSlot, hp: 5, attack: 1, countdown: 1);
            Assert.Less(lowUid, highUid);

            var far = SpawnOrthogonalMelee(sOtherDiagonalSlot, hp: 5, attack: 1, countdown: 2);
            var noneUid = SpawnNonePattern(SlotId.Board(6), hp: 5, attack: 1);

            Assert.IsTrue(mPhase.RegisterEnemyActionPhase().Accepted);

            CollectionAssert.AreEqual(new[] { lowUid, highUid }, mPhase.PendingEnemyActionUids);
            Assert.AreEqual(0, Countdown(lowUid));
            Assert.AreEqual(0, Countdown(highUid));
            Assert.AreEqual(1, Countdown(far), "未归零者不进名单，倒计时仅 −1");
            Assert.AreEqual(0, Countdown(noneUid), "「无」不推进倒计时");
        }

        [Test]
        public void ResolveNext_OrthogonalAdjacent_DealsUnidirectionalStrike_NoPlayerCounter_ResetsCountdown()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyNode()).Accepted);
            var monsterUid = SpawnOrthogonalMelee(sAdjacentSlot, hp: 5, attack: 3, countdown: 1);
            PrepareAvatar(hp: 20, armor: 0, attack: 0);
            var avatarHpBefore = AvatarHp();
            var monsterHpBefore = (int)Registry().Get(monsterUid).Stats.GetBase(StatId.Hp);

            Assert.IsTrue(mPhase.RegisterEnemyActionPhase().Accepted);
            var startIndex = mPipeline.EventLog.Entries.Count;
            Assert.IsTrue(mPhase.ResolveNextEnemyAction().Accepted);

            Assert.AreEqual(avatarHpBefore - 3, AvatarHp());
            Assert.AreEqual(monsterHpBefore, (int)Registry().Get(monsterUid).Stats.GetBase(StatId.Hp), "玩家不反击");
            Assert.AreEqual(3, Countdown(monsterUid), "开火后重置为频率");
            Assert.IsFalse(ContainsEngagementBegin(SliceEvents(startIndex)), "单向打击不开交战作用域");
            Assert.AreEqual(0, mPhase.PendingEnemyActionUids.Count);
        }

        [Test]
        public void ResolveNext_NotAdjacent_CancelsAndResetsCountdown_NoDamage()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyNode()).Accepted);
            var monsterUid = SpawnOrthogonalMelee(sDiagonalSlot, hp: 5, attack: 3, countdown: 1);
            PrepareAvatar(hp: 20, armor: 0, attack: 0);
            var avatarHpBefore = AvatarHp();

            Assert.IsTrue(mPhase.RegisterEnemyActionPhase().Accepted);
            Assert.IsTrue(mPhase.ResolveNextEnemyAction().Accepted);

            Assert.AreEqual(avatarHpBefore, AvatarHp());
            Assert.AreEqual(3, Countdown(monsterUid));
        }

        [Test]
        public void ResolveNext_ActionBanned_CancelsAndResetsCountdown()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyNode()).Accepted);
            var monsterUid = SpawnOrthogonalMelee(sAdjacentSlot, hp: 5, attack: 3, countdown: 1);
            PrepareAvatar(hp: 20, armor: 0, attack: 0);
            GrantActionBanned(monsterUid);
            var avatarHpBefore = AvatarHp();

            Assert.IsTrue(mPhase.RegisterEnemyActionPhase().Accepted);
            Assert.IsTrue(mPhase.ResolveNextEnemyAction().Accepted);

            Assert.AreEqual(avatarHpBefore, AvatarHp());
            Assert.AreEqual(3, Countdown(monsterUid));
        }

        [Test]
        public void Volley_DoesNotFillOrRotate_FinaleFillsWithoutRotate()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyNode()).Accepted);
            // 两只相邻近战：第一只打死 Avatar 前先清场位用道具式击杀模拟——改用高攻怪打死自己经反甲？
            // 更简：一只 hp1 怪在相邻，报名后用手动 Kill 不走 Finale；改为齐射中击杀靠反伤。
            var glassUid = SpawnOrthogonalMelee(sAdjacentSlot, hp: 1, attack: 1, countdown: 1);
            PrepareAvatar(hp: 20, armor: 0, attack: 0);
            ActivateRelic(ThornsReflectJson, "relic.enemy_action.thorns");

            Assert.IsTrue(mPhase.RegisterEnemyActionPhase().Accepted);
            var midStart = mPipeline.EventLog.Entries.Count;
            Assert.IsTrue(mPhase.ResolveNextEnemyAction().Accepted);
            var midEvents = SliceEvents(midStart);
            Assert.IsTrue(ContainsType(midEvents, CoreEventType.CardKilled), "反伤应击杀开火怪");
            Assert.IsFalse(ContainsType(midEvents, CoreEventType.SlotsFilled), "齐射中不补牌");
            Assert.IsFalse(ContainsType(midEvents, CoreEventType.BoardRotated), "齐射中不旋转");
            Assert.AreEqual(0, Board().GetCardUid(sAdjacentSlot), "死亡已离场，空位保留至收尾");

            var finaleStart = mPipeline.EventLog.Entries.Count;
            Assert.IsTrue(mPhase.ResolveEnemyActionFinale().Accepted);
            var finaleEvents = SliceEvents(finaleStart);
            Assert.IsTrue(ContainsType(finaleEvents, CoreEventType.SlotsFilled), "收尾补牌");
            Assert.IsFalse(ContainsType(finaleEvents, CoreEventType.BoardRotated), "收尾不旋转");
            Assert.AreNotEqual(0, glassUid);
        }

        [Test]
        public void PlayerDeath_AbortsRemainingRosterEntries()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyNode()).Accepted);
            var first = SpawnOrthogonalMelee(sAdjacentSlot, hp: 5, attack: 5, countdown: 1);
            var second = SpawnOrthogonalMelee(sOtherAdjacentSlot, hp: 5, attack: 5, countdown: 1);
            Assert.Less(first, second);
            PrepareAvatar(hp: 3, armor: 0, attack: 0);

            Assert.IsTrue(mPhase.RegisterEnemyActionPhase().Accepted);
            CollectionAssert.AreEqual(new[] { first, second }, mPhase.PendingEnemyActionUids);

            Assert.IsTrue(mPhase.ResolveNextEnemyAction().Accepted);
            Assert.AreEqual(GamePhase.Defeat, mPhase.CurrentPhase);
            Assert.AreEqual(0, AvatarHp());

            var beforeSecond = mPipeline.EventLog.Entries.Count;
            Assert.IsTrue(mPhase.ResolveNextEnemyAction().Accepted);
            Assert.AreEqual(
                0,
                CountDamageFrom(SliceEvents(beforeSecond), second),
                "玩家死亡后名单剩余不得再开火");
            Assert.AreEqual(0, mPhase.PendingEnemyActionUids.Count);
        }

        [Test]
        public void MidPhaseSpawnOrAccelerate_CannotJoinFrozenRoster()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyNode()).Accepted);
            var listed = SpawnOrthogonalMelee(sAdjacentSlot, hp: 5, attack: 1, countdown: 1);
            var waiting = SpawnOrthogonalMelee(sOtherDiagonalSlot, hp: 5, attack: 1, countdown: 3);
            PrepareAvatar(hp: 20, armor: 0, attack: 0);

            Assert.IsTrue(mPhase.RegisterEnemyActionPhase().Accepted);
            CollectionAssert.AreEqual(new[] { listed }, mPhase.PendingEnemyActionUids);

            Registry().Get(waiting).Counters.Set(CoreCounterKeys.AttackPatternCountdown, 0);
            var intruder = SpawnOrthogonalMelee(sOtherAdjacentSlot, hp: 5, attack: 1, countdown: 0);
            Assert.AreEqual(0, Countdown(intruder));

            Assert.IsTrue(mPhase.ResolveNextEnemyAction().Accepted);
            Assert.AreEqual(0, mPhase.PendingEnemyActionUids.Count, "冻结名单不得因加速/进场扩容");
            Assert.AreEqual(0, Countdown(waiting), "加速怪保留 0，但不插队本拍");
        }

        [Test]
        public void ThornsKillAttacker_StrikeDamageStillApplies()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyNode()).Accepted);
            var monsterUid = SpawnOrthogonalMelee(sAdjacentSlot, hp: 1, attack: 4, countdown: 1);
            PrepareAvatar(hp: 20, armor: 0, attack: 0);
            ActivateRelic(ThornsReflectJson, "relic.enemy_action.thorns");
            var avatarHpBefore = AvatarHp();

            Assert.IsTrue(mPhase.RegisterEnemyActionPhase().Accepted);
            Assert.IsTrue(mPhase.ResolveNextEnemyAction().Accepted);

            Assert.AreEqual(avatarHpBefore - 4, AvatarHp(), "反伤致死不撤销当次打击");
            Assert.AreEqual(ZoneId.Graveyard, Registry().Get(monsterUid).Zone.Value);
        }

        [Test]
        public void UnidirectionalStrike_DoesNotTriggerOnBattle()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyNode()).Accepted);
            SpawnOrthogonalMelee(sAdjacentSlot, hp: 5, attack: 2, countdown: 1);
            PrepareAvatar(hp: 20, armor: 0, attack: 5);
            ActivateRelic(BareOnBattleRelicJson, "relic.enemy_action.on_battle");
            var attackBefore = mStats.GetEffectiveInt(Registry().Get(Board().AvatarUid.Value), StatId.Attack);

            Assert.IsTrue(mPhase.RegisterEnemyActionPhase().Accepted);
            Assert.IsTrue(mPhase.ResolveNextEnemyAction().Accepted);

            Assert.AreEqual(
                attackBefore,
                mStats.GetEffectiveInt(Registry().Get(Board().AvatarUid.Value), StatId.Attack),
                "单向打击不得触发 OnBattle");
        }

        [Test]
        public void AttackChain_RunsEnemyPhaseAfterCountAndConditionalRotate()
        {
            Assert.IsTrue(mPhase.StartNode(CreateLivingNode()).Accepted);
            PlaceSoleBoardCardAt(sDiagonalSlot);
            var monsterUid = SpawnOrthogonalMelee(sAdjacentSlot, hp: 99, attack: 2, countdown: 1);
            PrepareAvatar(hp: 20, armor: 0, attack: 1);
            var avatarHpBefore = AvatarHp();
            var startIndex = mPipeline.EventLog.Entries.Count;

            var result = mPhase.Attack(sAdjacentSlot);
            Assert.IsTrue(result.Accepted, result.Reason);

            var events = SliceEvents(startIndex);
            Assert.IsTrue(ContainsType(events, CoreEventType.InteractionChanged));
            Assert.IsFalse(ContainsType(events, CoreEventType.BoardRotated), "未击杀不旋转");
            Assert.AreEqual(avatarHpBefore - 2 - 2, AvatarHp(), "交战回击 + 敌方单向打击各 2");
            Assert.AreEqual(3, Countdown(monsterUid));
        }

        [Test]
        public void UseItem_DoesNotRunEnemyActionPhase()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyNode()).Accepted);
            var monsterUid = SpawnOrthogonalMelee(sAdjacentSlot, hp: 5, attack: 3, countdown: 1);
            PrepareAvatar(hp: 20, armor: 0, attack: 0);
            mPipeline.Enqueue(new SpawnCardAction(
                "help.throwing_knife",
                CardKind.HelpCard,
                ZoneId.ItemSlots,
                SlotId.None,
                1,
                "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            var knifeUid = mArch.GetModel<DeckModel>().ItemSlotUids[
                mArch.GetModel<DeckModel>().ItemSlotUids.Count - 1];
            var avatarHpBefore = AvatarHp();
            var countdownBefore = Countdown(monsterUid);

            Assert.IsTrue(
                mPhase.ApplyUseItem(knifeUid, new List<int> { monsterUid }, null).Accepted);

            Assert.AreEqual(countdownBefore, Countdown(monsterUid), "道具不推进敌方时钟");
            Assert.AreEqual(avatarHpBefore, AvatarHp(), "道具路径不跑敌方行动");
            Assert.AreEqual(0, mPhase.PendingEnemyActionUids.Count);
        }

        [Test]
        public void NonePattern_NeverEntersRoster()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyNode()).Accepted);
            SpawnNonePattern(sAdjacentSlot, hp: 5, attack: 3);
            PrepareAvatar(hp: 20, armor: 0, attack: 0);
            var avatarHpBefore = AvatarHp();

            Assert.IsTrue(mPhase.RegisterEnemyActionPhase().Accepted);
            Assert.AreEqual(0, mPhase.PendingEnemyActionUids.Count);
            Assert.IsTrue(mPhase.ResolveNextEnemyAction().Accepted);
            Assert.IsTrue(mPhase.ResolveEnemyActionFinale().Accepted);
            Assert.AreEqual(avatarHpBefore, AvatarHp());
        }

        [Test]
        public void Register_AlreadyZeroCountdown_EntersRosterWithoutClamping()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyNode()).Accepted);
            var monsterUid = SpawnOrthogonalMelee(sAdjacentSlot, hp: 5, attack: 1, countdown: 0);

            Assert.IsTrue(mPhase.RegisterEnemyActionPhase().Accepted);

            CollectionAssert.AreEqual(new[] { monsterUid }, mPhase.PendingEnemyActionUids);
            Assert.AreEqual(0, Countdown(monsterUid));
        }

        [Test]
        public void Attack_Kill_RotatesOnce_ThenEnemyPhase_WithoutSecondRotate()
        {
            Assert.IsTrue(mPhase.StartNode(CreateLivingNode()).Accepted);
            PlaceSoleBoardCardAt(sDiagonalSlot);
            SpawnOrthogonalMelee(sAdjacentSlot, hp: 1, attack: 2, countdown: 1);
            PrepareAvatar(hp: 20, armor: 0, attack: 5);
            var startIndex = mPipeline.EventLog.Entries.Count;

            var result = mPhase.Attack(sAdjacentSlot);
            Assert.IsTrue(result.Accepted, result.Reason);

            var events = SliceEvents(startIndex);
            Assert.AreEqual(1, CountType(events, CoreEventType.BoardRotated), "一次互动至多一次玩家侧旋转");
            Assert.AreEqual(1, CountType(events, CoreEventType.InteractionChanged));
            Assert.IsTrue(ContainsType(events, CoreEventType.CardKilled));
            Assert.IsFalse(
                mPhase.CurrentPhase == GamePhase.Defeat,
                "击杀路径后敌方阶段不得因多余旋转外的原因卡死");
        }

        [Test]
        public void ResolveNext_DiagonalMelee_FiresOnlyOnDiagonal_ResetsToFrequency3()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyNode()).Accepted);
            var diagonalUid = SpawnPattern(
                AttackPattern.DiagonalMelee, sDiagonalSlot, hp: 5, attack: 3, countdown: 1);
            PrepareAvatar(hp: 20, armor: 0, attack: 0);
            var hpBefore = AvatarHp();

            Assert.IsTrue(mPhase.RegisterEnemyActionPhase().Accepted);
            Assert.IsTrue(mPhase.ResolveNextEnemyAction().Accepted);

            Assert.AreEqual(hpBefore - 3, AvatarHp(), "对角相邻应开火");
            Assert.AreEqual(3, Countdown(diagonalUid));
        }

        [Test]
        public void ResolveNext_DiagonalMelee_OrthogonalAdjacent_MissesAndResets()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyNode()).Accepted);
            var monsterUid = SpawnPattern(
                AttackPattern.DiagonalMelee, sAdjacentSlot, hp: 5, attack: 3, countdown: 1);
            PrepareAvatar(hp: 20, armor: 0, attack: 0);
            var hpBefore = AvatarHp();

            Assert.IsTrue(mPhase.RegisterEnemyActionPhase().Accepted);
            Assert.IsTrue(mPhase.ResolveNextEnemyAction().Accepted);

            Assert.AreEqual(hpBefore, AvatarHp(), "正交相邻对斜角近战不合格");
            Assert.AreEqual(3, Countdown(monsterUid), "错过窗口重置为频率");
        }

        [Test]
        public void ResolveNext_DiagonalMelee_AvatarOffCenter_UsesTrueDiagonalNotNonOrthogonal()
        {
            // Avatar 挪到上边中格(2)：真对角=4/6；格9 非正交但亦非对角——「非正交」语义会误开火。
            Assert.IsTrue(mPhase.StartNode(CreateEmptyNode()).Accepted);
            MoveAvatarTo(SlotId.Board(2));
            var trueDiagonal = SpawnPattern(
                AttackPattern.DiagonalMelee, SlotId.Board(4), hp: 5, attack: 2, countdown: 1);
            var fakeNonOrthogonal = SpawnPattern(
                AttackPattern.DiagonalMelee, SlotId.Board(9), hp: 5, attack: 4, countdown: 1);
            PrepareAvatar(hp: 20, armor: 0, attack: 0);
            var hpBefore = AvatarHp();

            Assert.IsTrue(mPhase.RegisterEnemyActionPhase().Accepted);
            CollectionAssert.AreEqual(
                new[] { trueDiagonal, fakeNonOrthogonal },
                mPhase.PendingEnemyActionUids);

            Assert.IsTrue(mPhase.ResolveNextEnemyAction().Accepted);
            Assert.AreEqual(hpBefore - 2, AvatarHp(), "真对角应开火");
            Assert.AreEqual(3, Countdown(trueDiagonal));

            Assert.IsTrue(mPhase.ResolveNextEnemyAction().Accepted);
            Assert.AreEqual(hpBefore - 2, AvatarHp(), "非正交远位不得按斜角近战开火");
            Assert.AreEqual(3, Countdown(fakeNonOrthogonal));
        }

        [Test]
        public void ResolveNext_OmnidirectionalMelee_FiresOnOrthogonalOrDiagonal_MissesWhenFar()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyNode()).Accepted);
            var ortho = SpawnPattern(
                AttackPattern.OmnidirectionalMelee, sAdjacentSlot, hp: 5, attack: 2, countdown: 1);
            PrepareAvatar(hp: 20, armor: 0, attack: 0);
            var hpBefore = AvatarHp();

            Assert.IsTrue(mPhase.RegisterEnemyActionPhase().Accepted);
            Assert.IsTrue(mPhase.ResolveNextEnemyAction().Accepted);
            Assert.AreEqual(hpBefore - 2, AvatarHp(), "正交相邻应开火");
            Assert.AreEqual(3, Countdown(ortho));

            NineGridArchitecture.ResetForTests();
            SetUp();
            Assert.IsTrue(mPhase.StartNode(CreateEmptyNode()).Accepted);
            var diag = SpawnPattern(
                AttackPattern.OmnidirectionalMelee, sDiagonalSlot, hp: 5, attack: 2, countdown: 1);
            PrepareAvatar(hp: 20, armor: 0, attack: 0);
            hpBefore = AvatarHp();

            Assert.IsTrue(mPhase.RegisterEnemyActionPhase().Accepted);
            Assert.IsTrue(mPhase.ResolveNextEnemyAction().Accepted);
            Assert.AreEqual(hpBefore - 2, AvatarHp(), "对角相邻应开火");
            Assert.AreEqual(3, Countdown(diag));

            // Avatar 在角(1) 时格9 非八向相邻。
            NineGridArchitecture.ResetForTests();
            SetUp();
            Assert.IsTrue(mPhase.StartNode(CreateEmptyNode()).Accepted);
            MoveAvatarTo(SlotId.Board(1));
            var far = SpawnPattern(
                AttackPattern.OmnidirectionalMelee, SlotId.Board(9), hp: 5, attack: 2, countdown: 1);
            PrepareAvatar(hp: 20, armor: 0, attack: 0);
            hpBefore = AvatarHp();

            Assert.IsTrue(mPhase.RegisterEnemyActionPhase().Accepted);
            Assert.IsTrue(mPhase.ResolveNextEnemyAction().Accepted);
            Assert.AreEqual(hpBefore, AvatarHp(), "非八向相邻应错过");
            Assert.AreEqual(3, Countdown(far));
        }

        [Test]
        public void ResolveNext_Ranged_NoPositionGate_ResetsToFrequency5()
        {
            // Avatar 在角(1)、怪在对角远位(9)：近战全不合格，远程仍应开火。
            Assert.IsTrue(mPhase.StartNode(CreateEmptyNode()).Accepted);
            MoveAvatarTo(SlotId.Board(1));
            var rangedUid = SpawnPattern(
                AttackPattern.Ranged, SlotId.Board(9), hp: 5, attack: 3, countdown: 1);
            PrepareAvatar(hp: 20, armor: 0, attack: 0);
            var hpBefore = AvatarHp();

            Assert.IsTrue(mPhase.RegisterEnemyActionPhase().Accepted);
            Assert.IsTrue(mPhase.ResolveNextEnemyAction().Accepted);

            Assert.AreEqual(hpBefore - 3, AvatarHp(), "远程无位置限制");
            Assert.AreEqual(5, Countdown(rangedUid), "开火后重置为频率 5");
        }

        [Test]
        public void MissedWindow_ResetsCountdownToFrequency_ForAllFiringPatterns()
        {
            AssertMissedWindowResets(
                AttackPattern.OrthogonalMelee,
                avatarSlot: SlotId.Board(5),
                missSlot: sDiagonalSlot,
                expectedFrequency: 3);
            AssertMissedWindowResets(
                AttackPattern.DiagonalMelee,
                avatarSlot: SlotId.Board(5),
                missSlot: sAdjacentSlot,
                expectedFrequency: 3);
            AssertMissedWindowResets(
                AttackPattern.OmnidirectionalMelee,
                avatarSlot: SlotId.Board(1),
                missSlot: SlotId.Board(9),
                expectedFrequency: 3);
        }

        private void AssertMissedWindowResets(
            AttackPattern pattern,
            SlotId avatarSlot,
            SlotId missSlot,
            int expectedFrequency)
        {
            NineGridArchitecture.ResetForTests();
            SetUp();
            Assert.IsTrue(mPhase.StartNode(CreateEmptyNode()).Accepted);
            if (avatarSlot != SlotId.Board(5))
            {
                MoveAvatarTo(avatarSlot);
            }

            var uid = SpawnPattern(pattern, missSlot, hp: 5, attack: 3, countdown: 1);
            PrepareAvatar(hp: 20, armor: 0, attack: 0);
            var hpBefore = AvatarHp();

            Assert.IsTrue(mPhase.RegisterEnemyActionPhase().Accepted);
            Assert.IsTrue(mPhase.ResolveNextEnemyAction().Accepted);

            Assert.AreEqual(hpBefore, AvatarHp(), pattern + " 位置不合格不得造成伤害");
            Assert.AreEqual(expectedFrequency, Countdown(uid), pattern + " 错过窗口应重置为频率");
        }

        private static int CountType(IReadOnlyList<CoreGameEvent> events, CoreEventType type)
        {
            var count = 0;
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].Type == type)
                {
                    count++;
                }
            }

            return count;
        }

        private int SpawnOrthogonalMelee(SlotId slot, int hp, int attack, int countdown)
        {
            return SpawnPattern(AttackPattern.OrthogonalMelee, slot, hp, attack, countdown);
        }

        private int SpawnPattern(AttackPattern pattern, SlotId slot, int hp, int attack, int countdown)
        {
            var frequency = AttackPatternRules.Frequency(pattern);
            var draft = new CardDraft("monster.test.pattern." + pattern, CardKind.Monster)
            {
                MaxHp = hp,
                Attack = attack,
                AttackPattern = pattern,
                ActionFrequency = frequency
            };
            var card = draft.Create(Registry());
            card.Counters.Set(CoreCounterKeys.AttackPatternCountdown, countdown);
            Board().PlaceCard(card, slot);
            return card.Uid;
        }

        private void MoveAvatarTo(SlotId slot)
        {
            var board = Board();
            var avatar = Registry().Get(board.AvatarUid.Value);
            board.SetAvatar(avatar, slot);
            Assert.AreEqual(slot, board.AvatarSlot.Value);
        }

        private int SpawnNonePattern(SlotId slot, int hp, int attack)
        {
            var draft = new CardDraft("monster.test.none", CardKind.Monster)
            {
                MaxHp = hp,
                Attack = attack,
                AttackPattern = AttackPattern.None,
                ActionFrequency = 0
            };
            var card = draft.Create(Registry());
            Board().PlaceCard(card, slot);
            return card.Uid;
        }

        private static NodeDeckOptions CreateEmptyNode()
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            };
        }

        /// <summary>开局留一只「无」锚点怪，避免 StartNode 末尾清场离开 InteractionLoop。</summary>
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

        private void PlaceSoleBoardCardAt(SlotId targetSlot)
        {
            var board = Board();
            var registry = Registry();
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

        private void PrepareAvatar(int hp, int armor, int attack)
        {
            var avatar = Registry().Get(Board().AvatarUid.Value);
            avatar.Stats.SetBase(StatId.MaxHp, hp);
            avatar.Stats.SetBase(StatId.Hp, hp);
            avatar.Stats.SetBase(StatId.Armor, armor);
            avatar.Stats.SetBase(StatId.CurrentArmor, armor);
            avatar.Stats.SetBase(StatId.Attack, attack);
        }

        private void GrantActionBanned(int cardUid)
        {
            mStats.RuleModifiers.Add(new RuleModifier(
                RuleId.ActionBanned,
                ModifierOp.Override,
                1f,
                ModifierLayer.Persistent,
                new ModifierSource("test:action_banned:" + cardUid),
                ModifierScope.Permanent,
                new TargetUidCondition(cardUid)));
        }

        private void ActivateRelic(string json, string sourceDefId)
        {
            var definition = mEffects.ParseJson(json);
            Assert.IsTrue(mEffects.Validate(definition).IsValid);
            mEffects.Activate(
                definition,
                new EffectOwner(EffectContainerType.Relic, sourceDefId, 0));
        }

        private int Countdown(int uid)
        {
            return Registry().Get(uid).Counters.Get(CoreCounterKeys.AttackPatternCountdown);
        }

        private int AvatarHp()
        {
            return (int)Registry().Get(Board().AvatarUid.Value).Stats.GetBase(StatId.Hp);
        }

        private BoardModel Board()
        {
            return mArch.GetModel<BoardModel>();
        }

        private CardRegistry Registry()
        {
            return mArch.GetModel<CardRegistry>();
        }

        private IReadOnlyList<CoreGameEvent> SliceEvents(int startIndex)
        {
            var entries = mPipeline.EventLog.Entries;
            var list = new List<CoreGameEvent>(entries.Count - startIndex);
            for (var i = startIndex; i < entries.Count; i++)
            {
                list.Add(entries[i]);
            }

            return list;
        }

        private static bool ContainsType(IReadOnlyList<CoreGameEvent> events, CoreEventType type)
        {
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].Type == type)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsEngagementBegin(IReadOnlyList<CoreGameEvent> events)
        {
            for (var i = 0; i < events.Count; i++)
            {
                var evt = events[i];
                if (evt.Type == CoreEventType.ActionStarted
                    && evt.ActionName != null
                    && evt.ActionName.IndexOf("BeginPlayerMonsterEngagement", System.StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountDamageFrom(IReadOnlyList<CoreGameEvent> events, int actorUid)
        {
            var count = 0;
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].Type == CoreEventType.DamageDealt
                    && events[i].ActorUid == actorUid
                    && events[i].Amount > 0)
                {
                    count++;
                }
            }

            return count;
        }
    }
}

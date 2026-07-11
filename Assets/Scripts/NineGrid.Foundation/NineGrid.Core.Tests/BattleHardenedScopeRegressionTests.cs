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
    /// 阶段二：UntilEnemyChanges / UntilBattleEnds 作用域清理回归。
    /// </summary>
    public sealed class BattleHardenedScopeRegressionTests
    {
        private const string BattleHardenedEffectJson =
            "{\"id\":\"skill.battle_hardened.battle\",\"typeTag\":\"【类型玩家技能】\",\"containerType\":\"PlayerSkill\","
            + "\"kind\":\"Triggered\","
            + "\"trigger\":{\"atom\":\"OnBattle\",\"sourceAction\":\"DealDamage\",\"targetKind\":\"Monster\",\"maxActionDepth\":0},"
            + "\"conditions\":[{\"atom\":\"EventFilter\",\"eventType\":\"DamageDealt\",\"actorIs\":\"Player\",\"targetKind\":\"Monster\"}],"
            + "\"target\":{\"atom\":\"Player\"},"
            + "\"action\":{\"atom\":\"AddModifier\",\"stat\":\"Attack\",\"op\":\"Add\",\"value\":2,\"layer\":\"Temporary\",\"scope\":\"UntilEnemyChanges\",\"source\":\"skill.battle_hardened\"}}";

        private const string MonsterBattleHardenedEffectJson =
            "{\"id\":\"skill.monster_battle_hardened.battle\",\"typeTag\":\"【类型怪物技能】\",\"containerType\":\"MonsterSkill\","
            + "\"kind\":\"Triggered\","
            + "\"trigger\":{\"atom\":\"OnBattle\",\"sourceAction\":\"DealDamage\",\"targetKind\":\"Monster\",\"maxActionDepth\":0},"
            + "\"conditions\":[{\"atom\":\"EventFilter\",\"eventType\":\"DamageDealt\",\"actorIs\":\"Player\",\"targetIs\":\"Self\"}],"
            + "\"target\":{\"atom\":\"Self\"},"
            + "\"action\":{\"atom\":\"AddModifier\",\"stat\":\"Attack\",\"op\":\"Add\",\"value\":2,\"layer\":\"Temporary\",\"scope\":\"UntilBattleEnds\",\"source\":\"skill.monster_battle_hardened\"}}";

        private static readonly SlotId sPrimarySlot = SlotId.Board(2);
        private static readonly SlotId sSecondarySlot = SlotId.Board(4);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IStatSystem mStats;
        private IEffectSystem mEffects;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, TableNineContentCatalog.CreateDefault());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 7UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mStats = mArch.GetSystem<IStatSystem>();
            mEffects = mArch.GetSystem<IEffectSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void BattleHardened_EnemyChange_ClearsBonusBeforeReapplying()
        {
            Assert.IsTrue(mPhase.StartNode(CreateTwoMonsterNode()).Accepted);
            var monsterA = FindAndPlace("monster.test_a", sPrimarySlot);
            var monsterB = FindAndPlace("monster.test_b", sSecondarySlot);
            ActivateBattleHardened();
            PrepareAvatarAttack(5);

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var avatarUid = board.AvatarUid.Value;

            Assert.IsTrue(mPhase.ApplyCombatHit(avatarUid, monsterA).Accepted);
            Assert.AreEqual(7, mStats.GetEffectiveInt(registry.Get(avatarUid), StatId.Attack), "首敌应 +2");

            Assert.IsTrue(mPhase.ApplyCombatHit(avatarUid, monsterB).Accepted);
            Assert.AreEqual(
                7,
                mStats.GetEffectiveInt(registry.Get(avatarUid), StatId.Attack),
                "换敌应清 UntilEnemyChanges 后再 +2，不应叠成 +4");
        }

        [Test]
        public void BattleHardened_SameEnemy_RepeatedAttacks_StackIsLinearNotCrossEnemy()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 40, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sPrimarySlot);
            ActivateBattleHardened();
            PrepareAvatarAttack(5);

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var avatarUid = board.AvatarUid.Value;
            var monsterUid = board.GetCardUid(sPrimarySlot);

            Assert.IsTrue(mPhase.ApplyCombatHit(avatarUid, monsterUid).Accepted);
            Assert.AreEqual(7, mStats.GetEffectiveInt(registry.Get(avatarUid), StatId.Attack));

            Assert.IsTrue(mPhase.ApplyCombatHit(avatarUid, monsterUid).Accepted);
            Assert.AreEqual(
                9,
                mStats.GetEffectiveInt(registry.Get(avatarUid), StatId.Attack),
                "同敌连打可线性叠层；跨敌清理由换敌用例覆盖");
        }

        [Test]
        public void MonsterBattleHardened_UntilBattleEnds_ClearedAfterExchange()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 30, attack: 1)).Accepted);
            var monsterUid = PlaceSoleBoardCardAt(sPrimarySlot);
            ActivateMonsterBattleHardened(monsterUid);
            PrepareAvatarAttack(5);

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var avatarUid = board.AvatarUid.Value;
            var pipeline = mArch.GetSystem<IActionPipelineSystem>();

            pipeline.Enqueue(new BeginPlayerMonsterEngagementAction(monsterUid));
            pipeline.Enqueue(new DealDamageAction(avatarUid, monsterUid, 1));
            Assert.Greater(pipeline.RunToCompletion(), 0);
            Assert.AreEqual(
                3,
                mStats.GetEffectiveInt(registry.Get(monsterUid), StatId.Attack),
                "OnBattle 后 UntilBattleEnds 修正应保留至战斗结束");

            pipeline.Enqueue(new EndBattleScopeCleanupAction());
            Assert.Greater(pipeline.RunToCompletion(), 0);
            Assert.AreEqual(
                1,
                mStats.GetEffectiveInt(registry.Get(monsterUid), StatId.Attack),
                "EndBattleScopeCleanup 应清掉 UntilBattleEnds +2");
        }

        [Test]
        public void BattleScope_TracksEngagedEnemyAcrossHits()
        {
            Assert.IsTrue(mPhase.StartNode(CreateTwoMonsterNode()).Accepted);
            var monsterA = FindAndPlace("monster.test_a", sPrimarySlot);
            var monsterB = FindAndPlace("monster.test_b", sSecondarySlot);
            PrepareAvatarAttack(5);

            var board = mArch.GetModel<BoardModel>();
            var scope = mArch.GetSystem<IBattleScopeSystem>();
            var avatarUid = board.AvatarUid.Value;

            Assert.AreEqual(0, scope.EngagedEnemyUid);
            Assert.IsTrue(mPhase.ApplyCombatHit(avatarUid, monsterA).Accepted);
            Assert.AreEqual(monsterA, scope.EngagedEnemyUid);
            Assert.IsTrue(mPhase.ApplyCombatHit(avatarUid, monsterB).Accepted);
            Assert.AreEqual(monsterB, scope.EngagedEnemyUid);
        }

        private void ActivateBattleHardened()
        {
            var definition = mEffects.ParseJson(BattleHardenedEffectJson);
            Assert.IsTrue(mEffects.Validate(definition).IsValid);
            mEffects.Activate(
                definition,
                new EffectOwner(EffectContainerType.PlayerSkill, "skill.battle_hardened", 0));
        }

        private void ActivateMonsterBattleHardened(int ownerUid)
        {
            var definition = mEffects.ParseJson(MonsterBattleHardenedEffectJson);
            Assert.IsTrue(mEffects.Validate(definition).IsValid);
            mEffects.Activate(
                definition,
                new EffectOwner(EffectContainerType.MonsterSkill, "skill.monster_battle_hardened", ownerUid));
        }

        private static NodeDeckOptions CreateSingleMonsterNode(int hp, int attack)
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1
            }.AddEnemyCard(new CardDraft("monster.test", CardKind.Monster) { MaxHp = hp, Attack = attack });
        }

        private static NodeDeckOptions CreateTwoMonsterNode()
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 2
            }
                .AddEnemyCard(new CardDraft("monster.test_a", CardKind.Monster) { MaxHp = 20, Attack = 0 })
                .AddEnemyCard(new CardDraft("monster.test_b", CardKind.Monster) { MaxHp = 20, Attack = 0 });
        }

        private void PrepareAvatarAttack(int attack)
        {
            var board = mArch.GetModel<BoardModel>();
            var avatar = mArch.GetModel<CardRegistry>().Get(board.AvatarUid.Value);
            avatar.Stats.SetBase(StatId.MaxHp, 99);
            avatar.Stats.SetBase(StatId.Hp, 99);
            avatar.Stats.SetBase(StatId.Armor, 0);
            avatar.Stats.SetBase(StatId.Attack, attack);
        }

        private int PlaceSoleBoardCardAt(SlotId targetSlot)
        {
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
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

                var card = registry.Get(uid);
                if (card.Slot.Value == targetSlot)
                {
                    return uid;
                }

                board.ClearSlot(card.Slot.Value);
                board.PlaceCard(card, targetSlot);
                return uid;
            }

            Assert.Fail("No board card to relocate.");
            return 0;
        }

        private int FindAndPlace(string defId, SlotId targetSlot)
        {
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var uid = FindBoardOrDrawPileUid(defId);
            Assert.Greater(uid, 0, "未找到 " + defId);

            var card = registry.Get(uid);
            if (card.Slot.Value == targetSlot && card.Zone.Value == ZoneId.Board)
            {
                return uid;
            }

            if (card.Zone.Value == ZoneId.DrawPile)
            {
                mArch.GetModel<DeckModel>().RemoveCard(card);
            }
            else if (card.Zone.Value == ZoneId.Board)
            {
                var occupant = board.GetCardUid(targetSlot);
                if (occupant > 0 && occupant != uid)
                {
                    var other = registry.Get(occupant);
                    var from = card.Slot.Value;
                    board.ClearSlot(from);
                    board.ClearSlot(targetSlot);
                    board.PlaceCard(card, targetSlot);
                    board.PlaceCard(other, from);
                    return uid;
                }

                board.ClearSlot(card.Slot.Value);
            }

            board.PlaceCard(card, targetSlot);
            return uid;
        }

        private int FindBoardOrDrawPileUid(string defId)
        {
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                if (slot == board.AvatarSlot.Value)
                {
                    continue;
                }

                var candidate = board.GetCardUid(slot);
                if (candidate > 0 && registry.Get(candidate).DefId == defId)
                {
                    return candidate;
                }
            }

            var deck = mArch.GetModel<DeckModel>();
            for (var i = 0; i < deck.DrawPileUids.Count; i++)
            {
                var candidate = deck.DrawPileUids[i];
                if (registry.Get(candidate).DefId == defId)
                {
                    return candidate;
                }
            }

            return 0;
        }
    }
}

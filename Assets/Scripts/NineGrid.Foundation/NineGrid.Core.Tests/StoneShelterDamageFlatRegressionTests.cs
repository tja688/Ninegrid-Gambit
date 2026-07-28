using NineGrid.Core;
using NineGrid.Core.Stats;
using NineGrid.Core.Effects;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// 石庇护 DamageFlatDelta：单只庇护石对其他怪物应恰好 -1，不得双算成 -2。
    /// 对照 battlelog-20260711-135406 op#32（4 攻对石虾甲2血2 却 amount=2）排查叠层。
    /// </summary>
    public sealed class StoneShelterDamageFlatRegressionTests
    {
        private const string StoneShelterRuleJson =
            "{\"id\":\"skill.stone_shelter.rule\",\"typeTag\":\"【类型怪物技能】\",\"containerType\":\"MonsterSkill\","
            + "\"kind\":\"RuleModifier\","
            + "\"requires\":[\"HasOwnerEntity\",\"CardZoneTriggerable\"],"
            + "\"conditions\":[{\"atom\":\"EventFilterTargetNotSelf\",\"targetKind\":\"Monster\"}],"
            + "\"ruleModifier\":{\"rule\":\"DamageFlatDelta\",\"op\":\"Add\",\"value\":-1,"
            + "\"layer\":\"Persistent\",\"scope\":\"Permanent\",\"source\":\"skill.stone_shelter\"}}";

        private static readonly SlotId sShrimpSlot = SlotId.Board(2);
        private static readonly SlotId sShelterSlot = SlotId.Board(4);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;
        private IEffectSystem mEffects;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 1UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
            mEffects = mArch.GetSystem<IEffectSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void SingleShelter_ReducesOtherMonsterDamageByExactlyOne()
        {
            Assert.IsTrue(mPhase.StartNode(CreateShrimpAndShelterNode()).Accepted);

            var shrimpUid = FindAndPlace("monster.stone_shrimp", sShrimpSlot);
            var shelterUid = FindAndPlace("monster.shelter_stone", sShelterSlot);
            Assert.Greater(shrimpUid, 0);
            Assert.Greater(shelterUid, 0);
            Assert.AreNotEqual(shrimpUid, shelterUid);

            ActivateStoneShelter(shelterUid);
            PrepareAvatarAttack(4);

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var shrimp = registry.Get(shrimpUid);
            Assert.AreEqual(2, (int)shrimp.Stats.GetBase(StatId.Armor), "石虾开局甲应为 2");
            Assert.AreEqual(2, (int)shrimp.Stats.GetBase(StatId.Hp), "石虾开局血应为 2");

            var startIndex = mPipeline.EventLog.Entries.Count;
            var hit = mPhase.ApplyCombatHit(board.AvatarUid.Value, shrimpUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);

            var damage = FindPrimaryDamageTo(startIndex, shrimpUid);
            Assert.AreEqual(
                3,
                damage,
                "单庇护石应对其他怪物 DamageFlatDelta=-1：4 攻 → amount=3（若为 2 则疑似双算）");
            Assert.AreEqual(1, (int)shrimp.Stats.GetBase(StatId.Hp), "3 伤破甲 2 后应剩 1 血");
            Assert.AreEqual(0, StatArmorUtility.GetCurrentArmor(shrimp));
            Assert.AreNotEqual(ZoneId.Graveyard, shrimp.Zone.Value, "未击杀，不应进坟场");
        }

        [Test]
        public void NoShelter_FourAttackKillsShrimpWithArmor2Hp2()
        {
            Assert.IsTrue(mPhase.StartNode(CreateShrimpOnlyNode()).Accepted);

            var shrimpUid = FindAndPlace("monster.stone_shrimp", sShrimpSlot);
            Assert.Greater(shrimpUid, 0);
            PrepareAvatarAttack(4);

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var startIndex = mPipeline.EventLog.Entries.Count;
            var hit = mPhase.ApplyCombatHit(board.AvatarUid.Value, shrimpUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);

            var damage = FindPrimaryDamageTo(startIndex, shrimpUid);
            Assert.AreEqual(4, damage, "无庇护时应全额 4 伤");
            var shrimp = registry.Get(shrimpUid);
            Assert.AreEqual(ZoneId.Graveyard, shrimp.Zone.Value, "4 伤应击杀甲2血2 的石虾");
        }

        [Test]
        public void DoubleActivateSameShelter_StacksFlatDelta_DocumentsCurrentBehavior()
        {
            // 表征：同 owner 连续 Activate 两次会叠两层 -1（无去重）。
            // 生产若出现 amount=2，优先查庇护石是否被 Bind 两次，而非公式本身。
            Assert.IsTrue(mPhase.StartNode(CreateShrimpAndShelterNode()).Accepted);

            var shrimpUid = FindAndPlace("monster.stone_shrimp", sShrimpSlot);
            var shelterUid = FindAndPlace("monster.shelter_stone", sShelterSlot);
            ActivateStoneShelter(shelterUid);
            ActivateStoneShelter(shelterUid);
            PrepareAvatarAttack(4);

            var board = mArch.GetModel<BoardModel>();
            var startIndex = mPipeline.EventLog.Entries.Count;
            Assert.IsTrue(mPhase.ApplyCombatHit(board.AvatarUid.Value, shrimpUid).Accepted);

            var damage = FindPrimaryDamageTo(startIndex, shrimpUid);
            Assert.AreEqual(
                2,
                damage,
                "当前实现：同卡 Activate×2 → flat=-2 → 4 攻结算 2；单次 Activate 见 SingleShelter 用例");
        }

        private void ActivateStoneShelter(int ownerUid)
        {
            var definition = mEffects.ParseJson(StoneShelterRuleJson);
            var validation = mEffects.Validate(definition);
            Assert.IsTrue(validation.IsValid, "石庇护 DSL 应可解析, issues=" + validation.Issues.Count);
            mEffects.Activate(
                definition,
                new EffectOwner(EffectContainerType.MonsterSkill, "skill.stone_shelter", ownerUid));
        }

        private void PrepareAvatarAttack(int attack)
        {
            var board = mArch.GetModel<BoardModel>();
            var avatar = mArch.GetModel<CardRegistry>().Get(board.AvatarUid.Value);
            avatar.Stats.SetBase(StatId.Attack, attack);
            avatar.Stats.SetBase(StatId.MaxHp, 10);
            avatar.Stats.SetBase(StatId.Hp, 10);
            avatar.Stats.SetBase(StatId.Armor, 0);
        }

        private static NodeDeckOptions CreateShrimpAndShelterNode()
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 2
            }
                .AddEnemyCard(new CardDraft("monster.stone_shrimp", CardKind.Monster)
                {
                    MaxHp = 2,
                    Attack = 6,
                    Armor = 2
                })
                .AddEnemyCard(new CardDraft("monster.shelter_stone", CardKind.Monster)
                {
                    MaxHp = 4,
                    Attack = 6,
                    Armor = 2
                });
        }

        private static NodeDeckOptions CreateShrimpOnlyNode()
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1
            }.AddEnemyCard(new CardDraft("monster.stone_shrimp", CardKind.Monster)
            {
                MaxHp = 2,
                Attack = 6,
                Armor = 2
            });
        }

        private int FindAndPlace(string defId, SlotId targetSlot)
        {
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var uid = 0;
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                if (slot == board.AvatarSlot.Value)
                {
                    continue;
                }

                var cardUid = board.GetCardUid(slot);
                if (cardUid > 0 && registry.Get(cardUid).DefId == defId)
                {
                    uid = cardUid;
                    break;
                }
            }

            Assert.Greater(uid, 0, "未找到 " + defId);
            var card = registry.Get(uid);
            if (card.Slot.Value == targetSlot)
            {
                return uid;
            }

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
            board.PlaceCard(card, targetSlot);
            return uid;
        }

        private int FindPrimaryDamageTo(int startIndex, int targetUid)
        {
            var entries = mPipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Type == CoreEventType.DamageDealt && e.TargetUid == targetUid && e.Amount > 0)
                {
                    return e.Amount;
                }
            }

            Assert.Fail("未找到对 uid=" + targetUid + " 的 DamageDealt");
            return 0;
        }
    }
}

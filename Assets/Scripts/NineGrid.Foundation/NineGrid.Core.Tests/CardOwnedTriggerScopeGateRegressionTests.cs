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
    /// EffectOwnerScopeGate 薄层：双怪误触拦截、本卡事件放行、schema 硬规则、与已修补 Atom 二验。
    /// </summary>
    public sealed class CardOwnedTriggerScopeGateRegressionTests
    {
        private const string DamageTakenGateJson =
            "{\"id\":\"test.gate.damage_taken\",\"typeTag\":\"【类型怪物技能】\",\"containerType\":\"MonsterSkill\","
            + "\"kind\":\"Triggered\","
            + "\"trigger\":{\"atom\":\"OnDamageTaken\"},"
            + "\"target\":{\"atom\":\"Self\"},"
            + "\"action\":{\"atom\":\"ModifyBaseStat\",\"stat\":\"Attack\",\"delta\":1,\"reason\":\"test.gate.damage_taken\"}}";

        private const string UnscopedCumulativeJson =
            "{\"id\":\"test.gate.cumulative\",\"typeTag\":\"【类型怪物技能】\",\"containerType\":\"MonsterSkill\","
            + "\"kind\":\"Triggered\","
            + "\"trigger\":{\"atom\":\"OnCumulative\",\"metric\":\"armorLost\",\"threshold\":1},"
            + "\"target\":{\"atom\":\"Self\"},"
            + "\"action\":{\"atom\":\"ModifyBaseStat\",\"stat\":\"Attack\",\"delta\":1,\"reason\":\"test.gate.cumulative\"}}";

        private const string BadBareOnBattleJson =
            "{\"id\":\"test.gate.bad_battle\",\"typeTag\":\"【类型怪物技能】\",\"containerType\":\"MonsterSkill\","
            + "\"kind\":\"Triggered\","
            + "\"trigger\":{\"atom\":\"OnBattle\",\"targetKind\":\"Monster\"},"
            + "\"target\":{\"atom\":\"Self\"},"
            + "\"action\":{\"atom\":\"Rotate\",\"count\":1}}";

        private const string BadGlobalRemoveJson =
            "{\"id\":\"test.gate.bad_remove\",\"typeTag\":\"【类型怪物技能】\",\"containerType\":\"MonsterSkill\","
            + "\"kind\":\"Triggered\","
            + "\"trigger\":{\"atom\":\"OnRemove\",\"ownerOnly\":false},"
            + "\"target\":{\"atom\":\"Self\"},"
            + "\"action\":{\"atom\":\"ModifyBaseStat\",\"stat\":\"Attack\",\"delta\":1,\"reason\":\"test.gate.bad_remove\"}}";

        private const string SharpStoneEffectJson =
            "{\"id\":\"skill.sharp_stone.armor_break\",\"typeTag\":\"【类型怪物技能】\",\"containerType\":\"MonsterSkill\",\"kind\":\"Triggered\",\"trigger\":{\"atom\":\"OnArmorBreak\"},\"target\":{\"atom\":\"Player\"},\"action\":{\"atom\":\"DealDamage\",\"amount\":1,\"actor\":\"Self\"}}";

        private static readonly SlotId sObserverSlot = SlotId.Board(2);
        private static readonly SlotId sVictimSlot = SlotId.Board(4);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;
        private IEffectSystem mEffects;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, TableNineContentCatalog.CreateDefault());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 17UL });
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
        public void OnDamageTaken_OtherMonsterDamaged_ObserverDoesNotGainAttack()
        {
            Assert.IsTrue(mPhase.StartNode(CreateTwoMonsterNode()).Accepted);
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();

            var observerUid = PlaceBoardCard("monster.test_a", sObserverSlot);
            var victimUid = PlaceBoardCard("monster.test_b", sVictimSlot);
            ActivateEffect(DamageTakenGateJson, observerUid, "test.gate.damage_taken");

            var atkBefore = (int)registry.Get(observerUid).Stats.GetBase(StatId.Attack);
            PrepareAvatarAttack(5);

            var hit = mPhase.ApplyCombatHit(board.AvatarUid.Value, victimUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);
            Assert.AreEqual(atkBefore, (int)registry.Get(observerUid).Stats.GetBase(StatId.Attack), "他怪受伤时观察者不得触发 OnDamageTaken");
        }

        [Test]
        public void OnDamageTaken_SelfDamaged_TriggersOnce()
        {
            Assert.IsTrue(mPhase.StartNode(CreateTwoMonsterNode()).Accepted);
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();

            var observerUid = PlaceBoardCard("monster.test_a", sObserverSlot);
            PlaceBoardCard("monster.test_b", sVictimSlot);
            ActivateEffect(DamageTakenGateJson, observerUid, "test.gate.damage_taken");

            var atkBefore = (int)registry.Get(observerUid).Stats.GetBase(StatId.Attack);
            PrepareAvatarAttack(5);

            var hit = mPhase.ApplyCombatHit(board.AvatarUid.Value, observerUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);
            Assert.AreEqual(atkBefore + 1, (int)registry.Get(observerUid).Stats.GetBase(StatId.Attack), "本怪受伤应触发 OnDamageTaken 一次");
        }

        [Test]
        public void OnCumulative_Unscoped_OtherMonsterArmorLost_DoesNotTriggerObserver()
        {
            Assert.IsTrue(mPhase.StartNode(CreateTwoMonsterNode(armor: 2)).Accepted);
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();

            var observerUid = PlaceBoardCard("monster.test_a", sObserverSlot);
            var victimUid = PlaceBoardCard("monster.test_b", sVictimSlot);
            ActivateEffect(UnscopedCumulativeJson, observerUid, "test.gate.cumulative");

            var atkBefore = (int)registry.Get(observerUid).Stats.GetBase(StatId.Attack);
            PrepareAvatarAttack(3);

            var hit = mPhase.ApplyCombatHit(board.AvatarUid.Value, victimUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);
            Assert.AreEqual(atkBefore, (int)registry.Get(observerUid).Stats.GetBase(StatId.Attack), "无 scope 的 OnCumulative 不得因他怪掉甲误触");
        }

        [Test]
        public void Validate_MonsterSkillBareOnBattle_ReportsScopeError()
        {
            var definition = mEffects.ParseJson(BadBareOnBattleJson);
            var validation = mEffects.Validate(definition);
            Assert.IsFalse(validation.IsValid);
            Assert.IsTrue(HasIssue(validation, "scope.monster-on-battle"));
        }

        [Test]
        public void Validate_OnRemoveGlobalWithoutFilter_ReportsScopeError()
        {
            var definition = mEffects.ParseJson(BadGlobalRemoveJson);
            var validation = mEffects.Validate(definition);
            Assert.IsFalse(validation.IsValid);
            Assert.IsTrue(HasIssue(validation, "scope.remove-global"));
        }

        [Test]
        public void PatchedOnRemoveAndOnArmorBreak_StillWorkWithGateSecondCheck()
        {
            Assert.IsTrue(mPhase.StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            }).Accepted);

            SpawnOnBoard("monster.big_skeleton", sObserverSlot);
            SpawnOnBoard("monster.headless_skeleton", sVictimSlot);

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var sharpUid = SpawnOnBoardReturnUid("monster.sharp_stone", SlotId.Board(7));
            ActivateEffect(SharpStoneEffectJson, sharpUid, "skill.sharp_stone.armor_break");

            var skullBefore = CountDefInDrawPile("monster.skull_head");
            PrepareAvatarAttack(10);

            var victimUid = board.GetCardUid(sVictimSlot);
            var hit = mPhase.ApplyCombatHit(board.AvatarUid.Value, victimUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);
            Assert.AreEqual(skullBefore, CountDefInDrawPile("monster.skull_head"), "二验：他卡移除不得触发散架");

            registry.Get(sharpUid).Stats.SetBase(StatId.Armor, 1);
            var avatarUid = board.AvatarUid.Value;
            registry.Get(avatarUid).Stats.SetBase(StatId.Armor, 1);
            registry.Get(avatarUid).Stats.SetBase(StatId.Attack, 3);
            var tankUid = SpawnOnBoardReturnUid("monster.tank", SlotId.Board(1));
            registry.Get(tankUid).Stats.SetBase(StatId.Armor, 3);

            var startIndex = mPipeline.EventLog.Entries.Count;
            var tankHit = mPhase.ApplyCombatHit(avatarUid, tankUid);
            Assert.IsTrue(tankHit.Accepted, tankHit.Reason);
            Assert.AreEqual(0, CountDamageToPlayerSince(startIndex), "二验：他怪碎甲不得触发尖石");
        }

        private void ActivateEffect(string json, int ownerUid, string sourceDefId)
        {
            var definition = mEffects.ParseJson(json);
            Assert.IsTrue(mEffects.Validate(definition).IsValid, json);
            mEffects.Activate(definition, new EffectOwner(EffectContainerType.MonsterSkill, sourceDefId, ownerUid));
        }

        private static NodeDeckOptions CreateTwoMonsterNode(int armor = 0)
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 2
            }
                .AddEnemyCard(new CardDraft("monster.test_a", CardKind.Monster) { MaxHp = 20, Attack = 0, Armor = armor })
                .AddEnemyCard(new CardDraft("monster.test_b", CardKind.Monster) { MaxHp = 20, Attack = 0, Armor = armor });
        }

        private int PlaceBoardCard(string defId, SlotId slot)
        {
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var candidate = SlotId.Board(i);
                if (candidate == board.AvatarSlot.Value)
                {
                    continue;
                }

                var uid = board.GetCardUid(candidate);
                if (uid > 0 && registry.Get(uid).DefId == defId)
                {
                    if (candidate != slot)
                    {
                        board.ClearSlot(candidate);
                        board.PlaceCard(registry.Get(uid), slot);
                    }

                    return uid;
                }
            }

            Assert.Fail("未找到 " + defId);
            return 0;
        }

        private void SpawnOnBoard(string defId, SlotId slot)
        {
            mPipeline.Enqueue(new SpawnCardAction(defId, CardKind.Monster, ZoneId.Board, slot, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
        }

        private int SpawnOnBoardReturnUid(string defId, SlotId slot)
        {
            SpawnOnBoard(defId, slot);
            return mArch.GetModel<BoardModel>().GetCardUid(slot);
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

        private int CountDefInDrawPile(string defId)
        {
            var registry = mArch.GetModel<CardRegistry>();
            var deck = mArch.GetModel<DeckModel>();
            var count = 0;
            for (var i = 0; i < deck.DrawPileUids.Count; i++)
            {
                if (registry.Get(deck.DrawPileUids[i]).DefId == defId)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountDamageToPlayerSince(int startIndex)
        {
            var board = mArch.GetModel<BoardModel>();
            var avatarUid = board.AvatarUid.Value;
            var entries = mPipeline.EventLog.Entries;
            var count = 0;
            for (var i = startIndex; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Type == CoreEventType.DamageDealt && entry.TargetUid == avatarUid && entry.Amount > 0)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool HasIssue(EffectValidationResult validation, string code)
        {
            for (var i = 0; i < validation.Issues.Count; i++)
            {
                if (validation.Issues[i].Code == code)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

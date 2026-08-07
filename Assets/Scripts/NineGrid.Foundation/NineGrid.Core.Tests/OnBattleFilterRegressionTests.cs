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
    /// 阶段一：裸 OnBattle 补过滤 + 暴力卡 sourceAction 约束回归。
    /// </summary>
    public sealed class OnBattleFilterRegressionTests
    {
        private const string BattleHardenedEffectJson =
            "{\"id\":\"relic.battle_hardened.battle\",\"typeTag\":\"【类型遗物】\",\"containerType\":\"Relic\","
            + "\"kind\":\"Triggered\","
            + "\"requires\":[\"NoOwnerEntity\"],"
            + "\"trigger\":{\"atom\":\"OnBattle\",\"sourceAction\":\"DealDamage\",\"targetKind\":\"Monster\",\"maxActionDepth\":0},"
            + "\"conditions\":[{\"atom\":\"EventFilterActorIsPlayer\",\"eventType\":\"DamageDealt\",\"targetKind\":\"Monster\"}],"
            + "\"target\":{\"atom\":\"Player\"},"
            + "\"action\":{\"atom\":\"AddModifier\",\"stat\":\"Attack\",\"op\":\"Add\",\"value\":2,\"layer\":\"Temporary\",\"scope\":\"UntilEnemyChanges\",\"source\":\"relic.battle_hardened\"}}";

        private const string SpaceMasteryEffectJson =
            "{\"id\":\"skill.space_mastery.battle\",\"typeTag\":\"【类型怪物技能】\",\"containerType\":\"MonsterSkill\","
            + "\"kind\":\"Triggered\","
            + "\"requires\":[\"HasOwnerEntity\",\"CardZoneTriggerable\"],"
            + "\"trigger\":{\"atom\":\"OnBattle\",\"sourceAction\":\"DealDamage\",\"targetKind\":\"Monster\",\"maxActionDepth\":0},"
            + "\"conditions\":[{\"atom\":\"EventFilterActorIsPlayerTargetIsSelf\",\"eventType\":\"DamageDealt\"}],"
            + "\"target\":{\"atom\":\"Self\"},"
            + "\"action\":{\"atom\":\"Rotate\",\"count\":1}}";

        private static readonly SlotId sPrimarySlot = SlotId.Board(2);
        private static readonly SlotId sSecondarySlot = SlotId.Board(4);

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
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, ContentCatalogBootstrap.Load());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 3UL });
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
        public void BattleHardened_PlayerAttack_AddsAttackOnce_NotOnCounter()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 30, attack: 1)).Accepted);
            PlaceSoleBoardCardAt(sPrimarySlot);
            ActivateBattleHardened();
            PrepareAvatarAttack(5);

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var avatarUid = board.AvatarUid.Value;
            var monsterUid = board.GetCardUid(sPrimarySlot);

            var playerHit = mPhase.ApplyCombatHit(avatarUid, monsterUid);
            Assert.IsTrue(playerHit.Accepted, playerHit.Reason);
            Assert.AreEqual(7, mStats.GetEffectiveInt(registry.Get(avatarUid), StatId.Attack), "玩家出手应触发历战 +2");

            var counterHit = mPhase.ApplyCombatHit(monsterUid, avatarUid);
            Assert.IsTrue(counterHit.Accepted, counterHit.Reason);
            Assert.AreEqual(
                7,
                mStats.GetEffectiveInt(registry.Get(avatarUid), StatId.Attack),
                "怪物反击不应再次触发历战叠攻");
        }

        [Test]
        public void SpaceMastery_PlayerAttackOnSelf_Rotates_NotOnThrowingKnifeOtherTarget()
        {
            Assert.IsTrue(mPhase.StartNode(CreateTwoMonsterNode()).Accepted);
            var spaceMasterUid = FindAndPlace("monster.test_a", sPrimarySlot);
            var otherUid = FindAndPlace("monster.test_b", sSecondarySlot);
            ActivateSpaceMastery(spaceMasterUid);

            var knifeUid = SpawnHelpIntoItemSlots("help.throwing_knife");
            var knifeStart = mPipeline.EventLog.Entries.Count;
            var knifeUse = mPhase.ApplyUseItem(knifeUid, new List<int> { otherUid }, null);
            Assert.IsTrue(knifeUse.Accepted, knifeUse.Reason);
            Assert.IsFalse(
                ContainsTypeSince(knifeStart, CoreEventType.BoardRotated),
                "飞刀直伤其他怪物不应触发空间掌握旋转");

            var board = mArch.GetModel<BoardModel>();
            var combatStart = mPipeline.EventLog.Entries.Count;
            var hit = mPhase.ApplyCombatHit(board.AvatarUid.Value, spaceMasterUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);
            Assert.IsTrue(
                ContainsTypeSince(combatStart, CoreEventType.BoardRotated),
                "玩家与本卡战斗后应旋转一次");
        }

        [Test]
        public void BrutalityCard_ThrowingKnife_DoesNotConsumeOnceMultiplier()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 40, attack: 0)).Accepted);
            var monsterUid = PlaceSoleBoardCardAt(sPrimarySlot);
            PrepareAvatarAttack(5);

            var brutalityUid = SpawnHelpIntoItemSlots("help.brutality_card");
            Assert.IsTrue(mPhase.ApplyUseItem(brutalityUid, null, null).Accepted);
            Assert.AreEqual(1, CountBrutalityOnceRules(), "使用暴力卡后应有 1 条 Once 翻倍规则");

            var knifeUid = SpawnHelpIntoItemSlots("help.throwing_knife");
            Assert.IsTrue(mPhase.ApplyUseItem(knifeUid, new List<int> { monsterUid }, null).Accepted);
            Assert.AreEqual(1, CountBrutalityOnceRules(), "飞刀直伤不应消耗暴力卡 Once 翻倍");

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var monster = registry.Get(monsterUid);
            var hpBefore = (int)monster.Stats.GetBase(StatId.Hp);
            var startIndex = mPipeline.EventLog.Entries.Count;
            var hit = mPhase.ApplyCombatHit(board.AvatarUid.Value, monsterUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);

            var damage = FindPrimaryDamageTo(startIndex, monsterUid);
            Assert.AreEqual(10, damage, "5 攻 × 暴力卡翻倍应对怪物造成 10 点战斗伤害");
            Assert.AreEqual(hpBefore - 10, (int)monster.Stats.GetBase(StatId.Hp));
            Assert.AreEqual(0, CountBrutalityOnceRules(), "战斗伤害后 Once 翻倍应被消耗");

            var secondStartIndex = mPipeline.EventLog.Entries.Count;
            var secondHit = mPhase.ApplyCombatHit(board.AvatarUid.Value, monsterUid);
            Assert.IsTrue(secondHit.Accepted, secondHit.Reason);

            var secondDamage = FindPrimaryDamageTo(secondStartIndex, monsterUid);
            Assert.AreEqual(5, secondDamage, "暴力卡只应强化下一次战斗伤害，第二次应恢复基础攻击伤害");
            Assert.AreEqual(hpBefore - 15, (int)monster.Stats.GetBase(StatId.Hp));
            Assert.AreEqual(0, CountBrutalityOnceRules(), "第二次战斗伤害后不应残留暴力卡 Once 规则");
        }

        private void ActivateBattleHardened()
        {
            var definition = mEffects.ParseJson(BattleHardenedEffectJson);
            Assert.IsTrue(mEffects.Validate(definition).IsValid);
            mEffects.Activate(
                definition,
                new EffectOwner(EffectContainerType.Relic, "relic.battle_hardened", 0));
        }

        private void ActivateSpaceMastery(int ownerUid)
        {
            var definition = mEffects.ParseJson(SpaceMasteryEffectJson);
            Assert.IsTrue(mEffects.Validate(definition).IsValid);
            mEffects.Activate(
                definition,
                new EffectOwner(EffectContainerType.MonsterSkill, "skill.space_mastery", ownerUid));
        }

        private int CountBrutalityOnceRules()
        {
            var modifiers = mStats.RuleModifiers.Modifiers;
            var count = 0;
            for (var i = 0; i < modifiers.Count; i++)
            {
                var modifier = modifiers[i];
                if (modifier.Rule == RuleId.DamageMultiplier
                    && modifier.Scope == ModifierScope.Once
                    && modifier.Source.Id == "help.brutality_card")
                {
                    count++;
                }
            }

            return count;
        }

        private int SpawnHelpIntoItemSlots(string defId)
        {
            mPipeline.Enqueue(new SpawnCardAction(defId, CardKind.HelpCard, ZoneId.ItemSlots, SlotId.None, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            var deck = mArch.GetModel<DeckModel>();
            Assert.Greater(deck.ItemSlotUids.Count, 0);
            return deck.ItemSlotUids[deck.ItemSlotUids.Count - 1];
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

        private static int FindPrimaryDamageTo(int startIndex, int targetUid)
        {
            return FindPrimaryDamageTo(NineGridArchitecture.Current.GetSystem<IActionPipelineSystem>().EventLog.Entries, startIndex, targetUid);
        }

        private static int FindPrimaryDamageTo(IReadOnlyList<CoreGameEvent> entries, int startIndex, int targetUid)
        {
            for (var i = startIndex; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Type == CoreEventType.DamageDealt && entry.TargetUid == targetUid)
                {
                    return entry.Amount;
                }
            }

            return -1;
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
    }
}

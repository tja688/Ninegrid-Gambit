using System.Collections.Generic;
using System.IO;
using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// #73 / ADR-0010 Phase C：外部门禁拆除后，自陈（单形态原子 / EventFilter / requires）须行为等价地挡住误触。
    /// </summary>
    public sealed class CardOwnedTriggerScopeGateRegressionTests
    {
        private const string DamageTakenSelfScopedJson =
            "{\"id\":\"test.self.damage_taken\",\"typeTag\":\"【类型怪物技能】\",\"containerType\":\"MonsterSkill\","
            + "\"kind\":\"Triggered\","
            + "\"requires\":[\"HasOwnerEntity\",\"CardZoneTriggerable\"],"
            + "\"trigger\":{\"atom\":\"OnDamageTaken\"},"
            + "\"conditions\":[{\"atom\":\"EventFilterTargetIsSelf\",\"eventType\":\"HpChanged\",\"maxDelta\":-1}],"
            + "\"target\":{\"atom\":\"Self\"},"
            + "\"action\":{\"atom\":\"ModifyBaseStat\",\"stat\":\"Attack\",\"delta\":1,\"reason\":\"test.self.damage_taken\"}}";

        private const string SelfArmorLostCumulativeJson =
            "{\"id\":\"test.self.cumulative\",\"typeTag\":\"【类型怪物技能】\",\"containerType\":\"MonsterSkill\","
            + "\"kind\":\"Triggered\","
            + "\"requires\":[\"HasOwnerEntity\",\"CardZoneTriggerable\"],"
            + "\"trigger\":{\"atom\":\"OnSelfArmorLostCumulative\",\"threshold\":1},"
            + "\"target\":{\"atom\":\"Self\"},"
            + "\"action\":{\"atom\":\"ModifyBaseStat\",\"stat\":\"Attack\",\"delta\":1,\"reason\":\"test.self.cumulative\"}}";

        private const string BadBareOnBattleJson =
            "{\"id\":\"test.gate.bad_battle\",\"typeTag\":\"【类型怪物技能】\",\"containerType\":\"MonsterSkill\","
            + "\"kind\":\"Triggered\","
            + "\"requires\":[\"HasOwnerEntity\",\"CardZoneTriggerable\"],"
            + "\"trigger\":{\"atom\":\"OnBattle\",\"targetKind\":\"Monster\"},"
            + "\"target\":{\"atom\":\"Self\"},"
            + "\"action\":{\"atom\":\"Rotate\",\"count\":1}}";

        private const string BadGlobalRemoveJson =
            "{\"id\":\"test.gate.bad_remove\",\"typeTag\":\"【类型怪物技能】\",\"containerType\":\"MonsterSkill\","
            + "\"kind\":\"Triggered\","
            + "\"requires\":[\"HasOwnerEntity\",\"CardZoneTriggerable\"],"
            + "\"trigger\":{\"atom\":\"OnAnyCardRemoved\"},"
            + "\"target\":{\"atom\":\"Self\"},"
            + "\"action\":{\"atom\":\"ModifyBaseStat\",\"stat\":\"Attack\",\"delta\":1,\"reason\":\"test.gate.bad_remove\"}}";

        private const string SharpStoneEffectJson =
            "{\"id\":\"skill.sharp_stone.armor_break\",\"typeTag\":\"【类型怪物技能】\",\"containerType\":\"MonsterSkill\",\"kind\":\"Triggered\","
            + "\"requires\":[\"HasOwnerEntity\",\"CardZoneTriggerable\"],"
            + "\"trigger\":{\"atom\":\"OnArmorBreak\"},\"target\":{\"atom\":\"Player\"},"
            + "\"action\":{\"atom\":\"DealDamage\",\"amount\":1,\"actor\":\"Self\"}}";

        private const string SelfMoveToSlotJson =
            "{\"id\":\"test.self.move_slot\",\"typeTag\":\"【类型怪物技能】\",\"containerType\":\"MonsterSkill\","
            + "\"kind\":\"Triggered\","
            + "\"requires\":[\"HasOwnerEntity\",\"CardZoneTriggerable\"],"
            + "\"trigger\":{\"atom\":\"OnMoveToSlot\",\"slot\":3,\"target\":\"Self\"},"
            + "\"target\":{\"atom\":\"Self\"},"
            + "\"action\":{\"atom\":\"ModifyBaseStat\",\"stat\":\"Attack\",\"delta\":1,\"reason\":\"test.self.move_slot\"}}";

        private const string OnSelfRemovedJson =
            "{\"id\":\"test.self.removed\",\"containerType\":\"MonsterSkill\",\"kind\":\"Triggered\","
            + "\"requires\":[\"HasOwnerEntity\",\"CardZoneTriggerable\"],"
            + "\"trigger\":{\"atom\":\"OnSelfRemoved\"},"
            + "\"target\":{\"atom\":\"Self\"},"
            + "\"action\":{\"atom\":\"ModifyBaseStat\",\"stat\":\"Attack\",\"delta\":1,\"reason\":\"test.self.removed\"}}";

        private const string OnSelfUsedJson =
            "{\"id\":\"test.self.used\",\"containerType\":\"HelpCard\",\"kind\":\"Triggered\","
            + "\"requires\":[\"HasOwnerEntity\",\"ActivatedByUse\",\"CardZoneTriggerable\"],"
            + "\"trigger\":{\"atom\":\"OnSelfUsed\"},"
            + "\"target\":{\"atom\":\"Player\"},"
            + "\"action\":{\"atom\":\"ModifyGold\",\"delta\":1}}";

        private static readonly SlotId sObserverSlot = SlotId.Board(2);
        private static readonly SlotId sVictimSlot = SlotId.Board(4);
        private static readonly SlotId sMoveTargetSlot = SlotId.Board(3);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;
        private IEffectSystem mEffects;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, ContentCatalogBootstrap.Load());
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
        public void EffectOwnerScopeGate_SourceFile_IsRemoved()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "Scripts/NineGrid.Foundation/NineGrid.Core/Effects"));
            Assert.IsFalse(
                File.Exists(Path.Combine(root, "EffectOwnerScopeGate.cs")),
                "EffectOwnerScopeGate.cs must be deleted (#73)");
            var effectSystem = File.ReadAllText(Path.Combine(root, "EffectSystem.cs"));
            Assert.IsFalse(effectSystem.Contains("EffectOwnerScopeGate"), "EffectSystem must not call EffectOwnerScopeGate");
            Assert.IsFalse(
                effectSystem.Contains("IsCardOwnedEffectInTriggerableZone"),
                "EffectSystem must not keep zone gate helper");
        }

        [Test]
        public void OnDamageTaken_WithTargetIsSelf_OtherMonsterDamaged_ObserverDoesNotGainAttack()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();

            var observerUid = SpawnOnBoardReturnUid("monster.big_skeleton", sObserverSlot);
            var victimUid = SpawnOnBoardReturnUid("monster.headless_skeleton", sVictimSlot);
            ActivateEffect(DamageTakenSelfScopedJson, observerUid, "test.self.damage_taken");

            var atkBefore = (int)registry.Get(observerUid).Stats.GetBase(StatId.Attack);
            PrepareAvatarAttack(5);

            var hit = mPhase.ApplyCombatHit(board.AvatarUid.Value, victimUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);
            Assert.AreEqual(atkBefore, (int)registry.Get(observerUid).Stats.GetBase(StatId.Attack), "他怪受伤时观察者不得触发自陈 OnDamageTaken");
        }

        [Test]
        public void OnDamageTaken_WithTargetIsSelf_SelfDamaged_TriggersOnce()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();

            var observerUid = SpawnOnBoardReturnUid("monster.big_skeleton", sObserverSlot);
            SpawnOnBoardReturnUid("monster.headless_skeleton", sVictimSlot);
            ActivateEffect(DamageTakenSelfScopedJson, observerUid, "test.self.damage_taken");

            var atkBefore = (int)registry.Get(observerUid).Stats.GetBase(StatId.Attack);
            PrepareAvatarAttack(5);

            var hit = mPhase.ApplyCombatHit(board.AvatarUid.Value, observerUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);
            Assert.AreEqual(atkBefore + 1, (int)registry.Get(observerUid).Stats.GetBase(StatId.Attack), "本怪受伤应触发一次");
        }

        [Test]
        public void OnSelfArmorLostCumulative_OtherMonsterArmorLost_DoesNotTriggerObserver()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();

            var observerUid = SpawnOnBoardReturnUid("monster.big_skeleton", sObserverSlot);
            var victimUid = SpawnOnBoardReturnUid("monster.headless_skeleton", sVictimSlot);
            registry.Get(victimUid).Stats.SetBase(StatId.Armor, 2);
            ActivateEffect(SelfArmorLostCumulativeJson, observerUid, "test.self.cumulative");

            var atkBefore = (int)registry.Get(observerUid).Stats.GetBase(StatId.Attack);
            PrepareAvatarAttack(3);

            var hit = mPhase.ApplyCombatHit(board.AvatarUid.Value, victimUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);
            Assert.AreEqual(atkBefore, (int)registry.Get(observerUid).Stats.GetBase(StatId.Attack), "OnSelfArmorLostCumulative 不得因他怪掉甲误触");
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
        public void OnMoveToSlot_TargetSelf_OtherMonsterMoves_ObserverDoesNotGainAttack()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            var registry = mArch.GetModel<CardRegistry>();

            var observerUid = SpawnOnBoardReturnUid("monster.big_skeleton", sObserverSlot);
            var victimUid = SpawnOnBoardReturnUid("monster.headless_skeleton", sVictimSlot);
            ActivateEffect(SelfMoveToSlotJson, observerUid, "test.self.move_slot");

            var atkBefore = (int)registry.Get(observerUid).Stats.GetBase(StatId.Attack);
            mPipeline.Enqueue(new MoveCardAction(victimUid, sMoveTargetSlot, "test", "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            Assert.AreEqual(atkBefore, (int)registry.Get(observerUid).Stats.GetBase(StatId.Attack), "target:Self 时他怪移动不得触发观察者");
        }

        [Test]
        public void OnMoveToSlot_TargetSelf_SelfMoves_TriggersOnce()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            var registry = mArch.GetModel<CardRegistry>();

            var observerUid = SpawnOnBoardReturnUid("monster.big_skeleton", sObserverSlot);
            SpawnOnBoardReturnUid("monster.headless_skeleton", sVictimSlot);
            ActivateEffect(SelfMoveToSlotJson, observerUid, "test.self.move_slot");

            var atkBefore = (int)registry.Get(observerUid).Stats.GetBase(StatId.Attack);
            mPipeline.Enqueue(new MoveCardAction(observerUid, sMoveTargetSlot, "test", "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            Assert.AreEqual(atkBefore + 1, (int)registry.Get(observerUid).Stats.GetBase(StatId.Attack), "本怪移动到目标格应触发一次");
        }

        [Test]
        public void AbsorbBone_AdjacentRemoved_SelfDeclarationDoesNotBlock()
        {
            Assert.IsTrue(mPhase.StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            }).Accepted);

            SpawnOnBoard("monster.skeleton_king", SlotId.Board(2));
            SpawnOnBoard("monster.headless_skeleton", SlotId.Board(1));

            var board = mArch.GetModel<BoardModel>();
            var kingUid = board.GetCardUid(SlotId.Board(2));
            var atkBefore = (int)mArch.GetModel<CardRegistry>().Get(kingUid).Stats.GetBase(StatId.Attack);
            var startIndex = mPipeline.EventLog.Entries.Count;
            PrepareAvatarAttack(10);

            var victimUid = board.GetCardUid(SlotId.Board(1));
            Assert.Greater(victimUid, 0);
            var hit = mPhase.ApplyCombatHit(board.AvatarUid.Value, victimUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);

            var king = mArch.GetModel<CardRegistry>().Get(kingUid);
            Assert.IsTrue(
                ContainsEffectTriggeredSince(startIndex, "skill.absorb_bone")
                || (int)king.Stats.GetBase(StatId.Attack) > atkBefore,
                "吸骨相邻怪物被移除时应触发（自陈 Adjacent，无外部门禁）");
        }

        [Test]
        public void OneMonsterDeath_DoesNotFireUnrelatedOnSelfRemoved()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();

            var observerUid = SpawnOnBoardReturnUid("monster.big_skeleton", sObserverSlot);
            var victimUid = SpawnOnBoardReturnUid("monster.headless_skeleton", sVictimSlot);
            registry.Get(victimUid).Stats.SetBase(StatId.Hp, 1);
            registry.Get(victimUid).Stats.SetBase(StatId.MaxHp, 1);
            registry.Get(victimUid).Stats.SetBase(StatId.Armor, 0);
            ActivateEffect(OnSelfRemovedJson, observerUid, "test.self.removed");

            var atkBefore = (int)registry.Get(observerUid).Stats.GetBase(StatId.Attack);
            PrepareAvatarAttack(10);
            var hit = mPhase.ApplyCombatHit(board.AvatarUid.Value, victimUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);
            Assert.AreEqual(atkBefore, (int)registry.Get(observerUid).Stats.GetBase(StatId.Attack), "他怪死亡不得触发观察者 OnSelfRemoved");
        }

        [Test]
        public void UseOneHelpCard_DoesNotFireOtherHelpCardOnSelfUsed()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            var usedUid = SpawnHelpIntoItemSlots("help.healing_potion");
            var idleUid = SpawnHelpIntoItemSlots("help.healing_potion");
            Assert.AreNotEqual(usedUid, idleUid);

            var usedInstance = ActivateHelpEffect(OnSelfUsedJson, usedUid, "test.self.used.a");
            var idleInstance = ActivateHelpEffect(OnSelfUsedJson, idleUid, "test.self.used.b");

            var events = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.ItemUsed, 1, "UseItem").WithCard(usedUid)
            };
            var ctx = new TriggerContext(TriggerPoint.OnUseHelpCard, TriggerTiming.Post, null, events, null);

            var usedProbe = mEffects.ProbeWhyNotTriggered(usedInstance.InstanceId, ctx);
            var idleProbe = mEffects.ProbeWhyNotTriggered(idleInstance.InstanceId, ctx);
            Assert.IsTrue(usedProbe.WouldHaveTriggered, usedProbe.Code + ": " + usedProbe.Message);
            Assert.AreEqual(
                EffectNonTriggerFailureKind.TriggerMismatch,
                idleProbe.Kind,
                "他卡被使用时，本卡 OnSelfUsed 不得匹配");

            Assert.Greater(mEffects.BuildTriggeredActions(usedInstance.InstanceId, ctx).Count, 0, "被使用帮助卡应能建出动作");
            Assert.AreEqual(0, mEffects.BuildTriggeredActions(idleInstance.InstanceId, ctx).Count, "未使用帮助卡不得建出动作");
        }

        private int SpawnHelpIntoItemSlots(string defId)
        {
            mPipeline.Enqueue(new SpawnCardAction(defId, CardKind.HelpCard, ZoneId.ItemSlots, SlotId.None, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            var deck = mArch.GetModel<DeckModel>();
            Assert.Greater(deck.ItemSlotUids.Count, 0);
            return deck.ItemSlotUids[deck.ItemSlotUids.Count - 1];
        }

        [Test]
        public void DefaultCatalog_ValidateCatalog_HasNoScopeValidationErrors()
        {
            var report = mArch.GetSystem<IContentSystem>().ValidateCatalog();
            Assert.IsTrue(report.IsValid, FormatScopeIssues(report));
        }

        [Test]
        public void PatchedOnRemoveAndOnArmorBreak_StillWorkWithoutExternalGate()
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
            Assert.AreEqual(skullBefore, CountDefInDrawPile("monster.skull_head"), "他卡移除不得触发散架");

            registry.Get(sharpUid).Stats.SetBase(StatId.Armor, 1);
            var avatarUid = board.AvatarUid.Value;
            registry.Get(avatarUid).Stats.SetBase(StatId.Armor, 1);
            registry.Get(avatarUid).Stats.SetBase(StatId.Attack, 3);
            var tankUid = SpawnOnBoardReturnUid("monster.tank", SlotId.Board(1));
            registry.Get(tankUid).Stats.SetBase(StatId.Armor, 3);

            var startIndex = mPipeline.EventLog.Entries.Count;
            var tankHit = mPhase.ApplyCombatHit(avatarUid, tankUid);
            Assert.IsTrue(tankHit.Accepted, tankHit.Reason);
            Assert.AreEqual(0, CountDamageToPlayerSince(startIndex), "他怪碎甲不得触发尖石");
        }

        private void ActivateEffect(string json, int ownerUid, string sourceDefId)
        {
            var definition = mEffects.ParseJson(json);
            Assert.IsTrue(mEffects.Validate(definition).IsValid, json);
            mEffects.Activate(definition, new EffectOwner(EffectContainerType.MonsterSkill, sourceDefId, ownerUid));
        }

        private EffectInstance ActivateHelpEffect(string json, int ownerUid, string sourceDefId)
        {
            var definition = mEffects.ParseJson(json);
            Assert.IsTrue(mEffects.Validate(definition).IsValid, json);
            return mEffects.Activate(definition, new EffectOwner(EffectContainerType.HelpCard, sourceDefId, ownerUid));
        }

        private static NodeDeckOptions CreateEmptyEnemyNode()
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            };
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

        private bool ContainsEffectTriggeredSince(int startIndex, string sourceDefId)
        {
            var entries = mPipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                var evt = entries[i];
                if (evt.Type == CoreEventType.EffectTriggered
                    && evt.SourceDefId == sourceDefId)
                {
                    return true;
                }
            }

            return false;
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

        private static string FormatScopeIssues(ContentValidationReport report)
        {
            if (report.Issues.Count == 0)
            {
                return string.Empty;
            }

            var scopeIssues = new List<string>();
            for (var i = 0; i < report.Issues.Count; i++)
            {
                var issue = report.Issues[i];
                if (issue.Contains("scope.") || issue.Contains(":scope."))
                {
                    scopeIssues.Add(issue);
                }
            }

            if (scopeIssues.Count > 0)
            {
                return string.Join("; ", scopeIssues);
            }

            return string.Join("; ", report.Issues);
        }
    }
}

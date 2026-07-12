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

        private const string UnscopedMoveToSlotJson =
            "{\"id\":\"test.gate.move_slot\",\"typeTag\":\"【类型怪物技能】\",\"containerType\":\"MonsterSkill\","
            + "\"kind\":\"Triggered\","
            + "\"trigger\":{\"atom\":\"OnMoveToSlot\",\"slot\":3,\"target\":\"Any\"},"
            + "\"target\":{\"atom\":\"Self\"},"
            + "\"action\":{\"atom\":\"ModifyBaseStat\",\"stat\":\"Attack\",\"delta\":1,\"reason\":\"test.gate.move_slot\"}}";

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
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();

            var observerUid = SpawnOnBoardReturnUid("monster.big_skeleton", sObserverSlot);
            var victimUid = SpawnOnBoardReturnUid("monster.headless_skeleton", sVictimSlot);
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
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();

            var observerUid = SpawnOnBoardReturnUid("monster.big_skeleton", sObserverSlot);
            SpawnOnBoardReturnUid("monster.headless_skeleton", sVictimSlot);
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
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();

            var observerUid = SpawnOnBoardReturnUid("monster.big_skeleton", sObserverSlot);
            var victimUid = SpawnOnBoardReturnUid("monster.headless_skeleton", sVictimSlot);
            registry.Get(victimUid).Stats.SetBase(StatId.Armor, 2);
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
        public void OnMoveToSlot_TargetAny_OtherMonsterMoves_ObserverDoesNotGainAttack()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            var registry = mArch.GetModel<CardRegistry>();

            var observerUid = SpawnOnBoardReturnUid("monster.big_skeleton", sObserverSlot);
            var victimUid = SpawnOnBoardReturnUid("monster.headless_skeleton", sVictimSlot);
            ActivateEffect(UnscopedMoveToSlotJson, observerUid, "test.gate.move_slot");

            var atkBefore = (int)registry.Get(observerUid).Stats.GetBase(StatId.Attack);
            mPipeline.Enqueue(new MoveCardAction(victimUid, sMoveTargetSlot, "test", "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            Assert.AreEqual(atkBefore, (int)registry.Get(observerUid).Stats.GetBase(StatId.Attack), "target:Any 时他怪移动到目标格不得触发观察者");
        }

        [Test]
        public void OnMoveToSlot_TargetAny_SelfMoves_TriggersOnce()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            var registry = mArch.GetModel<CardRegistry>();

            var observerUid = SpawnOnBoardReturnUid("monster.big_skeleton", sObserverSlot);
            SpawnOnBoardReturnUid("monster.headless_skeleton", sVictimSlot);
            ActivateEffect(UnscopedMoveToSlotJson, observerUid, "test.gate.move_slot");

            var atkBefore = (int)registry.Get(observerUid).Stats.GetBase(StatId.Attack);
            mPipeline.Enqueue(new MoveCardAction(observerUid, sMoveTargetSlot, "test", "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            Assert.AreEqual(atkBefore + 1, (int)registry.Get(observerUid).Stats.GetBase(StatId.Attack), "target:Any 视同 Self：本怪移动到目标格应触发一次");
        }

        [Test]
        public void AbsorbBone_AdjacentRemoved_GateDoesNotBlock()
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
                "S1 白名单：吸骨相邻怪物被移除时薄层不得误拦");
        }

        [Test]
        public void DefaultCatalog_ValidateCatalog_HasNoScopeValidationErrors()
        {
            var report = mArch.GetSystem<IContentSystem>().ValidateCatalog();
            Assert.IsTrue(report.IsValid, FormatScopeIssues(report));
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

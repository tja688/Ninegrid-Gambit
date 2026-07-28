using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Effects;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// 失败重开 / Bootstrap 后坚硬反伤叠乘回归。
    /// 根因：InitialGameFactory 清卡不清 Effect Trigger，uid 回收后孤儿效果认尸。
    /// </summary>
    public sealed class HardReflectBootstrapRegressionTests
    {
        private const string HardSlot4EffectJson =
            "{\"id\":\"skill.hard.slot4\",\"typeTag\":\"【类型怪物技能】\",\"containerType\":\"MonsterSkill\",\"kind\":\"Triggered\","
            + "\"requires\":[\"HasOwnerEntity\",\"CardZoneTriggerable\"],"
            + "\"trigger\":{\"atom\":\"OnBattle\",\"targetKind\":\"Monster\"},"
            + "\"conditions\":[{\"atom\":\"AtSlot\",\"target\":\"Self\",\"slot\":4},"
            + "{\"atom\":\"EventFilterTargetIsSelf\",\"eventType\":\"ArmorChanged\",\"maxDelta\":-1}],"
            + "\"target\":{\"atom\":\"Player\"},"
            + "\"action\":{\"atom\":\"DealDamage\",\"value\":{\"op\":\"Negate\",\"values\":[{\"source\":\"Event\",\"field\":\"Delta\"}]},\"actor\":\"Self\"}}";

        private static readonly SlotId sHardSlot = SlotId.Board(4);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;
        private IEffectSystem mEffects;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
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
        public void BootstrapCreate_DoesNotStackHardReflectAcrossRuns()
        {
            // 模拟生产路径：同 Architecture 上连续 Create（不清 ResetForTests），再打坚硬怪。
            for (var run = 0; run < 5; run++)
            {
                InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 1UL });
                Assert.IsTrue(mPhase.StartNode(CreateHardMonsterNode()).Accepted);

                var board = mArch.GetModel<BoardModel>();
                var registry = mArch.GetModel<CardRegistry>();
                var monsterUid = FindAndPlace("monster.big_stone", sHardSlot);
                Assert.Greater(monsterUid, 0);
                ActivateHardSlot4(monsterUid);

                var avatarUid = board.AvatarUid.Value;
                var avatar = registry.Get(avatarUid);
                avatar.Stats.SetBase(StatId.MaxHp, 10);
                avatar.Stats.SetBase(StatId.Hp, 10);
                avatar.Stats.SetBase(StatId.Armor, 1);
                avatar.Stats.SetBase(StatId.Attack, 3);

                var startIndex = mPipeline.EventLog.Entries.Count;
                var hit = mPhase.ApplyCombatHit(avatarUid, monsterUid);
                Assert.IsTrue(hit.Accepted, "run=" + run + " " + hit.Reason);

                var events = SliceEvents(startIndex);
                var hardHits = CountHardDamageToAvatar(events, avatarUid);
                Assert.AreEqual(
                    1,
                    hardHits,
                    "run=" + run + " 坚硬应只反伤一次；叠乘说明 Bootstrap 未清 Effect 运行时");
                Assert.AreNotEqual(GamePhase.Defeat, mPhase.CurrentPhase, "run=" + run + " 3 点反伤不应秒杀满血化身");
                Assert.AreEqual(
                    8,
                    mArch.GetSystem<IStatSystem>().GetEffectiveInt(registry.Get(avatarUid), StatId.Hp),
                    "run=" + run + " 期望 10HP+1甲 吃 3 反伤后剩 8HP");
            }
        }

        private void ActivateHardSlot4(int ownerUid)
        {
            var definition = mEffects.ParseJson(HardSlot4EffectJson);
            var validation = mEffects.Validate(definition);
            Assert.IsTrue(validation.IsValid, "坚硬 DSL 应可解析, issues=" + validation.Issues.Count);
            mEffects.Activate(
                definition,
                new EffectOwner(EffectContainerType.MonsterSkill, "skill.hard", ownerUid));
        }

        private static NodeDeckOptions CreateHardMonsterNode()
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1
            }.AddEnemyCard(new CardDraft("monster.big_stone", CardKind.Monster)
            {
                MaxHp = 1,
                Attack = 0,
                Armor = 5
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

        private static int CountHardDamageToAvatar(IReadOnlyList<CoreGameEvent> events, int avatarUid)
        {
            var count = 0;
            for (var i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (e.Type == CoreEventType.DamageDealt
                    && e.TargetUid == avatarUid
                    && e.Amount > 0
                    && string.Equals(e.SourceDefId, "skill.hard", System.StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }
    }
}

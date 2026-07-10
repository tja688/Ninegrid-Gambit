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
    /// 尖石 OnArmorBreak：仅本卡护甲归零触发；场上其他尖石 / 反伤打掉玩家甲不得误触。
    /// </summary>
    public sealed class SharpStoneArmorBreakRegressionTests
    {
        private const string SharpStoneEffectJson =
            "{\"id\":\"skill.sharp_stone.armor_break\",\"typeTag\":\"【类型怪物技能】\",\"containerType\":\"MonsterSkill\",\"kind\":\"Triggered\",\"trigger\":{\"atom\":\"OnArmorBreak\"},\"target\":{\"atom\":\"Player\"},\"action\":{\"atom\":\"DealDamage\",\"amount\":1,\"actor\":\"Self\"}}";

        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);
        private static readonly SlotId sSideSlot = SlotId.Board(4);

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
        public void OnArmorBreak_OtherMonsterArmorBreak_DoesNotTriggerFieldSharpStones()
        {
            Assert.IsTrue(mPhase.StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 3
            }
                .AddEnemyCard(new CardDraft("monster.tank", CardKind.Monster)
                {
                    MaxHp = 5,
                    Attack = 0,
                    Armor = 3
                })
                .AddEnemyCard(new CardDraft("monster.sharp_a", CardKind.Monster)
                {
                    MaxHp = 3,
                    Attack = 0,
                    Armor = 1
                })
                .AddEnemyCard(new CardDraft("monster.sharp_b", CardKind.Monster)
                {
                    MaxHp = 3,
                    Attack = 0,
                    Armor = 1
                })).Accepted);

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var tankUid = FindAndPlace("monster.tank", sAdjacentSlot);
            var sharpA = FindAndPlace("monster.sharp_a", sSideSlot);
            var sharpB = FindUid("monster.sharp_b");
            Assert.Greater(tankUid, 0);
            Assert.Greater(sharpA, 0);
            Assert.Greater(sharpB, 0);

            ActivateSharpStone(sharpA);
            ActivateSharpStone(sharpB);

            var avatarUid = board.AvatarUid.Value;
            var avatar = registry.Get(avatarUid);
            avatar.Stats.SetBase(StatId.MaxHp, 99);
            avatar.Stats.SetBase(StatId.Hp, 99);
            avatar.Stats.SetBase(StatId.Armor, 0);
            avatar.Stats.SetBase(StatId.Attack, 3);

            var hpBefore = mArch.GetSystem<IStatSystem>().GetEffectiveInt(avatar, StatId.Hp);
            var startIndex = mPipeline.EventLog.Entries.Count;
            var hit = mPhase.ApplyCombatHit(avatarUid, tankUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);

            var events = SliceEvents(startIndex);
            Assert.AreEqual(0, CountDamageToAvatar(events, avatarUid), "打碎其他怪护甲时，场上尖石不应触发");
            Assert.AreEqual(
                hpBefore,
                mArch.GetSystem<IStatSystem>().GetEffectiveInt(registry.Get(avatarUid), StatId.Hp));
        }

        [Test]
        public void OnArmorBreak_SelfArmorBreak_TriggersOnce_NoChainFromAvatarArmor()
        {
            Assert.IsTrue(mPhase.StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1
            }.AddEnemyCard(new CardDraft("monster.sharp_stone", CardKind.Monster)
            {
                MaxHp = 3,
                Attack = 0,
                Armor = 1
            })).Accepted);

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var sharpUid = FindAndPlace("monster.sharp_stone", sAdjacentSlot);
            Assert.Greater(sharpUid, 0);
            ActivateSharpStone(sharpUid);

            var avatarUid = board.AvatarUid.Value;
            var avatar = registry.Get(avatarUid);
            avatar.Stats.SetBase(StatId.MaxHp, 99);
            avatar.Stats.SetBase(StatId.Hp, 99);
            avatar.Stats.SetBase(StatId.Armor, 1);
            avatar.Stats.SetBase(StatId.Attack, 3);

            var startIndex = mPipeline.EventLog.Entries.Count;
            var hit = mPhase.ApplyCombatHit(avatarUid, sharpUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);

            var events = SliceEvents(startIndex);
            Assert.AreEqual(
                1,
                CountDamageToAvatar(events, avatarUid),
                "本卡护甲归零应只触发一次尖石；打掉玩家甲不得连锁再触发");
        }

        private void ActivateSharpStone(int ownerUid)
        {
            var definition = mEffects.ParseJson(SharpStoneEffectJson);
            var validation = mEffects.Validate(definition);
            Assert.IsTrue(validation.IsValid, "尖石 DSL 应可解析, issues=" + validation.Issues.Count);
            mEffects.Activate(
                definition,
                new EffectOwner(EffectContainerType.MonsterSkill, "skill.sharp_stone", ownerUid));
        }

        private int FindUid(string defId)
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
                if (uid > 0 && registry.Get(uid).DefId == defId)
                {
                    return uid;
                }
            }

            return 0;
        }

        private int FindAndPlace(string defId, SlotId targetSlot)
        {
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var uid = FindUid(defId);
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

        private static int CountDamageToAvatar(IReadOnlyList<CoreGameEvent> events, int avatarUid)
        {
            var count = 0;
            for (var i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (e.Type == CoreEventType.DamageDealt && e.TargetUid == avatarUid && e.Amount > 0)
                {
                    count++;
                }
            }

            return count;
        }
    }
}

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
    /// 落石：仅场上本卡累计掉甲触发；抽牌堆内实例不得因他卡掉甲洗入石人。
    /// 对照 battlelog-20260711-151955 node3 megalith 进场前狂补石人。
    /// </summary>
    public sealed class FallingRocksDeckGateRegressionTests
    {
        private const string FallingRocksEffectJson =
            "{\"id\":\"skill.falling_rocks.cumulative\",\"typeTag\":\"【类型怪物技能】\",\"containerType\":\"MonsterSkill\","
            + "\"kind\":\"Triggered\","
            + "\"trigger\":{\"atom\":\"OnCumulative\",\"metric\":\"armorLost\",\"threshold\":10,\"targetIs\":\"Self\"},"
            + "\"conditions\":[{\"atom\":\"CardZone\",\"target\":\"Self\",\"zone\":\"Board\"}],"
            + "\"target\":{\"atom\":\"Player\"},"
            + "\"action\":{\"atom\":\"ShuffleInto\",\"defId\":\"monster.stone_man\",\"kind\":\"Monster\",\"count\":1,\"top\":false}}";

        private static readonly SlotId sBoardSlot = SlotId.Board(2);

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
        public void FallingRocks_InDrawPile_OtherArmorLoss_DoesNotShuffleStoneMan()
        {
            Assert.IsTrue(mPhase.StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1
            }.AddEnemyCard(new CardDraft("monster.tank", CardKind.Monster)
            {
                MaxHp = 20,
                Attack = 0,
                Armor = 12
            })).Accepted);

            var tankUid = FindAndPlace("monster.tank", sBoardSlot);
            Assert.Greater(tankUid, 0, "坦克应在场上");

            mPipeline.Enqueue(new SpawnCardAction(
                "monster.megalith", CardKind.Monster, ZoneId.DrawPile, SlotId.None, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            // Spawn 的 OnDeal/OnEnter 可能触发补位，把抽牌堆实例发上场；强制收回抽牌堆以测门禁。
            var megalithUid = FindInDrawPile("monster.megalith");
            if (megalithUid == 0)
            {
                megalithUid = FindOnBoard("monster.megalith");
                Assert.Greater(megalithUid, 0, "应能生成巨石人");
                var megalith = mArch.GetModel<CardRegistry>().Get(megalithUid);
                if (megalith.Zone.Value == ZoneId.Board)
                {
                    mArch.GetModel<BoardModel>().ClearSlot(megalith.Slot.Value);
                }

                mArch.GetModel<DeckModel>().AddToDrawPile(megalith, false);
            }

            Assert.AreEqual(ZoneId.DrawPile, mArch.GetModel<CardRegistry>().Get(megalithUid).Zone.Value);

            ActivateFallingRocks(megalithUid);
            PrepareAvatarAttack(10);

            var stoneBefore = CountDefInDrawPile("monster.stone_man");
            var hit = mPhase.ApplyCombatHit(mArch.GetModel<BoardModel>().AvatarUid.Value, tankUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);

            Assert.AreEqual(
                stoneBefore,
                CountDefInDrawPile("monster.stone_man"),
                "抽牌堆内落石不得因他卡掉甲洗入石人");
        }

        [Test]
        public void FallingRocks_OnBoard_OtherArmorLoss_DoesNotShuffleStoneMan()
        {
            Assert.IsTrue(mPhase.StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 2
            }
                .AddEnemyCard(new CardDraft("monster.tank", CardKind.Monster)
                {
                    MaxHp = 20,
                    Attack = 0,
                    Armor = 12
                })
                .AddEnemyCard(new CardDraft("monster.megalith", CardKind.Monster)
                {
                    MaxHp = 8,
                    Attack = 2,
                    Armor = 12
                })).Accepted);

            var tankUid = FindAndPlace("monster.tank", SlotId.Board(4));
            var megalithUid = FindAndPlace("monster.megalith", sBoardSlot);
            Assert.Greater(tankUid, 0);
            Assert.Greater(megalithUid, 0);

            ActivateFallingRocks(megalithUid);
            PrepareAvatarAttack(10);

            var stoneBefore = CountDefInDrawPile("monster.stone_man");
            var hit = mPhase.ApplyCombatHit(mArch.GetModel<BoardModel>().AvatarUid.Value, tankUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);

            Assert.AreEqual(
                stoneBefore,
                CountDefInDrawPile("monster.stone_man"),
                "场上落石仅累计本卡掉甲，他卡掉甲不得洗入石人");
        }

        [Test]
        public void FallingRocks_OnBoard_SelfArmorLoss10_ShufflesStoneMan()
        {
            Assert.IsTrue(mPhase.StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1
            }.AddEnemyCard(new CardDraft("monster.megalith", CardKind.Monster)
            {
                MaxHp = 8,
                Attack = 2,
                Armor = 12
            })).Accepted);

            var megalithUid = FindAndPlace("monster.megalith", sBoardSlot);
            Assert.Greater(megalithUid, 0);
            ActivateFallingRocks(megalithUid);
            PrepareAvatarAttack(10);

            var stoneBefore = CountDefInDrawPile("monster.stone_man");
            var hit = mPhase.ApplyCombatHit(mArch.GetModel<BoardModel>().AvatarUid.Value, megalithUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);

            Assert.AreEqual(
                stoneBefore + 1,
                CountDefInDrawPile("monster.stone_man"),
                "场上本卡累计掉甲满10应洗入一张石人");
        }

        private void ActivateFallingRocks(int ownerUid)
        {
            var definition = mEffects.ParseJson(FallingRocksEffectJson);
            var validation = mEffects.Validate(definition);
            Assert.IsTrue(validation.IsValid, "落石 DSL 应可解析, issues=" + validation.Issues.Count);
            mEffects.Activate(
                definition,
                new EffectOwner(EffectContainerType.MonsterSkill, "skill.falling_rocks", ownerUid));
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

        private int FindInDrawPile(string defId)
        {
            var registry = mArch.GetModel<CardRegistry>();
            var deck = mArch.GetModel<DeckModel>();
            for (var i = 0; i < deck.DrawPileUids.Count; i++)
            {
                var uid = deck.DrawPileUids[i];
                if (registry.Get(uid).DefId == defId)
                {
                    return uid;
                }
            }

            return 0;
        }

        private int FindOnBoard(string defId)
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
            var uid = FindOnBoard(defId);
            if (uid == 0)
            {
                uid = FindInDrawPile(defId);
            }

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
    }
}

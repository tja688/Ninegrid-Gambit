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
    /// 全局区域门禁：卡组 / 坟场实例不得触发卡牌挂载效果；场上实例正常触发。
    /// </summary>
    public sealed class CardZoneDeckGateRegressionTests
    {
        private const string StoneLoverEffectJson =
            "{\"id\":\"skill.stone_lover.armor_lost\",\"typeTag\":\"【类型怪物技能】\",\"containerType\":\"MonsterSkill\","
            + "\"kind\":\"Triggered\","
            + "\"trigger\":{\"atom\":\"OnEvent\",\"eventType\":\"ArmorChanged\"},"
            + "\"target\":{\"atom\":\"Self\"},"
            + "\"action\":{\"atom\":\"ModifyBaseStat\",\"stat\":\"Attack\",\"delta\":1,\"reason\":\"skill.stone_lover\"},"
            + "\"conditions\":[{\"atom\":\"EventFilter\",\"eventType\":\"ArmorChanged\",\"maxDelta\":-1}]}";

        private static readonly SlotId sBoardSlot = SlotId.Board(2);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IEffectSystem mEffects;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, ContentCatalogBootstrap.Load());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 3UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mEffects = mArch.GetSystem<IEffectSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void StoneLover_InDrawPile_OtherArmorLoss_DoesNotGainAttack()
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
                .AddEnemyCard(new CardDraft("monster.tank", CardKind.Monster)
                {
                    MaxHp = 20,
                    Attack = 2,
                    Armor = 2
                })).Accepted);

            var tankOnBoardUid = FindAndPlace("monster.tank", SlotId.Board(4));
            var tankInDeckUid = FindInDrawPile("monster.tank", excludeUid: tankOnBoardUid);
            if (tankInDeckUid == 0)
            {
                tankInDeckUid = FindOnBoard("monster.tank", excludeUid: tankOnBoardUid);
                MoveCardToDrawPile(tankInDeckUid);
            }

            Assert.AreEqual(ZoneId.DrawPile, mArch.GetModel<CardRegistry>().Get(tankInDeckUid).Zone.Value);
            ActivateStoneLover(tankInDeckUid);
            PrepareAvatarAttack(10);

            var attackBefore = mArch.GetModel<CardRegistry>().Get(tankInDeckUid).Stats.GetBase(StatId.Attack);
            var hit = mPhase.ApplyCombatHit(mArch.GetModel<BoardModel>().AvatarUid.Value, tankOnBoardUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);

            Assert.AreEqual(
                attackBefore,
                mArch.GetModel<CardRegistry>().Get(tankInDeckUid).Stats.GetBase(StatId.Attack),
                "抽牌堆内实例不得因他卡掉甲而攻击+1");
        }

        [Test]
        public void StoneLover_OnBoard_OtherArmorLoss_GainsAttack()
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
                .AddEnemyCard(new CardDraft("monster.tank", CardKind.Monster)
                {
                    MaxHp = 20,
                    Attack = 2,
                    Armor = 2
                })).Accepted);

            var tankTargetUid = FindAndPlace("monster.tank", SlotId.Board(4));
            var stoneLoverUid = FindAndPlace("monster.tank", sBoardSlot, excludeUid: tankTargetUid);
            ActivateStoneLover(stoneLoverUid);
            PrepareAvatarAttack(10);

            var attackBefore = mArch.GetModel<CardRegistry>().Get(stoneLoverUid).Stats.GetBase(StatId.Attack);
            var hit = mPhase.ApplyCombatHit(mArch.GetModel<BoardModel>().AvatarUid.Value, tankTargetUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);

            Assert.AreEqual(
                attackBefore + 1,
                mArch.GetModel<CardRegistry>().Get(stoneLoverUid).Stats.GetBase(StatId.Attack));
        }

        private void ActivateStoneLover(int ownerUid)
        {
            var definition = mEffects.ParseJson(StoneLoverEffectJson);
            var validation = mEffects.Validate(definition);
            Assert.IsTrue(validation.IsValid, "石头爱好者 DSL 应可解析");
            mEffects.Activate(
                definition,
                new EffectOwner(EffectContainerType.MonsterSkill, "skill.stone_lover", ownerUid));
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

        private void MoveCardToDrawPile(int uid)
        {
            var board = mArch.GetModel<BoardModel>();
            var deck = mArch.GetModel<DeckModel>();
            var card = mArch.GetModel<CardRegistry>().Get(uid);
            if (card.Zone.Value == ZoneId.Board)
            {
                board.ClearSlot(card.Slot.Value);
            }

            deck.AddToDrawPile(card, false);
        }

        private int FindInDrawPile(string defId, int excludeUid = 0)
        {
            var registry = mArch.GetModel<CardRegistry>();
            var deck = mArch.GetModel<DeckModel>();
            for (var i = 0; i < deck.DrawPileUids.Count; i++)
            {
                var uid = deck.DrawPileUids[i];
                if (uid == excludeUid)
                {
                    continue;
                }

                if (registry.Get(uid).DefId == defId)
                {
                    return uid;
                }
            }

            return 0;
        }

        private int FindOnBoard(string defId, int excludeUid = 0)
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
                if (uid > 0 && uid != excludeUid && registry.Get(uid).DefId == defId)
                {
                    return uid;
                }
            }

            return 0;
        }

        private int FindAndPlace(string defId, SlotId targetSlot, int excludeUid = 0)
        {
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var uid = FindOnBoard(defId, excludeUid);
            if (uid == 0)
            {
                uid = FindInDrawPile(defId, excludeUid);
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
                board.ClearSlot(card.Slot.Value);
            }

            board.PlaceCard(card, targetSlot);
            return uid;
        }
    }
}

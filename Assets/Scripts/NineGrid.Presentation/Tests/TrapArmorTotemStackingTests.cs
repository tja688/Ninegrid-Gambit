using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 护甲增幅器永久叠甲回归：本卡每次移动/旋转落地后，正交邻接怪与玩家各 +1 当前护甲，可无限叠加且离邻不回收。
    /// </summary>
    public class TrapArmorTotemStackingTests
    {
        private IArchitecture mArch;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Interface;
            mArch.GetModel<RunModel>().SetPhase(GamePhase.InteractionLoop);
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void Rotate_AdjacentMonstersGainStackingArmor()
        {
            var totem = CreateTrapOnBoard("trap.armor_totem", SlotId.Board(8));
            var near = CreateMonsterOnBoard("monster.test.near", SlotId.Board(7));
            var far = CreateMonsterOnBoard("monster.test.far", SlotId.Board(1));
            ActivateTotemEffects(totem);

            Run(new RotateBoardClockwiseAction());
            Assert.AreEqual(1, StatArmorUtility.GetCurrentArmor(near), "第一次旋转：邻接怪 +1 甲");
            Assert.AreEqual(0, StatArmorUtility.GetCurrentArmor(far), "远处怪不加甲");

            Run(new RotateBoardClockwiseAction());
            Assert.AreEqual(2, StatArmorUtility.GetCurrentArmor(near), "第二次旋转：邻接怪再 +1 甲");
            Assert.AreEqual(0, StatArmorUtility.GetCurrentArmor(far), "远处怪仍不加甲");
        }

        [Test]
        public void LeaveAdjacent_ArmorRetained()
        {
            var totem = CreateTrapOnBoard("trap.armor_totem", SlotId.Board(8));
            var near = CreateMonsterOnBoard("monster.test.near", SlotId.Board(7));
            ActivateTotemEffects(totem);

            Run(new RotateBoardClockwiseAction());
            Assert.AreEqual(1, StatArmorUtility.GetCurrentArmor(near));

            Run(new MoveCardAction(near.Uid, SlotId.Board(1)));
            Assert.AreEqual(1, StatArmorUtility.GetCurrentArmor(near), "离开邻接后护甲保留");
        }

        [Test]
        public void Rotate_PlayerAdjacent_GainsStackingArmor()
        {
            var content = mArch.GetSystem<IContentSystem>();
            content.Load(NineGrid.Content.ContentCatalogBootstrap.Load());
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, content.Catalog);

            var avatar = CreateAvatarOnBoard(SlotId.Board(5));
            var totem = CreateRealCardOnBoard("trap.armor_totem", SlotId.Board(8));
            CreateRealCardOnBoard("monster.melee_3", SlotId.Board(7));
            CreateMonsterOnBoard("monster.test.far", SlotId.Board(1));

            Run(new RotateBoardClockwiseAction());
            Assert.AreEqual(1, StatArmorUtility.GetCurrentArmor(avatar), "邻接玩家第一次 +1 甲");

            Run(new RotateBoardClockwiseAction());
            Assert.AreEqual(2, StatArmorUtility.GetCurrentArmor(avatar), "邻接玩家第二次再 +1 甲");
        }

        [Test]
        public void RealContent_RotationsStackArmorOnAdjacent()
        {
            var content = mArch.GetSystem<IContentSystem>();
            content.Load(NineGrid.Content.ContentCatalogBootstrap.Load());
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, content.Catalog);
            Assert.IsTrue(content.HasCatalog, "需要真实内容目录");

            var avatar = CreateAvatarOnBoard(SlotId.Board(5));
            var totem = CreateRealCardOnBoard("trap.armor_totem", SlotId.Board(8));
            var m1 = CreateRealCardOnBoard("monster.melee_3", SlotId.Board(7));
            var m2 = CreateRealCardOnBoard("monster.melee_3", SlotId.Board(1));
            var monsters = new[] { m1, m2 };
            var monsterGainCounts = new int[monsters.Length];
            var playerArmorTotal = 0;

            for (var step = 0; step < 8; step++)
            {
                var playerArmorBefore = StatArmorUtility.GetCurrentArmor(avatar);
                var monsterArmorBefore = new int[monsters.Length];
                for (var i = 0; i < monsters.Length; i++)
                {
                    monsterArmorBefore[i] = StatArmorUtility.GetCurrentArmor(monsters[i]);
                }

                Run(new RotateBoardClockwiseAction(step % 2 == 0));

                var playerArmorAfter = StatArmorUtility.GetCurrentArmor(avatar);
                if (playerArmorAfter > playerArmorBefore)
                {
                    playerArmorTotal += playerArmorAfter - playerArmorBefore;
                }

                Assert.AreEqual(
                    playerArmorTotal,
                    playerArmorAfter,
                    "真实内容步" + step + "：玩家叠甲");

                for (var i = 0; i < monsters.Length; i++)
                {
                    var monsterArmorAfter = StatArmorUtility.GetCurrentArmor(monsters[i]);
                    if (monsterArmorAfter > monsterArmorBefore[i])
                    {
                        monsterGainCounts[i] += monsterArmorAfter - monsterArmorBefore[i];
                    }

                    Assert.AreEqual(
                        monsterGainCounts[i],
                        monsterArmorAfter,
                        "真实内容步" + step + "：" + monsters[i].DefId + " 叠甲");
                }
            }
        }

        // ==================== 基建 ====================

        private CardInstance CreateRealCardOnBoard(string defId, SlotId slot)
        {
            var content = mArch.GetSystem<IContentSystem>();
            var registry = mArch.GetModel<CardRegistry>();
            var draft = content.CreateDraft(defId);
            Assert.AreNotEqual(CardKind.Unknown, draft.Kind, defId + " 应在内容目录中");
            var card = draft.Create(registry);
            content.ApplyContentToCard(card);
            mArch.GetModel<BoardModel>().PlaceCard(card, slot);
            return card;
        }

        private const string RequiresJson = "\"requires\":[\"HasOwnerEntity\",\"CardZoneTriggerable\"],\"containerType\":\"Trap\"";

        private static readonly string ArmorOnMoveJson =
            "{\"id\":\"trap.armor_totem.armor_on_move\"," + RequiresJson + ",\"kind\":\"Triggered\","
            + "\"trigger\":{\"atom\":\"OnSelfMove\",\"every\":1},"
            + "\"target\":{\"atom\":\"FilteredCards\",\"kind\":\"Monster\",\"zone\":\"Board\",\"adjacentTo\":\"Self\",\"exclude\":[\"Self\"]},"
            + "\"action\":{\"atom\":\"GainArmor\",\"amount\":1,\"actor\":\"Self\"}}";

        private static readonly string ArmorPlayerOnMoveJson =
            "{\"id\":\"trap.armor_totem.armor_player_on_move\"," + RequiresJson + ",\"kind\":\"Triggered\","
            + "\"conditions\":[{\"atom\":\"Adjacent\",\"left\":\"Self\",\"right\":\"Player\"}],"
            + "\"trigger\":{\"atom\":\"OnSelfMove\",\"every\":1},"
            + "\"target\":{\"atom\":\"Player\"},"
            + "\"action\":{\"atom\":\"GainArmor\",\"amount\":\"{{amount}}\",\"actor\":\"Self\"}}";

        private void ActivateTotemEffects(CardInstance totem)
        {
            var effects = mArch.GetSystem<IEffectSystem>();
            var owner = new EffectOwner(EffectContainerType.Trap, "trap.armor_totem", totem.Uid);
            effects.Activate(effects.ParseJson(ArmorOnMoveJson), owner);
            effects.Activate(effects.ParseJson(ArmorPlayerOnMoveJson), owner);
        }

        private CardInstance CreateAvatarOnBoard(SlotId slot)
        {
            var avatar = mArch.GetModel<CardRegistry>().Create("avatar.default", CardKind.Avatar);
            avatar.Stats.SetBase(StatId.MaxHp, 20);
            avatar.Stats.SetBase(StatId.Hp, 20);
            avatar.Stats.SetBase(StatId.Attack, 2);
            mArch.GetModel<BoardModel>().SetAvatar(avatar, slot);
            return avatar;
        }

        private CardInstance CreateTrapOnBoard(string defId, SlotId slot)
        {
            var trap = mArch.GetModel<CardRegistry>().Create(defId, CardKind.Trap);
            trap.Stats.SetBase(StatId.MaxHp, 6);
            trap.Stats.SetBase(StatId.Hp, 6);
            mArch.GetModel<BoardModel>().PlaceCard(trap, slot);
            return trap;
        }

        private CardInstance CreateMonsterOnBoard(string defId, SlotId slot)
        {
            var monster = mArch.GetModel<CardRegistry>().Create(defId, CardKind.Monster);
            monster.Stats.SetBase(StatId.MaxHp, 10);
            monster.Stats.SetBase(StatId.Hp, 10);
            monster.Stats.SetBase(StatId.Attack, 1);
            mArch.GetModel<BoardModel>().PlaceCard(monster, slot);
            return monster;
        }

        private void Run(GameAction action)
        {
            mArch.GetSystem<IActionPipelineSystem>().Execute(action);
        }
    }
}

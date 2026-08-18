using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 技能「起来」（skill.rise_up）：
    /// 本卡对玩家造成一次伤害后，随机使一张其他背面怪物卡翻面为正面。
    /// </summary>
    public class RiseUpSkillRegressionTests
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
        public void RiseUp_WhenDamagingPlayer_FlipsOtherFaceDownMonsterToFaceUp()
        {
            LoadRealCatalog();
            var avatar = CreateAvatarOnBoard(SlotId.Board(5));
            var riseUpMonster = CreateRealCardOnBoard("monster.brainless_orc", SlotId.Board(1));
            var otherFaceDownMonster = CreateMonsterOnBoard("monster.test_target", SlotId.Board(2));
            otherFaceDownMonster.FaceUp = false;

            var otherFaceUpMonster = CreateMonsterOnBoard("monster.test_faceup", SlotId.Board(3));
            otherFaceUpMonster.FaceUp = true;

            // 怪物对玩家造成 1 点伤害
            Run(new DealDamageAction(riseUpMonster.Uid, avatar.Uid, 1, "test.attack"));

            Assert.IsTrue(
                otherFaceDownMonster.FaceUp,
                "场上的背面怪应该被「起来」技能单向翻开为正面");
            Assert.IsTrue(
                otherFaceUpMonster.FaceUp,
                "本来就是正面的怪不应该被翻为背面（新版语义是背面翻正面）");
        }

        [Test]
        public void RiseUp_WhenNoFaceDownMonsters_DoesNothingAndDoesNotConcealFaceUp()
        {
            LoadRealCatalog();
            var avatar = CreateAvatarOnBoard(SlotId.Board(5));
            var riseUpMonster = CreateRealCardOnBoard("monster.brainless_orc", SlotId.Board(1));
            var otherFaceUpMonster = CreateMonsterOnBoard("monster.test_faceup", SlotId.Board(2));
            otherFaceUpMonster.FaceUp = true;

            Run(new DealDamageAction(riseUpMonster.Uid, avatar.Uid, 1, "test.attack"));

            Assert.IsTrue(
                otherFaceUpMonster.FaceUp,
                "无背面怪时，已有正面怪绝不能被盖为背面");
            Assert.IsTrue(
                riseUpMonster.FaceUp,
                "起来技能不能翻自己");
        }

        [Test]
        public void OrcCommander_RiseUp_DamagingPlayer_FlipsOneFaceDownMonster()
        {
            LoadRealCatalog();
            var avatar = CreateAvatarOnBoard(SlotId.Board(5));
            var orcCommander = CreateRealCardOnBoard("monster.orc_commander", SlotId.Board(1));
            var monsterA = CreateMonsterOnBoard("monster.test_a", SlotId.Board(2));
            var monsterB = CreateMonsterOnBoard("monster.test_b", SlotId.Board(3));
            monsterA.FaceUp = false;
            monsterB.FaceUp = false;

            // 造成 1 点伤害，触发 1 次「起来」，从 2 只背面怪中随机翻 1 只
            Run(new DealDamageAction(orcCommander.Uid, avatar.Uid, 1, "test.attack"));

            var faceUpCount = (monsterA.FaceUp ? 1 : 0) + (monsterB.FaceUp ? 1 : 0);
            Assert.AreEqual(
                1,
                faceUpCount,
                "两只背面怪中应恰好有一只被「起来」翻为正面（count=1）");
        }

        // ==================== 基建 ====================

        private void LoadRealCatalog()
        {
            var content = mArch.GetSystem<IContentSystem>();
            content.Load(NineGrid.Content.ContentCatalogBootstrap.Load());
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, content.Catalog);
            Assert.IsTrue(content.HasCatalog, "需要真实内容目录");
        }

        private CardInstance CreateRealCardOnBoard(string defId, SlotId slot)
        {
            var content = mArch.GetSystem<IContentSystem>();
            var registry = mArch.GetModel<CardRegistry>();
            var draft = content.CreateDraft(defId);
            Assert.AreNotEqual(CardKind.Unknown, draft.Kind, defId + " 应在内容目录中");
            var card = draft.Create(registry);
            mArch.GetModel<BoardModel>().PlaceCard(card, slot);
            content.ApplyContentToCard(card);
            return card;
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

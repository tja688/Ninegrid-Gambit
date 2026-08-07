using NineGrid.Cards.Presentation;
using NineGrid.Content.CardPresentation;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests.Cards
{
    /// <summary>
    /// #158 首批：牌店可升级伤害（HelpCard 自有固定伤害）参数升格 + 描述限定引用。
    /// 生产内容（磁盘 Authoring/Streaming 镜像）上：伤害是装配实参（kernel 消费），
    /// 检查描述只含 <c>{装配id.键}</c> 限定令牌，faceIntro 草稿 ≤26 格，令牌契约校验干净。
    /// </summary>
    public sealed class CardFaceDamageParamPromotionContentTests
    {
        [SetUp]
        public void SetUp()
        {
            CardPresentationConfigCatalog.Invalidate();
        }

        [TearDown]
        public void TearDown()
        {
            CardPresentationConfigCatalog.Invalidate();
        }

        [Test]
        public void Bomb_DamageAmount_IsQualifiedAssemblyToken_AndContractClean()
        {
            // 爆弹：对所有怪物造成4点伤害，4 是可升级装配实参 help.bomb.use.amount。
            Assert.IsTrue(CardPresentationConfigCatalog.TryGet("help.bomb", out var dto), "help.bomb JSON 应可加载");
            Assert.IsNotNull(dto);

            Assert.AreEqual("对所有怪物造成{help.bomb.use.amount}点伤害", dto.description);
            Assert.AreEqual(0, CardDescriptionTokenRules.ValidateCard(dto).Count, "令牌契约应干净");

            var filled = CardFaceDescriptionParamFiller.FillFromAssemblies(dto.description, dto.effectAssemblies);
            Assert.AreEqual("对所有怪物造成4点伤害", filled);
        }

        [Test]
        public void ThrowingKnife_DamageAmount_IsQualifiedAssemblyToken_AndContractClean()
        {
            // 飞刀：对目标怪物造成6点伤害，6 是可升级装配实参 help.throwing_knife.use.amount。
            Assert.IsTrue(CardPresentationConfigCatalog.TryGet("help.throwing_knife", out var dto), "help.throwing_knife JSON 应可加载");
            Assert.IsNotNull(dto);

            Assert.AreEqual("对目标怪物造成{help.throwing_knife.use.amount}点伤害", dto.description);
            Assert.AreEqual(0, CardDescriptionTokenRules.ValidateCard(dto).Count, "令牌契约应干净");

            var filled = CardFaceDescriptionParamFiller.FillFromAssemblies(dto.description, dto.effectAssemblies);
            Assert.AreEqual("对目标怪物造成6点伤害", filled);
        }

        [Test]
        public void TouchedCards_HaveFaceIntroDraft_Within26Units()
        {
            Assert.IsTrue(CardPresentationConfigCatalog.TryGet("help.bomb", out var bomb));
            Assert.IsTrue(CardPresentationConfigCatalog.TryGet("help.throwing_knife", out var knife));

            Assert.IsFalse(string.IsNullOrWhiteSpace(bomb.faceIntro), "爆弹须有 faceIntro 草稿");
            Assert.IsFalse(string.IsNullOrWhiteSpace(knife.faceIntro), "飞刀须有 faceIntro 草稿");
            Assert.LessOrEqual(CardDescriptionTokenRules.CountUnits(bomb.faceIntro), 26);
            Assert.LessOrEqual(CardDescriptionTokenRules.CountUnits(knife.faceIntro), 26);
        }
    }
}

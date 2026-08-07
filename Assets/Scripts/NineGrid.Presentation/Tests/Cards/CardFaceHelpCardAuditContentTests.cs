using System.Collections.Generic;
using NineGrid.Cards.Presentation;
using NineGrid.Content.CardPresentation;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests.Cards
{
    /// <summary>
    /// #159 道具卡（HelpCard）逐卡审计迁移：全部 live HelpCard（deck.help + 战士卡组撞击教程）——
    /// 装配实参唯一化为 <c>{装配id.键}</c> 限定式（简单式 / defId 前缀退役）、
    /// 可见数字都进装配实参栏（叙事同值）、人手散文保留、描述格 ≤26、faceIntro 草稿 ≤26 格、
    /// 令牌契约校验干净；归档卡保持原样豁免。
    /// 生产内容（磁盘 Authoring/Streaming 镜像）上断言。
    /// </summary>
    public sealed class CardFaceHelpCardAuditContentTests
    {
        /// <summary>策划现行道具卡全集（对齐 FormalContentReachabilityContractTests 官方清单）。</summary>
        private static readonly string[] LiveHelpCardIds =
        {
            "help.healing_potion", "help.throwing_knife", "help.fireball",
            "help.rotation_wheel", "help.brutality_card", "help.bomb",
            "help.swap_card", "help.sturdy_shield", "help.teleport_card",
            "help.gold_card", "help.food_card", "help.kidnapping",
            "help.common_chest_card", "help.blue_chest_card", "help.golden_chest_card",
            "help.attack_card", "help.hp_card", "help.armor_card",
            "help.impact_tutorial",
        };

        /// <summary>#159 升格令牌的 6 张卡：描述唯一化 + 填值预期（装配实参权威）。</summary>
        private static readonly Dictionary<string, string> PromotedDescriptionExpectations =
            new Dictionary<string, string>
            {
                { "help.healing_potion", "恢复{help.healing_potion.use.amount}点[HP]" },
                { "help.swap_card", "选择除玩家外的{help.swap_card.use.count}张卡牌互换位置" },
                { "help.sturdy_shield", "玩家获得{help.sturdy_shield.use.amount}点[armor]" },
                { "help.teleport_card", "选择除玩家外的{help.teleport_card.use.count}张卡牌洗回卡组" },
                { "help.gold_card", "获取{help.gold_card.use.delta}[money]" },
                { "help.kidnapping", "破坏{help.kidnapping.use.count}张常规怪物，并偷取其[armor]" },
            };

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
        public void AllLiveHelpCards_PassTokenContract_AndWithin26Units()
        {
            // AC：每张 live HelpCard 通过审计 DoD——限定令牌、实参在、散文在、≤26 格。
            foreach (var id in LiveHelpCardIds)
            {
                Assert.IsTrue(CardPresentationConfigCatalog.TryGet(id, out var dto), id + " JSON 应可加载");
                Assert.IsNotNull(dto);

                var errors = CardDescriptionTokenRules.ValidateCard(dto);
                Assert.AreEqual(0, errors.Count, id + " 令牌契约应干净：\n" + string.Join("\n", errors));

                Assert.LessOrEqual(CardDescriptionTokenRules.CountUnits(dto.description), 26, id + " 描述 ≤26 格");
                Assert.LessOrEqual(CardDescriptionTokenRules.CountUnits(dto.faceIntro), 26, id + " faceIntro ≤26 格");
            }
        }

        [Test]
        public void PromotedCards_DescriptionUsesQualifiedAssemblyToken_AndFillsExpected()
        {
            // #159：简单式 / 写死数字全部唯一化到装配实参；填值=装配实参初始值。
            foreach (var pair in PromotedDescriptionExpectations)
            {
                Assert.IsTrue(CardPresentationConfigCatalog.TryGet(pair.Key, out var dto), pair.Key + " JSON 应可加载");
                Assert.IsNotNull(dto);

                Assert.AreEqual(pair.Value, dto.description, pair.Key + " 描述应为限定式令牌");

                var filled = CardFaceDescriptionParamFiller.FillFromAssemblies(dto.description, dto.effectAssemblies);
                Assert.IsFalse(filled.Contains('{'), pair.Key + " 填值后不应残留令牌：\n" + filled);
                Assert.IsFalse(filled.Contains("{amount}"), pair.Key + " 不应有简单式残留");
                Assert.IsFalse(filled.Contains("{count}"), pair.Key + " 不应有简单式残留");
                Assert.IsFalse(filled.Contains("{delta}"), pair.Key + " 不应有简单式残留");
            }
        }

        [Test]
        public void PromotedCards_FillValues_MatchAssemblyArgs()
        {
            // 装配实参权威：填值必须等于 argsJson 里该键的值（叙事同值也存实参）。
            Assert.AreEqual("恢复10点[HP]", Fill("help.healing_potion"));
            Assert.AreEqual("选择除玩家外的2张卡牌互换位置", Fill("help.swap_card"));
            Assert.AreEqual("玩家获得5点[armor]", Fill("help.sturdy_shield"));
            Assert.AreEqual("选择除玩家外的1张卡牌洗回卡组", Fill("help.teleport_card"));
            Assert.AreEqual("获取50[money]", Fill("help.gold_card"));
            Assert.AreEqual("破坏1张常规怪物，并偷取其[armor]", Fill("help.kidnapping"));
        }

        [Test]
        public void AllLiveHelpCards_HaveFaceIntroDraft()
        {
            foreach (var id in LiveHelpCardIds)
            {
                Assert.IsTrue(CardPresentationConfigCatalog.TryGet(id, out var dto), id + " JSON 应可加载");
                Assert.IsFalse(string.IsNullOrWhiteSpace(dto.faceIntro), id + " 须有 faceIntro 草稿（人稍后润色）");
            }
        }

        [Test]
        public void NoBlindBulkReplace_ProsePreserved()
        {
            // 审计非盲替换：无数字的散文句不得因迁移被改写（人手文案保留）。
            Assert.AreEqual("对选中怪物造成等同于玩家[attack]的伤害", Desc("help.fireball"));
            Assert.AreEqual("逆时针旋转一次", Desc("help.rotation_wheel"));
            Assert.AreEqual("玩家下一次对怪物造成的普通攻击伤害翻倍", Desc("help.brutality_card"));
            Assert.AreEqual("将[HP]完整恢复", Desc("help.food_card"));
            Assert.AreEqual("挑选并获取一个普通的遗物", Desc("help.common_chest_card"));
            Assert.AreEqual("挑选并获取一个进阶的遗物", Desc("help.blue_chest_card"));
            Assert.AreEqual("挑选并获取一个高级的遗物", Desc("help.golden_chest_card"));
        }

        private static string Desc(string id)
        {
            Assert.IsTrue(CardPresentationConfigCatalog.TryGet(id, out var dto), id + " JSON 应可加载");
            return dto.description;
        }

        private static string Fill(string id)
        {
            Assert.IsTrue(CardPresentationConfigCatalog.TryGet(id, out var dto), id + " JSON 应可加载");
            return CardFaceDescriptionParamFiller.FillFromAssemblies(dto.description, dto.effectAssemblies);
        }
    }
}

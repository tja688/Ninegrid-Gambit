using NineGrid.Content.CardPresentation;
using NUnit.Framework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// ADR-0035 / #154 / #155：描述投影静态通路契约——描述格计数（字符 / {…} / […] 各 1 格，硬上限 26）、
    /// 装配 id 必填、简单式与 defId 前缀式令牌在范围内卡（机关/遗物/道具，归档除外）上失败、
    /// 令牌限定符必须精确等于装配 id 且键真实存在于实参；检查描述 / 局内模板（liveTemplate）/
    /// 卡面介绍同受约束，局内模板可空。
    /// 纯函数测试（无磁盘），与内容卫生校验共用 <see cref="CardDescriptionTokenRules"/>。
    /// </summary>
    public sealed class CardDescriptionTokenContractTests
    {
        [Test]
        public void CountUnits_PlainChars_EachCountOne()
        {
            Assert.AreEqual(4, CardDescriptionTokenRules.CountUnits("攻击+2"));
            Assert.AreEqual(0, CardDescriptionTokenRules.CountUnits(null));
            Assert.AreEqual(0, CardDescriptionTokenRules.CountUnits(string.Empty));
        }

        [Test]
        public void CountUnits_ParamBlock_CountsOne()
        {
            Assert.AreEqual(
                1,
                CardDescriptionTokenRules.CountUnits("{relic.armor_strip_knife.battle.value}"));
        }

        [Test]
        public void CountUnits_IconBlock_CountsOne()
        {
            // [MHP]=1 格，+ 与 6 各 1 格。
            Assert.AreEqual(3, CardDescriptionTokenRules.CountUnits("[MHP]+6"));
        }

        [Test]
        public void CountUnits_MixedSentence_MatchesContract()
        {
            var text = "每移动1次，若玩家在[adjacent]则恢复{amount}点血量";
            // 每移动1次=5，=1，若玩家在=4，[adjacent]=1，则恢复=3，{amount}=1，点血量=3 → 18。
            Assert.AreEqual(18, CardDescriptionTokenRules.CountUnits(text));
        }

        [Test]
        public void CountUnits_UnclosedBlock_CountsOne()
        {
            Assert.AreEqual(3, CardDescriptionTokenRules.CountUnits("恢复["));
        }

        [Test]
        public void ValidateCard_ExactAssemblyIdTokens_IsClean()
        {
            var dto = InScopeCard(
                "help.healing_potion",
                "HelpCard",
                "deck.help",
                "恢复{help.healing_potion.use.amount}点[HP]",
                "每移动1次",
                new EffectAssemblyDto { id = "help.healing_potion.use", argsJson = "{\"amount\":10}" });

            Assert.AreEqual(0, CardDescriptionTokenRules.ValidateCard(dto).Count);
        }

        [Test]
        public void ValidateCard_QualifierCaseInsensitive_IsClean()
        {
            var dto = InScopeCard(
                "help.healing_potion",
                "HelpCard",
                "deck.help",
                "恢复{HELP.HEALING_POTION.USE.AMOUNT}点[HP]",
                null,
                new EffectAssemblyDto { id = "help.healing_potion.use", argsJson = "{\"amount\":10}" });

            Assert.AreEqual(0, CardDescriptionTokenRules.ValidateCard(dto).Count);
        }

        [Test]
        public void ValidateCard_MissingAssemblyId_Fails()
        {
            var dto = InScopeCard(
                "trap.healing_spring",
                "Trap",
                "deck.trap",
                "恢复{amount}点血量",
                null,
                new EffectAssemblyDto { id = null, argsJson = "{\"amount\":1}" });

            var errors = CardDescriptionTokenRules.ValidateCard(dto);
            Assert.AreEqual(2, errors.Count);
            StringAssert.Contains("missing id", errors[0]);
            StringAssert.Contains("简单式", errors[1]);
        }

        [Test]
        public void ValidateCard_SimpleToken_Fails()
        {
            var dto = InScopeCard(
                "help.bomb",
                "HelpCard",
                "deck.help",
                "对所有怪物造成{amount}点伤害",
                null,
                new EffectAssemblyDto { id = "help.bomb.use", argsJson = "{\"amount\":4}" });

            var errors = CardDescriptionTokenRules.ValidateCard(dto);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("{amount}", errors[0]);
            StringAssert.Contains("简单式", errors[0]);
        }

        [Test]
        public void ValidateCard_DefIdPrefixToken_Fails()
        {
            // 限定符 trap.attack_totem 只是装配 id 前缀，不是精确装配 id → 退役形式。
            var dto = InScopeCard(
                "trap.attack_totem",
                "Trap",
                "deck.trap",
                "[adjacent]的怪物获得[attack]+{trap.attack_totem.value}",
                null,
                new EffectAssemblyDto { id = "trap.attack_totem.aura", argsJson = "{\"value\":1}" },
                new EffectAssemblyDto { id = "trap.attack_totem.refresh", argsJson = "{\"value\":1}" });

            var errors = CardDescriptionTokenRules.ValidateCard(dto);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("{trap.attack_totem.value}", errors[0]);
        }

        [Test]
        public void ValidateCard_TemplateIdQualifiedToken_Fails()
        {
            var dto = InScopeCard(
                "help.bomb",
                "HelpCard",
                "deck.help",
                "造成{tpl.help.bomb.use.amount}点伤害",
                null,
                new EffectAssemblyDto
                {
                    id = "help.bomb.use",
                    templateId = "tpl.help.bomb.use",
                    argsJson = "{\"amount\":4}",
                });

            var errors = CardDescriptionTokenRules.ValidateCard(dto);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("{tpl.help.bomb.use.amount}", errors[0]);
        }

        [Test]
        public void ValidateCard_UnknownKeyInAssemblyArgs_Fails()
        {
            var dto = InScopeCard(
                "help.bomb",
                "HelpCard",
                "deck.help",
                "造成{help.bomb.use.amount}点伤害",
                null,
                new EffectAssemblyDto { id = "help.bomb.use", argsJson = "{\"value\":4}" });

            var errors = CardDescriptionTokenRules.ValidateCard(dto);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("amount", errors[0]);
            StringAssert.Contains("不在装配", errors[0]);
        }

        [Test]
        public void ValidateCard_Over26Units_Fails()
        {
            var dto = InScopeCard(
                "trap.long",
                "Trap",
                "deck.trap",
                new string('甲', 27),
                null,
                new EffectAssemblyDto { id = "trap.long.fx", argsJson = "{\"value\":1}" });

            var errors = CardDescriptionTokenRules.ValidateCard(dto);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("超描述格 26", errors[0]);
            StringAssert.Contains("27", errors[0]);
        }

        [Test]
        public void ValidateCard_Exactly26Units_Passes()
        {
            var dto = InScopeCard(
                "trap.long",
                "Trap",
                "deck.trap",
                new string('甲', 26),
                null,
                new EffectAssemblyDto { id = "trap.long.fx", argsJson = "{\"value\":1}" });

            Assert.AreEqual(0, CardDescriptionTokenRules.ValidateCard(dto).Count);
        }

        [Test]
        public void ValidateCard_FaceIntro_AlsoChecked()
        {
            var dto = InScopeCard(
                "relic.terror_mask",
                "Relic",
                "deck.relic",
                "[attack]+{relic.terror_mask.attack.value}",
                "每回合获得{value}点攻击",
                new EffectAssemblyDto { id = "relic.terror_mask.attack", argsJson = "{\"value\":2}" });

            var errors = CardDescriptionTokenRules.ValidateCard(dto);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("faceIntro", errors[0]);
        }

        [Test]
        public void ValidateCard_ArchivedDeck_IsExempt()
        {
            var dto = InScopeCard(
                "help.watchtower",
                "HelpCard",
                "deck.help_archive",
                "部署造成{amount}点伤害",
                null,
                new EffectAssemblyDto { id = null, argsJson = "{\"amount\":3}" });

            Assert.AreEqual(0, CardDescriptionTokenRules.ValidateCard(dto).Count);
        }

        [Test]
        public void ValidateCard_NonInScopeKind_IsExempt()
        {
            var dto = InScopeCard(
                "monster.skeleton",
                "Monster",
                "deck.skeleton_legion",
                "攻击+{value}",
                null,
                new EffectAssemblyDto { id = null, argsJson = "{\"value\":2}" });

            Assert.AreEqual(0, CardDescriptionTokenRules.ValidateCard(dto).Count);
        }

        [Test]
        public void ValidateCard_LiveTemplate_SimpleToken_Fails()
        {
            // ADR-0035 / #155：局内模板同受 {装配id.键} 契约约束，简单式报错。
            var dto = InScopeCard(
                "trap.rock",
                "Trap",
                "deck.trap",
                "每移动3次后触发",
                null,
                new EffectAssemblyDto { id = "trap.rock.trigger", argsJson = "{\"count\":3}" });
            dto.liveTemplate = "还剩{count}次后触发";

            var errors = CardDescriptionTokenRules.ValidateCard(dto);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("liveTemplate", errors[0]);
            StringAssert.Contains("{count}", errors[0]);
        }

        [Test]
        public void ValidateCard_LiveTemplate_Over26Units_Fails()
        {
            var dto = InScopeCard(
                "trap.rock",
                "Trap",
                "deck.trap",
                "每移动3次后触发",
                null,
                new EffectAssemblyDto { id = "trap.rock.trigger", argsJson = "{\"count\":3}" });
            dto.liveTemplate = new string('甲', 27);

            var errors = CardDescriptionTokenRules.ValidateCard(dto);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("liveTemplate", errors[0]);
            StringAssert.Contains("超描述格 26", errors[0]);
        }

        [Test]
        public void ValidateCard_LiveTemplate_EmptyOrNull_IsClean()
        {
            // 空局内模板合法：投影与检查描述同文（ADR-0035 #3）。
            var dto = InScopeCard(
                "help.potion",
                "HelpCard",
                "deck.help",
                "恢复{help.potion.use.amount}点[HP]",
                null,
                new EffectAssemblyDto { id = "help.potion.use", argsJson = "{\"amount\":10}" });
            dto.liveTemplate = string.Empty;

            Assert.AreEqual(0, CardDescriptionTokenRules.ValidateCard(dto).Count);
        }

        private static CardPresentationConfigDto InScopeCard(
            string contentId,
            string kind,
            string deckId,
            string description,
            string faceIntro,
            params EffectAssemblyDto[] assemblies)
        {
            return new CardPresentationConfigDto
            {
                schemaVersion = 2,
                contentId = contentId,
                kind = kind,
                deckId = deckId,
                description = description,
                faceIntro = faceIntro ?? string.Empty,
                effectAssemblies = assemblies ?? new EffectAssemblyDto[0],
            };
        }
    }
}

using System.Collections.Generic;
using NineGrid.Cards.Presentation;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests.Cards
{
    /// <summary>
    /// #160 遗物（Relic）逐卡审计迁移：全部 51 张 live 遗物（deck.relic，对齐
    /// FormalContentReachabilityContractTests 官方清单；9 件归档遗物豁免原样）——
    /// 装配实参唯一化为 <c>{装配id.键}</c> 限定式（简单式 / defId 前缀退役）、
    /// 可见数字都进装配实参栏（叙事同值，含内核常数的阈值/百分比叙事双胞胎）、
    /// 人手散文保留（含语句结构）、描述格 ≤26、faceIntro 草稿 ≤26 格、
    /// 令牌契约校验干净；归档卡保持原样豁免。
    /// 生产内容（磁盘 Authoring/Streaming 镜像）上断言。
    /// </summary>
    public sealed class CardFaceRelicAuditContentTests
    {
        /// <summary>策划现行 51 件遗物全集（白 27 / 蓝 15 / 金 8 / 独特 1，deck.relic）。</summary>
        private static readonly string[] LiveRelicIds =
        {
            "relic.junk_recycler", "relic.wood_shield", "relic.wood_sword", "relic.wood_armor",
            "relic.lucky_coin", "relic.throwing_knife_bag", "relic.potion_bag",
            "relic.junk_launcher", "relic.junk_coating", "relic.sling",
            "relic.shield_knife", "relic.gold_knife", "relic.punch_card_knife",
            "relic.armor_strip_knife", "relic.heavy_armor", "relic.gold_armor",
            "relic.foam_armor", "relic.composite_armor", "relic.blood_demon",
            "relic.body_potential", "relic.muscle_counter", "relic.gold_blood",
            "relic.swap_button", "relic.rotation_button", "relic.rpm_engine",
            "relic.trap_cell", "relic.rotation_trick",
            "relic.iron_shield", "relic.forge_tool", "relic.terror_mask",
            "relic.sharp_longsword", "relic.vitality_amulet", "relic.thorn_mail",
            "relic.junk_sword", "relic.junk_amplifier", "relic.junk_cycler",
            "relic.junk_body", "relic.blood_cycle", "relic.blood_violence",
            "relic.blood_burst", "relic.spinning_barb", "relic.blood_regen",
            "relic.golden_sword", "relic.dragon_scale_armor", "relic.golden_coffer",
            "relic.berserker_axe", "relic.phoenix_feather", "relic.metal_blood",
            "relic.beyond_dimension", "relic.craving", "relic.rotten_cleave_axe",
        };

        /// <summary>
        /// #160 填值预期（装配实参权威）：简单式 / 写死数字全部唯一化后的渲染结果，
        /// 含叙事同值双胞胎（阈值/百分比/步长）——填值必须等于 argsJson 里该键的值。
        /// </summary>
        private static readonly Dictionary<string, string> FillExpectations =
            new Dictionary<string, string>
            {
                { "relic.terror_mask", "[attack]+1，本场战斗每击杀6，随机破坏1只常规怪物" },
                { "relic.blood_demon", "每受到5点伤害，获得1点[MHP]" },
                { "relic.blood_cycle", "血量上限+4，受到伤害时恢复2点血量" },
                { "relic.blood_regen", "[MHP]+4；低于半血时，恢复2[HP]" },
                { "relic.blood_violence", "[MHP]+4；当[HP]低于[MHP]的50%时，玩家[attack]+2" },
                { "relic.body_potential", "[MHP]+2；[HP]每累计减少10点，添加1张道具到卡组" },
                { "relic.composite_armor", "每关卡开始时，每有3点[attack]，获得1点[armor]" },
                { "relic.forge_tool", "基础[armor]+2，进入战斗时吞噬5点[armor]使基础[armor]永久+1" },
                { "relic.gold_armor", "基础[armor]+1，每5点[money]视作1点[armor]" },
                { "relic.heavy_armor", "基础[armor]+1，进入战斗时每有2点基础[armor]就获得1点[armor]" },
                { "relic.junk_cycler", "每使用9张道具卡，添加1张道具卡到卡组" },
                { "relic.junk_launcher", "每使用1张道具卡，对随机1张怪物造成2点伤害" },
                { "relic.junk_sword", "[attack]+2,击杀时恢复2点[HP]" },
                { "relic.muscle_counter", "[MHP]+2；每拥有10点[MHP]，对怪物多造成1点伤害" },
                { "relic.phoenix_feather", "[MHP]+8，[HP]小于等于0时，恢复50%[HP]并[death]" },
                { "relic.punch_card_knife", "[attack]+1，每击杀5只怪物，添加1张常规道具卡到卡组" },
                { "relic.rotation_trick", "每击杀6张怪物卡，添加1张旋转轮到卡组" },
                { "relic.trap_cell", "当怪物移动到格1时，对该怪物造成4点伤害" },
                { "relic.vitality_amulet", "[MHP]+6；每场战斗结束时恢复6点[HP]" },
                { "relic.golden_sword", "[attack]+8，普通攻击时：机械疲劳，击杀时：上弦" },
            };

        /// <summary>
        /// #160 叙事同值双胞胎与内核常数对齐契约：描述里可见的阈值 / 百分比 / 步长 /
        /// 位置参数必须真实存在于装配实参，且与内核模板 body 常数一致（防漂移）。
        /// 键为卡 defId → {装配id → 期望实参}。
        /// </summary>
        private static readonly Dictionary<string, Dictionary<string, Dictionary<string, object>>> NarrativeArgExpectations =
            new Dictionary<string, Dictionary<string, Dictionary<string, object>>>
            {
                { "relic.blood_demon", Asm("relic.blood_demon.damage", Args("threshold", 5)) },
                { "relic.body_potential", Asm("relic.body_potential.hp_lost", Args("threshold", 10)) },
                { "relic.punch_card_knife", Asm("relic.punch_card_knife.remove", Args("threshold", 5)) },
                { "relic.rotation_trick", Asm("relic.rotation_trick.remove", Args("threshold", 6)) },
                { "relic.terror_mask", Asm("relic.terror_mask.remove", Args("threshold", 6)) },
                { "relic.junk_cycler", Asm("relic.junk_cycler.use", Args("threshold", 9)) },
                { "relic.junk_body", Asm("relic.junk_body.use", Args("threshold", 3)) },
                { "relic.composite_armor", Asm("relic.composite_armor.node_start", Args("every", 3)) },
                { "relic.heavy_armor", Asm("relic.heavy_armor.node_start", Args("every", 2)) },
                { "relic.muscle_counter", Asm("relic.muscle_counter.battle", Args("every", 10)) },
                { "relic.gold_armor", Asm("relic.gold_armor.rule", Args("every", 5)) },
                { "relic.gold_blood", Asm("relic.gold_blood.taken", Args("every", 1)) },
                { "relic.spinning_barb", Asm("relic.spinning_barb.move", Args("every", 1)) },
                { "relic.blood_violence", Asm("relic.blood_violence.atk", Args("pct", 50)) },
                { "relic.phoenix_feather", Asm("relic.phoenix_feather.fatal", Args("pct", 50)) },
                { "relic.trap_cell", Asm("relic.trap_cell.move", Args("slot", 1)) },
                { "relic.gold_knife", Asm("relic.gold_knife.kill", Args("delta", 2)) },
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
        public void AllLiveRelics_PassTokenContract_AndWithin26Units()
        {
            // AC：每张 live 遗物通过审计 DoD——限定令牌、实参在、散文在、≤26 格。
            foreach (var id in LiveRelicIds)
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
        public void PromotedRelics_DescriptionUsesQualifiedAssemblyToken_AndFillsExpected()
        {
            // #160：简单式 / 写死数字全部唯一化到装配实参；填值=装配实参（含叙事同值）。
            foreach (var pair in FillExpectations)
            {
                Assert.IsTrue(CardPresentationConfigCatalog.TryGet(pair.Key, out var dto), pair.Key + " JSON 应可加载");
                Assert.IsNotNull(dto);

                var filled = CardFaceDescriptionParamFiller.FillFromAssemblies(dto.description, dto.effectAssemblies);
                Assert.AreEqual(pair.Value, filled, pair.Key + " 填值应为装配实参渲染：\n" + filled);

                Assert.IsFalse(filled.Contains('{'), pair.Key + " 填值后不应残留令牌");
                Assert.IsFalse(filled.Contains("{value}"), pair.Key + " 不应有简单式残留");
                Assert.IsFalse(filled.Contains("{amount}"), pair.Key + " 不应有简单式残留");
                Assert.IsFalse(filled.Contains("{count}"), pair.Key + " 不应有简单式残留");
                Assert.IsFalse(filled.Contains("{delta}"), pair.Key + " 不应有简单式残留");
            }
        }

        [Test]
        public void NarrativeArgs_PinKernelConstants_InAssemblyArgs()
        {
            // 叙事同值双胞胎与内核常数对齐：阈值/百分比/步长真实入库，防「孤儿字面量」。
            foreach (var cardPair in NarrativeArgExpectations)
            {
                Assert.IsTrue(CardPresentationConfigCatalog.TryGet(cardPair.Key, out var dto), cardPair.Key + " JSON 应可加载");
                Assert.IsNotNull(dto);

                foreach (var asmPair in cardPair.Value)
                {
                    var matched = false;
                    foreach (var assembly in dto.effectAssemblies)
                    {
                        if (!string.Equals(assembly.id, asmPair.Key, System.StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        matched = true;
                        var args = EffectAssemblyResolver.ParseArgsJson(assembly.argsJson);
                        foreach (var kv in asmPair.Value)
                        {
                            Assert.IsTrue(args.ContainsKey(kv.Key), cardPair.Key + "/" + asmPair.Key + " 缺实参 " + kv.Key);
                            Assert.AreEqual(kv.Value, args[kv.Key], cardPair.Key + "/" + asmPair.Key + "." + kv.Key + " 应等于内核常数");
                        }
                    }

                    Assert.IsTrue(matched, cardPair.Key + " 未找到装配 " + asmPair.Key);
                }
            }
        }

        [Test]
        public void AllLiveRelics_HaveFaceIntroDraft()
        {
            foreach (var id in LiveRelicIds)
            {
                Assert.IsTrue(CardPresentationConfigCatalog.TryGet(id, out var dto), id + " JSON 应可加载");
                Assert.IsFalse(string.IsNullOrWhiteSpace(dto.faceIntro), id + " 须有 faceIntro 草稿（人稍后润色）");
            }
        }

        [Test]
        public void NoBlindBulkReplace_ProsePreserved()
        {
            // 审计非盲替换：无数字散文 / 数值已令牌化的句子不得被迁移改写（人手文案保留）。
            Assert.AreEqual("攻击时若目标当前[armor]大于{relic.armor_strip_knife.battle.value}，攻击伤害+{relic.armor_strip_knife.armor_hit.value}", Desc("relic.armor_strip_knife"));
            Assert.AreEqual("攻击+{relic.berserker_axe.base.value}，血量低于一半时玩家攻击翻倍", Desc("relic.berserker_axe"));
            Assert.AreEqual("攻击+{relic.beyond_dimension.base.value}，攻击后旋转{relic.beyond_dimension.battle.count}次", Desc("relic.beyond_dimension"));
            Assert.AreEqual("血量上限+{relic.blood_burst.max_hp.value}，受伤时对随机怪物：反伤", Desc("relic.blood_burst"));
            Assert.AreEqual("基础[armor]+{relic.dragon_scale_armor.base.value}，[MHP]+{relic.dragon_scale_armor.max_hp.delta}，所有怪物的[attack]{relic.dragon_scale_armor.rule.value}", Desc("relic.dragon_scale_armor"));
            Assert.AreEqual("受到伤害时，将损失的[HP]转换为等量的[armor]", Desc("relic.metal_blood"));
        }

        private static string Desc(string id)
        {
            Assert.IsTrue(CardPresentationConfigCatalog.TryGet(id, out var dto), id + " JSON 应可加载");
            return dto.description;
        }

        private static Dictionary<string, object> Args(params object[] keyValues)
        {
            var args = new Dictionary<string, object>();
            for (var i = 0; i < keyValues.Length; i += 2)
            {
                args[(string)keyValues[i]] = keyValues[i + 1];
            }

            return args;
        }

        private static Dictionary<string, Dictionary<string, object>> Asm(
            string assemblyId,
            Dictionary<string, object> args)
        {
            return new Dictionary<string, Dictionary<string, object>>
            {
                { assemblyId, args },
            };
        }
    }
}

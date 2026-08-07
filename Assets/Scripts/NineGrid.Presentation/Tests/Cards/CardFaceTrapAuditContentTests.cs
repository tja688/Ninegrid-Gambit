using System.Collections.Generic;
using NineGrid.Cards.Presentation;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests.Cards
{
    /// <summary>
    /// #161 机关（Trap）逐卡审计迁移：全部 10 张 live 机关卡（deck.trap，
    /// 常规六张 + 离开机关 + 烈焰/治疗泉/复活石特殊三张，对齐设计清单）——
    /// 装配实参唯一化为 <c>{装配id.键}</c> 限定式（简单式 / 写死数字 / 非稳定装配 id 退役）、
    /// 可见数字与内核常数（节奏 every / 格位 slot）以叙事同值双胞胎入库、
    /// 人手散文保留（含句序）、描述格 ≤26、faceIntro 草稿 ≤26 格、
    /// 令牌契约校验干净；倒计时卡（烈焰/复活石）局内模板与 Settled 投影由
    /// CardFaceCountdownProjectionContentTests 另案覆盖。
    /// 生产内容（磁盘 Authoring/Streaming 镜像）上断言。
    /// </summary>
    public sealed class CardFaceTrapAuditContentTests
    {
        /// <summary>策划现行 10 张机关卡全集（deck.trap；对齐策划清单与目录）。</summary>
        private static readonly string[] LiveTrapIds =
        {
            "trap.armor_totem", "trap.attack_totem", "trap.recovery_totem",
            "trap.spike", "trap.rolling_stone", "trap.bear_trap",
            "trap.healing_spring", "trap.flame", "trap.revive_stone",
            "trap.leave",
        };

        /// <summary>
        /// #161 填值预期（装配实参权威）：简单式 / 写死数字全部唯一化后的渲染结果，
        /// 含叙事同值双胞胎（节奏 every / 格位 slot）——填值必须等于 argsJson 里该键的值。
        /// </summary>
        private static readonly Dictionary<string, string> FillExpectations =
            new Dictionary<string, string>
            {
                { "trap.armor_totem", "[adjacent]的怪物和玩家[armor]+1" },
                { "trap.attack_totem", "[adjacent]的怪物和玩家[attack]+1" },
                { "trap.recovery_totem", "每移动1次，[adjacent]的怪物和玩家各恢复1点血量" },
                { "trap.spike", "每移动1次，[adjacent]的怪物和玩家各受到1点伤害" },
                { "trap.rolling_stone", "移动到格3时，移除格6上的常规怪物/道具/机关" },
                { "trap.bear_trap", "对下张[adjacent]的怪物/道具造成10点伤害/移除，触发后[death]" },
                { "trap.healing_spring", "每移动1次，若玩家在[adjacent]则恢复1点血量" },
                { "trap.flame", "每移动1次，对[adjacent]的玩家造成1点伤害，3次后[death]" },
                { "trap.revive_stone", "[action]6次后[death]，将1张重生骷髅打出到同格" },
                { "trap.leave", "魔免；只可被交战击破，[death]后离开房间" },
            };

        /// <summary>
        /// #161 叙事同值双胞胎与内核常数对齐契约：描述里可见的节奏 / 格位 /
        /// 伤害等数字必须真实存在于装配实参，且与内核模板 body 常数一致（防漂移）。
        /// 键为卡 defId → {装配id → 期望实参}。
        /// </summary>
        private static readonly Dictionary<string, Dictionary<string, Dictionary<string, object>>> NarrativeArgExpectations =
            new Dictionary<string, Dictionary<string, Dictionary<string, object>>>
            {
                { "trap.flame", Asm("trap.flame.move", Args("amount", 1, "every", 1)) },
                { "trap.spike", Asm("trap.spike.move", Args("amount", 1, "every", 1)) },
                { "trap.healing_spring", Asm("trap.healing_spring.heal_on_move", Args("amount", 1, "every", 1)) },
                { "trap.recovery_totem", Asm("trap.recovery_totem.heal_on_move", Args("amount", 1, "every", 1)) },
                { "trap.bear_trap", Asm("trap.bear_trap.fill", Args("amount", 10)) },
                { "trap.rolling_stone", Asm("trap.rolling_stone.slot3", Args("slot", 3, "targetSlot", 6)) },
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
        public void AllLiveTraps_PassTokenContract_AndWithin26Units()
        {
            // AC：每张 live 机关通过审计 DoD——限定令牌、实参在、散文在、≤26 格。
            foreach (var id in LiveTrapIds)
            {
                Assert.IsTrue(CardPresentationConfigCatalog.TryGet(id, out var dto), id + " JSON 应可加载");
                Assert.IsNotNull(dto);

                var errors = CardDescriptionTokenRules.ValidateCard(dto);
                Assert.AreEqual(0, errors.Count, id + " 令牌契约应干净：\n" + string.Join("\n", errors));

                Assert.LessOrEqual(CardDescriptionTokenRules.CountUnits(dto.description), 26, id + " 描述 ≤26 格");
                Assert.LessOrEqual(CardDescriptionTokenRules.CountUnits(dto.faceIntro), 26, id + " faceIntro ≤26 格");
                Assert.LessOrEqual(CardDescriptionTokenRules.CountUnits(dto.liveTemplate), 26, id + " liveTemplate ≤26 格");
            }
        }

        [Test]
        public void PromotedTraps_DescriptionUsesQualifiedAssemblyToken_AndFillsExpected()
        {
            // #161：简单式 / 写死数字 / 非稳定装配 id 全部唯一化到装配实参；填值=装配实参。
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
            // 叙事同值双胞胎与内核常数对齐：节奏 every / 格位 slot 真实入库，防「孤儿字面量」。
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
        public void AllLiveTraps_HaveFaceIntroDraft()
        {
            foreach (var id in LiveTrapIds)
            {
                Assert.IsTrue(CardPresentationConfigCatalog.TryGet(id, out var dto), id + " JSON 应可加载");
                Assert.IsFalse(string.IsNullOrWhiteSpace(dto.faceIntro), id + " 须有 faceIntro 草稿（人稍后润色）");
            }
        }

        [Test]
        public void NoBlindBulkReplace_ProsePreserved()
        {
            // 审计非盲替换：人手散文（句序与措辞）不得被迁移改写；仅死字面量 / 简单式令牌被替换。
            Assert.AreEqual("[adjacent]的怪物和玩家[armor]+{trap.armor_totem.aura.value}", Desc("trap.armor_totem"));
            Assert.AreEqual("[adjacent]的怪物和玩家[attack]+{trap.attack_totem.aura.value}", Desc("trap.attack_totem"));
            Assert.AreEqual("每移动{trap.flame.move.every}次，对[adjacent]的玩家造成{trap.flame.move.amount}点伤害，{trap.flame.remove.every}次后[death]", Desc("trap.flame"));
            Assert.AreEqual("魔免；只可被交战击破，[death]后离开房间", Desc("trap.leave"));
            Assert.AreEqual("每移动{trap.spike.move.every}次，[adjacent]的怪物和玩家各受到{trap.spike.move.amount}点伤害", Desc("trap.spike"));
        }

        [Test]
        public void CountdownTraps_RequireLiveTemplate_AndProjectionTokens()
        {
            // 倒计时卡（projectKey 装配）必须作者局内模板且引用投影令牌（ADR-0035 / #156）。
            AssertLiveTemplateCoversProjectKey("trap.flame");
            AssertLiveTemplateCoversProjectKey("trap.revive_stone");
        }

        private static void AssertLiveTemplateCoversProjectKey(string id)
        {
            Assert.IsTrue(CardPresentationConfigCatalog.TryGet(id, out var dto), id + " JSON 应可加载");
            Assert.IsNotNull(dto);

            var errors = CardDescriptionTokenRules.ValidateCard(dto);
            Assert.AreEqual(0, errors.Count, id + " 令牌契约应干净：\n" + string.Join("\n", errors));

            Assert.IsFalse(string.IsNullOrWhiteSpace(dto.liveTemplate), id + " 倒计时卡须作者局内模板");
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

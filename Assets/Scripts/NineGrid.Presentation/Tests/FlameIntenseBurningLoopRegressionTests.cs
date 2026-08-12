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
    /// 烈焰卡组「max depth 炸局」回归（ADR-0047）。
    /// 锁死的 bug 形态：卡面装配的效果 owner SourceDefId 是挂载卡 DefId（monster.dragon_follower），
    /// 而剧烈燃烧模板 EventFilterExcludeCause 排除的是 "skill.intense_burning"——排除永不命中，
    /// 幽灵炎对自己洗入的烈焰再次触发 → 无限自触发 → 撞 64 层深度保护 → 异常炸穿命令中途，
    /// Core/表现盘面分叉（卡不可点击、遗物哑火同源）。
    /// 修复分两层：① ShuffleInto 原子支持显式 cause，模板补 "cause":"skill.intense_burning"；
    /// ② 管线深度/总量超限从抛异常改为熔断遏制（丢弃分支 + PipelineFaultContained 诊断事件）。
    /// </summary>
    public class FlameIntenseBurningLoopRegressionTests
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
        public void RealContent_IntenseBurning_AddsExactlyOneExtraFlame_NoLoop()
        {
            LoadRealCatalog();
            var avatar = CreateAvatarOnBoard(SlotId.Board(5));
            var follower = CreateRealCardOnBoard("monster.dragon_follower", SlotId.Board(1));

            // 模拟献火等来源洗入一张烈焰（cause 非 skill.intense_burning，应触发剧烈燃烧一次）。
            Run(new ShuffleIntoDrawPileAction("trap.flame", CardKind.Trap, 1, false, "test.initial_deal"));

            Assert.AreEqual(
                2,
                CountDrawPileFlames(),
                "初始 1 张 + 剧烈燃烧额外 1 张 = 2；更多说明防循环排除失效，更少说明触发丢失");
            Assert.AreEqual(
                0,
                CountEvents(CoreEventType.PipelineFaultContained),
                "正常链不得触发管线熔断");

            var extraCause = FindLastFlameDealtCause();
            Assert.AreEqual(
                "skill.intense_burning",
                extraCause,
                "剧烈燃烧洗入的烈焰必须携带 skill.intense_burning cause 标记（防自触发环）");
        }

        [Test]
        public void RunawayTriggerLoop_IsContained_CommandFinishes_PipelineStaysUsable()
        {
            var avatar = CreateAvatarOnBoard(SlotId.Board(5));
            var owner = CreateMonsterOnBoard("monster.test.loop_owner", SlotId.Board(1));
            ActivateUnguardedLoopEffect(owner);

            var pipeline = mArch.GetSystem<IActionPipelineSystem>();

            // 无排除条件的 OnDeal→ShuffleInto 自触发环：修复前抛
            // InvalidOperationException 炸穿命令；现在必须熔断收尾。
            Assert.DoesNotThrow(
                () => Run(new ShuffleIntoDrawPileAction("trap.test_flame", CardKind.Trap, 1, false, "test.seed")),
                "失控触发环不得把异常抛出命令边界（否则 Core/表现分叉、盘面卡死）");

            Assert.Greater(
                CountEvents(CoreEventType.PipelineFaultContained),
                0,
                "熔断必须留下 PipelineFaultContained 诊断事件供日志审查");

            // 管线必须保持可用：熔断后还能正常执行后续命令。
            var resolved = pipeline.Execute(new ShuffleIntoDrawPileAction("trap.test_other", CardKind.Trap, 1, true, "test.after"));
            Assert.Greater(resolved, 0, "熔断后管线必须还能解算后续动作");
            Assert.IsFalse(pipeline.IsRunning, "熔断后 IsRunning 必须复位");
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
            content.ApplyContentToCard(card);
            mArch.GetModel<BoardModel>().PlaceCard(card, slot);
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

        /// <summary>复刻修复前的坏形态：OnDeal→ShuffleInto 自触发、无任何防环排除。</summary>
        private void ActivateUnguardedLoopEffect(CardInstance owner)
        {
            const string json =
                "{\"id\":\"test.unguarded_loop\","
                + "\"requires\":[\"HasOwnerEntity\",\"CardZoneTriggerable\"],\"containerType\":\"MonsterSkill\","
                + "\"kind\":\"Triggered\","
                + "\"trigger\":{\"atom\":\"OnDeal\"},"
                + "\"target\":{\"atom\":\"Player\"},"
                + "\"action\":{\"atom\":\"ShuffleInto\",\"defId\":\"trap.test_flame\",\"kind\":\"Trap\",\"count\":1,\"top\":false}}";
            var effects = mArch.GetSystem<IEffectSystem>();
            effects.Activate(effects.ParseJson(json), new EffectOwner(EffectContainerType.MonsterSkill, "monster.test.loop_owner", owner.Uid));
        }

        private void Run(GameAction action)
        {
            mArch.GetSystem<IActionPipelineSystem>().Execute(action);
        }

        private int CountDrawPileFlames()
        {
            var deck = mArch.GetModel<DeckModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var count = 0;
            foreach (var uid in deck.DrawPileUids)
            {
                CardInstance card;
                if (registry.TryGet(uid, out card) && card != null && card.DefId == "trap.flame")
                {
                    count++;
                }
            }

            return count;
        }

        private int CountEvents(CoreEventType type)
        {
            var entries = mArch.GetSystem<IActionPipelineSystem>().EventLog.Entries;
            var count = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].Type == type)
                {
                    count++;
                }
            }

            return count;
        }

        private string FindLastFlameDealtCause()
        {
            var entries = mArch.GetSystem<IActionPipelineSystem>().EventLog.Entries;
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i].Type == CoreEventType.CardDealt && entries[i].SourceDefId == "trap.flame")
                {
                    return entries[i].Cause;
                }
            }

            return string.Empty;
        }
    }
}

using System;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// ADR-0035 / 倒计时票：效果倒计时剩余经结算指令（EffectCountdownChanged）广播——
    /// 剩余只经 Settled 已提交投影值到达表现层，View 禁止直读 Core 计数器。
    /// 无 projectKey 的倒计时不投影（旧行为不变）。
    /// </summary>
    public sealed class EffectCountdownProjectionTests
    {
        private static readonly SlotId sSlot2 = SlotId.Board(2);
        private static readonly SlotId sSlot3 = SlotId.Board(3);
        private static readonly SlotId sSlot4 = SlotId.Board(4);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            EffectTemplateCatalog.Invalidate();
            CardPresentationConfigCatalog.Invalidate();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, ContentCatalogBootstrap.Load());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 42UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            EffectTemplateCatalog.Invalidate();
            CardPresentationConfigCatalog.Invalidate();
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void Flame_Move_EmitsCountdownRemaining_ThenFiresAndResets()
        {
            // 烈焰满 3 次盘面移动自移除；每移动一次广播一次剩余（2 → 1 → 触发复位 3）。
            StartEmptyNode();
            var flameUid = SpawnTrap("trap.flame", sSlot2);

            var eventStart = mPipeline.EventLog.Entries.Count;
            mPipeline.Enqueue(new MoveCardAction(flameUid, sSlot4, "test", "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            AssertCountdownCommitted(eventStart, flameUid, "trap.flame.remove.every", 2);

            eventStart = mPipeline.EventLog.Entries.Count;
            mPipeline.Enqueue(new MoveCardAction(flameUid, sSlot2, "test", "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            AssertCountdownCommitted(eventStart, flameUid, "trap.flame.remove.every", 1);

            eventStart = mPipeline.EventLog.Entries.Count;
            mPipeline.Enqueue(new MoveCardAction(flameUid, sSlot4, "test", "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            AssertCountdownCommitted(eventStart, flameUid, "trap.flame.remove.every", 3);
            Assert.AreEqual(
                ZoneId.Removed,
                mArch.GetModel<CardRegistry>().Get(flameUid).Zone.Value,
                "满 3 次盘面移动应自移除");
        }

        [Test]
        public void Flame_FirstMove_CountdownCommit_CarriesProjectionKeyAndCardUid()
        {
            StartEmptyNode();
            var flameUid = SpawnTrap("trap.flame", sSlot2);
            var eventStart = mPipeline.EventLog.Entries.Count;

            mPipeline.Enqueue(new MoveCardAction(flameUid, sSlot4, "test", "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            CoreGameEvent committed = null;
            var entries = mPipeline.EventLog.Entries;
            for (var i = eventStart; i < entries.Count; i++)
            {
                if (entries[i].Type == CoreEventType.EffectCountdownChanged
                    && entries[i].CardUid == flameUid)
                {
                    committed = entries[i];
                    break;
                }
            }

            Assert.IsNotNull(committed, "应广播效果倒计时剩余事件");
            Assert.AreEqual("trap.flame.remove.every", committed.Message, "事件应携带完整「装配id.键」投影令牌键");
            Assert.AreEqual(2, committed.ResultValue, "剩余应为计数器当前值（还差 2 次）");
        }

        [Test]
        public void CountdownWithoutProjectKey_EmitsNoCountdownEvent()
        {
            // 未作者 projectKey 的倒计时触发不投影（旧行为不变，避免无意义广播）。
            StartEmptyNode();
            var hostUid = SpawnTrap("trap.spike", sSlot2);

            var template = new EffectTemplateDefinition(
                "tpl.test.countdown_no_projection",
                new[] { "HasOwnerEntity", "CardZoneTriggerable" },
                new string[0],
                "{\"kind\":\"Triggered\",\"trigger\":{\"atom\":\"OnSelfMove\",\"every\":3},\"target\":{\"atom\":\"Self\"},\"action\":{\"atom\":\"RemoveCard\",\"destination\":\"Removed\",\"reason\":\"{{reason}}\"}}",
                ContentImplementationState.Implemented,
                "测试倒计时（无投影键）");
            var resolved = EffectAssemblyResolver.Resolve(
                template,
                "test.countdown_no_projection",
                EffectContainerType.Trap,
                EffectAssemblyResolver.ParseArgsJson("{\"reason\":\"test\"}"));
            var effects = mArch.GetSystem<IEffectSystem>();
            var definition = effects.ParseJson(resolved.Json);
            effects.Activate(definition, new EffectOwner(EffectContainerType.Trap, "trap.spike", hostUid));

            var eventStart = mPipeline.EventLog.Entries.Count;
            mPipeline.Enqueue(new MoveCardAction(hostUid, sSlot3, "test", "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            var entries = mPipeline.EventLog.Entries;
            for (var i = eventStart; i < entries.Count; i++)
            {
                Assert.AreNotEqual(
                    CoreEventType.EffectCountdownChanged,
                    entries[i].Type,
                    "无投影键的倒计时不得广播剩余");
            }
        }

        [Test]
        public void Flame_Content_DescriptionContract_And_ProjectionArgs_Clean()
        {
            // 首张倒计时卡内容契约：静态检查描述走 {装配id.键}、局内模板必填、描述格 ≤26、
            // 倒计时周期为实参 every、投影令牌键为「装配id.键」（ticket 倒计时卡验收）。
            Assert.IsTrue(CardPresentationConfigCatalog.TryGet("trap.flame", out var dto), "trap.flame JSON 应可加载");
            Assert.IsNotNull(dto);

            var errors = CardDescriptionTokenRules.ValidateCard(dto);
            Assert.IsEmpty(errors, string.Join("; ", errors));

            StringAssert.Contains("{trap.flame.move.amount}", dto.description);
            StringAssert.Contains("{trap.flame.remove.every}", dto.description);
            Assert.IsFalse(string.IsNullOrWhiteSpace(dto.liveTemplate), "倒计时卡必须作者局内模板");
            StringAssert.Contains("{trap.flame.remove.every}", dto.liveTemplate);
            Assert.LessOrEqual(CardDescriptionTokenRules.CountUnits(dto.description), CardDescriptionTokenRules.MaxUnits);
            Assert.LessOrEqual(CardDescriptionTokenRules.CountUnits(dto.liveTemplate), CardDescriptionTokenRules.MaxUnits);

            EffectAssemblyDto remove = null;
            for (var i = 0; i < dto.effectAssemblies.Length; i++)
            {
                if (dto.effectAssemblies[i] != null
                    && string.Equals(dto.effectAssemblies[i].id, "trap.flame.remove", StringComparison.Ordinal))
                {
                    remove = dto.effectAssemblies[i];
                    break;
                }
            }

            Assert.IsNotNull(remove, "trap.flame.remove 装配应存在");
            var args = EffectAssemblyResolver.ParseArgsJson(remove.argsJson);
            Assert.AreEqual(3, Convert.ToInt32(args["every"]), "倒计时周期应为装配实参 every");
            Assert.AreEqual("trap.flame.remove.every", args["projectKey"], "投影令牌键应为装配id.键");
        }

        private void AssertCountdownCommitted(int startIndex, int ownerUid, string projectionKey, int expectedRemaining)
        {
            var entries = mPipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                if (entries[i].Type == CoreEventType.EffectCountdownChanged
                    && entries[i].CardUid == ownerUid)
                {
                    Assert.AreEqual(projectionKey, entries[i].Message, "剩余事件应携带投影令牌键");
                    Assert.AreEqual(expectedRemaining, entries[i].ResultValue, "剩余事件应携带剩余次数");
                    return;
                }
            }

            Assert.Fail("缺少 EffectCountdownChanged 事件（owner=" + ownerUid + " key=" + projectionKey + "）");
        }

        private void StartEmptyNode()
        {
            Assert.IsTrue(mPhase.StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            }).Accepted);
        }

        private int SpawnTrap(string defId, SlotId slot)
        {
            mPipeline.Enqueue(new SpawnCardAction(defId, CardKind.Trap, ZoneId.Board, slot, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            var uid = mArch.GetModel<BoardModel>().GetCardUid(slot);
            return uid;
        }
    }
}

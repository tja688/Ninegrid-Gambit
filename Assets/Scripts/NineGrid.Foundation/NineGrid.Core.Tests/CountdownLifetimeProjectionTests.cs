using System;
using System.Collections.Generic;
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
    /// ADR-0035 / 倒计时票（#157）：倒计时/耐久卡全集局内模板 + Battle/Run 寿命。
    /// - Battle 作用域：离开战斗（清关 / 战败）把计数器复位到阈值并广播剩余（authority + projection）。
    /// - Run 作用域：跨战斗忠实保留剩余，离战不重置。
    /// - 效果卸载（Deactivate）广播 EffectCountdownCleared，表现层清除投影键（回退静态）。
    /// - 作用域标记（scope）仅作者/系统可见，绝不进入投影键或渲染文本。
    /// </summary>
    public sealed class CountdownLifetimeProjectionTests
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
        public void ReviveStone_Content_DescriptionContract_And_LiveTemplate_Clean()
        {
            // 第二张倒计时卡（复活石）内容契约：静态描述走 {装配id.键}（含修掉简单式 {count}）、
            // 局内模板必填并引用投影令牌、倒计时周期为实参 every、投影键=装配id.键、描述格 ≤26。
            Assert.IsTrue(CardPresentationConfigCatalog.TryGet("trap.revive_stone", out var dto), "trap.revive_stone JSON 应可加载");
            Assert.IsNotNull(dto);

            var errors = CardDescriptionTokenRules.ValidateCard(dto);
            Assert.IsEmpty(errors, string.Join("; ", errors));

            StringAssert.Contains("{trap.revive_stone.interact.every}", dto.description);
            StringAssert.Contains("{trap.revive_stone.remove.count}", dto.description);
            Assert.IsFalse(string.IsNullOrWhiteSpace(dto.liveTemplate), "倒计时卡必须作者局内模板");
            StringAssert.Contains("{trap.revive_stone.interact.every}", dto.liveTemplate);
            Assert.LessOrEqual(CardDescriptionTokenRules.CountUnits(dto.description), CardDescriptionTokenRules.MaxUnits);
            Assert.LessOrEqual(CardDescriptionTokenRules.CountUnits(dto.liveTemplate), CardDescriptionTokenRules.MaxUnits);

            EffectAssemblyDto interact = null;
            for (var i = 0; i < dto.effectAssemblies.Length; i++)
            {
                if (dto.effectAssemblies[i] != null
                    && string.Equals(dto.effectAssemblies[i].id, "trap.revive_stone.interact", StringComparison.Ordinal))
                {
                    interact = dto.effectAssemblies[i];
                    break;
                }
            }

            Assert.IsNotNull(interact, "trap.revive_stone.interact 装配应存在");
            var args = EffectAssemblyResolver.ParseArgsJson(interact.argsJson);
            Assert.AreEqual(6, Convert.ToInt32(args["every"]), "倒计时周期应为装配实参 every");
            Assert.AreEqual("trap.revive_stone.interact.every", args["projectKey"], "投影令牌键应为装配id.键");
        }

        [Test]
        public void ReviveStone_Interact_EmitsCountdownRemaining_ThenFiresAndRemoves()
        {
            // 复活石满 6 次互动自移除；每次互动广播一次剩余（5 → 4 → 3 → 2 → 1 → 触发复位 6）。
            StartEmptyNode();
            var stoneUid = SpawnTrap("trap.revive_stone", sSlot2);

            var eventStart = mPipeline.EventLog.Entries.Count;
            Interact();
            AssertCountdownCommitted(eventStart, stoneUid, "trap.revive_stone.interact.every", 5);

            eventStart = mPipeline.EventLog.Entries.Count;
            Interact();
            AssertCountdownCommitted(eventStart, stoneUid, "trap.revive_stone.interact.every", 4);

            eventStart = mPipeline.EventLog.Entries.Count;
            Interact();
            AssertCountdownCommitted(eventStart, stoneUid, "trap.revive_stone.interact.every", 3);

            eventStart = mPipeline.EventLog.Entries.Count;
            Interact();
            AssertCountdownCommitted(eventStart, stoneUid, "trap.revive_stone.interact.every", 2);

            eventStart = mPipeline.EventLog.Entries.Count;
            Interact();
            AssertCountdownCommitted(eventStart, stoneUid, "trap.revive_stone.interact.every", 1);

            eventStart = mPipeline.EventLog.Entries.Count;
            Interact();
            AssertCountdownCommitted(eventStart, stoneUid, "trap.revive_stone.interact.every", 6);
            Assert.AreEqual(
                ZoneId.Removed,
                mArch.GetModel<CardRegistry>().Get(stoneUid).Zone.Value,
                "满 6 次互动应自移除");
        }

        [Test]
        public void BattleScoped_LeaveBattle_ResetsCounterToPeriod_AndCommits()
        {
            // Battle 作用域：离战（清关）把 trap.flame 剩余复位到阈值并广播 EffectCountdownChanged。
            StartEmptyNode();
            var flameUid = SpawnTrap("trap.flame", sSlot2);

            mPipeline.Enqueue(new MoveCardAction(flameUid, sSlot4, "test", "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            var eventStart = mPipeline.EventLog.Entries.Count;
            mPipeline.Enqueue(new ResetBattleScopedCountdownsAction());
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            // 离战重置：剩余提交回阈值 3（Commit 读的就是 Core 计数器，authority + projection 同步）。
            AssertCountdownCommitted(eventStart, flameUid, "trap.flame.remove.every", 3);

            // 重置后再次盘面移动应从阈值重新倒计时（剩余 2），证明不是残留旧值。
            eventStart = mPipeline.EventLog.Entries.Count;
            mPipeline.Enqueue(new MoveCardAction(flameUid, sSlot2, "test", "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            AssertCountdownCommitted(eventStart, flameUid, "trap.flame.remove.every", 2);
        }

        [Test]
        public void BattleScoped_LeaveBattle_ResetsItemSlotCard_ForReal()
        {
            // Battle 作用域在持久卡（道具卡格）上也真重置：离战后计数器=阈值，投影键收到阈值提交。
            StartEmptyNode();
            var helpUid = SpawnHelpCardToItemSlots("help.food_card");
            var instanceId = ActivateCountdownOnCard(
                helpUid,
                scope: "battle",
                every: 3,
                projectionKey: "fx.battle.countdown.every");

            var key = CoreCounterKeys.EffectCounterPrefix + instanceId + ".interact";
            mArch.GetModel<CardRegistry>().Get(helpUid).Counters.Set(key, 1);
            Assert.AreEqual(1, mArch.GetModel<CardRegistry>().Get(helpUid).Counters.Get(key), "预置剩余 1");

            var eventStart = mPipeline.EventLog.Entries.Count;
            mPipeline.Enqueue(new ResetBattleScopedCountdownsAction());
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            Assert.AreEqual(3, mArch.GetModel<CardRegistry>().Get(helpUid).Counters.Get(key), "Battle 作用域离战须重置为阈值");
            AssertCountdownCommitted(eventStart, helpUid, "fx.battle.countdown.every", 3);
        }

        [Test]
        public void RunScoped_LeaveBattle_KeepsRemaining()
        {
            // Run 作用域：离战不重置，剩余忠实保留（遗物/道具格跨战斗计数）。
            StartEmptyNode();
            var helpUid = SpawnHelpCardToItemSlots("help.food_card");
            var instanceId = ActivateCountdownOnCard(
                helpUid,
                scope: "run",
                every: 3,
                projectionKey: "fx.run.countdown.every");

            var key = CoreCounterKeys.EffectCounterPrefix + instanceId + ".interact";
            mArch.GetModel<CardRegistry>().Get(helpUid).Counters.Set(key, 1);

            var eventStart = mPipeline.EventLog.Entries.Count;
            mPipeline.Enqueue(new ResetBattleScopedCountdownsAction());
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            Assert.AreEqual(1, mArch.GetModel<CardRegistry>().Get(helpUid).Counters.Get(key), "Run 作用域离战不得重置");
            AssertNoCountdownCommittedSince(eventStart, helpUid, "fx.run.countdown.every");
        }

        [Test]
        public void Unmount_DeactivateEffect_EmitsCountdownCleared()
        {
            // 效果卸载（DeactivateEffectAction）广播 EffectCountdownCleared（键为投影键），
            // 表现层清除该键并回退静态（销毁/卸载停止投影）。
            StartEmptyNode();
            var helpUid = SpawnHelpCardToItemSlots("help.food_card");
            var instanceId = ActivateCountdownOnCard(
                helpUid,
                scope: "run",
                every: 3,
                projectionKey: "fx.unmount.countdown.every");

            var eventStart = mPipeline.EventLog.Entries.Count;
            mPipeline.Enqueue(new DeactivateEffectAction(instanceId));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            var entries = mPipeline.EventLog.Entries;
            var cleared = false;
            for (var i = eventStart; i < entries.Count; i++)
            {
                if (entries[i].Type == CoreEventType.EffectCountdownCleared
                    && entries[i].CardUid == helpUid)
                {
                    Assert.AreEqual("fx.unmount.countdown.every", entries[i].Message, "清除事件应携带投影键");
                    cleared = true;
                    break;
                }
            }

            Assert.IsTrue(cleared, "卸载带投影键的效果应广播 EffectCountdownCleared");
        }

        [Test]
        public void ScopeMarker_NeverAppearsInProjectedPlayerText()
        {
            // 作用域标记（scope）只存在于 DSL 配置，绝不进入投影键或渲染字符串。
            Assert.IsTrue(CardPresentationConfigCatalog.TryGet("trap.revive_stone", out var dto));
            Assert.IsNotNull(dto);

            Assert.IsFalse(
                dto.liveTemplate.IndexOf("battle", StringComparison.OrdinalIgnoreCase) >= 0,
                "局内模板不得含 scope=battle 标记");
            Assert.IsFalse(
                dto.liveTemplate.IndexOf("run", StringComparison.OrdinalIgnoreCase) >= 0,
                "局内模板不得含 scope=run 标记");
            Assert.IsFalse(
                dto.description.IndexOf("battle", StringComparison.OrdinalIgnoreCase) >= 0,
                "检查描述不得含 scope 标记");
            StringAssert.Contains("{trap.revive_stone.interact.every}", dto.liveTemplate);
            StringAssert.Contains("{trap.revive_stone.remove.count}", dto.liveTemplate);
        }

        [Test]
        public void ScopeMarker_RunTrigger_ResolvesRunScope_AndProjectionKeyStaysClean()
        {
            // DSL scope:"run" 解析为 Run 作用域；投影键仍为「装配id.键」不含 scope 标记。
            var template = new EffectTemplateDefinition(
                "tpl.test.scope_run",
                new[] { "HasOwnerEntity", "CardZoneTriggerable" },
                new string[0],
                "{\"kind\":\"Triggered\",\"trigger\":{\"atom\":\"OnInteract\",\"every\":\"{{every}}\",\"projectKey\":\"{{projectKey}}\",\"scope\":\"{{scope}}\"},\"target\":{\"atom\":\"Self\"},\"action\":{\"atom\":\"RemoveCard\",\"destination\":\"Removed\",\"reason\":\"{{reason}}\"}}",
                ContentImplementationState.Implemented,
                "测试 run 作用域");
            var resolved = EffectAssemblyResolver.Resolve(
                template,
                "fx.card.cd",
                EffectContainerType.HelpCard,
                EffectAssemblyResolver.ParseArgsJson(
                    "{\"reason\":\"test\",\"every\":3,\"projectKey\":\"fx.card.cd.every\",\"scope\":\"run\"}"));
            var definition = mArch.GetSystem<IEffectSystem>().ParseJson(resolved.Json);
            var instance = mArch.GetSystem<IEffectSystem>().Activate(
                definition,
                new EffectOwner(EffectContainerType.HelpCard, "fx.card.cd", 0));

            var trigger = instance.Trigger as ICountdownProjectionTrigger;
            Assert.IsNotNull(trigger, "触发器应为倒计时投影触发");
            Assert.AreEqual(CountdownScope.Run, trigger.Scope, "scope:run 应解析为 Run 作用域");
            Assert.AreEqual("fx.card.cd.every", trigger.CountdownProjectionKey, "投影键应为「装配id.键」");
            Assert.IsFalse(
                trigger.CountdownProjectionKey.IndexOf("battle", StringComparison.OrdinalIgnoreCase) >= 0,
                "投影键不得含 battle 标记");
        }

        private string ActivateCountdownOnCard(int cardUid, string scope, int every, string projectionKey)
        {
            var template = new EffectTemplateDefinition(
                "tpl.test.countdown_lifetime",
                new[] { "HasOwnerEntity", "CardZoneTriggerable" },
                new string[0],
                "{\"kind\":\"Triggered\",\"trigger\":{\"atom\":\"OnInteract\",\"every\":\"{{every}}\",\"projectKey\":\"{{projectKey}}\",\"scope\":\"{{scope}}\"},\"target\":{\"atom\":\"Self\"},\"action\":{\"atom\":\"Sequence\",\"actions\":[{\"atom\":\"RemoveCard\",\"destination\":\"Removed\",\"reason\":\"{{reason}}\"}]}}",
                ContentImplementationState.Implemented,
                "测试倒计时寿命");
            var resolved = EffectAssemblyResolver.Resolve(
                template,
                "fx.lifetime." + scope,
                EffectContainerType.HelpCard,
                EffectAssemblyResolver.ParseArgsJson(
                    "{\"reason\":\"test\",\"every\":" + every + ",\"projectKey\":\"" + projectionKey + "\",\"scope\":\"" + scope + "\"}"));
            var definition = mArch.GetSystem<IEffectSystem>().ParseJson(resolved.Json);
            var instance = mArch.GetSystem<IEffectSystem>().Activate(
                definition,
                new EffectOwner(EffectContainerType.HelpCard, "fx.lifetime." + scope, cardUid));
            return instance.InstanceId;
        }

        private void Interact()
        {
            mPipeline.Enqueue(new ModifyInteractionCountAction(1));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
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

        private void AssertNoCountdownCommittedSince(int startIndex, int ownerUid, string projectionKey)
        {
            var entries = mPipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                if (entries[i].Type == CoreEventType.EffectCountdownChanged
                    && entries[i].CardUid == ownerUid
                    && string.Equals(entries[i].Message, projectionKey, StringComparison.Ordinal))
                {
                    Assert.Fail("不应广播剩余（owner=" + ownerUid + " key=" + projectionKey + "）");
                }
            }
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

        private int SpawnHelpCardToItemSlots(string defId)
        {
            mPipeline.Enqueue(new SpawnCardAction(defId, CardKind.HelpCard, ZoneId.ItemSlots, SlotId.None, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            var registry = mArch.GetModel<CardRegistry>();
            foreach (var pair in registry.Cards)
            {
                if (pair.Value.Kind == CardKind.HelpCard
                    && pair.Value.Zone.Value == ZoneId.ItemSlots
                    && string.Equals(pair.Value.DefId, defId, StringComparison.Ordinal))
                {
                    return pair.Key;
                }
            }

            Assert.Fail("道具卡格帮助卡未找到: " + defId);
            return 0;
        }
    }
}

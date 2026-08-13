using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 黄金鱼竿（relic.golden_coffer）获得时给宝箱卡回归：
    /// 复现策划报告——战斗内宝箱三选一拿到后既没有宝箱卡（Spawn 落入只供开局发牌
    /// 消费的 PlayerCardPool 暂存池，下节点被 ClearBattleZones 吞掉），遗物栏也不显示
    /// （DeactivateSelfEffect 曾无条件把遗物容器效果的宿主遗物 RemoveRelic）。
    /// </summary>
    public class RelicGoldenCofferGrantTests
    {
        private const string RelicId = "relic.golden_coffer";
        private const string GrantEffectId = "relic.golden_coffer.grant";
        private const string ChestCardId = "help.golden_chest_card";

        private IArchitecture mArch;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Interface;
            var content = mArch.GetSystem<IContentSystem>();
            content.Load(NineGrid.Content.ContentCatalogBootstrap.Load());
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, content.Catalog);
            Assert.IsTrue(content.HasCatalog, "需要真实内容目录");
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void InBattleGrant_ShufflesTwoChestsIntoDrawPile_RelicStaysEquipped()
        {
            CreateAvatar();
            StartCombatNode();

            Run(new GrantRelicAction(RelicId));

            Assert.AreEqual(
                2,
                CountChestCards(ZoneId.DrawPile),
                "战斗内获得应把 2 张金色宝箱卡洗入抽牌堆（不落死暂存池）");
            Assert.Contains(
                RelicId,
                (System.Collections.ICollection)mArch.GetModel<PlayerModel>().RelicDefIds,
                "一次性效果发放完后遗物应留在装备栏");
            Assert.IsTrue(
                mArch.GetModel<PlayerModel>().IsRelicEffectConsumed(GrantEffectId),
                "发放后应落一次性消费标记");
            Assert.IsFalse(
                HasActiveEffectInstanceFrom(RelicId),
                "一次性效果实例应已停用");
        }

        [Test]
        public void RemountAndNextNodeSelfHeal_DoNotRegrantChests()
        {
            CreateAvatar();
            StartCombatNode();
            Run(new GrantRelicAction(RelicId));
            Assert.AreEqual(2, CountChestCards(ZoneId.DrawPile), "前置：首次发放 2 张");

            // 模拟存档恢复 / 跨层重装直接重挂：消费标记应挡住 OnActivate 重发。
            var remounted = mArch.GetSystem<IContentSystem>().ActivateRelic(RelicId);
            Assert.AreEqual(0, remounted.Count, "已消费的一次性效果不应再被挂载");
            mArch.GetSystem<IActionPipelineSystem>().RunToCompletion();
            Assert.AreEqual(2, CountChestCards(ZoneId.DrawPile), "重挂不得重复发放");

            // 下一战斗节点：StartNode 自愈也不得重挂重发。
            var run = mArch.GetModel<RunModel>();
            run.NodeIndex.Value = 1;
            run.SetPhase(GamePhase.NodeCompleted);
            var next = mArch.GetSystem<IPhaseSystem>().StartNode(null);
            Assert.IsTrue(next.Accepted, "第二个战斗节点 StartNode 应被接受: " + next.Reason);

            Assert.AreEqual(
                0,
                CountChestCards(ZoneId.DrawPile),
                "新节点抽牌堆不应再冒出宝箱卡（自愈不得重发一次性效果）");
            Assert.Contains(
                RelicId,
                (System.Collections.ICollection)mArch.GetModel<PlayerModel>().RelicDefIds,
                "遗物应仍在装备栏");
        }

        [Test]
        public void OutOfBattleGrant_RoutesChestsToItemSlots()
        {
            CreateAvatar();

            // 非战斗相位（局外授予）：沿用帮助卡授予约定，直接写入道具卡格。
            Run(new GrantRelicAction(RelicId));

            Assert.AreEqual(
                2,
                CountChestCards(ZoneId.ItemSlots),
                "局外获得应把 2 张金色宝箱卡写入道具卡格");
            Assert.Contains(
                RelicId,
                (System.Collections.ICollection)mArch.GetModel<PlayerModel>().RelicDefIds,
                "遗物应留在装备栏");
        }

        [Test]
        public void DiscardThenRegrant_FiresAgain()
        {
            CreateAvatar();
            StartCombatNode();
            Run(new GrantRelicAction(RelicId));
            Assert.AreEqual(2, CountChestCards(ZoneId.DrawPile), "前置：首次发放 2 张");

            Run(new DiscardRelicAction(RelicId));
            Assert.IsFalse(
                mArch.GetModel<PlayerModel>().IsRelicEffectConsumed(GrantEffectId),
                "丢弃遗物应清掉一次性消费标记");

            Run(new GrantRelicAction(RelicId));
            Assert.AreEqual(
                4,
                CountChestCards(ZoneId.DrawPile),
                "重新获得应再次发放（2 + 2）");
        }

        /// <summary>凤凰羽毛显式 removeRelic:true：模板带新字段仍能通过校验并正常挂载。</summary>
        [Test]
        public void PhoenixFeather_WithRemoveRelicFlag_StillMounts()
        {
            CreateAvatar();

            Run(new GrantRelicAction("relic.phoenix_feather"));

            Assert.Contains(
                "relic.phoenix_feather",
                (System.Collections.ICollection)mArch.GetModel<PlayerModel>().RelicDefIds,
                "凤凰羽毛应进装备栏");
            Assert.IsTrue(
                HasActiveEffectInstanceFrom("relic.phoenix_feather"),
                "凤凰羽毛 OnFatalDamage 效果应处于挂载状态");
        }

        // ==================== 基建 ====================

        private CardInstance CreateAvatar()
        {
            var avatar = mArch.GetModel<CardRegistry>().Create("avatar.default", CardKind.Avatar);
            avatar.Stats.SetBase(StatId.MaxHp, 20);
            avatar.Stats.SetBase(StatId.Hp, 20);
            avatar.Stats.SetBase(StatId.Attack, 5);
            avatar.Stats.SetBase(StatId.Armor, 5);
            avatar.Stats.SetBase(StatId.CurrentArmor, 5);
            mArch.GetModel<BoardModel>().SetAvatar(avatar, SlotId.Board(5));
            return avatar;
        }

        private void StartCombatNode()
        {
            var run = mArch.GetModel<RunModel>();
            run.NodeIndex.Value = 0;
            run.SetPhase(GamePhase.NodeCompleted);
            var result = mArch.GetSystem<IPhaseSystem>().StartNode(null);
            Assert.IsTrue(result.Accepted, "StartNode 应被接受: " + result.Reason);
            Assert.AreEqual(
                GamePhase.InteractionLoop,
                run.Phase.Value,
                "战斗节点应进入 InteractionLoop");
        }

        private int CountChestCards(ZoneId zone)
        {
            var registry = mArch.GetModel<CardRegistry>();
            var deck = mArch.GetModel<DeckModel>();
            IReadOnlyList<int> uids;
            switch (zone)
            {
                case ZoneId.DrawPile:
                    uids = deck.DrawPileUids;
                    break;
                case ZoneId.ItemSlots:
                    uids = deck.ItemSlotUids;
                    break;
                case ZoneId.PlayerCardPool:
                    uids = deck.PlayerCardPoolUids;
                    break;
                default:
                    return 0;
            }

            var count = 0;
            for (var i = 0; i < uids.Count; i++)
            {
                CardInstance card;
                if (registry.TryGet(uids[i], out card)
                    && card != null
                    && card.DefId == ChestCardId)
                {
                    count++;
                }
            }

            return count;
        }

        private bool HasActiveEffectInstanceFrom(string relicDefId)
        {
            var instances = mArch.GetSystem<NineGrid.Core.Effects.IEffectSystem>().Instances;
            for (var i = 0; i < instances.Count; i++)
            {
                var owner = instances[i].Owner;
                if (owner != null && owner.SourceDefId == relicDefId)
                {
                    return true;
                }
            }

            return false;
        }

        private void Run(GameAction action)
        {
            mArch.GetSystem<IActionPipelineSystem>().Execute(action);
        }
    }
}

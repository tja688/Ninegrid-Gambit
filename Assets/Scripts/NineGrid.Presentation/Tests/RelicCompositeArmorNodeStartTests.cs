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
    /// 复合盔甲（relic.composite_armor）开局加甲回归：
    /// 「每关卡开始时，每有 3 点攻击，获得 1 点当前护甲」。
    /// 复现策划报告：基础护甲 5，进入战斗节点后当前护甲仍是 5（未加甲）。
    /// </summary>
    public class RelicCompositeArmorNodeStartTests
    {
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
        public void StartCombatNode_Attack5Armor5_GainsOneArmor()
        {
            var avatar = CreateAvatar(attack: 5, armor: 5);
            Run(new GrantRelicAction("relic.composite_armor"));
            Assert.Contains(
                "relic.composite_armor",
                (System.Collections.ICollection)mArch.GetModel<PlayerModel>().RelicDefIds,
                "遗物应已装备");

            StartCombatNode();

            Assert.AreEqual(
                7,
                StatArmorUtility.GetCurrentArmor(avatar),
                "攻击 5 → floor(5/2)=2，开局当前护甲应为 5+2=7");
        }

        [Test]
        public void StartCombatNode_Attack9Armor5_GainsThreeArmor()
        {
            var avatar = CreateAvatar(attack: 9, armor: 5);
            Run(new GrantRelicAction("relic.composite_armor"));

            StartCombatNode();

            Assert.AreEqual(
                9,
                StatArmorUtility.GetCurrentArmor(avatar),
                "攻击 9 → floor(9/2)=4，开局当前护甲应为 5+4=9");
        }

        [Test]
        public void StartCombatNode_WithoutRelic_ArmorStaysAtBase()
        {
            var avatar = CreateAvatar(attack: 5, armor: 5);

            StartCombatNode();

            Assert.AreEqual(5, StatArmorUtility.GetCurrentArmor(avatar), "无遗物时开局当前护甲应等于基础护甲");
        }

        /// <summary>
        /// 自愈回归（对照 corelog-20260812-124012 的「遗物在栏但效果全程哑火」）：
        /// 装备栏有遗物但效果实例缺失（授予链曾被反应栈深度异常打断）时，
        /// 下一次 StartNode 应重挂效果，OnNodeStart 当节点即生效。
        /// </summary>
        [Test]
        public void DeadRelicEffectInstances_SelfHealOnNextStartNode()
        {
            var avatar = CreateAvatar(attack: 5, armor: 5);
            Run(new GrantRelicAction("relic.composite_armor"));

            // 模拟坏档：效果实例被清、遗物仍在装备栏。
            var effects = mArch.GetSystem<NineGrid.Core.Effects.IEffectSystem>();
            var instances = effects.Instances;
            for (var i = 0; i < instances.Count; i++)
            {
                var owner = instances[i].Owner;
                if (owner != null && owner.SourceDefId == "relic.composite_armor")
                {
                    effects.Deactivate(instances[i].InstanceId);
                }
            }

            Assert.Contains(
                "relic.composite_armor",
                (System.Collections.ICollection)mArch.GetModel<PlayerModel>().RelicDefIds,
                "坏档前提：遗物仍在装备栏");
            Assert.IsFalse(HasActiveEffectInstanceFrom("relic.composite_armor"), "坏档前提：效果实例已缺失");

            StartCombatNode();

            Assert.IsTrue(
                HasActiveEffectInstanceFrom("relic.composite_armor"),
                "StartNode 应自愈重挂缺失的遗物效果");
            Assert.AreEqual(
                7,
                StatArmorUtility.GetCurrentArmor(avatar),
                "自愈后 OnNodeStart 当节点生效：护甲应为 5+floor(5/2)=7");
        }

        /// <summary>
        /// 真实获取链路复现（对照 corelog-20260812-124012）：正式 Bootstrap → 战斗节点内
        /// 用宝箱卡开遗物三选一 → SelectReward 拿遗物 → 下一战斗节点 OnNodeStart 应触发。
        /// </summary>
        [Test]
        public void InBattleChestPick_RelicEffectActivates_AndFiresNextNodeStart()
        {
            var snapshot = InitialGameFactory.Create(mArch, new InitialGameOptions());
            var phase = mArch.GetSystem<IPhaseSystem>();
            var registry = mArch.GetModel<CardRegistry>();
            var content = mArch.GetSystem<IContentSystem>();
            var deck = mArch.GetModel<DeckModel>();
            var avatar = registry.Get(snapshot.AvatarUid);

            var startResult = phase.StartNode(null);
            Assert.IsTrue(startResult.Accepted, "首个战斗节点 StartNode 应被接受: " + startResult.Reason);

            // 局内宝箱：真实卡 + 真实 UseItem 链路（OfferRewardChoice → 遗物三选一）。
            var chestDraft = content.CreateDraft("help.common_chest_card");
            Assert.AreNotEqual(CardKind.Unknown, chestDraft.Kind, "help.common_chest_card 应在内容目录中");
            var chest = chestDraft.Create(registry);
            content.ApplyContentToCard(chest);
            chest.Zone.Value = ZoneId.ItemSlots;
            deck.AddToItemSlots(chest);

            var useResult = phase.UseItem(chest.Uid);
            Assert.IsTrue(useResult.Accepted, "开宝箱应被接受: " + useResult.Reason);

            var pending = mArch.GetModel<PendingChoiceModel>();
            Assert.AreEqual(PendingChoiceKind.Reward, pending.Kind.Value, "宝箱应弹出遗物三选一");

            var optionIndex = FindRelicOptionIndex(pending, "relic.composite_armor");
            var pickedDefId = pending.RewardOptions[optionIndex].DefId;
            var selectResult = phase.SelectReward(optionIndex);
            Assert.IsTrue(selectResult.Accepted, "选取遗物应被接受: " + selectResult.Reason);
            Assert.Contains(
                pickedDefId,
                (System.Collections.ICollection)mArch.GetModel<PlayerModel>().RelicDefIds,
                "遗物应已进装备栏");

            // 关键断言 1：效果实例真的激活了（订阅仍在）。
            Assert.IsTrue(
                HasActiveEffectInstanceFrom(pickedDefId),
                pickedDefId + " 的效果实例应处于激活状态");

            // 推进到下一战斗节点，验证 OnNodeStart 真的开火。
            var run = mArch.GetModel<RunModel>();
            run.NodeIndex.Value = 1;
            run.SetPhase(GamePhase.NodeCompleted);
            var armorBeforeReset = StatArmorUtility.GetCurrentArmor(avatar);
            var effectiveArmor = StatArmorUtility.GetEffectiveArmor(mArch.GetSystem<IStatSystem>(), avatar);
            var effectiveAttack = mArch.GetSystem<IStatSystem>().GetEffectiveInt(avatar, StatId.Attack);
            var next = phase.StartNode(null);
            Assert.IsTrue(next.Accepted, "第二个战斗节点 StartNode 应被接受: " + next.Reason);

            if (pickedDefId == "relic.composite_armor")
            {
                Assert.AreEqual(
                    effectiveArmor + effectiveAttack / 2,
                    StatArmorUtility.GetCurrentArmor(avatar),
                    "复合盔甲开局应加 floor(攻击/2) 点当前护甲（战前甲=" + armorBeforeReset + "）");
            }

            // 关键断言 2：下一节点开始后效果实例仍存活（未被中途清掉）。
            Assert.IsTrue(
                HasActiveEffectInstanceFrom(pickedDefId),
                pickedDefId + " 的效果实例在下一节点仍应存活");
        }

        /// <summary>优先选 composite_armor；池中没有就选第一件遗物（激活断言仍有效）。</summary>
        private int FindRelicOptionIndex(PendingChoiceModel pending, string preferredDefId)
        {
            var fallback = -1;
            for (var i = 0; i < pending.RewardOptions.Count; i++)
            {
                var entry = pending.RewardOptions[i];
                if (entry == null || entry.Kind != CardKind.Relic)
                {
                    continue;
                }

                if (entry.DefId == preferredDefId)
                {
                    return i;
                }

                if (fallback < 0)
                {
                    fallback = i;
                }
            }

            Assert.GreaterOrEqual(fallback, 0, "三选一里应至少有一件遗物");
            return fallback;
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

        // ==================== 基建 ====================

        private CardInstance CreateAvatar(int attack, int armor)
        {
            var avatar = mArch.GetModel<CardRegistry>().Create("avatar.default", CardKind.Avatar);
            avatar.Stats.SetBase(StatId.MaxHp, 20);
            avatar.Stats.SetBase(StatId.Hp, 20);
            avatar.Stats.SetBase(StatId.Attack, attack);
            avatar.Stats.SetBase(StatId.Armor, armor);
            avatar.Stats.SetBase(StatId.CurrentArmor, armor);
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

        private void Run(GameAction action)
        {
            mArch.GetSystem<IActionPipelineSystem>().Execute(action);
        }
    }
}

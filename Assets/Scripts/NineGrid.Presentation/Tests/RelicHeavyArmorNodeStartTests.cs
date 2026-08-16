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
    /// 重护甲（relic.heavy_armor）开局加甲回归：
    /// 「每有 every 点有效护甲，获得 gain 点当前护甲」。
    /// </summary>
    public class RelicHeavyArmorNodeStartTests
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
        public void StartCombatNode_BaseArmor0WithHeavyArmor_GainsOneFromEffectiveArmor()
        {
            var avatar = CreateAvatar(armor: 0);
            Run(new GrantRelicAction("relic.heavy_armor"));

            StartCombatNode();

            Assert.AreEqual(
                1,
                StatArmorUtility.GetCurrentArmor(avatar),
                "有效护甲 1（重护甲永久 +1）→ floor(1/2)=0，当前甲应保持 1");
        }

        [Test]
        public void StartCombatNode_BaseArmor5WithHeavyArmor_GainsThreeFromEffectiveArmor()
        {
            var avatar = CreateAvatar(armor: 5);
            Run(new GrantRelicAction("relic.heavy_armor"));

            StartCombatNode();

            Assert.AreEqual(
                9,
                StatArmorUtility.GetCurrentArmor(avatar),
                "有效护甲 6（基础 5 + 重护甲 1）→ floor(6/2)=3，当前甲应为 6+3=9");
        }

        [Test]
        public void HeavyArmorAssemblyParameters_DriveNodeStartGain()
        {
            var avatar = CreateAvatar(armor: 9);
            Run(new GrantRelicAction("relic.heavy_armor"));

            StartCombatNode();

            Assert.AreEqual(
                15,
                StatArmorUtility.GetCurrentArmor(avatar),
                "有效护甲 10 → floor(10/2)=5，当前甲应为 10+5=15");
        }

        private CardInstance CreateAvatar(int armor)
        {
            var avatar = mArch.GetModel<CardRegistry>().Create("avatar.default", CardKind.Avatar);
            avatar.Stats.SetBase(StatId.MaxHp, 20);
            avatar.Stats.SetBase(StatId.Hp, 20);
            avatar.Stats.SetBase(StatId.Attack, 3);
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
            Assert.AreEqual(GamePhase.InteractionLoop, run.Phase.Value, "战斗节点应进入 InteractionLoop");
        }

        private void Run(GameAction action)
        {
            mArch.GetSystem<IActionPipelineSystem>().Execute(action);
        }
    }
}

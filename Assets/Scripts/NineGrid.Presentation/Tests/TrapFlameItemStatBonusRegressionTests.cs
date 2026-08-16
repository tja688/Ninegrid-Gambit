using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NineGrid.Cards;
using NineGrid.Flow;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 卡店「道具卡数值强化」（UpgradeItemStats → PlayerModel.ItemStatBonus）只应作用于
    /// HelpCard 自有效果的固定数值；烈焰机关（trap.flame）是 Trap，不是道具卡，
    /// 其「移动后对邻接玩家造成 1 点伤害」不得被 ItemStatBonus 放大。
    /// </summary>
    public class TrapFlameItemStatBonusRegressionTests
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
        public void RealTrapFlame_WithItemStatBonus_StillDealsBaseOneDamage()
        {
            LoadRealCatalog();
            mArch.GetModel<PlayerModel>().AddItemStatBonus(12);

            var avatar = CreateAvatarOnBoard(SlotId.Board(5));
            var flame = CreateRealCardOnBoard("trap.flame", SlotId.Board(4));

            Run(new MoveCardAction(flame.Uid, SlotId.Board(8)));

            var damage = FindDamageDealtToAvatar();
            Assert.IsNotNull(damage, "烈焰机关移动到邻接格后必须对玩家产生一次伤害事件");
            Assert.AreEqual(
                1,
                damage.Amount,
                "烈焰机关是 Trap，不属道具卡；ItemStatBonus 不得叠加到它的固定伤害上");
        }

        [Test]
        public void TrapFlame_PresentationDescription_DoesNotPrintItemStatBonus()
        {
            LoadRealCatalog();
            mArch.GetModel<PlayerModel>().AddItemStatBonus(12);

            var snapshot = CoreCardPresentationMapper.BuildVisualSnapshotFromDefId(
                "trap.flame",
                CardPresentationKind.Trap);

            StringAssert.Contains(
                "1点伤害",
                snapshot.BasicDescription,
                "烈焰机关卡面描述必须保持基础 1 点伤害");
            StringAssert.DoesNotContain(
                "13点伤害",
                snapshot.BasicDescription,
                "Trap 卡面不得套用道具卡 ItemStatBonus 印刷数值");
        }

        [Test]
        public void FlameBoilingStacks_AreClearedAtNodeEnd_NotLeakedToLaterNodes()
        {
            LoadRealCatalog();
            CreateAvatarOnBoard(SlotId.Board(5));
            CreateRealCardOnBoard("monster.salamander", SlotId.Board(4));

            for (var i = 0; i < 5; i++)
            {
                Run(new ModifyInteractionCountAction(1));
            }

            Assert.Greater(
                CountRuleModifiers("skill.flame_boiling"),
                0,
                "5 次玩家互动应叠出 1 层烈焰沸腾（trap.flame 伤害 +1）");
            Assert.AreEqual(
                ModifierScope.UntilNodeEnds,
                FindRuleModifier("skill.flame_boiling").Scope,
                "烈焰沸腾是[场上]技能，叠层必须随节点结束清空，不能跨节点泄漏");

            Run(new ClearNodeTransientModifiersAction());

            Assert.AreEqual(
                0,
                CountRuleModifiers("skill.flame_boiling"),
                "烈焰沸腾是[场上]技能，节点结束必须清空，不能把加成带进后续节点"
                + "（否则会被误读为道具卡数值强化影响烈焰机关）");
        }

        private void LoadRealCatalog()
        {
            var content = mArch.GetSystem<IContentSystem>();
            content.Load(NineGrid.Content.ContentCatalogBootstrap.Load());
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, content.Catalog);
            Assert.IsTrue(content.HasCatalog, "需要真实内容目录");
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

        private void Run(GameAction action)
        {
            mArch.GetSystem<IActionPipelineSystem>().Execute(action);
        }

        private CoreGameEvent FindDamageDealtToAvatar()
        {
            var entries = mArch.GetSystem<IActionPipelineSystem>().EventLog.Entries;
            var avatarUid = mArch.GetModel<BoardModel>().AvatarUid.Value;
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];
                if (entry.Type == CoreEventType.DamageDealt
                    && entry.TargetUid == avatarUid
                    && entry.SourceDefId == "trap.flame")
                {
                    return entry;
                }
            }

            return null;
        }

        private int CountRuleModifiers(string sourceId)
        {
            var modifiers = mArch.GetSystem<IStatSystem>().RuleModifiers.Modifiers;
            var count = 0;
            for (var i = 0; i < modifiers.Count; i++)
            {
                if (string.Equals(modifiers[i].Source.Id, sourceId, System.StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private RuleModifier FindRuleModifier(string sourceId)
        {
            var modifiers = mArch.GetSystem<IStatSystem>().RuleModifiers.Modifiers;
            for (var i = 0; i < modifiers.Count; i++)
            {
                if (string.Equals(modifiers[i].Source.Id, sourceId, System.StringComparison.Ordinal))
                {
                    return modifiers[i];
                }
            }

            return null;
        }
    }
}

using NineGrid.Content;
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
    /// 教学阶段3 行动/移动假人：节奏自毁模板必须带 ADR-0010 requires。
    /// 空 requires 会在 SpawnCard → ApplyContentToCard → Activate 时抛
    /// InvalidOperationException，管线熔断后场上只剩两张提示机关。
    /// </summary>
    public class TutorialDummyRhythmDeathRequiresTests
    {
        private IArchitecture mArch;

        [SetUp]
        public void SetUp()
        {
            EffectTemplateCatalog.Invalidate();
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Interface;
            mArch.GetModel<RunModel>().SetPhase(GamePhase.InteractionLoop);
            var content = mArch.GetSystem<IContentSystem>();
            content.Load(ContentCatalogBootstrap.Load());
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, content.Catalog);
            Assert.IsTrue(content.HasCatalog, "需要真实内容目录");
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void RhythmDeathTemplate_DeclaresOwnerRequires()
        {
            Assert.IsTrue(
                EffectTemplateCatalog.TryGet("tpl.tutorial.dummy.rhythm_death", out var template)
                && template != null,
                "教学假人节奏自毁模板必须存在");
            CollectionAssert.Contains(template.Requires, EffectRequireTokens.HasOwnerEntity);
            CollectionAssert.Contains(template.Requires, EffectRequireTokens.CardZoneTriggerable);
        }

        [Test]
        public void ActionAndMoveDummies_SpawnOntoBoard_WithoutPipelineFault()
        {
            CreateAvatarOnBoard(SlotId.Board(5));
            var pipeline = mArch.GetSystem<IActionPipelineSystem>();
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();

            Assert.DoesNotThrow(
                () => pipeline.Execute(new SpawnCardAction(
                    "monster.tutorial.action_dummy",
                    CardKind.Trap,
                    ZoneId.Board,
                    SlotId.Board(6),
                    1,
                    "test.tutorialPhase3")),
                "行动假人 SpawnCard 不得因效果 requires 缺失熔断");
            Assert.DoesNotThrow(
                () => pipeline.Execute(new SpawnCardAction(
                    "monster.tutorial.move_dummy",
                    CardKind.Trap,
                    ZoneId.Board,
                    SlotId.Board(8),
                    1,
                    "test.tutorialPhase3")),
                "移动假人 SpawnCard 不得因效果 requires 缺失熔断");

            Assert.AreEqual(0, CountPipelineFaults(), "假人入场不得留下 PipelineFault");
            Assert.IsFalse(board.IsEmpty(SlotId.Board(6)), "格6应有行动假人");
            Assert.IsFalse(board.IsEmpty(SlotId.Board(8)), "格8应有移动假人");

            var actionUid = board.GetCardUid(SlotId.Board(6));
            var moveUid = board.GetCardUid(SlotId.Board(8));
            Assert.IsTrue(registry.TryGet(actionUid, out var actionCard) && actionCard != null);
            Assert.IsTrue(registry.TryGet(moveUid, out var moveCard) && moveCard != null);
            Assert.AreEqual("monster.tutorial.action_dummy", actionCard.DefId);
            Assert.AreEqual("monster.tutorial.move_dummy", moveCard.DefId);
            Assert.AreEqual(CardKind.Trap, actionCard.Kind);
            Assert.AreEqual(CardKind.Trap, moveCard.Kind);
            Assert.AreEqual(CardRhythmSource.Action, actionCard.RhythmSource);
            Assert.AreEqual(CardRhythmSource.Move, moveCard.RhythmSource);
        }

        private CardInstance CreateAvatarOnBoard(SlotId slot)
        {
            var avatar = mArch.GetModel<CardRegistry>().Create("avatar.default", CardKind.Avatar);
            avatar.Stats.SetBase(StatId.MaxHp, 20);
            avatar.Stats.SetBase(StatId.Hp, 20);
            avatar.Stats.SetBase(StatId.Attack, 3);
            mArch.GetModel<BoardModel>().SetAvatar(avatar, slot);
            return avatar;
        }

        private int CountPipelineFaults()
        {
            var log = mArch.GetSystem<IActionPipelineSystem>().EventLog;
            var count = 0;
            for (var i = 0; i < log.Entries.Count; i++)
            {
                if (log.Entries[i].Type == CoreEventType.PipelineFaultContained)
                {
                    count++;
                }
            }

            return count;
        }
    }
}

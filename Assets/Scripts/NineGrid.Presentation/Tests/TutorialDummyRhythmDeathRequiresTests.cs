using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NineGrid.Flow.Presentation;
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

        [Test]
        public void TrapOnlyRhythmBoard_HasParticipatingEnemy_ForSchedulerGate()
        {
            CreateAvatarOnBoard(SlotId.Board(5));
            var pipeline = mArch.GetSystem<IActionPipelineSystem>();
            var board = mArch.GetModel<BoardModel>();

            pipeline.Execute(new SpawnCardAction(
                "monster.tutorial.action_dummy",
                CardKind.Trap,
                ZoneId.Board,
                SlotId.Board(6),
                1,
                "test.schedulerGate"));
            pipeline.Execute(new SpawnCardAction(
                "monster.tutorial.move_dummy",
                CardKind.Trap,
                ZoneId.Board,
                SlotId.Board(8),
                1,
                "test.schedulerGate"));

            Assert.IsFalse(board.IsEmpty(SlotId.Board(6)), "格6应有行动假人");
            Assert.IsFalse(board.IsEmpty(SlotId.Board(8)), "格8应有移动假人");
            Assert.IsTrue(
                EnemyActionPhaseScheduler.HasParticipatingEnemy(mArch),
                "纯机关节奏场须开齐射：EnemyActionPhaseScheduler 不得因无 Monster 跳过 Register");
        }

        [Test]
        public void TrapOnlyRhythmBoard_ActionDummySelfDestructsViaEnemyActionPhase()
        {
            CreateAvatarOnBoard(SlotId.Board(5));
            var pipeline = mArch.GetSystem<IActionPipelineSystem>();
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var phase = mArch.GetSystem<IPhaseSystem>();

            pipeline.Execute(new SpawnCardAction(
                "monster.tutorial.action_dummy",
                CardKind.Trap,
                ZoneId.Board,
                SlotId.Board(6),
                1,
                "test.trapOnlySelfDestruct"));

            Assert.IsTrue(
                EnemyActionPhaseScheduler.HasParticipatingEnemy(mArch),
                "门禁打开后才测自毁链");

            var actionUid = board.GetCardUid(SlotId.Board(6));
            Assert.IsTrue(registry.TryGet(actionUid, out var actionCard) && actionCard != null);

            for (var i = 1; i <= 4; i++)
            {
                RunFullEnemyActionPhase(phase);
                Assert.AreEqual(ZoneId.Board, actionCard.Zone.Value);
                Assert.AreEqual(5 - i, actionCard.Counters.Get(CoreCounterKeys.AttackPatternCountdown));
            }

            RunFullEnemyActionPhase(phase);
            Assert.AreEqual(ZoneId.Removed, actionCard.Zone.Value, "纯机关场经齐射后行动假人应自毁");
        }

        [Test]
        public void ActionDummy_TicksDownOnInteractions_AndSelfDestructsAtZero()
        {
            CreateAvatarOnBoard(SlotId.Board(5));
            var pipeline = mArch.GetSystem<IActionPipelineSystem>();
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var phase = mArch.GetSystem<IPhaseSystem>();

            pipeline.Execute(new SpawnCardAction(
                "monster.tutorial.action_dummy",
                CardKind.Trap,
                ZoneId.Board,
                SlotId.Board(6),
                1,
                "test.actionDummyTick"));

            var actionUid = board.GetCardUid(SlotId.Board(6));
            Assert.IsTrue(registry.TryGet(actionUid, out var actionCard) && actionCard != null);
            Assert.AreEqual(5, actionCard.Counters.Get(CoreCounterKeys.AttackPatternCountdown), "初始倒计时应为 5");

            // 推进 4 次互动：每次 -1，卡牌仍在场
            for (var i = 1; i <= 4; i++)
            {
                RunFullEnemyActionPhase(phase);
                Assert.AreEqual(ZoneId.Board, actionCard.Zone.Value, $"第 {i} 次互动后假人应仍在场");
                Assert.AreEqual(5 - i, actionCard.Counters.Get(CoreCounterKeys.AttackPatternCountdown), $"第 {i} 次互动后倒计时应为 {5 - i}");
            }

            // 第 5 次互动：归零并在敌方行动阶段触发节奏自毁
            RunFullEnemyActionPhase(phase);
            Assert.AreEqual(ZoneId.Removed, actionCard.Zone.Value, "第 5 次互动归零后行动假人应已自毁离场 (Removed)");
            Assert.IsTrue(board.IsEmpty(SlotId.Board(6)), "格6应变为空格");
        }

        [Test]
        public void MoveDummy_TicksDownOnBoardMoves_AndSelfDestructsAtZero()
        {
            CreateAvatarOnBoard(SlotId.Board(5));
            var pipeline = mArch.GetSystem<IActionPipelineSystem>();
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var phase = mArch.GetSystem<IPhaseSystem>();

            pipeline.Execute(new SpawnCardAction(
                "monster.tutorial.move_dummy",
                CardKind.Trap,
                ZoneId.Board,
                SlotId.Board(8),
                1,
                "test.moveDummyTick"));

            var moveUid = board.GetCardUid(SlotId.Board(8));
            Assert.IsTrue(registry.TryGet(moveUid, out var moveCard) && moveCard != null);
            Assert.AreEqual(5, moveCard.Counters.Get(CoreCounterKeys.AttackPatternCountdown), "初始移动倒计时应为 5");

            // 顺时针旋转 4 次：每次外圈移动触发 MoveTick -1
            for (var i = 1; i <= 4; i++)
            {
                pipeline.Execute(new RotateBoardClockwiseAction());
                RunFullEnemyActionPhase(phase);
                Assert.AreEqual(ZoneId.Board, moveCard.Zone.Value, $"第 {i} 次移动后假人应仍在场");
                Assert.AreEqual(5 - i, moveCard.Counters.Get(CoreCounterKeys.AttackPatternCountdown), $"第 {i} 次移动后倒计时应为 {5 - i}");
            }

            // 第 5 次移动：归零并在敌方行动阶段触发节奏自毁
            pipeline.Execute(new RotateBoardClockwiseAction());
            Assert.AreEqual(0, moveCard.Counters.Get(CoreCounterKeys.AttackPatternCountdown), "第 5 次移动后倒计时应为 0");
            RunFullEnemyActionPhase(phase);
            Assert.AreEqual(ZoneId.Removed, moveCard.Zone.Value, "第 5 次移动归零后移动假人应已自毁离场 (Removed)");
        }

        private void RunFullEnemyActionPhase(IPhaseSystem phase)
        {
            phase.RegisterEnemyActionPhase();
            var guard = 0;
            while (phase.PendingEnemyActionUids != null
                && phase.PendingEnemyActionUids.Count > 0
                && guard++ < 8)
            {
                phase.ResolveNextEnemyAction();
            }

            phase.ResolveEnemyActionFinale();
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

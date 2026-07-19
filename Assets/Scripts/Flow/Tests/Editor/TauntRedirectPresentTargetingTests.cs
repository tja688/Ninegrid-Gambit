using NineGrid.Cards;
using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NineGrid.Flow.Presentation;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Flow.Tests
{
    /// <summary>
    /// 嘲讽致死后 Present 必须使用 Resolve 批捕获的 combatUid，禁止现场再 Resolve。
    /// </summary>
    public sealed class TauntRedirectPresentTargetingTests
    {
        private static readonly SlotId sTaunterSlot = SlotId.Board(4);
        private static readonly SlotId sOtherMonsterSlot = SlotId.Board(2);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;
        private CoreCommandDispatcher mDispatcher;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, TableNineContentCatalog.CreateDefault());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 13UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
            mDispatcher = new CoreCommandDispatcher(mArch);
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void Decide_CapturedResolvedUid_KeepsTauntRedirectAfterLethalHit()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            SpawnOnBoard("monster.skeleton_taunter", sTaunterSlot);
            SpawnOnBoard("monster.headless_skeleton", sOtherMonsterSlot);

            var board = mArch.GetModel<BoardModel>();
            var avatar = mArch.GetModel<CardRegistry>().Get(board.AvatarUid.Value);
            avatar.Stats.SetBase(StatId.Attack, 99);

            var otherUid = board.GetCardUid(sOtherMonsterSlot);
            var taunterUid = board.GetCardUid(sTaunterSlot);
            Assert.AreEqual(taunterUid, mPhase.ResolvePlayerAttackTargetUid(otherUid));

            var capturedResolvedUid = mPhase.ResolvePlayerAttackTargetUid(otherUid);
            var hit = mPhase.ApplyCombatHit(board.AvatarUid.Value, capturedResolvedUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);
            Assert.AreEqual(ZoneId.Graveyard, mArch.GetModel<CardRegistry>().Get(taunterUid).Zone.Value);

            // 致死后现场 Resolve 会丢嘲讽（本 bug 的触发条件）。
            Assert.AreEqual(
                otherUid,
                mPhase.ResolvePlayerAttackTargetUid(otherUid),
                "致死后重 Resolve 应已失去嘲讽——Present 绝不能依赖此结果");

            DirectorAttackPresentTargeting.Decide(
                otherUid,
                capturedResolvedUid,
                out var combatUid,
                out var useTauntRedirect);
            Assert.IsTrue(useTauntRedirect);
            Assert.AreEqual(taunterUid, combatUid);
        }

        [Test]
        public void AttackIntent_LethalTauntRedirect_ProjectsCapturedResolvedCombatUid()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            SpawnOnBoard("monster.skeleton_taunter", sTaunterSlot);
            SpawnOnBoard("monster.headless_skeleton", sOtherMonsterSlot);

            var board = mArch.GetModel<BoardModel>();
            var avatar = mArch.GetModel<CardRegistry>().Get(board.AvatarUid.Value);
            avatar.Stats.SetBase(StatId.Attack, 99);

            var otherUid = board.GetCardUid(sOtherMonsterSlot);
            var taunterUid = board.GetCardUid(sTaunterSlot);

            var projectedResolvedUid = 0;
            var projectedClickedSlot = 0;
            var hitPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var boardPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
            var factory = new AttackIntentScriptFactory(
                mArch,
                mDispatcher,
                hitPresent,
                boardPresent,
                onHitBatchProjected: (start, slot, resolvedUid, result) =>
                {
                    projectedClickedSlot = slot;
                    projectedResolvedUid = resolvedUid;
                    Assert.IsTrue(result.Accepted);
                    Assert.IsTrue(
                        IntentBatchProjection.ContainsCardKilled(mPipeline, start, taunterUid),
                        "Core 应击杀嘲讽怪");
                });
            var director = new PresentationDirector(factory);

            bool preview;
            Assert.IsTrue(director.TrySubmitIntent(
                new InputIntent(InputIntentKinds.Attack, sOtherMonsterSlot.Index),
                out preview));

            director.Tick(0.016f); // Resolve CombatHit

            Assert.AreEqual(sOtherMonsterSlot.Index, projectedClickedSlot);
            Assert.AreEqual(taunterUid, projectedResolvedUid);

            DirectorAttackPresentTargeting.Decide(
                otherUid,
                projectedResolvedUid,
                out var combatUid,
                out var useTauntRedirect);
            Assert.IsTrue(useTauntRedirect);
            Assert.AreEqual(taunterUid, combatUid);

            // 对照：若 Present 错误地再 Resolve，会得到 otherUid。
            Assert.AreEqual(otherUid, mPhase.ResolvePlayerAttackTargetUid(otherUid));
        }

        [Test]
        public void BoardIntentLegality_AllowsClickOtherWhileTauntActive()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            SpawnOnBoard("monster.skeleton_taunter", sTaunterSlot);
            SpawnOnBoard("monster.headless_skeleton", sOtherMonsterSlot);

            // 空开局可能已因清场进入 Reward；合法性需要可 Attack 的 InteractionLoop。
            mPipeline.Enqueue(new ClearPendingChoicesAction());
            mPipeline.Enqueue(new ChangePhaseAction(GamePhase.InteractionLoop));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            string reason;
            Assert.IsTrue(
                BoardIntentLegality.TryExplainAttack(mArch, sOtherMonsterSlot.Index, out reason),
                reason);
            Assert.AreEqual(
                mArch.GetModel<BoardModel>().GetCardUid(sTaunterSlot),
                mPhase.ResolvePlayerAttackTargetUid(mArch.GetModel<BoardModel>().GetCardUid(sOtherMonsterSlot)),
                "应仍存在嘲讽重定向，但合法性不得拒点");
        }

        private static NodeDeckOptions CreateEmptyEnemyNode()
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            };
        }

        private void SpawnOnBoard(string defId, SlotId slot)
        {
            mPipeline.Enqueue(new SpawnCardAction(defId, CardKind.Monster, ZoneId.Board, slot, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0, "应成功生成 " + defId);
            Assert.AreEqual(
                defId,
                mArch.GetModel<CardRegistry>().Get(mArch.GetModel<BoardModel>().GetCardUid(slot)).DefId);
        }

        private sealed class RecordingPresentChannel : IPresentChannel
        {
            private readonly int mTicksUntilComplete;
            private int mTicks;
            private bool mBegan;

            public RecordingPresentChannel(int ticksUntilComplete)
            {
                mTicksUntilComplete = ticksUntilComplete;
            }

            public int BeginCount { get; private set; }
            public int ActiveBatchId { get; private set; }

            public bool IsComplete
            {
                get { return mBegan && mTicks >= mTicksUntilComplete; }
            }

            public void Begin(int batchId)
            {
                mBegan = true;
                mTicks = 0;
                ActiveBatchId = batchId;
                BeginCount++;
            }

            public void Tick(float deltaTime)
            {
                if (mBegan)
                {
                    mTicks++;
                }
            }
        }
    }
}

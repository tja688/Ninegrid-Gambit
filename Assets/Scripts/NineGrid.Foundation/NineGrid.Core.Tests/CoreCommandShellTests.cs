using System;
using System.Collections.Generic;
using NineGrid.Core.Commands;
using NineGrid.Core.Systems;
using NUnit.Framework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// 命令集壳：只校验 Command 注册表与 Dispatcher 契约，不跑节点/效果集成。
    /// </summary>
    public sealed class CoreCommandShellTests
    {
        private static readonly (Type CommandType, GameCommandKind Kind)[] s_CommandRegistry =
        {
            (typeof(StartNodeCommand), GameCommandKind.StartNode),
            (typeof(AttackCommand), GameCommandKind.Attack),
            (typeof(CombatHitCommand), GameCommandKind.CombatHit),
            (typeof(ResolvePostKillBoardCommand), GameCommandKind.ResolvePostKillBoard),
            (typeof(AdvanceInteractionCountCommand), GameCommandKind.AdvanceInteractionCount),
            (typeof(ResolvePostKillFillCommand), GameCommandKind.ResolvePostKillFill),
            (typeof(ResolvePostKillRotateCommand), GameCommandKind.ResolvePostKillRotate),
            (typeof(ResolveFusionRefillCommand), GameCommandKind.ResolveFusionRefill),
            (typeof(ResolveDrainRefillCommand), GameCommandKind.ResolveDrainRefill),
            (typeof(PickupItemCommand), GameCommandKind.PickupItem),
            (typeof(ClickEmptyCommand), GameCommandKind.ClickEmpty),
            (typeof(UseItemCommand), GameCommandKind.UseItem),
            (typeof(ApplyUseItemCommand), GameCommandKind.ApplyUseItem),
            (typeof(SelectRewardCommand), GameCommandKind.SelectReward),
            (typeof(SkipHelpChoiceCommand), GameCommandKind.SkipHelpChoice),
            (typeof(SelectRoomCommand), GameCommandKind.SelectRoom),
            (typeof(EnterRoomCommand), GameCommandKind.EnterRoom),
            (typeof(PresentationFinishedCommand), GameCommandKind.PresentationFinished),
        };

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void CommandRegistry_CoversEveryGameCommandKind()
        {
            Assert.AreEqual(Enum.GetValues(typeof(GameCommandKind)).Length, s_CommandRegistry.Length);

            for (var i = 0; i < s_CommandRegistry.Length; i++)
            {
                var (commandType, kind) = s_CommandRegistry[i];
                Assert.IsFalse(commandType.IsAbstract);
                Assert.IsFalse(commandType.IsInterface);
                Assert.AreEqual(kind.ToString(), commandType.Name.Replace("Command", string.Empty));
            }
        }

        [Test]
        public void FreshArchitecture_ExposesStartNodeAsOnlyLegalCommand()
        {
            var phase = NineGridArchitecture.Current.GetSystem<IPhaseSystem>();

            CollectionAssert.Contains(phase.LegalCommands, GameCommandKind.StartNode);
            Assert.IsFalse(phase.CanExecute(GameCommandKind.Attack));
            Assert.IsFalse(phase.CanExecute(GameCommandKind.PresentationFinished));
        }

        [Test]
        public void Dispatcher_RejectsIllegalCommand()
        {
            var dispatcher = new CoreCommandDispatcher(NineGridArchitecture.Current);
            var result = dispatcher.Send(new AttackCommand(SlotId.Board(1)));

            Assert.IsFalse(result.Accepted);
        }

        [Test]
        public void Dispatcher_RejectsPresentationFinishedWhenInputUnlocked()
        {
            var dispatcher = new CoreCommandDispatcher(NineGridArchitecture.Current);
            var result = dispatcher.Send(new PresentationFinishedCommand(1));

            Assert.IsFalse(result.Accepted);
            Assert.IsFalse(result.BatchOpened);
        }

        [Test]
        public void PresentationSync_FinishBatch_ClearsActiveNonLockingBatch()
        {
            var sync = NineGridArchitecture.Current.GetSystem<IPresentationSyncSystem>();
            var batch = new PresentationBatch(7, Array.Empty<PresentationInstruction>(), null);

            Assert.IsFalse(batch.RequiresAcknowledgement);
            sync.OpenBatch(batch);
            Assert.AreEqual(7, sync.ActiveBatchId);
            Assert.IsFalse(sync.IsInputLocked);

            var result = sync.FinishBatch(7);
            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(0, sync.ActiveBatchId);
            Assert.IsFalse(sync.IsInputLocked);
        }

        [Test]
        public void PresentationSync_FinishBatch_RejectsWhenNoActiveBatch()
        {
            var sync = NineGridArchitecture.Current.GetSystem<IPresentationSyncSystem>();
            var result = sync.FinishBatch(1);

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(0, sync.ActiveBatchId);
        }

        [Test]
        public void InitialGameFactory_ClearsOrphanPresentationLock_SoStartNodeIsLegal()
        {
            // 上一局导演硬清可能留下未 Ack 的阻塞批；Bootstrap 必须清掉，否则
            // phase=BuildEnemyPool 时 StartNode 仍被 IsInputLocked 拒掉。
            var arch = NineGridArchitecture.Current;
            var sync = arch.GetSystem<IPresentationSyncSystem>();
            var phase = arch.GetSystem<IPhaseSystem>();
            var map = PresentationEventMap.Get(CoreEventType.ItemUsed);
            Assert.IsTrue(map.LocksInput);
            sync.OpenBatch(new PresentationBatch(
                99,
                new[]
                {
                    new PresentationInstruction(new CoreGameEvent(CoreEventType.ItemUsed, 1, "UseItem"), map),
                },
                null));
            Assert.IsTrue(sync.IsInputLocked);
            Assert.IsFalse(phase.CanExecute(GameCommandKind.StartNode));

            InitialGameFactory.Create(arch);

            Assert.IsFalse(sync.IsInputLocked);
            Assert.AreEqual(0, sync.ActiveBatchId);
            Assert.AreEqual(GamePhase.BuildEnemyPool, phase.CurrentPhase);
            Assert.IsTrue(phase.CanExecute(GameCommandKind.StartNode));
            Assert.IsTrue(phase.StartNode(NodeDeckOptions.CreateDefaultBattle()).Accepted);
        }
    }
}

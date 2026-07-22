using System;
using NineGrid.Presentation.Systems;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// #43 批次5：BoardPresent Drain/Shuffle 静态桥已删；盘面 Present 走 IBattleSessionSystem。
    /// </summary>
    public sealed class BoardPresentBridgeHookTests
    {
        [Test]
        public void BoardPresentDrainHook_TypeIsDeleted()
        {
            Assert.IsNull(
                Type.GetType("NineGrid.Cards.BoardPresentDrainHook, NineGrid.Presentation"),
                "BoardPresentDrainHook 应已删除");
        }

        [Test]
        public void BoardPresentShuffleHook_TypeIsDeleted()
        {
            Assert.IsNull(
                Type.GetType("NineGrid.Cards.BoardPresentShuffleHook, NineGrid.Presentation"),
                "BoardPresentShuffleHook 应已删除");
        }

        [Test]
        public void BoardPresentDrainController_TypeIsDeleted()
        {
            Assert.IsNull(
                Type.GetType(
                    "NineGrid.Presentation.Controllers.BoardPresentDrainController, NineGrid.Presentation"),
                "BoardPresentDrainController 应已删除");
        }

        [Test]
        public void BoardPresentShuffleController_TypeIsDeleted()
        {
            Assert.IsNull(
                Type.GetType(
                    "NineGrid.Presentation.Controllers.BoardPresentShuffleController, NineGrid.Presentation"),
                "BoardPresentShuffleController 应已删除");
        }

        [Test]
        public void BattleSessionSystem_ExposesBoardPresentSurface()
        {
            var systemType = typeof(IBattleSessionSystem);
            Assert.IsNotNull(systemType.GetMethod("DrainPostKillBoardAsync"));
            Assert.IsNotNull(systemType.GetMethod("FlushPendingShuffleIntoPresentationAsync"));
        }
    }
}

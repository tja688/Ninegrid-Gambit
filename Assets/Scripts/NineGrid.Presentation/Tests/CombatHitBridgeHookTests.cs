using System;
using NineGrid.Presentation.Systems;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// #43 批次5：CombatHitBridgeHook 已删；交战/结算走 IBattleSessionSystem。
    /// </summary>
    public sealed class CombatHitBridgeHookTests
    {
        [Test]
        public void CombatHitSink_TypeIsDeleted()
        {
            Assert.IsNull(
                Type.GetType("NineGrid.Cards.CombatHitSink, NineGrid.Presentation"),
                "CombatHitSink 应已删除");
        }

        [Test]
        public void CombatHitBridgeHook_TypeIsDeleted()
        {
            Assert.IsNull(
                Type.GetType("NineGrid.Cards.CombatHitBridgeHook, NineGrid.Presentation"),
                "CombatHitBridgeHook 应已删除");
        }

        [Test]
        public void BattleSessionSystem_ExposesCombatHitAndSettlementSurface()
        {
            var systemType = typeof(IBattleSessionSystem);
            Assert.IsNotNull(systemType.GetMethod("ApplyCombatHit"));
            Assert.IsNotNull(systemType.GetMethod("ResolvePostKillBoard"));
            Assert.IsNotNull(systemType.GetMethod("RaiseBattleEnded"));
            Assert.IsNotNull(systemType.GetMethod("TryEnterNodeSettlement"));
        }
    }
}

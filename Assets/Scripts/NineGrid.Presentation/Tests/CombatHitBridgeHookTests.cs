using System;
using NineGrid.Cards;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// #43 批次2：交战业务桥在 CombatHitBridgeHook；CombatHitSink 与 BridgeHook external-hold 已删。
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
        public void CombatHitBridgeHook_ExposesCombatHitBusinessBridge_WithoutExternalHold()
        {
            var hookType = typeof(CombatHitBridgeHook);
            Assert.IsNotNull(hookType.GetField("ApplyCombatHit"));
            Assert.IsNotNull(hookType.GetMethod("RequestCombatHit"));
            Assert.IsNull(
                hookType.GetField("BeginDirectorExternalHold"),
                "external-hold 应已迁出 BridgeHook");
            Assert.IsNull(hookType.GetField("EndDirectorExternalHold"));
            Assert.IsNull(hookType.GetField("ForceEndDirectorExternalHold"));
        }
    }
}

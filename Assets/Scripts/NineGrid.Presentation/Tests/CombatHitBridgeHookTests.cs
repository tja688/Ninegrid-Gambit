using NineGrid.Cards;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// C2：交战业务桥迁至 CombatHitBridgeHook；CombatHitSink 不再暴露业务委托/Request。
    /// </summary>
    public sealed class CombatHitBridgeHookTests
    {
        [Test]
        public void CombatHitSink_NoLongerExposesCombatHitBusinessBridge()
        {
            var sinkType = typeof(CombatHitSink);
            Assert.IsNull(
                sinkType.GetField("ApplyCombatHit"),
                "ApplyCombatHit 应已从 CombatHitSink 删除");
            Assert.IsNull(
                sinkType.GetMethod("RequestCombatHit"),
                "RequestCombatHit 应已从 CombatHitSink 删除");
            Assert.IsNull(
                sinkType.GetField("ResolvePostKillBoard"),
                "ResolvePostKillBoard 应已从 CombatHitSink 删除");
            Assert.IsNull(
                sinkType.GetMethod("RequestPostKillBoard"),
                "RequestPostKillBoard 应已从 CombatHitSink 删除");
            Assert.IsNull(
                sinkType.GetField("SyncCardPresentation"),
                "SyncCardPresentation 应已从 CombatHitSink 删除");
            Assert.IsNull(
                sinkType.GetField("SyncBoardFromCore"),
                "SyncBoardFromCore 应已从 CombatHitSink 删除");
            Assert.IsNull(
                sinkType.GetField("NotifyBattleEnded"),
                "NotifyBattleEnded 应已从 CombatHitSink 删除");
            Assert.IsNull(
                sinkType.GetField("NotifyNodeSettlementReady"),
                "NotifyNodeSettlementReady 应已从 CombatHitSink 删除");
            Assert.IsNull(
                sinkType.GetField("BeginDirectorExternalHold"),
                "BeginDirectorExternalHold 应已从 CombatHitSink 删除");
            Assert.IsNull(
                sinkType.GetField("EndDirectorExternalHold"),
                "EndDirectorExternalHold 应已从 CombatHitSink 删除");
            Assert.IsNull(
                sinkType.GetField("ForceEndDirectorExternalHold"),
                "ForceEndDirectorExternalHold 应已从 CombatHitSink 删除");
        }

        [Test]
        public void CombatHitBridgeHook_ExposesCombatHitBusinessBridge()
        {
            var hookType = typeof(CombatHitBridgeHook);
            Assert.IsNotNull(hookType.GetField("ApplyCombatHit"));
            Assert.IsNotNull(hookType.GetMethod("RequestCombatHit"));
            Assert.IsNotNull(hookType.GetField("BeginDirectorExternalHold"));
        }
    }
}

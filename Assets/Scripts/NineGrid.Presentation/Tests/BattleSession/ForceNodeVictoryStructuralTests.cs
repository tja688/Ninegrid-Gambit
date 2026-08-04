using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.BattleSession
{
    /// <summary>
    /// 护栏：强制胜利不得再 Offer help.choice（ADR-0021 / #83）。
    /// </summary>
    public sealed class ForceNodeVictoryStructuralTests
    {
        [Test]
        public void TryForceNodeVictory_Source_DoesNotOfferHelpChoice()
        {
            var path = Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    "Scripts",
                    "NineGrid.Presentation",
                    "Flow",
                    "BattleSession",
                    "BattleSessionCheat.cs"));
            Assert.IsTrue(File.Exists(path), path);
            var source = File.ReadAllText(path);
            var start = source.IndexOf("TryForceNodeVictory", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0);
            var nextMethod = source.IndexOf("private static void ApplySkillDescription", start, System.StringComparison.Ordinal);
            Assert.Greater(nextMethod, start);
            var body = source.Substring(start, nextMethod - start);
            Assert.IsFalse(
                body.Contains("OfferRewardChoiceAction"),
                "TryForceNodeVictory 不得再 OfferRewardChoice（含通关三选一）");
            Assert.IsFalse(
                body.Contains("\"help.choice\""),
                "TryForceNodeVictory 不得硬编码 help.choice 奖池");
            Assert.IsTrue(
                body.Contains("TryCompleteClearedNode"),
                "TryForceNodeVictory 应走 PhaseSystem.TryCompleteClearedNode");
            Assert.IsTrue(
                body.Contains("ClearResidualCombatFieldViews"),
                "TryForceNodeVictory 须在 Core 清关后收口场上残留战斗卡视图（机关/帮助/漏网怪）");
            Assert.IsTrue(
                body.Contains("TryEnterNodeSettlement"),
                "跳关须走 TryEnterNodeSettlement（卡组残留由 RaiseSettlementReady 清）");
        }

        [Test]
        public void RaiseSettlementReady_Source_ClearsResidualBattleDeckViews()
        {
            var path = Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    "Scripts",
                    "NineGrid.Presentation",
                    "Flow",
                    "BattleSession",
                    "BattleSessionExecutor.cs"));
            Assert.IsTrue(File.Exists(path), path);
            var source = File.ReadAllText(path);
            var start = source.IndexOf("private void RaiseSettlementReady()", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0);
            var clearMethod = source.IndexOf(
                "private void ClearResidualBattleDeckViews()",
                start,
                System.StringComparison.Ordinal);
            Assert.Greater(clearMethod, start, "RaiseSettlementReady 之后须有 ClearResidualBattleDeckViews");
            var raiseBody = source.Substring(start, clearMethod - start);
            Assert.IsTrue(
                raiseBody.Contains("ClearResidualBattleDeckViews()"),
                "清关选房前须同步清掉卡组抽牌堆残留视图，避免选房仍见上局牌");
        }
    }
}

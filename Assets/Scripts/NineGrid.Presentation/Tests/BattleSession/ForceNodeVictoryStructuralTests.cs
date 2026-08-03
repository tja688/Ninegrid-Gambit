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
        }
    }
}

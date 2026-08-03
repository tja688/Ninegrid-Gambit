using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.FlowShell
{
    /// <summary>
    /// 护栏：图标驻留 Select+Enter 后必须接上房内场地板，避免 PlayRoomEventAsync 死代码导致商店空盘困死。
    /// </summary>
    public sealed class InRoomBoardWiringStructuralTests
    {
        private static readonly string OrchestratorPath = Path.GetFullPath(
            Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Flow",
                "GameFlow",
                "GameFlowOrchestrator.cs"));

        [Test]
        public void PlayRoomIconChoice_CallsPresentInRoomSessionAfterEnter()
        {
            var text = File.ReadAllText(OrchestratorPath);
            Assert.IsTrue(
                text.IndexOf("PresentInRoomSessionAfterEnterAsync", StringComparison.Ordinal) >= 0,
                "应存在 PresentInRoomSessionAfterEnterAsync");

            // PlayRoomIconChoiceAsync 体内须调用 PresentInRoomSessionAfterEnterAsync
            var iconMethod = Regex.Match(
                text,
                @"PlayRoomIconChoiceAsync\s*\([^)]*\)\s*\{(?<body>[\s\S]*?)\n        private ",
                RegexOptions.CultureInvariant);
            Assert.IsTrue(iconMethod.Success, "无法定位 PlayRoomIconChoiceAsync 方法体");
            Assert.IsTrue(
                iconMethod.Groups["body"].Value.IndexOf(
                    "PresentInRoomSessionAfterEnterAsync",
                    StringComparison.Ordinal) >= 0,
                "PlayRoomIconChoiceAsync 须在进房会话后调用 PresentInRoomSessionAfterEnterAsync");
        }

        [Test]
        public void PlayRoomIconChoice_WaitsForConsumerBoardSession()
        {
            var text = File.ReadAllText(OrchestratorPath);
            Assert.IsTrue(
                text.IndexOf("IsAwaitingInRoomBoard", StringComparison.Ordinal) >= 0,
                "应存在 IsAwaitingInRoomBoard，避免进店后 WaitUntil 永远等不到 NodeCompleted");
            Assert.IsTrue(
                text.IndexOf("IsConsumerBoardPool", StringComparison.Ordinal) >= 0,
                "房内会话判定应复用 PendingChoiceModel.IsConsumerBoardPool");
        }

        [Test]
        public void DeadPlayRoomEventAsync_IsAbsent()
        {
            var text = File.ReadAllText(OrchestratorPath);
            Assert.IsFalse(
                Regex.IsMatch(text, @"\bPlayRoomEventAsync\s*\("),
                "独立 PlayRoomEventAsync 已并入图标进房路径，不应再保留二次 EnterRoom 死代码");
        }

        [Test]
        public void InRoomBoardSessions_DoNotHoldChoiceOverlayForWait()
        {
            // ADR-0020：房内场地板是受保护场地；整段 ChoiceOverlay 会让 BoardWalk
            // （ProtectedField）ownerMismatch，商店/卡店/特殊房全部点不动。
            var text = File.ReadAllText(OrchestratorPath);
            var methodNames = new[]
            {
                "PresentShopBoardAsync",
                "PresentTavernBoardAsync",
                "PresentRewardBoardAsync",
            };

            for (var i = 0; i < methodNames.Length; i++)
            {
                var name = methodNames[i];
                var match = Regex.Match(
                    text,
                    name + @"\s*\([^)]*\)\s*\{(?<body>[\s\S]*?)\n        (?:private |public |///)",
                    RegexOptions.CultureInvariant);
                Assert.IsTrue(match.Success, "无法定位 " + name + " 方法体");
                var body = match.Groups["body"].Value;
                Assert.IsFalse(
                    body.IndexOf("SetChoiceOverlay(true)", StringComparison.Ordinal) >= 0,
                    name + " 不得整段 SetChoiceOverlay(true)；店内跳格/购买须走 ProtectedField");
            }
        }
    }
}

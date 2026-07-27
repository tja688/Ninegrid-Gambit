using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// #55/#56 结构护栏：卡面数值提交入口与攻击/反击/用道具路径直读捷径。
    /// </summary>
    public sealed class CardFaceBeatStructuralTests
    {
        [Test]
        public void ProductionSources_DoNotContain_StoneLoverCardFaceSync()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "Scripts", "NineGrid.Presentation"));
            var banned = new[]
            {
                "TrySyncStoneLoverCardPresentation",
                "IsStoneLoverArmorLostTrigger",
                "StoneLoverArmorLostCause",
            };
            var offenders = Directory
                .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(path => path.IndexOf("\\Tests\\", StringComparison.OrdinalIgnoreCase) < 0
                               && path.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) < 0)
                .SelectMany(path =>
                {
                    var text = File.ReadAllText(path);
                    return banned
                        .Where(token => text.IndexOf(token, StringComparison.Ordinal) >= 0)
                        .Select(token => path.Substring(Application.dataPath.Length).TrimStart('\\', '/') + " :: " + token);
                })
                .ToArray();

            Assert.IsEmpty(offenders, "观察型提前同步应已删除：\n" + string.Join("\n", offenders));
        }

        [Test]
        public void ProductionSources_HitFrame_DoesNotSyncManagedCardPresentation()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Cards",
                "Battle",
                "FieldBattlePresentationExecutor.cs"));
            var text = File.ReadAllText(path);
            Assert.IsFalse(
                Regex.IsMatch(text, @"ApplyHitFrameVisuals[\s\S]{0,400}SyncManagedCardPresentation"),
                "命中帧不得直读 SyncManagedCardPresentation");
            Assert.AreEqual(
                2,
                Regex.Matches(text, @"BattleBeatHook\.NotifyBeat\(PresentationBeat\.Impact\)").Count,
                "攻击与反击两处命中帧均应报 Impact 锚点");
        }

        [Test]
        public void PresentStep_ReportsSettled_BeforeAcknowledge()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Flow",
                "Presentation",
                "BatchLockstepSteps.cs"));
            var text = File.ReadAllText(path);
            var settled = text.IndexOf(
                "BattleBeatHook.NotifyBeat(PresentationBeat.Settled)",
                StringComparison.Ordinal);
            var ack = text.IndexOf("mGate.TryAcknowledge(mBatchId)", StringComparison.Ordinal);
            Assert.Greater(settled, 0, "PresentStep 应报 Settled");
            Assert.Greater(ack, settled, "Settled 须在 TryAcknowledge 之前");
        }

        [Test]
        public void UseItemPresent_DoesNotCommitAllSpawnedCards_ForNumericAlign()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Flow",
                "BattleSession",
                "BattleSessionExecutor.UseItem.cs"));
            var text = File.ReadAllText(path);
            Assert.IsFalse(
                text.IndexOf("CommitAllSpawnedCards()", StringComparison.Ordinal) >= 0,
                "用道具 Present 不得 CommitAllSpawnedCards 对齐终值");
            Assert.IsTrue(
                text.IndexOf(
                    "RefreshVisualsPreservingCommittedStatsOnAllSpawned()",
                    StringComparison.Ordinal) >= 0,
                "用道具 Present 只刷新已提交投影的视觉");
        }

        [Test]
        public void ExploreAndUseItem_BatchProjection_DoesNotCommitCardFaceStats()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Flow",
                "BattleSession",
                "CoreBatchProjectionCoordinator.cs"));
            var text = File.ReadAllText(path);

            string MethodBody(string methodName)
            {
                var start = text.IndexOf("public void " + methodName + "(", StringComparison.Ordinal);
                Assert.Greater(start, 0, "缺少 " + methodName);
                var brace = text.IndexOf('{', start);
                Assert.Greater(brace, 0);
                var depth = 0;
                for (var i = brace; i < text.Length; i++)
                {
                    if (text[i] == '{')
                    {
                        depth++;
                    }
                    else if (text[i] == '}')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            return text.Substring(brace, i - brace + 1);
                        }
                    }
                }

                Assert.Fail("未闭合 " + methodName);
                return string.Empty;
            }

            var explore = MethodBody("OnExploreBatchProjected");
            var useItem = MethodBody("OnUseItemBatchProjected");
            var banned = new[]
            {
                "CommitAllSpawnedCards",
                "ApplyToManagedCard",
                "SyncManagedCardPresentation",
                "TryRead(",
            };
            foreach (var token in banned)
            {
                Assert.IsFalse(
                    explore.IndexOf(token, StringComparison.Ordinal) >= 0,
                    "探索批次投影不得 " + token + "（解算结束即写卡面）");
                Assert.IsFalse(
                    useItem.IndexOf(token, StringComparison.Ordinal) >= 0,
                    "用道具批次投影不得 " + token + "（解算结束即写卡面）");
            }
        }

        [Test]
        public void JsonPresentation_DoesNotOverwrite_RuntimeStats()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Flow",
                "CoreCardPresentationMapper.cs"));
            var text = File.ReadAllText(path);
            Assert.IsFalse(
                Regex.IsMatch(text, @"dto\.stats\.attack\s*>\s*0"),
                "JSON stats 不得盖写运行时卡面数值");
        }

        [Test]
        public void ApplyToManagedCard_FirstCommit_DoesNotTryRead_CoreStats()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Flow",
                "CoreCardPresentationMapper.cs"));
            var text = File.ReadAllText(path);
            var methodStart = text.IndexOf(
                "public static void ApplyToManagedCard(ManagedCard card",
                StringComparison.Ordinal);
            Assert.Greater(methodStart, 0);
            var brace = text.IndexOf('{', methodStart);
            Assert.Greater(brace, 0);
            var depth = 0;
            var end = -1;
            for (var i = brace; i < text.Length; i++)
            {
                if (text[i] == '{')
                {
                    depth++;
                }
                else if (text[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        end = i;
                        break;
                    }
                }
            }

            Assert.Greater(end, brace);
            var body = text.Substring(brace, end - brace + 1);
            Assert.IsFalse(
                body.IndexOf("TryRead(", StringComparison.Ordinal) >= 0,
                "首次 ApplyToManagedCard 不得 TryRead Core 写数值；生成类指令走 CardFaceStatHandler");
            Assert.IsTrue(
                body.IndexOf("ApplyVisualsByDefId", StringComparison.Ordinal) >= 0,
                "首次应只刷视觉");
        }

        [Test]
        public void CardFaceStatHandler_Consumes_SpawnDealAvatar_Kinds()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Flow",
                "Presentation",
                "CardFaceStatHandler.cs"));
            var text = File.ReadAllText(path);
            Assert.IsTrue(
                text.IndexOf("ApplySpawnFace", StringComparison.Ordinal) >= 0,
                "生成类指令应走 ApplySpawnFace");
            Assert.IsTrue(
                Regex.IsMatch(text, @"SpawnCard[\s\S]{0,120}DealCard[\s\S]{0,120}ShowAvatar"),
                "SpawnCard / DealCard / ShowAvatar 应同一提交出口");
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// #55/#56/#57/#58/#59/#60/#61 结构护栏：卡面数值提交入口、多处理器排期器与攻击/反击/用道具路径直读捷径；
    /// 飘字/FX/金币须经表演锚点装饰处理器，禁投影瞬间 EventLog 旁路；排期器禁 SyncFromCore。
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
            Assert.IsFalse(
                Regex.IsMatch(text, @"ApplyHitFrameVisuals[\s\S]{0,400}SpawnDamagePopups"),
                "命中帧不得另行 SpawnDamagePopups（飘字改由 Impact 装饰处理器消费）");
        }

        [Test]
        public void ProductionSources_DoNotCall_PresentEffectTriggersFromEventLog()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "Scripts", "NineGrid.Presentation"));
            var offenders = Directory
                .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(path => path.IndexOf("\\Tests\\", StringComparison.OrdinalIgnoreCase) < 0
                               && path.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) < 0)
                .Where(path =>
                {
                    var text = File.ReadAllText(path);
                    return text.IndexOf("PresentEffectTriggersFromEventLog", StringComparison.Ordinal) >= 0;
                })
                .Select(path => path.Substring(Application.dataPath.Length).TrimStart('\\', '/'))
                .ToArray();

            Assert.IsEmpty(
                offenders,
                "效果脉冲不得再扫 EventLog 旁路，须经 Impact 装饰处理器：\n" + string.Join("\n", offenders));
        }

        [Test]
        public void ProductionSources_DoNotCall_SpawnDamagePopups()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "Scripts", "NineGrid.Presentation"));
            var offenders = Directory
                .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(path => path.IndexOf("\\Tests\\", StringComparison.OrdinalIgnoreCase) < 0
                               && path.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) < 0)
                .Where(path =>
                {
                    var text = File.ReadAllText(path);
                    return text.IndexOf("SpawnDamagePopups", StringComparison.Ordinal) >= 0;
                })
                .Select(path => path.Substring(Application.dataPath.Length).TrimStart('\\', '/'))
                .ToArray();

            Assert.IsEmpty(
                offenders,
                "伤害飘字不得再经 SpawnDamagePopups 双轨，须经 Impact 装饰处理器：\n" + string.Join("\n", offenders));
        }

        [Test]
        public void CompositionRoot_Registers_DamageAndEffect_DecorativeHandlers()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Setup",
                "PresentationCompositionRoot.cs"));
            var text = File.ReadAllText(path);
            Assert.IsTrue(
                text.IndexOf("new DamageFloaterBeatHandler()", StringComparison.Ordinal) >= 0,
                "组合根须注册飘字装饰处理器");
            Assert.IsTrue(
                text.IndexOf("new EffectTriggerPulseBeatHandler()", StringComparison.Ordinal) >= 0,
                "组合根须注册 FX 脉冲装饰处理器");
            Assert.IsTrue(
                text.IndexOf("new BattleBeatScheduler(", StringComparison.Ordinal) >= 0
                && text.IndexOf("new CardFaceStatHandler()", StringComparison.Ordinal) >= 0
                && text.IndexOf("new DamageFloaterBeatHandler()", StringComparison.Ordinal) >= 0
                && text.IndexOf("new EffectTriggerPulseBeatHandler()", StringComparison.Ordinal) >= 0,
                "装饰处理器须注入排期器且不占主线 ack");
            Assert.IsTrue(
                text.IndexOf("new GoldGainBeatHandler()", StringComparison.Ordinal) >= 0,
                "组合根须注册金币装饰处理器");
            Assert.IsTrue(
                text.IndexOf("new PlayerInfoHudBeatHandler()", StringComparison.Ordinal) >= 0,
                "组合根须注册 Avatar HUD 处理器");
            Assert.IsTrue(
                text.IndexOf("BattleBeatHook.PresentStandalone", StringComparison.Ordinal) >= 0,
                "组合根须接线非锁步 PresentStandalone");
        }

        [Test]
        public void UseItemPresent_ReportsImpact_BeforeLethalVacate()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Flow",
                "BattleSession",
                "BattleSessionExecutor.UseItem.cs"));
            var text = File.ReadAllText(path);
            var impact = text.IndexOf(
                "BattleBeatHook.NotifyBeat(PresentationBeat.Impact)",
                StringComparison.Ordinal);
            var vacate = text.IndexOf("BeginUseItemLethalVictims", StringComparison.Ordinal);
            Assert.Greater(impact, 0, "用道具 Present 须在 Vacate 前报 Impact（飘字定位）");
            Assert.Greater(vacate, impact, "Impact 须在 BeginUseItemLethalVictims 之前");
            Assert.IsFalse(
                text.IndexOf("SpawnDamagePopups", StringComparison.Ordinal) >= 0,
                "用道具 Present 不得 SpawnDamagePopups");
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
            var flush = text.IndexOf("BattleBeatFlush.FlushBeats()", StringComparison.Ordinal);
            var ack = text.IndexOf("mGate.TryAcknowledge(mBatchId)", StringComparison.Ordinal);
            Assert.Greater(flush, 0, "PresentStep 应调用统一 FlushBeats");
            Assert.Greater(ack, flush, "FlushBeats 须在 TryAcknowledge 之前");
        }

        [Test]
        public void ProductionSources_DoNotCall_PresentGoldGainsFromEventLog()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "Scripts", "NineGrid.Presentation"));
            var offenders = Directory
                .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(path => path.IndexOf("\\Tests\\", StringComparison.OrdinalIgnoreCase) < 0
                               && path.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) < 0)
                .Where(path =>
                {
                    var name = Path.GetFileName(path);
                    if (name.Equals("GoldGainPresentationScheduler.cs", StringComparison.Ordinal))
                    {
                        return false;
                    }

                    var text = File.ReadAllText(path);
                    return text.IndexOf("PresentGoldGainsFromEventLog", StringComparison.Ordinal) >= 0;
                })
                .Select(path => path.Substring(Application.dataPath.Length).TrimStart('\\', '/'))
                .ToArray();

            Assert.IsEmpty(
                offenders,
                "金币不得再扫 EventLog 旁路，须经 Settled 装饰处理器 / BattleBeatFlush：\n"
                + string.Join("\n", offenders));
        }

        [Test]
        public void BattleBeatScheduler_DoesNot_SyncFromCore_PlayerInfo()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Flow",
                "Presentation",
                "BattleBeatScheduler.cs"));
            var text = File.ReadAllText(path);
            Assert.IsFalse(
                text.IndexOf("SyncFromCore", StringComparison.Ordinal) >= 0,
                "排期器不得再 SyncFromCore 衔接补丁；Avatar HUD 改由指令处理器驱动");
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
            Assert.IsTrue(
                text.IndexOf("OfferReward", StringComparison.Ordinal) >= 0
                && text.IndexOf("ApplyOfferReward", StringComparison.Ordinal) >= 0,
                "OfferReward 须由 CardFaceStatHandler 消费");
        }

        [Test]
        public void BounceFan_DoesNot_Use_ClearCombatStats_NumericBypass()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Flow",
                "BounceFanChoicePresenter.cs"));
            var text = File.ReadAllText(path);
            Assert.IsFalse(
                text.IndexOf("clearCombatStats: true", StringComparison.Ordinal) >= 0,
                "Bounce 不得再用 clearCombatStats 清战斗数值旁路");
            Assert.IsTrue(
                text.IndexOf("PresentLatestEventOfType", StringComparison.Ordinal) >= 0
                || text.IndexOf("PresentSingleEvent", StringComparison.Ordinal) >= 0
                || text.IndexOf("NotifyPresentStandalone", StringComparison.Ordinal) >= 0,
                "Bounce spawn 后须经排期器提交 OfferReward 投影");
        }

        [Test]
        public void ProductionCommitPresentation_Only_From_Handler_Or_VisualMapper()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "Scripts", "NineGrid.Presentation"));
            var allowed = new[]
            {
                "CardFaceStatHandler.cs",
                "CoreCardPresentationMapper.cs",
            };
            var offenders = Directory
                .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(path => path.IndexOf("\\Tests\\", StringComparison.OrdinalIgnoreCase) < 0
                               && path.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) < 0)
                .Where(path =>
                {
                    var name = Path.GetFileName(path);
                    for (var i = 0; i < allowed.Length; i++)
                    {
                        if (string.Equals(name, allowed[i], StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }
                    }

                    return true;
                })
                .Where(path => File.ReadAllText(path).IndexOf(".CommitPresentation(", StringComparison.Ordinal) >= 0)
                .Select(path => path.Substring(Application.dataPath.Length).TrimStart('\\', '/'))
                .ToArray();

            Assert.IsEmpty(
                offenders,
                "卡面 CommitPresentation 生产调用方只允许 CardFaceStatHandler / CoreCardPresentationMapper：\n"
                + string.Join("\n", offenders));
        }

        [Test]
        public void MarkFieldDead_DoesNot_DirectZero_CardFaceHp()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Cards",
                "CardManagerSingleton.cs"));
            var text = File.ReadAllText(path);
            var start = text.IndexOf("public void MarkFieldDead(ManagedCard card)", StringComparison.Ordinal);
            Assert.Greater(start, 0);
            var brace = text.IndexOf('{', start);
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
                body.IndexOf("Hp = 0", StringComparison.Ordinal) >= 0,
                "MarkFieldDead 不得本地直置零已提交投影 Hp");
            Assert.IsFalse(
                body.IndexOf("SetHealth(0", StringComparison.Ordinal) >= 0,
                "MarkFieldDead 不得底盘 SetHealth(0) 旁路");
            Assert.IsFalse(
                body.IndexOf("ReapplyCommittedPresentation", StringComparison.Ordinal) >= 0,
                "MarkFieldDead 不得借重放投影改血量");
        }

        [Test]
        public void ApplyKill_Uses_InstructionRemainingHp_Not_HardcodedZero()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Flow",
                "Presentation",
                "CardFaceStatHandler.cs"));
            var text = File.ReadAllText(path);
            var start = text.IndexOf("private static void ApplyKill(", StringComparison.Ordinal);
            Assert.Greater(start, 0);
            var brace = text.IndexOf('{', start);
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
            Assert.IsTrue(
                body.IndexOf("RemainingHp", StringComparison.Ordinal) >= 0,
                "KillCard 须取指令 RemainingHp");
            Assert.IsFalse(
                Regex.IsMatch(body, @"hp:\s*0\b"),
                "KillCard 不得硬编码 hp: 0 旁路");
        }

        [Test]
        public void StandardCardView_NumericSetters_AreNotPublic()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Cards",
                "StandardCardView.cs"));
            var text = File.ReadAllText(path);
            var banned = new[]
            {
                "public void SetAttack(",
                "public void SetHealth(",
                "public void SetArmor(",
                "public void AddAttack(",
                "public void AddHealth(",
                "public void AddArmor(",
            };
            foreach (var token in banned)
            {
                Assert.IsFalse(
                    text.IndexOf(token, StringComparison.Ordinal) >= 0,
                    "底盘数值 Setter 不得公开：" + token);
            }
        }

        [Test]
        public void Adr0005_Accepted_And_CrossRefs_0001_0002_0004()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "docs", "adr", "0005-card-face-beat-commit.md"));
            Assert.IsTrue(File.Exists(path), "missing ADR-0005");
            var text = File.ReadAllText(path);
            Assert.IsTrue(
                Regex.IsMatch(text, @"^---\s*\r?\nstatus:\s*accepted\s*\r?\n---", RegexOptions.Multiline),
                "ADR-0005 status 应为 accepted");
            Assert.IsTrue(text.IndexOf("ADR-0001", StringComparison.Ordinal) >= 0, "须交叉引用 ADR-0001");
            Assert.IsTrue(text.IndexOf("ADR-0002", StringComparison.Ordinal) >= 0, "须交叉引用 ADR-0002");
            Assert.IsTrue(text.IndexOf("ADR-0004", StringComparison.Ordinal) >= 0, "须交叉引用 ADR-0004");
            Assert.IsTrue(text.IndexOf("ADR-0007", StringComparison.Ordinal) >= 0, "须交叉引用 ADR-0007（装饰消费者）");
            Assert.IsTrue(text.IndexOf("决策 1", StringComparison.Ordinal) >= 0
                          || text.IndexOf("决策1", StringComparison.Ordinal) >= 0
                          || text.IndexOf("D1", StringComparison.Ordinal) >= 0,
                "须显式记录 #53 决策 1–6");
        }

        [Test]
        public void BattleBeatScheduler_Accepts_Multiple_IBattleBeatHandlers()
        {
            var schedulerPath = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Flow",
                "Presentation",
                "BattleBeatScheduler.cs"));
            var handlerPath = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Flow",
                "Presentation",
                "CardFaceStatHandler.cs"));
            var ifacePath = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Flow",
                "Presentation",
                "IBattleBeatHandler.cs"));
            Assert.IsTrue(File.Exists(ifacePath), "missing IBattleBeatHandler");
            var iface = File.ReadAllText(ifacePath);
            Assert.IsTrue(iface.IndexOf("bool TryApply(", StringComparison.Ordinal) >= 0);
            var handler = File.ReadAllText(handlerPath);
            Assert.IsTrue(
                handler.IndexOf(": IBattleBeatHandler", StringComparison.Ordinal) >= 0,
                "CardFaceStatHandler 须实现 IBattleBeatHandler");
            var scheduler = File.ReadAllText(schedulerPath);
            Assert.IsTrue(
                Regex.IsMatch(scheduler, @"params\s+IBattleBeatHandler\[\]"),
                "排期器须可注入多个 IBattleBeatHandler");
            Assert.IsTrue(
                scheduler.IndexOf("Unconsumed presentation instruction after Settled", StringComparison.Ordinal) >= 0,
                "未消费诊断须使用 presentation instruction 措辞");
        }

        [Test]
        public void Adr0007_Accepted_MultiHandler_And_CrossRefs_0005()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "docs", "adr", "0007-unified-presentation-pipeline.md"));
            Assert.IsTrue(File.Exists(path), "missing ADR-0007");
            var text = File.ReadAllText(path);
            Assert.IsTrue(
                Regex.IsMatch(text, @"^---\s*\r?\nstatus:\s*accepted\s*\r?\n---", RegexOptions.Multiline),
                "ADR-0007 status 应为 accepted");
            Assert.IsTrue(text.IndexOf("IBattleBeatHandler", StringComparison.Ordinal) >= 0);
            Assert.IsTrue(text.IndexOf("表演消费归属", StringComparison.Ordinal) >= 0);
            Assert.IsTrue(text.IndexOf("ADR-0005", StringComparison.Ordinal) >= 0, "须交叉引用 ADR-0005");
            Assert.IsTrue(
                text.IndexOf("不另建第二张", StringComparison.Ordinal) >= 0
                || text.IndexOf("不新建第二张", StringComparison.Ordinal) >= 0
                || text.IndexOf("不另建第二张锚点表", StringComparison.Ordinal) >= 0,
                "须否决第二张锚点表");
        }
    }
}

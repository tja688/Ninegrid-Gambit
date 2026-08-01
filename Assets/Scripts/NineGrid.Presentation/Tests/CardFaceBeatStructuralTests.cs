using System;
using System.Collections.Generic;
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
                text.IndexOf("new CardFaceFlipBeatHandler()", StringComparison.Ordinal) >= 0,
                "组合根须注册翻牌朝向处理器");
            Assert.IsTrue(
                text.IndexOf("new BattleBeatScheduler(", StringComparison.Ordinal) >= 0
                && text.IndexOf("new CardFaceStatHandler()", StringComparison.Ordinal) >= 0
                && text.IndexOf("new CardFaceFlipBeatHandler()", StringComparison.Ordinal) >= 0
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
            Assert.IsTrue(
                text.IndexOf("BattleBeatHook.FlushUpdateFaceUp", StringComparison.Ordinal) >= 0,
                "组合根须接线 FlushUpdateFaceUp（PresentStep 通道前翻牌）");
            Assert.IsTrue(
                text.IndexOf("BattleBeatHook.FlushImpactExcept", StringComparison.Ordinal) >= 0,
                "组合根须接线 FlushImpactExcept（ADR-0018 Vacate 前选择性 Impact）");
            Assert.IsTrue(
                text.IndexOf("FlipPlaybackCoordinator.Reset()", StringComparison.Ordinal) >= 0,
                "组合根拆卸须 Reset FlipPlaybackCoordinator");
        }

        [Test]
        public void Alpha5_CoreFlip_DevKey_PresentsCardFaceChangedViaBeatFlush()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Foundation",
                "NineGrid.DevTest",
                "Cards",
                "GroundFieldManagerDevKeys.cs"));
            Assert.IsTrue(File.Exists(path), "missing " + path);
            var text = File.ReadAllText(path);
            Assert.IsTrue(
                text.IndexOf("FlipCardAction", StringComparison.Ordinal) >= 0,
                "Alpha5 须走 Core FlipCardAction");
            Assert.IsTrue(
                text.IndexOf("BattleBeatFlush.PresentEventLogSlice(arch", StringComparison.Ordinal) >= 0,
                "Alpha5 须经 PresentEventLogSlice 走正式翻牌表现，禁止只改 Core FaceUp");
            Assert.IsTrue(
                text.IndexOf("FlipPlaybackCoordinator.WaitIdleAsync", StringComparison.Ordinal) >= 0,
                "Alpha5 同刷后须 WaitIdleAsync，避免未播完就返回");
            var flip = text.IndexOf("pipeline.Enqueue(new FlipCardAction", StringComparison.Ordinal);
            var present = text.IndexOf("BattleBeatFlush.PresentEventLogSlice(arch", StringComparison.Ordinal);
            var waitIdle = text.IndexOf("FlipPlaybackCoordinator.WaitIdleAsync", StringComparison.Ordinal);
            Assert.Greater(flip, 0);
            Assert.Greater(present, flip, "PresentEventLogSlice 调用须在 Enqueue FlipCardAction 之后");
            Assert.Greater(waitIdle, present, "WaitIdleAsync 须在 PresentEventLogSlice 之后");
        }

        [Test]
        public void UseItemPresent_FlushesNonTriggerImpact_BeforeLethalVacate()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Flow",
                "BattleSession",
                "BattleSessionExecutor.UseItem.cs"));
            var text = File.ReadAllText(path);
            var flushExcept = text.IndexOf(
                "BattleBeatFlush.FlushImpactExcept(PresentationInstructionKind.TriggerEffect)",
                StringComparison.Ordinal);
            var vacate = text.IndexOf("BeginUseItemLethalVictims", StringComparison.Ordinal);
            Assert.Greater(flushExcept, 0, "用道具 Present 须在 Vacate 前冲刷非 TriggerEffect Impact（飘字定位）");
            Assert.Greater(vacate, flushExcept, "FlushImpactExcept 须在 BeginUseItemLethalVictims 之前");
            Assert.IsFalse(
                Regex.IsMatch(
                    text,
                    @"PlayUseItemPresentCoreAsync[\s\S]{0,800}NotifyBeat\(PresentationBeat\.Impact\)"),
                "用道具 Present 不得在 Vacate/Drain 前整批 NotifyBeat(Impact)——会提前消费 TriggerEffect（ADR-0018）");
            Assert.IsFalse(
                text.IndexOf("SpawnDamagePopups", StringComparison.Ordinal) >= 0,
                "用道具 Present 不得 SpawnDamagePopups");
        }

        [Test]
        public void BoardDrain_ReportsImpact_AfterMotion_BeforeNonMotionSteps()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Flow",
                "BattleSession",
                "BoardPresentationPlayer.cs"));
            var text = File.ReadAllText(path);
            var drainCore = text.IndexOf("DrainPostKillBoardCoreAsync", StringComparison.Ordinal);
            var drainSteps = text.IndexOf("DrainBoardStepsAsync(", StringComparison.Ordinal);
            Assert.Greater(drainCore, 0);
            Assert.Greater(drainSteps, drainCore);

            // Core 入口不得在调用 DrainBoardStepsAsync 之前抢跑 Impact。
            var coreSlice = text.Substring(drainCore, drainSteps - drainCore);
            Assert.IsFalse(
                coreSlice.IndexOf(
                    "BattleBeatHook.NotifyBeat(PresentationBeat.Impact)",
                    StringComparison.Ordinal) >= 0,
                "DrainPostKillBoardCoreAsync 不得在运动步前冲刷 Impact（OnSelfMove 观感倒置）");

            var stepsMethod = text.IndexOf(
                "private async UniTask DrainBoardStepsAsync",
                StringComparison.Ordinal);
            Assert.Greater(stepsMethod, 0);
            var stepsEnd = text.IndexOf(
                "private async UniTask DrainDealsAsync",
                stepsMethod,
                StringComparison.Ordinal);
            Assert.Greater(stepsEnd, stepsMethod);
            var stepsBody = text.Substring(stepsMethod, stepsEnd - stepsMethod);
            var impactInSteps = stepsBody.IndexOf(
                "BattleBeatHook.NotifyBeat(PresentationBeat.Impact)",
                StringComparison.Ordinal);
            Assert.Greater(impactInSteps, 0, "DrainBoardStepsAsync 须冲刷 Impact");
            Assert.IsTrue(
                stepsBody.IndexOf("impactFlushed", StringComparison.Ordinal) >= 0,
                "Impact 须门控");
            // 同批 [Deal, Rotate] 时不得在 Deal 前抢跑：仅在 Remove 前或尾部冲刷。
            Assert.IsTrue(
                stepsBody.IndexOf(
                    "step.Kind == BoardPresentationStepKind.Remove",
                    StringComparison.Ordinal) >= 0,
                "Impact 须绑在 Remove 前（或尾部），不可绑在任意非运动步");
            Assert.IsFalse(
                Regex.IsMatch(
                    stepsBody,
                    @"step\.Kind\s*!=\s*BoardPresentationStepKind\.Rotate[\s\S]{0,200}NotifyBeat\(PresentationBeat\.Impact\)"),
                "禁止「首个非 Rotate/Move/Swap 即 Impact」——会把同批 Deal 前置脉冲");
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
            var flushFaceUp = text.IndexOf("BattleBeatFlush.FlushUpdateFaceUp()", StringComparison.Ordinal);
            var begin = text.IndexOf("mChannel.Begin(mBatchId)", StringComparison.Ordinal);
            var flush = text.IndexOf("BattleBeatFlush.FlushBeats()", StringComparison.Ordinal);
            var idleGate = text.IndexOf("FlipPlaybackCoordinator.IsIdle", StringComparison.Ordinal);
            var ack = text.IndexOf("mGate.TryAcknowledge(mBatchId)", StringComparison.Ordinal);
            Assert.Greater(flushFaceUp, 0, "PresentStep 应具备 FlushUpdateFaceUp（默认通道前）");
            Assert.Greater(begin, flushFaceUp, "FlushUpdateFaceUp 调用点须在 channel.Begin 源码之前");
            Assert.Greater(flush, begin, "FlushBeats 须在 channel.Begin 之后");
            Assert.Greater(idleGate, 0, "PresentStep 须门控 FlipPlaybackCoordinator.IsIdle");
            Assert.Greater(ack, flush, "FlushBeats 须在 TryAcknowledge 之前");
            Assert.Greater(ack, idleGate, "Idle 门控须在 TryAcknowledge 之前出现");
            Assert.IsTrue(
                text.IndexOf("mFlushFaceUpBeforeBegin", StringComparison.Ordinal) >= 0,
                "PresentStep 须支持战斗通道延后当批 FaceUp");
        }

        [Test]
        public void CombatPresentSteps_DeferFaceUpUntilAfterChannel()
        {
            var attackPath = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Flow",
                "Presentation",
                "AttackIntentScriptFactory.cs"));
            var enemyPath = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Flow",
                "Presentation",
                "EnemyActionPhaseScheduler.cs"));
            var attack = File.ReadAllText(attackPath);
            var enemy = File.ReadAllText(enemyPath);
            Assert.GreaterOrEqual(
                Regex.Matches(attack, @"flushFaceUpBeforeBegin:\s*false").Count,
                3,
                "攻击/反击 Present 须延后当批 FaceUp（命中后再翻）");
            Assert.GreaterOrEqual(
                Regex.Matches(enemy, @"flushFaceUpBeforeBegin:\s*false").Count,
                1,
                "敌方开火 CounterHit Present 须延后当批 FaceUp");
        }

        [Test]
        public void CardFaceFlipBeatHandler_DoesNot_Forget_PlayFlipAsync()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Flow",
                "Presentation",
                "CardFaceFlipBeatHandler.cs"));
            var text = File.ReadAllText(path);
            Assert.IsFalse(
                Regex.IsMatch(text, @"PlayFlipAsync\s*\([^)]*\)\s*\.Forget\s*\("),
                "Handler 不得对 PlayFlipAsync 直接 Forget，须经 FlipPlaybackCoordinator");
            Assert.IsTrue(
                text.IndexOf("FlipPlaybackCoordinator.Enqueue", StringComparison.Ordinal) >= 0,
                "Handler 须 Enqueue 到 FlipPlaybackCoordinator");
        }

        [Test]
        public void BattleBeatScheduler_Exposes_FlushUpdateFaceUp()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Flow",
                "Presentation",
                "BattleBeatScheduler.cs"));
            var text = File.ReadAllText(path);
            Assert.IsTrue(
                text.IndexOf("public void FlushUpdateFaceUp()", StringComparison.Ordinal) >= 0,
                "排期器须暴露 FlushUpdateFaceUp");
            Assert.IsTrue(
                text.IndexOf("PresentationInstructionKind.UpdateFaceUp", StringComparison.Ordinal) >= 0,
                "FlushUpdateFaceUp 只消费 UpdateFaceUp");
            Assert.IsTrue(
                text.IndexOf("public void FlushImpactExcept(", StringComparison.Ordinal) >= 0,
                "排期器须暴露 FlushImpactExcept（ADR-0018）");
        }

        [Test]
        public void ChoicePresent_DefersTriggerEffect_WhenBoardDrainFollows()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Flow",
                "BattleSession",
                "BattleSessionExecutor.Choice.cs"));
            var text = File.ReadAllText(path);
            var excluding = text.IndexOf(
                "PresentEventLogSliceExcluding",
                StringComparison.Ordinal);
            var drain = text.IndexOf("DrainPostKillBoardAsync", StringComparison.Ordinal);
            var only = text.IndexOf("PresentEventLogSliceOnly", StringComparison.Ordinal);
            Assert.Greater(excluding, 0, "有盘面 Drain 时须 PresentEventLogSliceExcluding(TriggerEffect)");
            Assert.Greater(drain, excluding, "Drain 须在 Excluding Present 之后");
            Assert.Greater(only, drain, "Drain 后须 PresentEventLogSliceOnly(TriggerEffect)");
            Assert.IsTrue(
                text.IndexOf("PresentationInstructionKind.TriggerEffect", StringComparison.Ordinal) >= 0,
                "Choice 延迟认领须点名 TriggerEffect");
        }

        [Test]
        public void ProductionSources_PlayEffectTriggerPulse_IsAllowlisted()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "Scripts", "NineGrid.Presentation"));
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CardEffectTriggerPulseSink.cs",
                "CardEffectManager.cs",
                "CardEffectManagerExtensions.cs",
                // P2：嘲讽交战 UX 电报，产品定性前暂留白名单（非 ADR-0018 强制收口）。
                "CardAttackBasicAdapter.cs",
            };
            var offenders = Directory
                .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(path => path.IndexOf("\\Tests\\", StringComparison.OrdinalIgnoreCase) < 0
                               && path.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) < 0)
                .Where(path =>
                {
                    var name = Path.GetFileName(path);
                    if (allowed.Contains(name))
                    {
                        return false;
                    }

                    var text = File.ReadAllText(path);
                    return text.IndexOf("PlayEffectTriggerPulse(", StringComparison.Ordinal) >= 0;
                })
                .Select(path => path.Substring(Application.dataPath.Length).TrimStart('\\', '/'))
                .ToArray();

            Assert.IsEmpty(
                offenders,
                "PlayEffectTriggerPulse 生产调用须走 Impact 装饰 / 白名单：\n"
                + string.Join("\n", offenders));
        }

        [Test]
        public void HolyDuel_Fallback_EmitsEffectTriggered_NotSilentDealDamage()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Foundation",
                "NineGrid.Core",
                "Systems",
                "PhaseSystem.cs"));
            var text = File.ReadAllText(path);
            var applyMark = text.IndexOf("private void ApplyHolyDuelMark", StringComparison.Ordinal);
            Assert.Greater(applyMark, 0);
            var nextMethod = text.IndexOf(
                "private string FindHolyDuelActivateInstanceId",
                applyMark + 1,
                StringComparison.Ordinal);
            Assert.Greater(nextMethod, applyMark);
            var body = text.Substring(applyMark, nextMethod - applyMark);
            Assert.IsTrue(
                body.IndexOf("EmitEffectTriggeredAction", StringComparison.Ordinal) >= 0,
                "神圣决斗 fallback 须 EmitEffectTriggeredAction（ADR-0018）");
            Assert.IsFalse(
                Regex.IsMatch(
                    body,
                    @"holyDuelInstanceId\s*==\s*null[\s\S]{0,200}pipeline\.Enqueue\(new DealDamageAction"),
                "fallback 不得在无 EffectTriggered 时直接裸 Enqueue DealDamageAction");
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
        public void BounceFan_BuildEntries_DoesNot_ScaleZero_Before_RewardFlush()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Flow",
                "BounceFanChoicePresenter.cs"));
            var text = File.ReadAllText(path);
            Assert.IsTrue(
                text.IndexOf("ApplyVisualsByDefId(managed, choiceKind)", StringComparison.Ordinal) >= 0,
                "须调用 ApplyVisualsByDefId");
            // BuildEntries 末尾不得再置 wrapper scale=0：Begin 随后 PresentLatestEventOfType(RewardOffered)
            // 会二次 Commit；祖先 scale=0 时世界空间锚定会把主图标钉出 Mask。
            // 入场归零只允许出现在 PlayEntryAnimation（entry.Wrapper.localScale）。
            Assert.IsFalse(
                text.IndexOf("wrapper.transform.localScale = Vector3.zero;", StringComparison.Ordinal) >= 0,
                "BuildEntries 内不得 wrapper.transform.localScale = Vector3.zero");
        }

        [Test]
        public void ProductionCommitPresentation_Only_From_Handler_Or_VisualMapper()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "Scripts", "NineGrid.Presentation"));
            var allowed = new[]
            {
                "CardFaceStatHandler.cs",
                "CardFaceFlipBeatHandler.cs",
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
                "卡面 CommitPresentation 生产调用方只允许 CardFaceStatHandler / CardFaceFlipBeatHandler / CoreCardPresentationMapper：\n"
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

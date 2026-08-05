using System.Collections;
using System.Reflection;
using NineGrid.Core;
using NineGrid.Flow;
using NineGrid.Flow.Diagnostics;
using NineGrid.Presentation.Commands;
using NineGrid.Presentation.Systems;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NineGrid.Presentation.Tests.FlowShell
{
    /// <summary>
    /// #125：正式开始使用正式 RunOptions，隔离 QuickTest 镜像。
    /// 契约：正式入口无 QuickTest 标志/作弊/动态装配；\0 仍具镜像与作弊标志。
    /// </summary>
    public sealed class GameFlowRunModeContractTests
    {
        [Test]
        public void FormalOptions_HasNoQuickTestFlags()
        {
            var options = GameFlowRunOptions.CreateFormal();
            Assert.IsNull(options.QuickTest);
            Assert.IsFalse(options.QuickTestMode);
        }

        [Test]
        public void QuickTestOptions_CarriesPayload()
        {
            var qt = new QuickTestRunOptions
            {
                NodeOrder = QuickTestNodeOrderMode.Sequential,
                SkillIds = new[] { "skill.sacrifice" },
            };
            var options = GameFlowRunOptions.CreateQuickTest(qt);
            Assert.IsNotNull(options.QuickTest);
            Assert.IsTrue(options.QuickTestMode);
            Assert.AreSame(qt, options.QuickTest);
        }

        [Test]
        public void QuickTestOptions_DefaultsToFreshPayload()
        {
            var options = GameFlowRunOptions.CreateQuickTest();
            Assert.IsNotNull(options.QuickTest);
            Assert.IsTrue(options.QuickTestMode);
        }

        [Test]
        public void FormalBeginRun_LeavesShellFormal_NoRunTag()
        {
            using (PresentationArchitectureFixture.CreateBare())
            {
                var shell = GameFlowShellSystem.EnsureRegistered();
                shell.Bind(new FakeGameFlowView());
                LogAssert.Expect(LogType.Error, "[GameFlow] 未绑定 IBattleSessionSystem，无法入场。");
                LogAssert.Expect(LogType.Error, "[GameFlow] 局内会话未就绪，终止节点循环。");

                NineGridArchitecture.Interface.SendCommand(
                    new BeginGameFlowRunCommand(GameFlowRunOptions.CreateFormal()));

                Assert.IsFalse(shell.IsQuickTestMode);
                Assert.AreEqual(string.Empty, DiagTraceShared.RunTag);
                Assert.IsEmpty(ReadInternalList(shell, "QuickTestSkillIds"));
                Assert.IsEmpty(ReadInternalList(shell, "QuickTestTrapContentIds"));
            }
        }

        [Test]
        public void FormalBeginRun_DefaultCommand_IsAlsoFormal()
        {
            using (PresentationArchitectureFixture.CreateBare())
            {
                var shell = GameFlowShellSystem.EnsureRegistered();
                shell.Bind(new FakeGameFlowView());
                LogAssert.Expect(LogType.Error, "[GameFlow] 未绑定 IBattleSessionSystem，无法入场。");
                LogAssert.Expect(LogType.Error, "[GameFlow] 局内会话未就绪，终止节点循环。");

                NineGridArchitecture.Interface.SendCommand(new BeginGameFlowRunCommand());

                Assert.IsFalse(shell.IsQuickTestMode);
                Assert.AreEqual(string.Empty, DiagTraceShared.RunTag);
            }
        }

        [Test]
        public void Channel0_RemainsQuickTestMirror_WithCheatFlags()
        {
            using (PresentationArchitectureFixture.CreateBare())
            {
                var shell = GameFlowShellSystem.EnsureRegistered();
                shell.Bind(new FakeGameFlowView());
                LogAssert.Expect(LogType.Error, "[GameFlow] 未绑定 IBattleSessionSystem，无法入场。");
                LogAssert.Expect(LogType.Error, "[GameFlow] 局内会话未就绪，终止节点循环。");

                // \0 = 流程测试：正式节点序 + QuickTest 作弊，但无动态技能/机关。
                Assert.IsTrue(shell.TryBeginQuickTestFromPickerCode(0));

                Assert.IsTrue(shell.IsQuickTestMode);
                Assert.AreEqual(DiagTraceShared.QuickTestRunTag, DiagTraceShared.RunTag);
                Assert.IsEmpty(ReadInternalList(shell, "QuickTestSkillIds"));
                Assert.IsEmpty(ReadInternalList(shell, "QuickTestTrapContentIds"));
                Assert.AreEqual(
                    QuickTestNodeOrderMode.Sequential,
                    ReadInternalField<QuickTestNodeOrderMode>(shell, "mQuickTestNodeOrderMode"),
                    "\0 应保持 Sequential 正式节点序镜像");
                Assert.AreEqual(99, GameFlowShellSystem.QuickTestAvatarHp);
                Assert.AreEqual(5, GameFlowShellSystem.QuickTestAvatarAttack);
            }
        }

        [Test]
        public void GameFlowRunOptions_HasNoVagueTestModeBoolean()
        {
            Assert.IsNull(
                typeof(GameFlowRunOptions).GetField("TestMode"),
                "GameFlowRunOptions.TestMode 已删除：正式/QuickTest 以载荷与工厂区分");
        }

        [Test]
        public void ShellInterface_NoLongerExposesIsTestMode()
        {
            Assert.IsNull(
                typeof(IGameFlowShellSystem).GetProperty("IsTestMode"),
                "IGameFlowShellSystem.IsTestMode 已删除，禁止含糊测试模式布尔量");
        }

        [Test]
        public void BeginGameFlowRunCommand_HasNoBooleanComboConstructor()
        {
            var ctor = typeof(BeginGameFlowRunCommand).GetConstructor(
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(bool), typeof(bool), typeof(QuickTestRunOptions) },
                null);
            Assert.IsNull(
                ctor,
                "BeginGameFlowRunCommand 不再提供 (bool testMode, bool quickTestMode, ...) 组合构造");
        }

        [Test]
        public void Channels1to9_KeepDynamicSkillTrapChannels()
        {
            for (var code = 1; code <= QuickTestDeckCatalog.MaxPickerCode; code++)
            {
                Assert.IsTrue(QuickTestDeckCatalog.TryResolvePickerCode(code, out var preset), "\\" + code);
                Assert.IsTrue(
                    preset.SkillIds.Count > 0 || preset.TrapContentIds.Count > 0,
                    "\\" + code + " 应保留动态技能/机关通道");
            }
        }

        [Test]
        public void Channel0_HasNoDirectedTrapInjection()
        {
            // #135：\0 与正式镜像一致——正式三张常规机关由 Core 装填路径注入，
            // \0 不得再经 QuickTest trapContentIds 额外定向注入。
            Assert.IsTrue(QuickTestDeckCatalog.TryResolvePickerCode(0, out var preset));
            Assert.IsEmpty(preset.TrapContentIds, "\\0 不得携带定向机关注入");
        }

        [Test]
        public void Channels1to9_EachCarryExactlyOneDirectedTrap_AndNeverLeaveTrap()
        {
            // #135：\1–\9 在 Core 三张常规机关之上可额外定向注入恰一张机关；离开机关不得被定向注入。
            for (var code = 1; code <= QuickTestDeckCatalog.MaxPickerCode; code++)
            {
                Assert.IsTrue(QuickTestDeckCatalog.TryResolvePickerCode(code, out var preset), "\\" + code);
                Assert.AreEqual(1, preset.TrapContentIds.Count, "\\" + code + " 定向注入恰一张机关");
                Assert.AreNotEqual(
                    "trap.leave",
                    preset.TrapContentIds[0],
                    "\\" + code + " 不得定向注入离开机关（离开机关只按清关阈值动态洗入）");
            }
        }

        private static ICollection ReadInternalList(GameFlowShellSystem shell, string propertyName)
        {
            var prop = typeof(GameFlowShellSystem).GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(prop, propertyName);
            var value = prop.GetValue(shell);
            if (value == null)
            {
                return System.Array.Empty<object>();
            }

            return (ICollection)value;
        }

        private static T ReadInternalField<T>(GameFlowShellSystem shell, string fieldName)
        {
            var field = typeof(GameFlowShellSystem).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            return (T)field.GetValue(shell);
        }

        private sealed class FakeGameFlowView : IGameFlowView
        {
            public float RoomEventStubSeconds => 0.05f;
            public float VictoryNoticeSeconds => 0.05f;
            public float DefeatNoticeSeconds => 0.05f;
            public string VictoryMessage => "胜利";
            public string DefeatMessage => "失败";

            public void EnsureViewBindings()
            {
            }

            public void ShowNotice(string message)
            {
            }

            public void HideNotice()
            {
            }

            public void ShowMainMenuPanels()
            {
            }

            public void ShowInRunShell(bool inBattle = true)
            {
            }

            public void ShowRewardOverlay()
            {
            }

            public void ShowRoomEventOverlay()
            {
            }

            public void HideAllOverlays()
            {
            }

            public void QuitGame()
            {
            }
        }
    }
}

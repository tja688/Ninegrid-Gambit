using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NineGrid.Cards;
using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Setup;
using NineGrid.Presentation.Systems;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// #43 批次7：合同收缩静态护栏——旧四类名 / Sink / 第二份流程态 / System 不绑具体 View。
    /// </summary>
    public sealed class HostContractStructuralTests
    {
        private static readonly string[] ForbiddenTypeNames =
        {
            "NineGrid.Flow.InBattleManagerSingleton",
            "NineGrid.Cards.GroundFieldManagerSingleton",
            "NineGrid.Cards.FieldBattleManagerSingleton",
            "NineGrid.Flow.MainGameLoopManagerSingleton",
            "NineGrid.Cards.CombatHitSink",
            "NineGrid.Flow.Presentation.DirectorIntentRuntime",
        };

        [Test]
        public void RenamedHosts_ExistUnderContractNames()
        {
            Assert.IsNotNull(typeof(BattleSessionController));
            Assert.IsNotNull(typeof(GroundFieldView));
            Assert.IsNotNull(typeof(FieldBattleView));
            Assert.IsNotNull(typeof(GameFlowController));
            Assert.IsTrue(typeof(IBattleSessionView).IsAssignableFrom(typeof(BattleSessionController)));
            Assert.IsTrue(typeof(IGroundFieldViewBinding).IsAssignableFrom(typeof(GroundFieldView)));
            Assert.IsTrue(typeof(IFieldBattleViewBinding).IsAssignableFrom(typeof(FieldBattleView)));
            Assert.IsTrue(typeof(IGameFlowView).IsAssignableFrom(typeof(GameFlowController)));
        }

        [Test]
        public void ForbiddenLegacyTypes_AreAbsentFromPresentationAssembly()
        {
            var asm = typeof(PresentationCompositionRoot).Assembly;
            for (var i = 0; i < ForbiddenTypeNames.Length; i++)
            {
                var name = ForbiddenTypeNames[i];
                Assert.IsNull(asm.GetType(name), name + " 应已删除/改名");
                Assert.IsNull(Type.GetType(name + ", NineGrid.Presentation"), name + " 应已删除/改名");
            }
        }

        [Test]
        public void GroundAndFieldSystems_DoNotExposeConcreteViewTypes()
        {
            AssertNoConcreteViewInPublicSurface(typeof(IGroundFieldGeometrySystem));
            AssertNoConcreteViewInPublicSurface(typeof(GroundFieldGeometrySystem));
            AssertNoConcreteViewInPublicSurface(typeof(IFieldBattlePresentationSystem));
            AssertNoConcreteViewInPublicSurface(typeof(FieldBattlePresentationSystem));
        }

        [Test]
        public void GameFlowController_HasNoPrivateFlowStateField()
        {
            var fields = typeof(GameFlowController).GetFields(
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            for (var i = 0; i < fields.Length; i++)
            {
                var field = fields[i];
                Assert.AreNotEqual(
                    "_state",
                    field.Name,
                    "GameFlowController 不应再持有第二份 _state；权威在 GameFlowShellSystem");
                Assert.AreNotEqual(
                    typeof(GameFlowShellState),
                    field.FieldType,
                    "GameFlowController 不应再持有可写 GameFlowShellState 字段");
            }
        }

        [Test]
        public void ProductionSources_DoNotLocalNewPresentationDirector_OutsideRuntime()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "Scripts", "NineGrid.Presentation"));
            var offenders = Directory
                .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(path => path.IndexOf("\\Tests\\", StringComparison.OrdinalIgnoreCase) < 0
                               && path.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) < 0)
                .Where(path =>
                {
                    var name = Path.GetFileName(path);
                    return !string.Equals(name, "PresentationRuntimeSystem.cs", StringComparison.OrdinalIgnoreCase);
                })
                .Where(path => Regex.IsMatch(
                    File.ReadAllText(path),
                    @"\bnew\s+PresentationDirector\b"))
                .Select(path => path.Substring(Application.dataPath.Length).TrimStart('\\', '/'))
                .ToArray();

            Assert.IsEmpty(offenders, "生产路径禁止本地 new PresentationDirector：\n" + string.Join("\n", offenders));
        }

        [Test]
        public void ProductionSources_DoNotContainLegacyHostTypeNames()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "Scripts"));
            var banned = new[]
            {
                "InBattleManagerSingleton",
                "GroundFieldManagerSingleton",
                "FieldBattleManagerSingleton",
                "MainGameLoopManagerSingleton",
            };
            var offenders = Directory
                .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(path => path.IndexOf("\\Tests\\", StringComparison.OrdinalIgnoreCase) < 0
                               && path.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) < 0)
                .Where(path =>
                {
                    var text = File.ReadAllText(path);
                    for (var i = 0; i < banned.Length; i++)
                    {
                        if (text.IndexOf(banned[i], StringComparison.Ordinal) >= 0)
                        {
                            return true;
                        }
                    }

                    return false;
                })
                .Select(path => path.Substring(Application.dataPath.Length).TrimStart('\\', '/'))
                .ToArray();

            Assert.IsEmpty(offenders, "生产脚本中仍残留旧四宿主类型名：\n" + string.Join("\n", offenders));
        }

        private static void AssertNoConcreteViewInPublicSurface(Type type)
        {
            var forbidden = new[] { typeof(GroundFieldView), typeof(FieldBattleView) };
            foreach (var member in type.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static))
            {
                Type memberType = null;
                if (member is PropertyInfo property)
                {
                    memberType = property.PropertyType;
                }
                else if (member is MethodInfo method && method.Name.StartsWith("get_", StringComparison.Ordinal))
                {
                    continue;
                }
                else if (member is MethodInfo methodInfo)
                {
                    if (forbidden.Any(f => f == methodInfo.ReturnType))
                    {
                        Assert.Fail(type.Name + "." + methodInfo.Name + " 返回具体 View");
                    }

                    var parameters = methodInfo.GetParameters();
                    for (var i = 0; i < parameters.Length; i++)
                    {
                        if (forbidden.Any(f => f == parameters[i].ParameterType))
                        {
                            Assert.Fail(type.Name + "." + methodInfo.Name + " 参数暴露具体 View");
                        }
                    }

                    continue;
                }
                else if (member is FieldInfo field)
                {
                    memberType = field.FieldType;
                }

                if (memberType != null && forbidden.Any(f => f == memberType))
                {
                    Assert.Fail(type.Name + "." + member.Name + " 暴露具体 View");
                }
            }
        }
    }
}

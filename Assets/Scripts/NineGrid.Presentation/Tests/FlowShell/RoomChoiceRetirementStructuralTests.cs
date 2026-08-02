using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NineGrid.Flow;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.FlowShell
{
    /// <summary>
    /// #90：退役房间二选一浮层与旧属性三选一 UI；SelectorManager 只剩 Bounce。
    /// </summary>
    public sealed class RoomChoiceRetirementStructuralTests
    {
        private static readonly string PresentationRoot = Path.GetFullPath(
            Path.Combine(Application.dataPath, "Scripts", "NineGrid.Presentation"));

        [Test]
        public void RoomChoicePresenter_TypeIsAbsent()
        {
            var asm = typeof(GameFlowController).Assembly;
            Assert.IsNull(
                asm.GetType("NineGrid.Flow.RoomChoicePresenter"),
                "RoomChoicePresenter 应已删除");
        }

        [Test]
        public void ProductionSources_DoNotReferenceRoomChoicePresenter()
        {
            var offenders = EnumerateProductionCs()
                .Where(path => File.ReadAllText(path).IndexOf("RoomChoicePresenter", StringComparison.Ordinal) >= 0)
                .Select(Relativize)
                .ToArray();

            Assert.IsEmpty(offenders, "生产脚本仍引用 RoomChoicePresenter：\n" + string.Join("\n", offenders));
        }

        [Test]
        public void IGameFlowView_HasNoRoomChoiceOverlayApi()
        {
            var banned = new[]
            {
                "ShowRoomChoiceOverlay",
                "BeginRoomChoice",
                "HideRoomChoice",
                "IsRoomChoiceActive",
            };
            var names = typeof(IGameFlowView).GetMembers()
                .Select(m => m.Name)
                .ToHashSet(StringComparer.Ordinal);
            for (var i = 0; i < banned.Length; i++)
            {
                Assert.IsFalse(names.Contains(banned[i]), "IGameFlowView 不应再暴露 " + banned[i]);
            }
        }

        [Test]
        public void SelectorManager_HasNoBeginRoomChoice()
        {
            Assert.IsNull(
                typeof(SelectorManagerSingleton).GetMethod("BeginRoomChoice"),
                "SelectorManagerSingleton 只应保留 Bounce 路由");
        }

        [Test]
        public void ProductionSources_DoNotPresentStatBoostBounceChoice()
        {
            var banned = new[]
            {
                "PresentStatBoostChoiceAsync",
                "StatBoostOptions",
            };
            var offenders = EnumerateProductionCs()
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
                .Select(Relativize)
                .ToArray();

            Assert.IsEmpty(
                offenders,
                "旧属性三选一 Bounce 入口应已退役：\n" + string.Join("\n", offenders));
        }

        [Test]
        public void BounceFanChoicePresenter_StillExistsForChestRelics()
        {
            Assert.IsNotNull(typeof(BounceFanChoicePresenter));
            Assert.IsNotNull(typeof(SelectorManagerSingleton).GetMethod(
                "BeginBounceChoice",
                new[]
                {
                    typeof(System.Collections.Generic.IReadOnlyList<string>),
                    typeof(Action<int, string>),
                    typeof(Action),
                    typeof(bool),
                }));
        }

        [Test]
        public void UiPanelRouter_HasNoRoomChoicePanelApi()
        {
            Assert.IsNull(typeof(UiPanelRouter).GetProperty("RoomChoicePanel"));
            Assert.IsNull(typeof(UiPanelRouter).GetMethod("ShowRoomChoiceOverlay"));
            var text = File.ReadAllText(Path.Combine(PresentationRoot, "Flow", "UiPanelRouter.cs"));
            Assert.IsFalse(
                Regex.IsMatch(text, @"RoomChoisePanel"),
                "UiPanelRouter 不应再接线 RoomChoisePanel");
        }

        private static System.Collections.Generic.IEnumerable<string> EnumerateProductionCs()
        {
            return Directory
                .EnumerateFiles(PresentationRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => path.IndexOf("\\Tests\\", StringComparison.OrdinalIgnoreCase) < 0
                               && path.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) < 0);
        }

        private static string Relativize(string path)
        {
            return path.Substring(Application.dataPath.Length).TrimStart('\\', '/');
        }
    }
}

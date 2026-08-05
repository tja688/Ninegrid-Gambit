using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NineGrid.Flow;
using NineGrid.Flow.BoardBriefTip;
using NineGrid.Presentation.Setup;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// #141 MainScene 卫生护栏：退役绑定 / 失效 Marker / 隐式 Find 不得回流。
    /// 权威装配入口唯一：PresentationSceneRoot + PresentationSceneBindings + PresentationCompositionRoot。
    /// </summary>
    public sealed class MainSceneHygieneStructuralTests
    {
        private const string MainScenePath = "Assets/Scenes/MainScene.unity";
        private const string PresentationRoot = "Assets/Scripts/NineGrid.Presentation";

        [Test]
        public void DescriptionManagerSingleton_TypeIsRemoved()
        {
            var asm = typeof(GameFlowController).Assembly;
            Assert.IsNull(
                asm.GetType("NineGrid.Flow.DescriptionManagerSingleton"),
                "DescriptionManagerSingleton 应已删除（退役动态描述 HUD，见 #141）");
        }

        [Test]
        public void EnabledBuildScenes_DoNotSerialize_DescriptionManagerSingleton()
        {
            var offenders = GetEnabledBuildScenePaths()
                .Where(path => ReadProjectText(path).Contains("DescriptionManagerSingleton"))
                .ToArray();
            Assert.IsEmpty(offenders, "构建场景不得再序列化 DescriptionManagerSingleton：\n" + string.Join("\n", offenders));
        }

        [Test]
        public void MainScene_DoesNotSerialize_LivingUiContentMarker()
        {
            var yaml = ReadProjectText(MainScenePath);
            Assert.IsFalse(
                yaml.Contains("NineGrid.LivingUI::NineGrid.LivingUI.Unity.LivingUiContentMarker"),
                "MainScene 无 LivingUiDirector，不得残留失效 LivingUiContentMarker（静态主菜单；见 #141）");
        }

        [Test]
        public void MainScene_SerializesExactlyOne_PresentationSceneRoot()
        {
            var yaml = ReadProjectText(MainScenePath);
            Assert.AreEqual(
                1,
                Regex.Matches(yaml, "NineGrid.Presentation::NineGrid.Presentation.Setup.PresentationSceneRoot").Count,
                "MainScene 须恰好一个 PresentationSceneRoot（唯一生产装配入口）");
        }

        [Test]
        public void MainScene_BriefTipPanel_SerializesBoardBriefTipPresenter()
        {
            var yaml = ReadProjectText(MainScenePath);
            var block = ExtractBlockContaining(yaml, "NineGrid.Flow.BoardBriefTip.BoardBriefTipPresenter");
            Assert.IsNotNull(block, "MainScene 简要解释文字框应序列化 BoardBriefTipPresenter（场景单真相，不再靠运行时 AddComponent）");
            Assert.IsTrue(
                Regex.IsMatch(block, @"^\s*panelRoot:\s*\{fileID: [1-9]\d*\}", RegexOptions.Multiline),
                "BoardBriefTipPresenter.panelRoot 应显式指向场景面板");
        }

        [Test]
        public void MainScene_CardManagerSingleton_CardRootIsExplicitlyWired()
        {
            var yaml = ReadProjectText(MainScenePath);
            var block = ExtractBlockContaining(yaml, "NineGrid.Presentation::NineGrid.Cards.CardManagerSingleton");
            Assert.IsNotNull(block, "MainScene 应序列化 CardManagerSingleton");
            Assert.IsTrue(
                Regex.IsMatch(block, @"^\s*cardRoot:\s*\{fileID: [1-9]\d*\}", RegexOptions.Multiline),
                "CardManagerSingleton.cardRoot 应显式绑定场景 Cards 节点（#141 不再运行时自造）");
        }

        [Test]
        public void UiPanelRouter_HasNoInGameInfoTextApi()
        {
            Assert.IsNull(typeof(UiPanelRouter).GetProperty("InGameInfoText"));
            Assert.IsNull(typeof(UiPanelRouter).GetMethod("SetInGameInfoTextVisible"));
            var text = ReadProjectText(PresentationRoot + "/Flow/UiPanelRouter.cs");
            Assert.IsFalse(text.Contains("InGameInfoText"), "UiPanelRouter 不应再接线 inGameInfoText（目标对象不存在，见 #141）");
        }

        [Test]
        public void GameFlowController_HasNoRetiredNoticeTextChannel()
        {
            Assert.IsNull(
                typeof(GameFlowController).GetField("noticeText", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic),
                "GameFlowController 不应再持有 noticeText（旧 NoticeText 通道已退役，ADR-0020 / #141）");
        }

        [Test]
        public void PresentationOutputProjector_HasNoUpdateAvatarDebugText()
        {
            Assert.IsNull(
                typeof(PresentationOutputProjector).GetMethod("UpdateAvatarDebugText"),
                "Avatar 调试文本已是 no-op，含隐式 FindFirstObjectByType 的通道应删除（#141）");
        }

        [Test]
        public void PresentationSceneRoot_DoesNotDuplicateBind_GameFlowShellView()
        {
            var text = ReadProjectText(PresentationRoot + "/Setup/PresentationSceneRoot.cs");
            Assert.IsFalse(
                text.Contains("GameFlowShellSystem"),
                "流程壳 View 由 GameFlowController.Awake 自绑定；PresentationSceneRoot 不得重复 Bind（#141 单入口）");
        }

        [Test]
        public void BattleSessionController_NoImplicitSceneLookup()
        {
            var text = ReadProjectText(PresentationRoot + "/Flow/BattleSessionController.cs");
            var banned = new[] { "FindObjectOfType", "FindDeep", "GameObject.Find", "Resources.Find" };
            for (var i = 0; i < banned.Length; i++)
            {
                Assert.IsFalse(text.Contains(banned[i]), "BattleSessionController 不得隐式查找 " + banned[i]);
            }

            Assert.IsNotNull(
                typeof(BattleSessionController).GetMethod("BindSceneHosts"),
                "BattleSessionController 依赖须经 BindSceneHosts 由 PresentationSceneRoot 注入");
        }

        private static string[] GetEnabledBuildScenePaths()
        {
            return EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();
        }

        private static string ExtractBlockContaining(string yaml, string marker)
        {
            var index = yaml.IndexOf(marker, StringComparison.Ordinal);
            if (index < 0)
            {
                return null;
            }

            var start = yaml.LastIndexOf("---", index, StringComparison.Ordinal);
            var end = yaml.IndexOf("\n---", index, StringComparison.Ordinal);
            if (end < 0)
            {
                end = yaml.Length;
            }

            return yaml.Substring(start, end - start);
        }

        private static string ReadProjectText(string relativePath)
        {
            var full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            Assert.IsTrue(File.Exists(full), "missing " + relativePath);
            return File.ReadAllText(full);
        }
    }
}

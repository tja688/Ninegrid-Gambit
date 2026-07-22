using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.BehaviorBaseline
{
    /// <summary>
    /// #43 批次0：四个宿主脚本 GUID 与 PresentationSceneRoot 场景绑定快照。
    /// 后续批次只允许预期字段迁移；GUID 必须随 .meta 保留。
    /// </summary>
    public sealed class SceneBindingBaselineTests
    {
        public const string PresentationSceneRootGuid = "f9ec36670b0774044923439f73976bda";
        public const string InBattleManagerGuid = "a0b2cfd72c60cf34f89ee4d20ba8e695";
        public const string GroundFieldManagerGuid = "dd5fe24c2f9ad124ab1bc2768cd540eb";
        public const string FieldBattleManagerGuid = "09e2feeec00025844b9d28eca71b83de";
        public const string MainGameLoopManagerGuid = "6fbe150bdc09a5c45bbac7c3eaf541b6";

        private static readonly string[] RequiredSceneRootFields =
        {
            "inBattle",
            "mainGameLoop",
            "relicManager",
            "selectorManager",
            "descriptionManager",
            "damageNumberManager",
            "goldGainFxManager",
            "cardManager",
            "cardHand",
            "cardDeck",
            "groundField",
            "fieldBattle",
        };

        [Test]
        public void HostScriptMetas_KeepBaselineGuids()
        {
            AssertGuid(
                "Assets/Scripts/NineGrid.Presentation/Setup/PresentationSceneRoot.cs.meta",
                PresentationSceneRootGuid);
            AssertGuid(
                "Assets/Scripts/NineGrid.Presentation/Flow/InBattleManagerSingleton.cs.meta",
                InBattleManagerGuid);
            AssertGuid(
                "Assets/Scripts/NineGrid.Presentation/Cards/GroundFieldManagerSingleton.cs.meta",
                GroundFieldManagerGuid);
            AssertGuid(
                "Assets/Scripts/NineGrid.Presentation/Cards/FieldBattleManagerSingleton.cs.meta",
                FieldBattleManagerGuid);
            AssertGuid(
                "Assets/Scripts/NineGrid.Presentation/Flow/MainGameLoopManagerSingleton.cs.meta",
                MainGameLoopManagerGuid);
        }

        [TestCase("Assets/Scenes/MainScene.unity")]
        [TestCase("Assets/Scenes/UITestSence.unity")]
        public void BattleScenes_ContainExactlyOneOfEachHostScript(string scenePath)
        {
            var yaml = ReadProjectText(scenePath);
            Assert.AreEqual(1, CountGuidRefs(yaml, PresentationSceneRootGuid), scenePath + " PresentationSceneRoot");
            Assert.AreEqual(1, CountGuidRefs(yaml, InBattleManagerGuid), scenePath + " InBattle");
            Assert.AreEqual(1, CountGuidRefs(yaml, GroundFieldManagerGuid), scenePath + " GroundField");
            Assert.AreEqual(1, CountGuidRefs(yaml, FieldBattleManagerGuid), scenePath + " FieldBattle");
            Assert.AreEqual(1, CountGuidRefs(yaml, MainGameLoopManagerGuid), scenePath + " MainGameLoop");
        }

        [TestCase("Assets/Scenes/MainScene.unity")]
        [TestCase("Assets/Scenes/UITestSence.unity")]
        public void PresentationSceneRoot_BindingFields_MatchBaselineSnapshot(string scenePath)
        {
            var yaml = ReadProjectText(scenePath);
            var block = ExtractSceneRootBlock(yaml);
            Assert.IsNotNull(block, scenePath + " missing PresentationSceneRoot component block");

            for (var i = 0; i < RequiredSceneRootFields.Length; i++)
            {
                var field = RequiredSceneRootFields[i];
                Assert.IsTrue(
                    Regex.IsMatch(block, @"^\s*" + Regex.Escape(field) + @":\s*\{fileID:", RegexOptions.Multiline),
                    scenePath + " missing SceneRoot field " + field);
            }
        }

        private static void AssertGuid(string metaRelativePath, string expectedGuid)
        {
            var text = ReadProjectText(metaRelativePath);
            var match = Regex.Match(text, @"^guid:\s*([0-9a-fA-F]{32})\s*$", RegexOptions.Multiline);
            Assert.IsTrue(match.Success, metaRelativePath + " missing guid");
            Assert.AreEqual(expectedGuid, match.Groups[1].Value, metaRelativePath);
        }

        private static int CountGuidRefs(string yaml, string guid)
        {
            return Regex.Matches(yaml, @"guid:\s*" + Regex.Escape(guid)).Count;
        }

        private static string ExtractSceneRootBlock(string yaml)
        {
            var marker = "guid: " + PresentationSceneRootGuid;
            var index = yaml.IndexOf(marker, System.StringComparison.Ordinal);
            if (index < 0)
            {
                return null;
            }

            var start = yaml.LastIndexOf("---", index, System.StringComparison.Ordinal);
            if (start < 0)
            {
                start = index;
            }

            var end = yaml.IndexOf("\n---", index, System.StringComparison.Ordinal);
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

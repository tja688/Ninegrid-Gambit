using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// #126 构建护栏：Release 不得序列化 #if UNITY_EDITOR || DEVELOPMENT_BUILD 专属
    /// DevTest 组件（Release 下类不存在 → Missing Script）。构建启用场景与预制体只能序列化
    /// DevTestSceneInstaller / DevTestStandardCardInstaller（始终编译的运行时安装宿主），
    /// DevTest 组件一律由宿主运行时 AddComponent。
    /// </summary>
    public sealed class DevTestSceneSerializationStructuralTests
    {
        private const string DevTestFolder = "Assets/Scripts/NineGrid.Foundation/NineGrid.DevTest";

        // 这些脚本类仅 Editor / Development Build 编译；序列化到构建场景或预制体即 Release Missing Script。
        private static readonly string[] DevTestComponentScriptPaths =
        {
            DevTestFolder + "/Cards/CardDeckManagerDevKeys.cs",
            DevTestFolder + "/Cards/CardHandManagerDevKeys.cs",
            DevTestFolder + "/Cards/GroundFieldManagerDevKeys.cs",
            DevTestFolder + "/Cards/StandardCardViewDevKeys.cs",
            DevTestFolder + "/Flow/DamageNumberManagerDevKeys.cs",
            DevTestFolder + "/Flow/GoldGainFxManagerDevKeys.cs",
            DevTestFolder + "/Flow/InBattleManagerDevKeys.cs",
            DevTestFolder + "/Flow/MainGameLoopManagerDevKeys.cs",
            DevTestFolder + "/Flow/QuickTestEntryInputHandler.cs",
            DevTestFolder + "/Flow/SelectorManagerDevKeys.cs",
        };

        private const string DevTestSceneInstallerScriptPath = DevTestFolder + "/Flow/DevTestSceneInstaller.cs";
        private const string DevTestCardInstallerScriptPath = DevTestFolder + "/Cards/DevTestStandardCardInstaller.cs";

        [Test]
        public void EnabledBuildScenesAndPrefabs_DoNotSerialize_DevTestOnlyComponents()
        {
            var devTestGuids = DevTestComponentScriptPaths.Select(ReadScriptGuid).ToArray();
            var offenders = new List<string>();
            var scanned = 0;

            foreach (var assetPath in GetGatedAssetPaths())
            {
                scanned++;
                var yaml = ReadProjectText(assetPath);
                for (var i = 0; i < DevTestComponentScriptPaths.Length; i++)
                {
                    var count = CountGuidRefs(yaml, devTestGuids[i]);
                    if (count > 0)
                    {
                        offenders.Add(assetPath + " :: " + Path.GetFileName(DevTestComponentScriptPaths[i]) + " x" + count);
                    }
                }
            }

            Assert.Greater(scanned, 0, "门禁未扫描到任何场景/预制体");
            Assert.IsEmpty(
                offenders,
                "构建场景与预制体不得序列化 DevTest-only 组件（Release 会产生 Missing Script）。请改为由 DevTest*Installer 运行时安装：\n"
                + string.Join("\n", offenders));
        }

        [Test]
        public void MainScene_SerializesDevTestSceneInstallerHost()
        {
            var scenePaths = GetEnabledBuildScenePaths();
            var mainScene = scenePaths.FirstOrDefault(path => path.EndsWith("MainScene.unity", System.StringComparison.Ordinal));
            Assert.IsNotNull(mainScene, "构建场景应包含 MainScene.unity");

            var yaml = ReadProjectText(mainScene);
            var installerGuid = ReadScriptGuid(DevTestSceneInstallerScriptPath);
            Assert.AreEqual(
                1,
                CountGuidRefs(yaml, installerGuid),
                "MainScene 须恰好序列化一个 DevTestSceneInstaller（Release 空壳 / Dev 运行时安装宿主）");
        }

        [Test]
        public void StandardCardChassisPrefab_SerializesRuntimeInstallerNotDevKeys()
        {
            const string chassis = "Assets/Resources/Prefabs/老Standard Card.prefab";
            var yaml = ReadProjectText(chassis);
            var devKeysGuid = ReadScriptGuid(DevTestFolder + "/Cards/StandardCardViewDevKeys.cs");
            Assert.AreEqual(
                0,
                CountGuidRefs(yaml, devKeysGuid),
                "卡牌底盘预制体不得再序列化 StandardCardViewDevKeys（Release 生成卡牌即 Missing Script）");

            var installerGuid = ReadScriptGuid(DevTestCardInstallerScriptPath);
            Assert.AreEqual(
                1,
                CountGuidRefs(yaml, installerGuid),
                "卡牌底盘预制体须序列化 DevTestStandardCardInstaller 运行时安装宿主");
        }

        private static IEnumerable<string> GetGatedAssetPaths()
        {
            var paths = new List<string>(GetEnabledBuildScenePaths());
            paths.AddRange(Directory
                .EnumerateFiles(
                    Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Assets")),
                    "*.prefab",
                    SearchOption.AllDirectories)
                .Select(full => full.Substring(Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Length).TrimStart('\\', '/')));
            return paths;
        }

        private static IEnumerable<string> GetEnabledBuildScenePaths()
        {
            return EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();
        }

        private static string ReadScriptGuid(string scriptRelativePath)
        {
            var metaPath = scriptRelativePath + ".meta";
            var text = ReadProjectText(metaPath);
            var match = System.Text.RegularExpressions.Regex.Match(text, @"^guid:\s*([0-9a-fA-F]{32})\s*$",
                System.Text.RegularExpressions.RegexOptions.Multiline);
            Assert.IsTrue(match.Success, metaPath + " missing guid");
            return match.Groups[1].Value;
        }

        private static int CountGuidRefs(string yaml, string guid)
        {
            return System.Text.RegularExpressions.Regex.Matches(yaml, @"guid:\s*" + System.Text.RegularExpressions.Regex.Escape(guid)).Count;
        }

        private static string ReadProjectText(string relativePath)
        {
            var full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            Assert.IsTrue(File.Exists(full), "missing " + relativePath);
            return File.ReadAllText(full);
        }
    }
}

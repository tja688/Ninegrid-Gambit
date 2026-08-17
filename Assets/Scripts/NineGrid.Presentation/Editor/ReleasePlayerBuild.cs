using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NineGrid.Presentation.Editor
{
    /// <summary>
    /// Release（非 Development）Player 打包入口（#142 终验）：验证 Release 无 QuickTest 入口、
    /// 无 DevTest Missing Script。镜像 <see cref="DevPlayerBuild"/>，但不带 Development flag——
    /// 因此 #if UNITY_EDITOR || DEVELOPMENT_BUILD 的 DevTest 组件被剥掉，DevTest.dll 近似空壳。
    /// </summary>
    public static class ReleasePlayerBuild
    {
        private const string StatusFile = "Temp/ninegrid_release_player_build_status.json";
        private const string DesktopReleasePlayerFolderName = "game2";
        private const string DesktopCleanSaveReleasePlayerFolderName = "game3";
        private const string DesktopReleasePlayerExeName = "Ninegrid Gambit.exe";

        private static bool sPending;
        private static bool sBuilding;
        private static BuildPlayerOptions sPendingOptions;
        private static string sPendingBuildId;

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
        }

        [MenuItem("NineGrid/Build/Release Windows64 Player")]
        public static void BuildReleaseWindows64FromMenu()
        {
            var result = QueueReleaseWindows64(
                "Builds/ReleaseWin64/NinegridGambit.exe",
                cleanCache: true);
            Debug.Log("[ReleasePlayerBuild] " + result);
        }

        [MenuItem("NineGrid/Build/Release Windows64 Player (Desktop/game2)")]
        public static void BuildReleaseWindows64ToDesktopGame2FromMenu()
        {
            var result = QueueReleaseWindows64(
                GetDesktopGame2OutputPath(),
                cleanCache: true);
            Debug.Log("[ReleasePlayerBuild] " + result);
        }

        [MenuItem("NineGrid/Build/Release Windows64 Player (Desktop/game3 - Clean Save)")]
        public static void BuildReleaseWindows64ToDesktopGame3FromMenu()
        {
            ClearAllSaveDataAndTutorialProfile();
            CleanDesktopGame3OutputFolder();
            var result = QueueReleaseWindows64(
                GetDesktopGame3OutputPath(),
                cleanCache: true);
            Debug.Log("[ReleasePlayerBuild] [game3 Clean Build] " + result);
        }

        [MenuItem("NineGrid/Save/清空本机存档与教学标记 (Reset All Saves & Tutorial)")]
        public static void ClearAllSaveDataFromMenu()
        {
            ClearAllSaveDataAndTutorialProfile();
            EditorUtility.DisplayDialog(
                "存档与教学标记已清空",
                "本机 persistentDataPath 存档目录（NineGridSaves）、教学完成标记及 PlayerPrefs 已全部清空。\n下次开始游戏将重新进入新手教程流程。",
                "确定");
        }

        /// <summary>
        /// 桌面 Release 包输出路径：<c>Desktop/game2/Ninegrid Gambit.exe</c>（非 Development，无 Debug Console / F9 画面实验室 / F12 作弊面板）。
        /// </summary>
        public static string GetDesktopGame2OutputPath()
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            return Path.Combine(desktop, DesktopReleasePlayerFolderName, DesktopReleasePlayerExeName);
        }

        /// <summary>
        /// 桌面纯净存档 Release 包输出路径：<c>Desktop/game3/Ninegrid Gambit.exe</c>（非 Development，打包前已重置本机存档与教学标记）。
        /// </summary>
        public static string GetDesktopGame3OutputPath()
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            return Path.Combine(desktop, DesktopCleanSaveReleasePlayerFolderName, DesktopReleasePlayerExeName);
        }

        /// <summary>
        /// 清理旧的 Desktop/game3 输出目录，确保无残留旧包文件。
        /// </summary>
        public static void CleanDesktopGame3OutputFolder()
        {
            try
            {
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                var game3Dir = Path.Combine(desktop, DesktopCleanSaveReleasePlayerFolderName);
                if (Directory.Exists(game3Dir))
                {
                    Directory.Delete(game3Dir, true);
                    Debug.Log("[ReleasePlayerBuild] 已清理旧的桌面 game3 输出目录：" + game3Dir);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ReleasePlayerBuild] 清理旧桌面 game3 目录时遇到异常（可忽略）：" + ex.Message);
            }
        }

        /// <summary>
        /// 清空本机 persistentDataPath 下所有 NineGridSaves 存档、教学完成标记以及 PlayerPrefs。
        /// </summary>
        public static void ClearAllSaveDataAndTutorialProfile()
        {
            try
            {
                var deletedCount = 0;
                if (Directory.Exists(Application.persistentDataPath))
                {
                    var savesDir = Path.Combine(Application.persistentDataPath, "NineGridSaves");
                    if (Directory.Exists(savesDir))
                    {
                        var files = Directory.GetFiles(savesDir, "*.*", SearchOption.AllDirectories);
                        deletedCount += files.Length;
                        Directory.Delete(savesDir, true);
                    }

                    var es3Files = Directory.GetFiles(Application.persistentDataPath, "*.es3", SearchOption.AllDirectories);
                    foreach (var file in es3Files)
                    {
                        try
                        {
                            File.Delete(file);
                            deletedCount++;
                        }
                        catch
                        {
                        }
                    }
                }

                if (Application.isPlaying)
                {
                    NineGrid.Flow.Tutorial.TutorialProgressStore.ResetCompleted();
                }

                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();

                Debug.Log($"[ReleasePlayerBuild] 本机存档与教学标记已全部清空（清理了 {deletedCount} 个存档文件，已重置 PlayerPrefs 与 TutorialProfile）。");
            }
            catch (Exception ex)
            {
                Debug.LogError("[ReleasePlayerBuild] 清空存档与教学标记时发生异常：" + ex.Message);
            }
        }

        /// <summary>
        /// Queue an async non-Development StandaloneWindows64 build. Poll <see cref="GetStatusJson"/>.
        /// </summary>
        public static string QueueReleaseWindows64(string outputPath, bool cleanCache = true)
        {
            if (sBuilding || sPending)
            {
                return "{\"status\":\"busy\",\"success\":false,\"message\":\"A NineGrid ReleasePlayerBuild is already queued or in progress.\"}";
            }

            EditorUserBuildSettings.development = false;

            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                return "{\"status\":\"error\",\"success\":false,\"message\":\"No enabled scenes in EditorBuildSettings.\"}";
            }

            var fullPath = Path.IsPathRooted(outputPath)
                ? outputPath
                : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), outputPath));

            var parent = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }

            var options = BuildOptions.DetailedBuildReport;
            if (cleanCache)
            {
                options |= BuildOptions.CleanBuildCache;
            }

            var buildId = "releasebuild_" + Guid.NewGuid().ToString("N").Substring(0, 12);
            sPendingOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = fullPath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = options
            };
            sPendingBuildId = buildId;
            sPending = true;

            var optionLabel = "Release|DetailedBuildReport" + (cleanCache ? "|CleanBuildCache" : string.Empty);
            var queued = "{\"status\":\"queued\",\"buildId\":\"" + buildId
                + "\",\"outputPath\":\"" + Escape(fullPath)
                + "\",\"options\":\"" + optionLabel
                + "\",\"message\":\"Poll NineGrid.Presentation.Editor.ReleasePlayerBuild.GetStatusJson()\"}";
            WriteStatus(queued);
            return queued;
        }

        public static string GetStatusJson()
        {
            if (!File.Exists(StatusFile))
            {
                return "{\"status\":\"idle\"}";
            }

            return File.ReadAllText(StatusFile);
        }

        private static void OnUpdate()
        {
            if (!sPending || sBuilding)
            {
                return;
            }

            sPending = false;
            sBuilding = true;
            var opts = sPendingOptions;
            var buildId = sPendingBuildId;

            try
            {
                var startedAt = DateTime.UtcNow;
                WriteStatus("{\"status\":\"building\",\"buildId\":\"" + buildId
                    + "\",\"buildStartedAt\":\"" + startedAt.ToString("o")
                    + "\",\"outputPath\":\"" + Escape(opts.locationPathName)
                    + "\",\"options\":\"" + Escape(opts.options.ToString()) + "\"}");

                var report = BuildPipeline.BuildPlayer(opts);
                var summary = report.summary;
                var success = summary.result == BuildResult.Succeeded;
                WriteStatus("{\"status\":\"completed\",\"buildId\":\"" + buildId
                    + "\",\"result\":\"" + summary.result
                    + "\",\"success\":" + (success ? "true" : "false")
                    + ",\"outputPath\":\"" + Escape(summary.outputPath)
                    + "\",\"totalErrors\":" + summary.totalErrors
                    + ",\"totalWarnings\":" + summary.totalWarnings
                    + ",\"buildTimeMs\":" + ((long)summary.totalTime.TotalMilliseconds)
                    + ",\"options\":\"" + Escape(opts.options.ToString())
                    + "\",\"development\":" + (((opts.options & BuildOptions.Development) != 0) ? "true" : "false")
                    + "}");
            }
            catch (Exception ex)
            {
                WriteStatus("{\"status\":\"completed\",\"buildId\":\"" + buildId
                    + "\",\"result\":\"Failed\",\"success\":false,\"message\":\"" + Escape(ex.Message) + "\"}");
            }
            finally
            {
                sBuilding = false;
            }
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static void WriteStatus(string json)
        {
            try
            {
                var dir = Path.GetDirectoryName(StatusFile);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(StatusFile, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogError("[ReleasePlayerBuild] Failed to write status: " + ex.Message);
            }
        }
    }
}

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

        /// <summary>
        /// 桌面 Release 包输出路径：<c>Desktop/game2/Ninegrid Gambit.exe</c>（非 Development，无 Debug Console / F9 画面实验室 / F12 作弊面板）。
        /// </summary>
        public static string GetDesktopGame2OutputPath()
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            return Path.Combine(desktop, DesktopReleasePlayerFolderName, DesktopReleasePlayerExeName);
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

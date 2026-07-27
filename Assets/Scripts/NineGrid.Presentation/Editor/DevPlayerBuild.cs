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
    /// Development Player 打包入口。绕过 Pipeline <c>build --options</c> 的 string[] 绑定失效
    /// （CLI 传的 Development 到不了服务端，实际只打出 DetailedBuildReport / Release）。
    /// </summary>
    public static class DevPlayerBuild
    {
        private const string StatusFile = "Temp/ninegrid_dev_player_build_status.json";

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

        [MenuItem("NineGrid/Build/Development Windows64 Player")]
        public static void BuildDevelopmentWindows64FromMenu()
        {
            var result = QueueDevelopmentWindows64(
                "Builds/DevWin64/NinegridGambit.exe",
                cleanCache: true);
            Debug.Log("[DevPlayerBuild] " + result);
        }

        /// <summary>
        /// Queue an async Development StandaloneWindows64 build. Poll <see cref="GetStatusJson"/>.
        /// </summary>
        public static string QueueDevelopmentWindows64(string outputPath, bool cleanCache = true)
        {
            if (sBuilding || sPending)
            {
                return "{\"status\":\"busy\",\"success\":false,\"message\":\"A NineGrid DevPlayerBuild is already queued or in progress.\"}";
            }

            EditorUserBuildSettings.development = true;

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

            var options = BuildOptions.Development | BuildOptions.DetailedBuildReport;
            if (cleanCache)
            {
                options |= BuildOptions.CleanBuildCache;
            }

            var buildId = "devbuild_" + Guid.NewGuid().ToString("N").Substring(0, 12);
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

            var optionLabel = "Development|DetailedBuildReport" + (cleanCache ? "|CleanBuildCache" : string.Empty);
            var queued = "{\"status\":\"queued\",\"buildId\":\"" + buildId
                + "\",\"outputPath\":\"" + Escape(fullPath)
                + "\",\"options\":\"" + optionLabel
                + "\",\"message\":\"Poll NineGrid.Presentation.Editor.DevPlayerBuild.GetStatusJson()\"}";
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
                Debug.LogError("[DevPlayerBuild] Failed to write status: " + ex.Message);
            }
        }
    }
}

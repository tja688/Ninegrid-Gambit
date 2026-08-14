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
        private const string DesktopDevPlayerFolderName = "game1";
        private const string DesktopDevPlayerExeName = "Ninegrid Gambit.exe";

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

        [MenuItem("NineGrid/Build/Development Windows64 Player (Desktop/game1)")]
        public static void BuildDevelopmentWindows64ToDesktopGame1FromMenu()
        {
            var result = QueueDevelopmentWindows64(
                GetDesktopGame1OutputPath(),
                cleanCache: true);
            Debug.Log("[DevPlayerBuild] " + result);
        }

        /// <summary>
        /// 桌面 Development 包输出路径：<c>Desktop/game1/Ninegrid Gambit.exe</c>（含 F12 作弊面板）。
        /// </summary>
        public static string GetDesktopGame1OutputPath()
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            return Path.Combine(desktop, DesktopDevPlayerFolderName, DesktopDevPlayerExeName);
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
                if (success)
                {
                    TryWriteGame1TesterKit(opts.locationPathName);
                }

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

        private static void TryWriteGame1TesterKit(string outputExePath)
        {
            if (string.IsNullOrEmpty(outputExePath))
            {
                return;
            }

            var normalized = outputExePath.Replace('\\', '/');
            if (normalized.IndexOf("/" + DesktopDevPlayerFolderName + "/", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return;
            }

            var outputDir = Path.GetDirectoryName(outputExePath);
            if (string.IsNullOrEmpty(outputDir))
            {
                return;
            }

            try
            {
                var readmePath = Path.Combine(outputDir, "请先读-卡死时把这个文件夹发给开发者.txt");
                File.WriteAllText(
                    readmePath,
                    BuildTesterReadmeText(),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

                File.WriteAllText(
                    Path.Combine(outputDir, "启动游戏.bat"),
                    "@echo off\r\n"
                    + "chcp 65001 >nul\r\n"
                    + "cd /d \"%~dp0\"\r\n"
                    + "start \"\" \"%~dp0" + DesktopDevPlayerExeName + "\"\r\n",
                    Encoding.ASCII);

                File.WriteAllText(
                    Path.Combine(outputDir, "启动-关闭鼠标兜底.bat"),
                    "@echo off\r\n"
                    + "chcp 65001 >nul\r\n"
                    + "cd /d \"%~dp0\"\r\n"
                    + "start \"\" \"%~dp0" + DesktopDevPlayerExeName + "\" -ng-no-rawinput\r\n",
                    Encoding.ASCII);

                File.WriteAllText(
                    Path.Combine(outputDir, "收集诊断包.bat"),
                    BuildCollectDiagnosticBat(),
                    Encoding.ASCII);

                Debug.Log("[DevPlayerBuild] 已写入试玩取证说明与启动脚本：" + outputDir);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DevPlayerBuild] 写入试玩取证文件失败: " + ex.Message);
            }
        }

        private static string BuildTesterReadmeText()
        {
            return "【九宫牌局 · 试玩包取证说明】\r\n\r\n"
                + "若出现卡死 / 未响应 / 切屏后无法操作：\r\n"
                + "1. 不要立刻删文件夹；先双击「收集诊断包.bat」生成 zip\r\n"
                + "2. 把整个 game1 文件夹（或 zip）发给开发者\r\n\r\n"
                + "优先查看的目录（与 exe 同级）：\r\n"
                + "  GameLogs\\HangReports\\\r\n"
                + "    - watchdog-alive-*.txt（证明看门狗已启动）\r\n"
                + "    - stall-in-progress.txt（卡死进行中会留下，杀进程也不会丢）\r\n"
                + "    - hang-*.txt / hang-*.dmp（卡死 ≥5 秒）\r\n\r\n"
                + "业务日志：GameLogs\\Logs\\\r\n"
                + "Unity 日志：%USERPROFILE%\\AppData\\LocalLow\\DefaultCompany\\Ninegrid Gambit\\Player.log\r\n\r\n"
                + "复现步骤（请尽量按此操作后收集）：\r\n"
                + "  启动游戏 → Alt-Tab 切到浏览器/其他应用 → 再切回游戏\r\n\r\n"
                + "排除法：双击「启动-关闭鼠标兜底.bat」（带 -ng-no-rawinput）对比是否仍卡死。\r\n"
                + "游戏内 F12 打开作弊面板 →「记录log」可查看取证路径并保存手动快照。\r\n";
        }

        private static string BuildCollectDiagnosticBat()
        {
            return "@echo off\r\n"
                + "chcp 65001 >nul\r\n"
                + "setlocal EnableExtensions\r\n"
                + "cd /d \"%~dp0\"\r\n"
                + "set \"STAMP=%date:~0,4%%date:~5,2%%date:~8,2%-%time:~0,2%%time:~3,2%%time:~6,2%\"\r\n"
                + "set \"STAMP=%STAMP: =0%\"\r\n"
                + "set \"ZIP=%~dp0diagnostic-bundle-%STAMP%.zip\"\r\n"
                + "set \"TEMP_DIR=%TEMP%\\ninegrid-diagnostic-%STAMP%\"\r\n"
                + "mkdir \"%TEMP_DIR%\" 2>nul\r\n"
                + "if exist \"%~dp0GameLogs\" xcopy /E /I /Y \"%~dp0GameLogs\" \"%TEMP_DIR%\\GameLogs\" >nul\r\n"
                + "if exist \"%~dp0ManualBugSnapshots\" xcopy /E /I /Y \"%~dp0ManualBugSnapshots\" \"%TEMP_DIR%\\ManualBugSnapshots\" >nul\r\n"
                + "set \"APPDATA_HANG=%USERPROFILE%\\AppData\\LocalLow\\DefaultCompany\\Ninegrid Gambit\\HangReports\"\r\n"
                + "if exist \"%APPDATA_HANG%\" xcopy /E /I /Y \"%APPDATA_HANG%\" \"%TEMP_DIR%\\HangReports-AppData\" >nul\r\n"
                + "set \"APPDATA_LOG=%USERPROFILE%\\AppData\\LocalLow\\DefaultCompany\\Ninegrid Gambit\"\r\n"
                + "if exist \"%APPDATA_LOG%\\Player.log\" copy /Y \"%APPDATA_LOG%\\Player.log\" \"%TEMP_DIR%\\\" >nul\r\n"
                + "if exist \"%APPDATA_LOG%\\Player-prev.log\" copy /Y \"%APPDATA_LOG%\\Player-prev.log\" \"%TEMP_DIR%\\\" >nul\r\n"
                + "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Compress-Archive -Path '%TEMP_DIR%\\*' -DestinationPath '%ZIP%' -Force\"\r\n"
                + "rmdir /S /Q \"%TEMP_DIR%\" 2>nul\r\n"
                + "echo.\r\n"
                + "echo 已生成: %ZIP%\r\n"
                + "echo 请把该 zip 或整个 game1 文件夹发给开发者。\r\n"
                + "pause\r\n";
        }
    }
}

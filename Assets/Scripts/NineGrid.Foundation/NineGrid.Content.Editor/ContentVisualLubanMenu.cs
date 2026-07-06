using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace NineGrid.Content.Editor
{
    public static class ContentVisualLubanMenu
    {
        public const string OpenEditorMenu = "TableNine/Content/Content Visual Editor";
        public const string RegenerateMenu = "TableNine/Content/Regenerate Luban";
        private const string GenScriptRelative = "Assets/Tools/Luban/gen_table_nine.ps1";

        [MenuItem(OpenEditorMenu)]
        public static void OpenEditor()
        {
            ContentVisualEditorWindow.ShowWindow();
        }

        [MenuItem(RegenerateMenu)]
        public static void RegenerateLuban()
        {
            if (!EditorUtility.DisplayDialog(
                    "Regenerate Luban",
                    "将运行 gen_table_nine.ps1 并刷新 StreamingAssets / Generated 代码。继续？",
                    "运行",
                    "取消"))
            {
                return;
            }

            string error;
            if (!TryRunGenScript(out error))
            {
                EditorUtility.DisplayDialog("Luban 生成失败", error, "确定");
                return;
            }

            AssetDatabase.Refresh();
            Debug.Log("TableNine Luban regeneration completed.");
        }

        public static bool TryRunGenScript(out string error)
        {
            error = null;
            var projectRoot = Directory.GetCurrentDirectory();
            var scriptPath = Path.Combine(projectRoot, GenScriptRelative);
            if (!File.Exists(scriptPath))
            {
                error = "找不到脚本：" + scriptPath;
                return false;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + scriptPath + "\"",
                WorkingDirectory = Path.GetDirectoryName(scriptPath),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            try
            {
                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        error = "无法启动 PowerShell 进程。";
                        return false;
                    }

                    var stdout = process.StandardOutput.ReadToEnd();
                    var stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        error = string.IsNullOrEmpty(stderr) ? stdout : stderr;
                        if (string.IsNullOrEmpty(error))
                        {
                            error = "Luban 退出码：" + process.ExitCode;
                        }

                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }

            return true;
        }
    }
}

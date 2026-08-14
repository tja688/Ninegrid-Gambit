using System.IO;
using UnityEngine;

namespace NineGrid.Presentation.Platform
{
    /// <summary>
    /// Win Player 卡死/崩溃取证目录解析。HangReports 双写：exe 旁 GameLogs + persistentDataPath。
    /// </summary>
    public static class WindowsHangReportPaths
    {
        public static string ResolvePersistentReportsDir()
        {
            return Path.Combine(Application.persistentDataPath, WindowsHangWatchdogInfo.ReportFolderName);
        }

        public static string ResolvePortableReportsDir()
        {
            try
            {
                var dataParent = Directory.GetParent(Application.dataPath);
                if (dataParent != null)
                {
                    return Path.Combine(
                        dataParent.FullName, "GameLogs", WindowsHangWatchdogInfo.ReportFolderName);
                }
            }
            catch
            {
                // 回退 persistentDataPath。
            }

            return ResolvePersistentReportsDir();
        }

        public static string ResolvePortableGameLogsDir()
        {
            try
            {
                var dataParent = Directory.GetParent(Application.dataPath);
                if (dataParent != null)
                {
                    return Path.Combine(dataParent.FullName, "GameLogs");
                }
            }
            catch
            {
                // 回退 persistentDataPath。
            }

            return Application.persistentDataPath;
        }

        /// <summary>供 F12 面板与试玩说明展示的只读路径摘要。</summary>
        public static string FormatDiagnosticPathSummary()
        {
            return "卡死取证（优先打包发给开发者）："
                + "\nGameLogs: " + ResolvePortableGameLogsDir()
                + "\nHangReports: " + ResolvePortableReportsDir()
                + "\n备份 HangReports: " + ResolvePersistentReportsDir();
        }
    }
}

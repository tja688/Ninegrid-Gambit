using UnityEngine;

namespace NineGrid.Presentation.Platform
{
    /// <summary>
    /// 标记类型：Windows Player 卡死看门狗见条件编译实现。
    /// 主线程每帧喂心跳；后台线程发现心跳停滞（窗口「未响应」级卡死）时，
    /// 向 persistentDataPath/HangReports 落文本报告 + minidump（含全线程原生栈），
    /// 短暂停滞（>2s 后恢复）记入 stalls 流水账。用于外部打包版无 Editor 取证。
    /// </summary>
    public static class WindowsHangWatchdogInfo
    {
        public const string ReportFolderName = "HangReports";
        public const string DisableArg = "-ng-no-watchdog";
        public const string DumpSecondsArgPrefix = "-ng-hang-dump-seconds=";
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [DefaultExecutionOrder(-990)]
    public sealed class WindowsHangWatchdog : MonoBehaviour
    {
        private const int PollIntervalMs = 500;
        private const int StallNoteThresholdMs = 2000;
        private const int DefaultDumpThresholdSeconds = 12;
        private const int MaxDumpsPerSession = 2;
        private const int MaxKeptDumpFiles = 3;
        private const int MaxKeptTextFiles = 12;

        private static long sLastBeatTimestamp;
        private static volatile int sLastFrame;
        private static volatile string sSceneName = string.Empty;
        private static volatile bool sHasFocus = true;
        private static volatile bool sQuitting;

        private System.Threading.Thread mThread;
        private string mReportDir;
        private string mSessionStamp;
        private string mSystemSummary = string.Empty;
        private long mStartTimestamp;
        private int mDumpThresholdMs = DefaultDumpThresholdSeconds * 1000;
        private bool mRunInBackground;
        private uint mProcessId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (HasCommandLineArg(WindowsHangWatchdogInfo.DisableArg))
            {
                Debug.Log("[WindowsHangWatchdog] 已按命令行参数禁用。");
                return;
            }

            var go = new GameObject(nameof(WindowsHangWatchdog));
            DontDestroyOnLoad(go);
            go.AddComponent<WindowsHangWatchdog>();
        }

        private static bool HasCommandLineArg(string arg)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], arg, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void Awake()
        {
            // 主线程缓存所有 Unity / 系统信息；看门狗线程绝不触碰 Unity API。
            mReportDir = System.IO.Path.Combine(
                Application.persistentDataPath, WindowsHangWatchdogInfo.ReportFolderName);
            mSessionStamp = System.DateTime.Now.ToString(
                "yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            mRunInBackground = Application.runInBackground;
            mProcessId = GetCurrentProcessId();
            mStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            mSystemSummary =
                "app=" + Application.productName + " " + Application.version
                + " unity=" + Application.unityVersion
                + (Debug.isDebugBuild ? " (Development Build)" : string.Empty)
                + "\nos=" + SystemInfo.operatingSystem
                + "\ncpu=" + SystemInfo.processorType
                + " ram=" + SystemInfo.systemMemorySize + "MB"
                + "\ngpu=" + SystemInfo.graphicsDeviceName
                + " api=" + SystemInfo.graphicsDeviceType;

            ParseDumpThresholdOverride();

            try
            {
                System.IO.Directory.CreateDirectory(mReportDir);
                PruneOldReports();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[WindowsHangWatchdog] 报告目录初始化失败: " + ex.Message);
            }

            // 预载 dbghelp，避免卡死时在看门狗线程首次触发 DllImport 解析撞 loader lock。
            LoadLibrary("dbghelp.dll");

            Beat();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            sSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            mThread = new System.Threading.Thread(WatchLoop)
            {
                IsBackground = true,
                Name = "NineGrid.HangWatchdog",
                Priority = System.Threading.ThreadPriority.BelowNormal,
            };
            mThread.Start();
            Debug.Log(
                "[WindowsHangWatchdog] 已启动：主线程停滞 >" + (mDumpThresholdMs / 1000)
                + "s 将写报告与 minidump 到 " + mReportDir);
        }

        private void ParseDumpThresholdOverride()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
            {
                if (!args[i].StartsWith(
                        WindowsHangWatchdogInfo.DumpSecondsArgPrefix,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = args[i].Substring(WindowsHangWatchdogInfo.DumpSecondsArgPrefix.Length);
                if (int.TryParse(value, out var seconds) && seconds >= 5)
                {
                    mDumpThresholdMs = seconds * 1000;
                }
            }
        }

        private static void OnSceneLoaded(
            UnityEngine.SceneManagement.Scene scene,
            UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            sSceneName = scene.name;
        }

        private void Update()
        {
            Beat();
        }

        private static void Beat()
        {
            System.Threading.Interlocked.Exchange(
                ref sLastBeatTimestamp, System.Diagnostics.Stopwatch.GetTimestamp());
            sLastFrame = Time.frameCount;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            sHasFocus = hasFocus;
            Beat();
        }

        private void OnApplicationPause(bool paused)
        {
            Beat();
        }

        private void OnApplicationQuit()
        {
            sQuitting = true;
        }

        private void OnDestroy()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void WatchLoop()
        {
            var inStall = false;
            var dumpedThisStall = false;
            var dumpsWritten = 0;
            long stallMaxMs = 0;
            var stallFrame = 0;
            var stallScene = string.Empty;

            while (!sQuitting)
            {
                System.Threading.Thread.Sleep(PollIntervalMs);
                if (sQuitting)
                {
                    return;
                }

                if (System.Diagnostics.Debugger.IsAttached)
                {
                    inStall = false;
                    continue;
                }

                // 主循环合法暂停（失焦且不后台运行）不算卡死。
                if (!sHasFocus && !mRunInBackground)
                {
                    inStall = false;
                    continue;
                }

                var last = System.Threading.Interlocked.Read(ref sLastBeatTimestamp);
                var elapsedMs = TimestampToMs(System.Diagnostics.Stopwatch.GetTimestamp() - last);

                if (elapsedMs >= StallNoteThresholdMs)
                {
                    if (!inStall)
                    {
                        inStall = true;
                        dumpedThisStall = false;
                        stallMaxMs = elapsedMs;
                        stallFrame = sLastFrame;
                        stallScene = sSceneName;
                    }
                    else if (elapsedMs > stallMaxMs)
                    {
                        stallMaxMs = elapsedMs;
                    }

                    if (!dumpedThisStall && elapsedMs >= mDumpThresholdMs)
                    {
                        dumpedThisStall = true;
                        if (dumpsWritten < MaxDumpsPerSession)
                        {
                            dumpsWritten++;
                            TryWriteHangReportAndDump(elapsedMs, stallFrame, stallScene, dumpsWritten);
                        }
                    }

                    continue;
                }

                if (inStall)
                {
                    inStall = false;
                    TryAppendStallLog(stallMaxMs, stallFrame, stallScene, dumpedThisStall);
                }
            }
        }

        private static long TimestampToMs(long timestampDelta)
        {
            return timestampDelta * 1000 / System.Diagnostics.Stopwatch.Frequency;
        }

        private void TryWriteHangReportAndDump(long elapsedMs, int frame, string scene, int dumpIndex)
        {
            var baseName = "hang-" + mSessionStamp + "-" + dumpIndex;
            var reportPath = System.IO.Path.Combine(mReportDir, baseName + ".txt");
            var dumpPath = System.IO.Path.Combine(mReportDir, baseName + ".dmp");

            // 先落文本再尝试 dump：即使 MiniDumpWriteDump 本身出问题，报告也已保住。
            try
            {
                var uptimeSeconds = TimestampToMs(
                    System.Diagnostics.Stopwatch.GetTimestamp() - mStartTimestamp) / 1000;
                var report = new System.Text.StringBuilder();
                report.AppendLine("[NineGrid HangWatchdog] 检测到主线程卡死");
                report.AppendLine("time=" + System.DateTime.Now.ToString(
                    "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture));
                report.AppendLine("mainThreadStalledMs>=" + elapsedMs);
                report.AppendLine("lastFrame=" + frame + " scene=" + scene);
                report.AppendLine("uptimeSeconds=" + uptimeSeconds);
                report.AppendLine(mSystemSummary);
                report.AppendLine("dump=" + dumpPath);
                report.AppendLine("Player.log 同目录上级：%USERPROFILE%\\AppData\\LocalLow\\"
                    + Application.companyName + "\\" + Application.productName);
                report.AppendLine("dump 可用 Visual Studio / WinDbg 打开，看 Main Thread 卡在哪个调用栈。");
                System.IO.File.WriteAllText(reportPath, report.ToString());
            }
            catch
            {
                // 报告失败不阻止 dump。
            }

            var dumpOk = false;
            try
            {
                dumpOk = WriteMiniDump(dumpPath);
            }
            catch
            {
                dumpOk = false;
            }

            try
            {
                System.IO.File.AppendAllText(
                    reportPath,
                    "dumpResult=" + (dumpOk ? "ok" : "failed") + System.Environment.NewLine);
            }
            catch
            {
                // 忽略：取证尽力而为。
            }
        }

        private bool WriteMiniDump(string dumpPath)
        {
            // 紧凑取证 dump：全线程栈 + 线程信息 + 句柄表（查死锁链）+ 内存布局，
            // 不含全量堆内存，体积可控（几 MB ~ 几十 MB），玩家可直接回传。
            const uint MiniDumpWithDataSegs = 0x00000001;
            const uint MiniDumpWithHandleData = 0x00000004;
            const uint MiniDumpWithUnloadedModules = 0x00000020;
            const uint MiniDumpWithIndirectlyReferencedMemory = 0x00000040;
            const uint MiniDumpWithProcessThreadData = 0x00000100;
            const uint MiniDumpWithFullMemoryInfo = 0x00000800;
            const uint MiniDumpWithThreadInfo = 0x00001000;
            const uint MiniDumpIgnoreInaccessibleMemory = 0x00020000;

            const uint dumpType =
                MiniDumpWithDataSegs
                | MiniDumpWithHandleData
                | MiniDumpWithUnloadedModules
                | MiniDumpWithIndirectlyReferencedMemory
                | MiniDumpWithProcessThreadData
                | MiniDumpWithFullMemoryInfo
                | MiniDumpWithThreadInfo
                | MiniDumpIgnoreInaccessibleMemory;

            using (var stream = new System.IO.FileStream(
                dumpPath,
                System.IO.FileMode.Create,
                System.IO.FileAccess.ReadWrite,
                System.IO.FileShare.None))
            {
                return MiniDumpWriteDump(
                    GetCurrentProcess(),
                    mProcessId,
                    stream.SafeFileHandle,
                    dumpType,
                    System.IntPtr.Zero,
                    System.IntPtr.Zero,
                    System.IntPtr.Zero);
            }
        }

        private void TryAppendStallLog(long stallMaxMs, int frame, string scene, bool dumped)
        {
            try
            {
                var line = "[" + System.DateTime.Now.ToString(
                        "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)
                    + "] 主线程停滞约 " + stallMaxMs + " ms 后恢复"
                    + "（frame " + frame + ", scene " + scene + (dumped ? ", 已写 dump" : string.Empty)
                    + "）" + System.Environment.NewLine;
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(mReportDir, "stalls-" + mSessionStamp + ".txt"), line);
            }
            catch
            {
                // 忽略：流水账尽力而为。
            }
        }

        private void PruneOldReports()
        {
            PruneByPattern("*.dmp", MaxKeptDumpFiles);
            PruneByPattern("*.txt", MaxKeptTextFiles);
        }

        private void PruneByPattern(string pattern, int keep)
        {
            var files = System.IO.Directory.GetFiles(mReportDir, pattern);
            if (files.Length <= keep)
            {
                return;
            }

            System.Array.Sort(
                files,
                (a, b) => System.IO.File.GetLastWriteTimeUtc(a)
                    .CompareTo(System.IO.File.GetLastWriteTimeUtc(b)));
            for (var i = 0; i < files.Length - keep; i++)
            {
                try
                {
                    System.IO.File.Delete(files[i]);
                }
                catch
                {
                    // 忽略单个删除失败。
                }
            }
        }

        [System.Runtime.InteropServices.DllImport("dbghelp.dll", SetLastError = true)]
        private static extern bool MiniDumpWriteDump(
            System.IntPtr hProcess,
            uint processId,
            Microsoft.Win32.SafeHandles.SafeFileHandle hFile,
            uint dumpType,
            System.IntPtr expParam,
            System.IntPtr userStreamParam,
            System.IntPtr callbackParam);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern System.IntPtr GetCurrentProcess();

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern uint GetCurrentProcessId();

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        private static extern System.IntPtr LoadLibrary(string fileName);
    }
#endif
}

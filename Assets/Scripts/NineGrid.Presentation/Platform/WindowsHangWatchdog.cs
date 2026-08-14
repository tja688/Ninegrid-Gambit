using UnityEngine;

namespace NineGrid.Presentation.Platform
{
    /// <summary>
    /// 标记类型：Windows Player 卡死看门狗见条件编译实现。
    /// 主线程每帧喂心跳；后台线程发现心跳停滞时双写
    /// GameLogs/HangReports（exe 旁）与 persistentDataPath/HangReports。
    /// </summary>
    public static class WindowsHangWatchdogInfo
    {
        public const string ReportFolderName = "HangReports";
        public const string DisableArg = "-ng-no-watchdog";
        public const string DumpSecondsArgPrefix = "-ng-hang-dump-seconds=";
        public const string StallInProgressFileName = "stall-in-progress.txt";
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [DefaultExecutionOrder(-990)]
    public sealed class WindowsHangWatchdog : MonoBehaviour
    {
        private const int PollIntervalMs = 500;
        private const int StallNoteThresholdMs = 2000;
        private const int DefaultDumpThresholdSeconds = 5;
        private const int MaxDumpsPerSession = 2;
        private const int MaxKeptDumpFiles = 3;
        private const int MaxKeptTextFiles = 12;

        private static long sLastBeatTimestamp;
        private static volatile int sLastFrame;
        private static volatile string sSceneName = string.Empty;
        private static volatile bool sHasFocus = true;
        private static volatile bool sQuitting;

        private System.Threading.Thread mThread;
        private string[] mReportDirs = System.Array.Empty<string>();
        private string mSessionStamp = string.Empty;
        private string mSystemSummary = string.Empty;
        private string mPlayerLogHint = string.Empty;
        private string mCommandLineSummary = string.Empty;
        private long mStartTimestamp;
        private int mDumpThresholdMs = DefaultDumpThresholdSeconds * 1000;
        private bool mRunInBackground;
        private uint mProcessId;
        private bool mCrashHandlersInstalled;
        private WindowsHangWatchdogUnhandledExceptionFilter mNativeCrashFilter;

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
            mSessionStamp = System.DateTime.Now.ToString(
                "yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            mRunInBackground = Application.runInBackground;
            mProcessId = GetCurrentProcessId();
            mStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            mPlayerLogHint = "%USERPROFILE%\\AppData\\LocalLow\\"
                + Application.companyName + "\\" + Application.productName;
            mCommandLineSummary = string.Join(" ", System.Environment.GetCommandLineArgs());
            mSystemSummary =
                "app=" + Application.productName + " " + Application.version
                + " unity=" + Application.unityVersion
                + (Debug.isDebugBuild ? " (Development Build)" : string.Empty)
                + "\nos=" + SystemInfo.operatingSystem
                + "\ncpu=" + SystemInfo.processorType
                + " ram=" + SystemInfo.systemMemorySize + "MB"
                + "\ngpu=" + SystemInfo.graphicsDeviceName
                + " api=" + SystemInfo.graphicsDeviceType
                + "\ncmdline=" + mCommandLineSummary;

            ParseDumpThresholdOverride();
            mReportDirs = BuildReportDirectories();

            try
            {
                for (var i = 0; i < mReportDirs.Length; i++)
                {
                    System.IO.Directory.CreateDirectory(mReportDirs[i]);
                    PruneOldReports(mReportDirs[i]);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[WindowsHangWatchdog] 报告目录初始化失败: " + ex.Message);
            }

            LoadLibrary("dbghelp.dll");
            InstallCrashHandlers();
            WriteWatchdogAliveFiles();

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
                + "s 将写报告与 minidump。优先目录："
                + WindowsHangReportPaths.ResolvePortableReportsDir());
        }

        private static string[] BuildReportDirectories()
        {
            var persistent = WindowsHangReportPaths.ResolvePersistentReportsDir();
            var portable = WindowsHangReportPaths.ResolvePortableReportsDir();
            if (string.Equals(persistent, portable, System.StringComparison.OrdinalIgnoreCase))
            {
                return new[] { persistent };
            }

            return new[] { portable, persistent };
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

        private void InstallCrashHandlers()
        {
            if (mCrashHandlersInstalled)
            {
                return;
            }

            mCrashHandlersInstalled = true;
            mNativeCrashFilter = new WindowsHangWatchdogUnhandledExceptionFilter(this);
            mNativeCrashFilter.Install();
            System.AppDomain.CurrentDomain.UnhandledException += OnManagedUnhandledException;
        }

        private void OnManagedUnhandledException(object sender, System.UnhandledExceptionEventArgs args)
        {
            var ex = args.ExceptionObject as System.Exception;
            var detail = ex != null ? ex.ToString() : (args.ExceptionObject?.ToString() ?? "unknown");
            TryWriteCrashReport("managed-unhandled", detail, args.IsTerminating);
        }

        internal void TryWriteCrashReport(string kind, string detail, bool terminating)
        {
            var baseName = "crash-" + mSessionStamp + "-" + kind;
            var body = BuildCommonReportHeader("crash", 0, sLastFrame, sSceneName)
                + "terminating=" + terminating + System.Environment.NewLine
                + "detail=" + detail + System.Environment.NewLine;
            WriteTextToAllReports(baseName + ".txt", body);
            TryWriteDumpToAllReports(baseName + ".dmp", baseName + ".txt");
        }

        private void WriteWatchdogAliveFiles()
        {
            var body = BuildCommonReportHeader("watchdog-alive", 0, 0, sSceneName)
                + "dumpThresholdSeconds=" + (mDumpThresholdMs / 1000) + System.Environment.NewLine
                + "stallNoteThresholdMs=" + StallNoteThresholdMs + System.Environment.NewLine
                + "reportDirs=" + string.Join(" | ", mReportDirs) + System.Environment.NewLine
                + "portableGameLogs=" + WindowsHangReportPaths.ResolvePortableGameLogsDir()
                + System.Environment.NewLine;
            WriteTextToAllReports("watchdog-alive-" + mSessionStamp + ".txt", body);
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
            System.AppDomain.CurrentDomain.UnhandledException -= OnManagedUnhandledException;
            mNativeCrashFilter?.Uninstall();
        }

        private void WatchLoop()
        {
            var inStall = false;
            var dumpedThisStall = false;
            var inProgressWritten = false;
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
                    inProgressWritten = false;
                    continue;
                }

                if (!sHasFocus && !mRunInBackground)
                {
                    inStall = false;
                    inProgressWritten = false;
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
                        inProgressWritten = false;
                        stallMaxMs = elapsedMs;
                        stallFrame = sLastFrame;
                        stallScene = sSceneName;
                    }
                    else if (elapsedMs > stallMaxMs)
                    {
                        stallMaxMs = elapsedMs;
                    }

                    if (!inProgressWritten)
                    {
                        inProgressWritten = true;
                        TryWriteStallInProgress(stallMaxMs, stallFrame, stallScene);
                    }

                    if (!dumpedThisStall && elapsedMs >= mDumpThresholdMs)
                    {
                        dumpedThisStall = true;
                        if (dumpsWritten < MaxDumpsPerSession)
                        {
                            dumpsWritten++;
                            TryWriteHangReportAndDump(stallMaxMs, stallFrame, stallScene, dumpsWritten);
                        }
                    }

                    continue;
                }

                if (inStall)
                {
                    inStall = false;
                    inProgressWritten = false;
                    TryAppendStallLog(stallMaxMs, stallFrame, stallScene, dumpedThisStall);
                    TryFinalizeStallInProgress(stallMaxMs, stallFrame, stallScene, dumpedThisStall);
                }
            }
        }

        private static long TimestampToMs(long timestampDelta)
        {
            return timestampDelta * 1000 / System.Diagnostics.Stopwatch.Frequency;
        }

        private string BuildCommonReportHeader(string kind, long elapsedMs, int frame, string scene)
        {
            var uptimeSeconds = TimestampToMs(
                System.Diagnostics.Stopwatch.GetTimestamp() - mStartTimestamp) / 1000;
            return "[NineGrid HangWatchdog] " + kind + System.Environment.NewLine
                + "time=" + System.DateTime.Now.ToString(
                    "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)
                + System.Environment.NewLine
                + "mainThreadStalledMs>=" + elapsedMs + System.Environment.NewLine
                + "lastFrame=" + frame + " scene=" + scene + System.Environment.NewLine
                + "uptimeSeconds=" + uptimeSeconds + System.Environment.NewLine
                + "unityFocus=" + sHasFocus
                + " runInBackground=" + mRunInBackground + System.Environment.NewLine
                + "mitigationEnabled=" + WindowsHighPollingMouseMitigationRuntime.MitigationEnabled
                + " nolegacyActive=" + WindowsHighPollingMouseMitigationRuntime.NolegacyActive
                + " nativeFocus=" + WindowsHighPollingMouseMitigationRuntime.HasNativeFocus
                + " insideClient=" + WindowsHighPollingMouseMitigationRuntime.InsideClient
                + System.Environment.NewLine
                + mSystemSummary + System.Environment.NewLine
                + "playerLogDir=" + mPlayerLogHint + System.Environment.NewLine
                + "portableHangReports=" + WindowsHangReportPaths.ResolvePortableReportsDir()
                + System.Environment.NewLine
                + "portableGameLogs=" + WindowsHangReportPaths.ResolvePortableGameLogsDir()
                + System.Environment.NewLine;
        }

        private void TryWriteStallInProgress(long elapsedMs, int frame, string scene)
        {
            var body = BuildCommonReportHeader("stall-in-progress", elapsedMs, frame, scene)
                + "note=主线程仍在卡死中；若任务管理器结束进程，本文件会保留供开发者分析。"
                + System.Environment.NewLine;
            WriteTextToAllReports(WindowsHangWatchdogInfo.StallInProgressFileName, body);
        }

        private void TryFinalizeStallInProgress(long stallMaxMs, int frame, string scene, bool dumped)
        {
            for (var i = 0; i < mReportDirs.Length; i++)
            {
                try
                {
                    var inProgress = System.IO.Path.Combine(
                        mReportDirs[i], WindowsHangWatchdogInfo.StallInProgressFileName);
                    if (!System.IO.File.Exists(inProgress))
                    {
                        continue;
                    }

                    var recovered = System.IO.Path.Combine(
                        mReportDirs[i],
                        "stall-recovered-" + mSessionStamp + ".txt");
                    var footer = System.Environment.NewLine
                        + "recoveredAfterMs=" + stallMaxMs
                        + " frame=" + frame
                        + " scene=" + scene
                        + (dumped ? " dumped=true" : string.Empty)
                        + System.Environment.NewLine;
                    System.IO.File.AppendAllText(inProgress, footer);
                    System.IO.File.Move(inProgress, recovered);
                }
                catch
                {
                    // 尽力而为。
                }
            }
        }

        private void TryWriteHangReportAndDump(long elapsedMs, int frame, string scene, int dumpIndex)
        {
            var baseName = "hang-" + mSessionStamp + "-" + dumpIndex;
            var reportBody = BuildCommonReportHeader("hang", elapsedMs, frame, scene)
                + "dumpFile=" + baseName + ".dmp" + System.Environment.NewLine
                + "dumpHint=用 Visual Studio / WinDbg 打开，查看 Main Thread 调用栈。"
                + System.Environment.NewLine;
            WriteTextToAllReports(baseName + ".txt", reportBody);
            TryWriteDumpToAllReports(baseName + ".dmp", baseName + ".txt");
        }

        private void WriteTextToAllReports(string fileName, string body)
        {
            for (var i = 0; i < mReportDirs.Length; i++)
            {
                try
                {
                    System.IO.File.WriteAllText(
                        System.IO.Path.Combine(mReportDirs[i], fileName), body);
                }
                catch
                {
                    // 尽力而为。
                }
            }
        }

        private void TryWriteDumpToAllReports(string dumpFileName, string reportFileName)
        {
            for (var i = 0; i < mReportDirs.Length; i++)
            {
                var dumpPath = System.IO.Path.Combine(mReportDirs[i], dumpFileName);
                var reportPath = System.IO.Path.Combine(mReportDirs[i], reportFileName);
                var dumpOk = false;
                var lastError = 0;
                try
                {
                    dumpOk = WriteMiniDump(dumpPath, out lastError);
                }
                catch (System.Exception ex)
                {
                    lastError = MarshalGetLastErrorSafe();
                    try
                    {
                        System.IO.File.AppendAllText(
                            reportPath,
                            "dumpResult=exception:" + ex.GetType().Name + " " + ex.Message
                            + " lastError=" + lastError + System.Environment.NewLine);
                    }
                    catch
                    {
                        // 忽略。
                    }

                    continue;
                }

                try
                {
                    System.IO.File.AppendAllText(
                        reportPath,
                        "dumpResult=" + (dumpOk ? "ok" : "failed")
                        + " lastError=" + lastError
                        + " path=" + dumpPath + System.Environment.NewLine);
                }
                catch
                {
                    // 忽略。
                }
            }
        }

        private bool WriteMiniDump(string dumpPath, out int lastError)
        {
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
                var ok = MiniDumpWriteDump(
                    GetCurrentProcess(),
                    mProcessId,
                    stream.SafeFileHandle,
                    dumpType,
                    System.IntPtr.Zero,
                    System.IntPtr.Zero,
                    System.IntPtr.Zero);
                lastError = ok ? 0 : MarshalGetLastErrorSafe();
                return ok;
            }
        }

        private void TryAppendStallLog(long stallMaxMs, int frame, string scene, bool dumped)
        {
            var line = "[" + System.DateTime.Now.ToString(
                    "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)
                + "] 主线程停滞约 " + stallMaxMs + " ms 后恢复"
                + "（frame " + frame + ", scene " + scene + (dumped ? ", 已写 dump" : string.Empty)
                + "）" + System.Environment.NewLine;
            for (var i = 0; i < mReportDirs.Length; i++)
            {
                try
                {
                    System.IO.File.AppendAllText(
                        System.IO.Path.Combine(mReportDirs[i], "stalls-" + mSessionStamp + ".txt"),
                        line);
                }
                catch
                {
                    // 忽略。
                }
            }
        }

        private void PruneOldReports(string reportDir)
        {
            PruneByPattern(reportDir, "*.dmp", MaxKeptDumpFiles);
            PruneByPattern(reportDir, "*.txt", MaxKeptTextFiles);
        }

        private static void PruneByPattern(string reportDir, string pattern, int keep)
        {
            var files = System.IO.Directory.GetFiles(reportDir, pattern);
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

        private static int MarshalGetLastErrorSafe()
        {
            try
            {
                return System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            }
            catch
            {
                return 0;
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

    internal sealed class WindowsHangWatchdogUnhandledExceptionFilter
    {
        private readonly WindowsHangWatchdog mOwner;
        private WindowsHangWatchdogNativeFilter mFilter;
        private bool mInstalled;

        internal WindowsHangWatchdogUnhandledExceptionFilter(WindowsHangWatchdog owner)
        {
            mOwner = owner;
        }

        internal void Install()
        {
            if (mInstalled)
            {
                return;
            }

            mFilter = new WindowsHangWatchdogNativeFilter(OnNativeUnhandledException);
            mFilter.Install();
            mInstalled = true;
        }

        internal void Uninstall()
        {
            if (!mInstalled)
            {
                return;
            }

            mFilter?.Uninstall();
            mInstalled = false;
        }

        private int OnNativeUnhandledException(ref WindowsHangWatchdogNativeFilter.ExceptionPointers pointers)
        {
            try
            {
                mOwner.TryWriteCrashReport(
                    "native-unhandled",
                    "exceptionRecordPtr=" + pointers.ExceptionRecord
                    + " contextRecordPtr=" + pointers.ContextRecord,
                    true);
            }
            catch
            {
                // 过滤器内不得抛。
            }

            return 0;
        }
    }

    internal sealed class WindowsHangWatchdogNativeFilter
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        internal struct ExceptionPointers
        {
            public System.IntPtr ExceptionRecord;
            public System.IntPtr ContextRecord;
        }

        internal delegate int UnhandledExceptionFilterDelegate(ref ExceptionPointers exceptionInfo);

        private readonly UnhandledExceptionFilterDelegate mDelegate;
        private System.IntPtr mPreviousFilter;

        internal WindowsHangWatchdogNativeFilter(UnhandledExceptionFilterDelegate handler)
        {
            mDelegate = handler;
        }

        internal void Install()
        {
            mPreviousFilter = SetUnhandledExceptionFilter(
                System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(mDelegate));
        }

        internal void Uninstall()
        {
            if (mPreviousFilter != System.IntPtr.Zero)
            {
                SetUnhandledExceptionFilter(mPreviousFilter);
                mPreviousFilter = System.IntPtr.Zero;
            }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern System.IntPtr SetUnhandledExceptionFilter(System.IntPtr filter);
    }
#endif
}

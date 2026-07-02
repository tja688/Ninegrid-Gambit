#if UNITY_EDITOR
using NineGrid.Presentation.Debugging;
using NineGrid.Presentation.Editor.Ui;
using NineGrid.Presentation.Shell;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace NineGrid.Presentation.Editor
{
    /// <summary>
    /// 主流程控制台：Harness 快速推进 + 右上角菜单直达表演调试。
    /// </summary>
    public sealed class MainFlowDirectorEditorWindow : EditorWindow
    {
        private const string MainScenePath = "Assets/Scenes/MainScene.unity";

        private VisualElement contentRoot;
        private HelpBox statusHelpBox;
        private VisualElement statsContainer;

        [MenuItem("TableNine/主流程控制台", priority = 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<MainFlowDirectorEditorWindow>();
            window.titleContent = new GUIContent("主流程控制台");
            window.minSize = new Vector2(720f, 520f);
            window.Show();
        }

        [MenuItem("CONTEXT/MainFlowDirector/打开主流程控制台", false, 0)]
        private static void OpenFromComponentContext(MenuCommand command)
        {
            ShowWindow();
            if (command.context is MainFlowDirector director)
            {
                Selection.activeObject = director;
                EditorGUIUtility.PingObject(director);
            }
        }

        [MenuItem("CONTEXT/MainFlowDirector/打开表演调试", false, 1)]
        private static void OpenPerformanceDebugFromContext(MenuCommand command)
        {
            PerformanceDebugEditorWindow.ShowWindow();
            if (command.context is MainFlowDirector director)
            {
                Selection.activeObject = director;
            }
        }

        private void OnEnable()
        {
            BuildShell();
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode
                || state == PlayModeStateChange.EnteredEditMode)
            {
                RebuildContent();
            }
        }

        private void OnEditorUpdate()
        {
            RefreshStatus();
            RefreshStats();
        }

        private void BuildShell()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.flexGrow = 1;
            rootVisualElement.style.backgroundColor = PerformanceDebugWarmConsoleUi.Theme.RootBg;

            rootVisualElement.Add(PerformanceDebugWarmConsoleUi.BuildHeader(
                "主流程控制台",
                "MainScene Harness 入口。右上角 ⋮ 可打开完整表演调试面板。",
                PopulateOverflowMenu));

            rootVisualElement.Add(PerformanceDebugWarmConsoleUi.BuildToolbar(
                ("MainScene Play", OpenMainSceneAndPlay, "打开 MainScene 并进入 Play Mode"),
                ("仅打开 MainScene", OpenMainSceneOnly, "不自动 Play"),
                ("选中 Director", SelectDirectorInScene, "在层级中定位 MainFlowDirector")));

            var scroll = PerformanceDebugWarmConsoleUi.CreateContentScroll(out contentRoot);
            rootVisualElement.Add(scroll);

            statusHelpBox = PerformanceDebugWarmConsoleUi.CreateStatusHelpBox(string.Empty);
            contentRoot.Add(statusHelpBox);

            statsContainer = new VisualElement();
            contentRoot.Add(statsContainer);

            RebuildContent();
        }

        private static void PopulateOverflowMenu(DropdownMenu menu)
        {
            menu.AppendAction(
                "表演调试…",
                _ => PerformanceDebugEditorWindow.ShowWindow(),
                DropdownMenuAction.AlwaysEnabled);
            menu.AppendSeparator();
            menu.AppendAction(
                "刷新状态",
                _ =>
                {
                    var window = GetWindow<MainFlowDirectorEditorWindow>(utility: false, title: null, focus: false);
                    window?.RebuildContent();
                },
                DropdownMenuAction.AlwaysEnabled);
        }

        private void RebuildContent()
        {
            if (contentRoot == null)
            {
                return;
            }

            for (var i = contentRoot.childCount - 1; i >= 2; i--)
            {
                contentRoot.RemoveAt(i);
            }

            contentRoot.Add(BuildHarnessSection());
            contentRoot.Add(BuildTipsSection());
            RefreshStatus();
            RefreshStats();
        }

        private VisualElement BuildHarnessSection()
        {
            return PerformanceDebugWarmConsoleUi.CreateSectionCard(
                "Harness 推进",
                EditorApplication.isPlaying
                    ? "Play Mode 中可直接驱动主流程屏态。"
                    : "进入 Play Mode 后可用；也可先点工具栏 MainScene Play。",
                column =>
                {
                    column.Add(PerformanceDebugWarmConsoleUi.CreateButtonRow(
                        MakeHarnessButton("启动 Harness", StartHarness, "回到主菜单并准备调试"),
                        MakeHarnessButton("节点完成", () => InvokeHarness(h => h.NotifyNodeComplete()),
                            "模拟节点完成，推进到奖励屏"),
                        MakeHarnessButton("确认奖励", () => InvokeHarness(h => h.ConfirmReward()),
                            "从奖励屏确认并继续"),
                        MakeHarnessButton("胜利", () => InvokeHarness(h => h.TriggerVictory()),
                            "直接展示胜利结局"),
                        MakeHarnessButton("失败", () => InvokeHarness(h => h.TriggerDefeat()),
                            "直接展示失败结局")));

                    column.Add(PerformanceDebugWarmConsoleUi.CreateButtonRow(
                        MakeHarnessButton("打开表演调试", PerformanceDebugEditorWindow.ShowWindow,
                            "完整模块/时间线调试（Flow、Shell、Interaction 等）")));
                });
        }

        private static VisualElement BuildTipsSection()
        {
            return PerformanceDebugWarmConsoleUi.CreateSectionCard(
                "使用提示",
                null,
                column =>
                {
                    column.Add(PerformanceDebugWarmConsoleUi.CreateChecklistLabel(
                        "选中 Directors/MainFlowDirector，Inspector 右上角 ⋮ 也可打开本窗口或表演调试。"));
                    column.Add(PerformanceDebugWarmConsoleUi.CreateChecklistLabel(
                        "Shell 模块（shell.main-flow / shell.selection）在表演调试面板的 Shell 池。"));
                    column.Add(PerformanceDebugWarmConsoleUi.CreateChecklistLabel(
                        "PerformanceTestScene 仍走原表演测试锚点流程，与本 Harness 互不冲突。"));
                },
                expanded: false);
        }

        private static Button MakeHarnessButton(string text, System.Action click, string tooltip)
        {
            var button = new Button(click) { text = text };
            button.tooltip = tooltip;
            return button;
        }

        private static void StartHarness()
        {
            if (!EnsurePlayMode())
            {
                return;
            }

            InvokeHarness(h => h.StartHarness());
        }

        private static void InvokeHarness(System.Action<MainFlowHarnessDriver> action)
        {
            if (!EnsurePlayMode())
            {
                return;
            }

            MainFlowDirector director = MainFlowDirector.Current;
            if (director?.HarnessDriver == null)
            {
                EditorUtility.DisplayDialog(
                    "主流程控制台",
                    "未找到 MainFlowDirector / HarnessDriver。请确认 MainScene Directors 下已挂接线。",
                    "确定");
                return;
            }

            action(director.HarnessDriver);
        }

        private static bool EnsurePlayMode()
        {
            if (EditorApplication.isPlaying)
            {
                return true;
            }

            EditorUtility.DisplayDialog(
                "主流程控制台",
                "请先进入 Play Mode（可用工具栏「MainScene Play」）。",
                "确定");
            return false;
        }

        private void RefreshStatus()
        {
            if (statusHelpBox == null)
            {
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                statusHelpBox.messageType = HelpBoxMessageType.Info;
                statusHelpBox.text = "未进入 Play Mode。点击工具栏「MainScene Play」或自行 Play 后开始 Harness。";
                return;
            }

            string sceneName = SceneManager.GetActiveScene().name;
            if (sceneName != "MainScene" && sceneName != "PerformanceTestScene")
            {
                statusHelpBox.messageType = HelpBoxMessageType.Warning;
                statusHelpBox.text = $"当前场景为 {sceneName}。主流程 Harness 面向 MainScene。";
                return;
            }

            if (MainFlowDirector.Current == null)
            {
                statusHelpBox.messageType = HelpBoxMessageType.Warning;
                statusHelpBox.text = "Play Mode 中未找到 MainFlowDirector。";
                return;
            }

            if (!PerformanceDebugSession.IsConnected)
            {
                statusHelpBox.messageType = HelpBoxMessageType.Warning;
                statusHelpBox.text = "MainFlowDirector 已就绪，但 PerformanceDebugBootstrap 未连接（表演调试部分功能不可用）。";
                return;
            }

            statusHelpBox.messageType = HelpBoxMessageType.Info;
            statusHelpBox.text = sceneName == "MainScene"
                ? "已连接 MainScene · Shell Harness。可用下方按钮或 ⋮ → 表演调试。"
                : "已连接 PerformanceTestScene 表演调试。";
        }

        private void RefreshStats()
        {
            if (statsContainer == null)
            {
                return;
            }

            statsContainer.Clear();

            MainFlowDirector director = Application.isPlaying ? MainFlowDirector.Current : null;
            MainFlowFsm fsm = director?.FlowFsm;
            string screen = fsm != null ? fsm.CurrentScreen.ToString() : "-";
            string harness = director != null && director.UseHarness ? "开启" : "关闭";
            string bootstrap = PerformanceDebugSession.IsConnected ? "已连接" : "未连接";
            string playState = EditorApplication.isPlaying ? "Play Mode" : "Edit Mode";

            statsContainer.Add(PerformanceDebugWarmConsoleUi.CreateStatsGrid(
                ("运行态", playState, "Editor / Play"),
                ("当前屏态", screen, "MainFlowFsm.CurrentScreen"),
                ("Harness", harness, "useHarness 开关"),
                ("调试 Bootstrap", bootstrap, "PerformanceDebugBootstrap")));
        }

        private static void OpenMainSceneAndPlay()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(MainScenePath);
            EditorApplication.isPlaying = true;
        }

        private static void OpenMainSceneOnly()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(MainScenePath);
        }

        private static void SelectDirectorInScene()
        {
            MainFlowDirector director = Object.FindFirstObjectByType<MainFlowDirector>(FindObjectsInactive.Include);
            if (director == null)
            {
                EditorUtility.DisplayDialog("主流程控制台", "当前场景未找到 MainFlowDirector。", "确定");
                return;
            }

            Selection.activeObject = director;
            EditorGUIUtility.PingObject(director);
        }
    }
}
#endif

using NineGrid.Presentation.Debugging.Timeline;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NineGrid.Presentation.Debugging
{
    /// <summary>
    /// PerformanceTestScene / MainScene 入口：初始化 harness、catalog、runner、timeline 编排。
    /// 调试 UI 由 Editor 窗口 <c>TableNine/表演调试面板</c> 提供。
    /// </summary>
    public sealed class PerformanceDebugBootstrap : MonoBehaviour
    {
        [SerializeField] private GameObject standardCardPrefab;

        private PerformanceDebugHarness harness;
        private PerformanceDebugCatalog catalog;
        private PerformanceDebugSequenceRunner runner;
        private PerformanceDebugTimelineRunner timelineRunner;
        private bool shellSceneMode;

        public PerformanceDebugHarness Harness => harness;
        public PerformanceDebugCatalog Catalog => catalog;
        public PerformanceDebugSequenceRunner Runner => runner;
        public PerformanceDebugTimelineRunner TimelineRunner => timelineRunner;
        public bool ShellSceneMode => shellSceneMode;

        private void Awake()
        {
            if (!TryResolveSceneMode(out shellSceneMode))
            {
                enabled = false;
                return;
            }

            if (harness != null)
            {
                return;
            }

            if (!shellSceneMode && standardCardPrefab == null)
            {
                standardCardPrefab = LoadStandardCardPrefab();
            }

            harness = PerformanceDebugHarness.Create(transform, standardCardPrefab, this, shellSceneMode);
            catalog = PerformanceDebugCatalog.Discover();
            runner = new PerformanceDebugSequenceRunner(catalog, harness, this);
            timelineRunner = new PerformanceDebugTimelineRunner(catalog, harness, this);

            PerformanceDebugSession.Register(this);
            string sceneLabel = shellSceneMode ? "MainScene Shell Harness" : "PerformanceTestScene";
            harness.Log.Info(
                $"Catalog loaded {catalog.Modules.Count} modules ({sceneLabel}). Open TableNine/表演调试面板 in the Editor.");
        }

        private void OnDestroy()
        {
            PerformanceDebugSession.Unregister(this);
        }

        private static bool TryResolveSceneMode(out bool shellMode)
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (sceneName == "MainScene")
            {
                shellMode = true;
                return true;
            }

            shellMode = false;
            return sceneName == "PerformanceTestScene";
        }

        private static GameObject LoadStandardCardPrefab()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Standard Card.prefab");
#else
            return null;
#endif
        }
    }
}

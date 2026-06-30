using UnityEngine;

namespace NineGrid.Presentation.Debugging
{
    /// <summary>
    /// PerformanceTestScene 专用入口：初始化 harness、catalog、runner。
    /// 调试 UI 由 Editor 窗口 <c>TableNine/表演调试面板</c> 提供。
    /// </summary>
    public sealed class PerformanceDebugBootstrap : MonoBehaviour
    {
        [SerializeField] private GameObject standardCardPrefab;

        private PerformanceDebugHarness harness;
        private PerformanceDebugCatalog catalog;
        private PerformanceDebugSequenceRunner runner;

        public PerformanceDebugHarness Harness => harness;
        public PerformanceDebugCatalog Catalog => catalog;
        public PerformanceDebugSequenceRunner Runner => runner;

        private void Awake()
        {
            if (!IsPerformanceTestScene())
            {
                enabled = false;
                return;
            }

            if (harness != null)
            {
                return;
            }

            if (standardCardPrefab == null)
            {
                standardCardPrefab = LoadStandardCardPrefab();
            }

            harness = PerformanceDebugHarness.Create(transform, standardCardPrefab);
            catalog = PerformanceDebugCatalog.Discover();
            runner = new PerformanceDebugSequenceRunner(catalog, harness, this);

            PerformanceDebugSession.Register(this);
            harness.Log.Info(
                $"Catalog loaded {catalog.Modules.Count} modules. Open TableNine/表演调试面板 in the Editor.");
        }

        private void OnDestroy()
        {
            PerformanceDebugSession.Unregister(this);
        }

        private static bool IsPerformanceTestScene()
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
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

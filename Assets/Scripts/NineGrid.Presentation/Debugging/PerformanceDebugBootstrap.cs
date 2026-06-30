using UnityEngine;

namespace NineGrid.Presentation.Debugging
{
    /// <summary>
    /// PerformanceTestScene 专用入口：初始化 harness、catalog、调试面板。
    /// </summary>
    public sealed class PerformanceDebugBootstrap : MonoBehaviour
    {
        [SerializeField] private GameObject standardCardPrefab;
        [SerializeField] private bool showPanelOnStart = true;
        [SerializeField] private KeyCode togglePanelKey = KeyCode.F1;

        private PerformanceDebugHarness harness;
        private PerformanceDebugCatalog catalog;
        private PerformanceDebugSequenceRunner runner;
        private PerformanceDebugPanel panel;

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

            if (showPanelOnStart)
            {
                panel = PerformanceDebugPanel.Create(gameObject, harness, catalog, runner);
            }

            harness.Log.Info($"Catalog loaded {catalog.Modules.Count} modules. Press {togglePanelKey} to toggle panel.");
        }

        private void Update()
        {
            if (panel == null && Input.GetKeyDown(togglePanelKey))
            {
                panel = PerformanceDebugPanel.Create(gameObject, harness, catalog, runner);
            }
            else if (panel != null && Input.GetKeyDown(togglePanelKey))
            {
                panel.ToggleVisible();
            }
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

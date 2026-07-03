using System.Diagnostics;
using System.IO;
using NineGrid.Presentation.Bridge;
using NineGrid.Presentation.Interaction;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NineGrid.Presentation.Debugging.Trace
{
    /// <summary>
    /// 局内卡牌 Debug 追踪：JSONL 事件日志 + 批末不变量校验 + AI 摘要。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleTraceController : MonoBehaviour
    {
        [SerializeField] private BattleTraceLevel traceLevel = BattleTraceLevel.Batch;
        [SerializeField] private bool includeInteraction = true;
        [SerializeField] private KeyCode flushKey = KeyCode.F9;

        private NineGridSceneBootstrap mBootstrap;

        public BattleTraceLevel TraceLevel
        {
            get => traceLevel;
            set
            {
                traceLevel = value;
                RestartSessionIfNeeded();
            }
        }

        public void InitializeAfterBootstrap(NineGridSceneBootstrap bootstrap)
        {
            mBootstrap = bootstrap;
            ConfigureHooks();
            RestartSessionIfNeeded();
        }

        private void Awake()
        {
            mBootstrap = GetComponent<NineGridSceneBootstrap>()
                ?? NineGridSceneBootstrap.Current
                ?? FindObjectOfType<NineGridSceneBootstrap>();
        }

        private void OnEnable()
        {
            if (mBootstrap != null)
            {
                RestartSessionIfNeeded();
            }
        }

        private void OnDisable()
        {
            BattleTraceSession.End(flushSummary: true);
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(flushKey))
            {
                FlushNow();
            }
        }

        public void RestartSessionIfNeeded()
        {
            BattleTraceSession.End(flushSummary: false);
            if (traceLevel == BattleTraceLevel.Off)
            {
                return;
            }

            BattleTraceSession.Start(traceLevel, includeInteraction);
            ConfigureHooks();
            if (mBootstrap?.FlowRegistry != null)
            {
                BattleTraceHooks.WrapFlowRegistry(mBootstrap.FlowRegistry);
            }
        }

        [ContextMenu("BattleTrace/Flush Summary")]
        public void FlushNow()
        {
            BattleTraceSession.FlushSummary();
        }

        [ContextMenu("BattleTrace/Open Log Folder")]
        public void OpenLogFolder()
        {
            string directory = Path.Combine(Application.persistentDataPath, "BattleTraces");
            Directory.CreateDirectory(directory);
#if UNITY_EDITOR
            EditorUtility.RevealInFinder(directory);
#else
            Process.Start(directory);
#endif
        }

        private void ConfigureHooks()
        {
            if (mBootstrap == null)
            {
                return;
            }

            BattleTraceHooks.Configure(
                mBootstrap.Architecture,
                mBootstrap.ViewRegistry,
                mBootstrap.InteractionCoordinator);
        }
    }
}

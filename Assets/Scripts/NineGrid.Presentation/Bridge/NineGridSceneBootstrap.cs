using DamageNumbersPro;
using NineGrid.Core;
using NineGrid.Presentation.Bridge;
using NineGrid.Presentation.Interaction;
using NineGrid.Presentation.Orchestration;
using NineGrid.Presentation.Shell;
using NineGrid.Presentation.Visuals;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Bridge
{
    /// <summary>
    /// MainScene 生产入口：Architecture / InitialGame / BatchPlayer / CommandGateway。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NineGridSceneBootstrap : MonoBehaviour
    {
        public static NineGridSceneBootstrap Current { get; private set; }

        [Header("Prefabs")]
        [SerializeField] private GameObject standardCardPrefab;
        [SerializeField] private DamageNumber damageNumberPrefab;

        private GameObject mModuleHost;
        private CoreCommandDispatcher mDispatcher;
        private HandItemsReconcilable mHandItemsReconcilable;

        public CommandGateway Gateway { get; private set; }
        public InGameInteractionCoordinator InteractionCoordinator { get; private set; }
        public TableNineViewRegistry ViewRegistry { get; private set; }
        public TableNineActorFactory ActorFactory { get; private set; }
        public PresentationBatchPlayer BatchPlayer { get; private set; }
        public FlowRegistry FlowRegistry { get; private set; }
        public SceneStagingRoots StagingRoots { get; private set; }
        public TableNineHudModelBinder HudBinder { get; private set; }
        public IArchitecture Architecture { get; private set; }

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Debug.LogWarning("[NineGridSceneBootstrap] Duplicate bootstrap detected; ignoring.");
                enabled = false;
                return;
            }

            Current = this;
            InitializeRuntime();
        }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        private void InitializeRuntime()
        {
            Architecture = NineGridArchitecture.Current;
            if (!ContentCatalogRuntimeBootstrap.EnsureLoaded(Architecture))
            {
                Debug.LogWarning("[NineGridSceneBootstrap] Content catalog failed to load.");
            }

            InitialGameFactory.Create(Architecture);

            if (!SceneStagingRoots.TryResolve(out SceneStagingRoots roots))
            {
                Debug.LogError("[NineGridSceneBootstrap] Scene staging roots not found.");
                enabled = false;
                return;
            }

            StagingRoots = roots;
            EnsureStandardCardPrefab();
            EnsureDamageNumberPrefab();

            ViewRegistry = new TableNineViewRegistry();
            ViewRegistry.IndexFromScene(roots);

            ActorFactory = new TableNineActorFactory(
                roots.ActorsRoot,
                standardCardPrefab,
                ViewRegistry,
                Architecture);

            mModuleHost = new GameObject("PresentationModuleHost");
            mModuleHost.transform.SetParent(transform, false);

            var services = ProductionOrchestrationSetup.Install(
                mModuleHost,
                roots,
                ViewRegistry,
                ActorFactory,
                standardCardPrefab,
                damageNumberPrefab);

            FlowRegistry = services.FlowRegistry;
            BatchPlayer = services.BatchPlayer;
            mHandItemsReconcilable = services.HandItemsReconcilable;

            mDispatcher = new CoreCommandDispatcher(Architecture);
            Gateway = new CommandGateway(this, Architecture, mDispatcher, BatchPlayer);

            InteractionCoordinator = InGameInteractionSetup.Install(
                this,
                mModuleHost,
                roots,
                Gateway,
                ViewRegistry,
                ActorFactory,
                services.HandItemsReconcilable);

            HudBinder = HudDirectBindingSetup.Install(Architecture, transform, services.InGameUiFlow);
            ReconcileInitialSnapshot();
            WireShellBridge();
            Debug.Log("[NineGridSceneBootstrap] Production bridge ready.");
        }

        private void WireShellBridge()
        {
            if (MainFlowDirector.Current != null)
            {
                MainFlowDirector.Current.WireProductionBridge(Gateway, Architecture);
            }
        }

        public void ReconcileInitialSnapshot()
        {
            var snapshot = CoreViewSnapshotFactory.Capture(Architecture);
            var reconcilable = new BoardCardsReconcilable(ViewRegistry, ActorFactory);
            reconcilable.ApplySnapshot(snapshot);
            mHandItemsReconcilable?.ApplySnapshot(snapshot);

            TableNineStatusPanelView statusPanel = FindObjectOfType<TableNineStatusPanelView>();
            statusPanel?.ApplyAvatarCombatStats(snapshot);
        }

        private void EnsureStandardCardPrefab()
        {
            if (standardCardPrefab != null)
            {
                return;
            }

#if UNITY_EDITOR
            standardCardPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Standard Card.prefab");
#endif
            if (standardCardPrefab == null)
            {
                Debug.LogWarning("[NineGridSceneBootstrap] Standard Card prefab is not assigned.");
            }
        }

        private void EnsureDamageNumberPrefab()
        {
            if (damageNumberPrefab != null)
            {
                return;
            }

#if UNITY_EDITOR
            damageNumberPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<DamageNumber>(
                "Assets/Arts/Prefabs/Presentation/DamageNumbers/DNP_RedGlow.prefab");
#endif
        }
    }
}

using DamageNumbersPro;
using NineGrid.Core;
using NineGrid.Presentation.Bridge;
using NineGrid.Presentation.Debugging.Trace;
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
        [SerializeField] private GameObject itemUseOptionPrefab;

        private GameObject mModuleHost;
        private CoreCommandDispatcher mDispatcher;
        private HandItemsPresenter mHandItemsPresenter;

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
            mHandItemsPresenter = services.HandItemsPresenter;

            mDispatcher = new CoreCommandDispatcher(Architecture);
            Gateway = new CommandGateway(this, Architecture, mDispatcher, BatchPlayer, OnBatchPlaybackComplete);

            InteractionCoordinator = InGameInteractionSetup.Install(
                this,
                mModuleHost,
                roots,
                Gateway,
                ViewRegistry,
                ActorFactory,
                services.HandItemsPresenter);

            HudBinder = HudDirectBindingSetup.Install(Architecture, transform, services.InGameUiFlow);
            BuildInitialActors();
            WireShellBridge();
            EnsureBattleTrace();
            Debug.Log("[NineGridSceneBootstrap] Production bridge ready.");
        }

        private void EnsureBattleTrace()
        {
            var trace = GetComponent<BattleTraceController>();
            if (trace == null)
            {
                trace = gameObject.AddComponent<BattleTraceController>();
            }

            trace.InitializeAfterBootstrap(this);
        }

        private void WireShellBridge()
        {
            MainFlowDirector director = MainFlowDirector.Current;
            if (director == null)
            {
                return;
            }

            director.WireProductionBridge(Gateway, Architecture);
            EnsureItemUseOptionPrefab();
            InGameInteractionSetup.WireItemUseSelection(
                InteractionCoordinator,
                StagingRoots,
                director.SelectionFsm,
                director.SelectionPresentation,
                director.GetComponent<SelectionFsmOwner>(),
                itemUseOptionPrefab);
        }

        private void EnsureItemUseOptionPrefab()
        {
            if (itemUseOptionPrefab != null)
            {
                return;
            }

#if UNITY_EDITOR
            itemUseOptionPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/StandUISelection.prefab");
#endif
        }

        public void BuildInitialActors()
        {
            var snapshot = CoreViewSnapshotFactory.Capture(Architecture);
            InitialActorsBuilder.BuildBoardCards(snapshot, ViewRegistry, ActorFactory);
            mHandItemsPresenter?.BuildInitialHandItems(snapshot);

            TableNineStatusPanelView statusPanel = FindObjectOfType<TableNineStatusPanelView>();
            statusPanel?.ApplyAvatarCombatStats(snapshot);
        }

        private void OnBatchPlaybackComplete(PresentationBatch batch)
        {
            if (batch == null)
            {
                return;
            }

            var snapshot = CoreViewSnapshotFactory.Capture(Architecture);
            BoardInteractionActorWiring.WireAllBoardActors(ViewRegistry, snapshot);
            HandInteractionActorWiring.WireAllHandActors(ViewRegistry, snapshot, InteractionCoordinator);
            BattleTraceHooks.OnBatchPlaybackSettled(batch.BatchId);
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

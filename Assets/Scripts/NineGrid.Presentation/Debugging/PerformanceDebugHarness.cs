using System.Collections.Generic;
using DamageNumbersPro;
using NineGrid.Core;
using NineGrid.Presentation.Contracts;
using NineGrid.Presentation.Orchestration;
using NineGrid.Presentation.Visuals;
using UnityEngine;

namespace NineGrid.Presentation.Debugging
{
    public sealed class PerformanceDebugHarness
    {
        private Transform bootstrapRoot;
        private GameObject moduleHost;
        private readonly Dictionary<System.Type, Component> moduleCache = new();

        public Transform ActorsRoot { get; private set; }
        public Transform AnchorsRoot { get; private set; }
        public Transform PanelsRoot { get; private set; }
        public Transform HandActorsRoot { get; private set; }
        public Transform NineGridAnchors { get; private set; }
        public Transform HandCardAnchors { get; private set; }
        public Transform CardDeckAnchors { get; private set; }
        public Transform PanelsAnchor { get; private set; }
        public PerformanceDebugViewRegistry Registry { get; } = new();
        public DebugActorFactory ActorFactory { get; private set; }
        public PerformanceDebugLogBuffer Log { get; } = new();
        public FlowRegistry FlowRegistry { get; private set; }
        public ReactionRegistry ReactionRegistry { get; private set; }
        public PresentationBatchPlayer BatchPlayer { get; private set; }
        public PerformanceDebugBatchRunner BatchRunner { get; private set; }

        public static PerformanceDebugHarness Create(Transform bootstrapRoot, GameObject cardPrefab, MonoBehaviour coroutineHost = null)
        {
            var harness = new PerformanceDebugHarness();
            harness.Initialize(bootstrapRoot, cardPrefab, coroutineHost);
            return harness;
        }

        private void Initialize(Transform bootstrapRoot, GameObject cardPrefab, MonoBehaviour coroutineHost)
        {
            this.bootstrapRoot = bootstrapRoot;
            ResolveSceneRoots();
            ActorFactory = new DebugActorFactory(ActorsRoot, cardPrefab);
            IndexAnchors();
            moduleHost = CreateAndWireModuleHost(bootstrapRoot, cardPrefab);
            WireOrchestration(coroutineHost);
            Log.Info("Harness ready. Stage is empty until Play or Rebuild Context.");
        }

        private void WireOrchestration(MonoBehaviour coroutineHost)
        {
            var services = PerformanceDebugOrchestrationSetup.Install(moduleHost, Registry);
            FlowRegistry = services.FlowRegistry;
            ReactionRegistry = services.ReactionRegistry;
            BatchPlayer = services.BatchPlayer;
            if (coroutineHost != null)
            {
                BatchRunner = new PerformanceDebugBatchRunner(this, coroutineHost);
            }
        }

        public IFlowBinding GetFlowBinding(FlowId flowId)
        {
            if (FlowRegistry != null && FlowRegistry.TryGet(flowId, out IFlowBinding binding))
            {
                return binding;
            }

            return null;
        }

        private void ResolveSceneRoots()
        {
            if (!PerformanceDebugAnchorIndexing.TryResolveStagingRoots(
                    out Transform anchorsRoot,
                    out Transform panelsRoot,
                    out Transform nineGridAnchors,
                    out Transform cardDeckAnchors,
                    out Transform handCardAnchors,
                    out Transform panelsAnchor,
                    out Transform actorsRoot,
                    out Transform handActorsRoot))
            {
                Log.Warn("Staging roots not found in active scene. Anchor indexing will be empty until reindex.");
            }

            AnchorsRoot = anchorsRoot;
            PanelsRoot = panelsRoot;
            NineGridAnchors = nineGridAnchors;
            HandCardAnchors = handCardAnchors;
            CardDeckAnchors = cardDeckAnchors;
            PanelsAnchor = panelsAnchor;
            ActorsRoot = actorsRoot;
            HandActorsRoot = handActorsRoot;
        }

        public PerformanceDebugContext CreateContext()
        {
            return new PerformanceDebugContext
            {
                Harness = this,
                Registry = Registry,
                ActorFactory = ActorFactory,
                Log = Log,
                MainCamera = Camera.main,
            };
        }

        public T GetModule<T>() where T : Component
        {
            System.Type type = typeof(T);
            if (moduleCache.TryGetValue(type, out Component cached) && cached != null)
            {
                return cached as T;
            }

            T found = moduleHost != null ? moduleHost.GetComponentInChildren<T>(true) : null;
            if (found != null)
            {
                moduleCache[type] = found;
            }

            return found;
        }

        public void ApplyContextPreset(PerformanceDebugContextPreset preset)
        {
            ClearPerformanceStage();
            BuildContextPreset(preset);
            Log.Info($"Context preset applied: {preset}");
        }

        public void ReindexAnchors()
        {
            ResolveSceneRoots();
            IndexAnchors();
            Log.Info($"Anchors reindexed: {Registry.Anchors.Count} entries.");
        }

        /// <summary>
        /// 停止模块并销毁 Actors/HandActors 下所有运行时卡牌，不重建预设演员。
        /// </summary>
        public void ClearPerformanceStage()
        {
            StopAllModules();
            ActorFactory?.DestroyAll();
            DestroyAllChildren(ActorsRoot);
            DestroyAllChildren(HandActorsRoot);
            Registry.ClearActors();
        }

        public void ClearDebugActors()
        {
            ActorFactory?.DestroyAll();
            Registry.ClearActors();
        }

        public Transform ResolveGridAnchor(string slotName)
        {
            if (string.IsNullOrEmpty(slotName))
            {
                return null;
            }

            Transform anchor = Registry.ResolveAnchor($"grid.{slotName}");
            if (anchor != null)
            {
                return anchor;
            }

            return NineGridAnchors != null ? NineGridAnchors.Find(slotName) : null;
        }

        private void BuildContextPreset(PerformanceDebugContextPreset preset)
        {
            switch (preset)
            {
                case PerformanceDebugContextPreset.BattlePair:
                    BuildBattlePair();
                    break;
                case PerformanceDebugContextPreset.Board9:
                    BuildBoard9();
                    break;
                case PerformanceDebugContextPreset.Deck20:
                    BuildDeck20();
                    break;
                case PerformanceDebugContextPreset.Hand7:
                    BuildHand7();
                    break;
                case PerformanceDebugContextPreset.Selection3:
                    BuildSelection3();
                    break;
                case PerformanceDebugContextPreset.StatusPanel:
                    BuildStatusPanel();
                    break;
            }
        }

        public void StopAllModules()
        {
            if (moduleHost == null)
            {
                return;
            }

            MonoBehaviour[] behaviours = moduleHost.GetComponentsInChildren<MonoBehaviour>(true);
            for (var i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                switch (behaviour)
                {
                    case IDirectedFlow flow:
                        flow.StopAndRestore();
                        break;
                    case IPlannedReaction reaction:
                        reaction.StopAndRestore();
                        break;
                    case ILocalCue cue:
                        cue.StopAndRestore();
                        break;
                    case IInteractivePresenter presenter:
                        presenter.ForceReset();
                        break;
                }
            }
        }

        private void BuildBattlePair()
        {
            Transform playerAnchor = ResolveGridAnchor("slot5_Player") ?? ResolveGridAnchor("slot6");
            Transform enemyAnchor = ResolveGridAnchor("slot6");
            if (playerAnchor == null || enemyAnchor == null)
            {
                Log.Warn($"BattlePair anchors missing. player={(playerAnchor != null)} enemy={(enemyAnchor != null)}");
            }

            Transform player = ActorFactory.SpawnAtAnchor("player", PerformanceDebugActorUids.Player, playerAnchor);
            Transform enemy = ActorFactory.SpawnAtAnchor("enemy", PerformanceDebugActorUids.Enemy, enemyAnchor);
            Registry.RegisterActor("player", player);
            Registry.RegisterActor("enemy", enemy);
            Registry.RegisterActor(PerformanceDebugActorUids.Player, player);
            Registry.RegisterActor(PerformanceDebugActorUids.Enemy, enemy);
            if (playerAnchor != null)
            {
                Registry.RegisterAnchor(PerformanceDebugActorUids.PlayerSlot, playerAnchor);
            }

            if (enemyAnchor != null)
            {
                Registry.RegisterAnchor(PerformanceDebugActorUids.EnemySlot, enemyAnchor);
            }
        }

        private void BuildBoard9()
        {
            for (var i = 1; i <= 9; i++)
            {
                string slotName = i == 5 ? "slot5_Player" : $"slot{i}";
                Transform anchor = ResolveGridAnchor(slotName) ?? ResolveGridAnchor($"slot{i}");
                string actorId = $"board{i}";
                int cardUid = PerformanceDebugActorUids.BoardCard(i);
                Transform actor = ActorFactory.SpawnAtAnchor(actorId, cardUid, anchor);
                Registry.RegisterActor(actorId, actor);
                Registry.RegisterActor(cardUid, actor);
                if (anchor != null)
                {
                    Registry.RegisterAnchor(SlotId.Board(i), anchor);
                }
            }

            RegisterBoardPresetBattleAliases();
        }

        private void RegisterBoardPresetBattleAliases()
        {
            Transform player = Registry.ResolveActor(PerformanceDebugActorUids.BoardCard(5));
            Transform enemy = Registry.ResolveActor(PerformanceDebugActorUids.BoardCard(6));
            if (player != null)
            {
                Registry.RegisterActor(PerformanceDebugActorUids.Player, player);
            }

            if (enemy != null)
            {
                Registry.RegisterActor(PerformanceDebugActorUids.Enemy, enemy);
            }
        }

        private void BuildDeck20()
        {
            if (CardDeckAnchors == null)
            {
                return;
            }

            for (var i = 0; i < CardDeckAnchors.childCount; i++)
            {
                Transform slot = CardDeckAnchors.GetChild(i);
                if (!PerformanceDebugAnchorIndexing.IsDeckPileSlotAnchor(slot.name))
                {
                    continue;
                }

                string actorId = $"deck{slot.name.Substring(4)}";
                Transform actor = ActorFactory.SpawnAtAnchor(actorId, slot);
                Registry.RegisterActor(actorId, actor);
                Registry.RegisterAnchor($"deck.{slot.name}", slot);
            }
        }

        private void BuildHand7()
        {
            Transform parent = HandActorsRoot != null ? HandActorsRoot : HandCardAnchors;
            if (HandCardAnchors == null)
            {
                return;
            }

            Transform[] slotAnchors = PerformanceDebugAnchorIndexing.CollectHandCardSlotAnchors(HandCardAnchors);
            for (var i = 0; i < slotAnchors.Length; i++)
            {
                Transform anchor = slotAnchors[i];
                string actorId = $"hand{i + 1}";
                Transform actor = ActorFactory.SpawnAtAnchor(actorId, anchor, useLocalSpace: parent == HandActorsRoot);
                if (parent == HandActorsRoot)
                {
                    actor.SetParent(HandActorsRoot, false);
                }

                Registry.RegisterActor(actorId, actor);
            }
        }

        private void BuildSelection3()
        {
            Vector3 center = PanelsAnchor != null ? PanelsAnchor.position : Vector3.zero;
            float spacing = 2.2f;
            for (var i = 0; i < 3; i++)
            {
                string actorId = $"option{i + 1}";
                Vector3 position = center + new Vector3((i - 1) * spacing, 0f, 0f);
                Transform actor = ActorFactory.Spawn(actorId, position, Quaternion.identity);
                Registry.RegisterActor(actorId, actor);
            }
        }

        private void BuildStatusPanel()
        {
            Transform anchor = ResolveGridAnchor("slot5_Player") ?? ResolveGridAnchor("slot6");
            Transform actor = ActorFactory.SpawnAtAnchor("statusCard", anchor);
            EnsureCardStatusView(actor);
            Registry.RegisterActor("statusCard", actor);
            Registry.RegisterActor("player", actor);
        }

        private static void EnsureCardStatusView(Transform actor)
        {
            if (actor == null)
            {
                return;
            }

            TableNineCardStatusView view = actor.GetComponent<TableNineCardStatusView>();
            if (view == null)
            {
                view = actor.gameObject.AddComponent<TableNineCardStatusView>();
            }

            view.EnsureBindings();
            view.ConfigureDigitSprites(TableNineDigitSpriteLibrary.LoadDefaultDigits());
        }

        private void IndexAnchors()
        {
            PerformanceDebugAnchorIndexing.Reindex(
                Registry,
                AnchorsRoot,
                PanelsRoot,
                NineGridAnchors,
                CardDeckAnchors,
                HandCardAnchors,
                PanelsAnchor);
            RegisterBoardSlotAnchors();
        }

        private void RegisterBoardSlotAnchors()
        {
            for (var i = 1; i <= 9; i++)
            {
                Transform anchor = ResolveGridAnchor(i == 5 ? "slot5_Player" : $"slot{i}");
                if (anchor != null)
                {
                    Registry.RegisterAnchor(SlotId.Board(i), anchor);
                }
            }
        }

        private static void DestroyAllChildren(Transform root)
        {
            if (root == null)
            {
                return;
            }

            for (var i = root.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(root.GetChild(i).gameObject);
            }
        }

        private GameObject CreateAndWireModuleHost(Transform bootstrapRoot, GameObject cardPrefab)
        {
            var host = new GameObject("PerformanceDebugModuleHost");
            host.transform.SetParent(bootstrapRoot, false);
            PerformanceDebugModuleHostSetup.Install(host, this, cardPrefab, LoadDamageNumberPrefab());
            return host;
        }

        private static DamageNumbersPro.DamageNumber LoadDamageNumberPrefab()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<DamageNumber>(
                "Assets/Plugins/DamageNumbersPro/Demo C#/Demo_Popup.prefab");
#else
            return null;
#endif
        }

        private static Transform FindChildRecursive(Transform root, string name)
        {
            return PerformanceDebugAnchorIndexing.FindChildRecursive(root, name);
        }
    }
}

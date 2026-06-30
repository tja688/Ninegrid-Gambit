using System.Collections.Generic;
using DamageNumbersPro;
using NineGrid.Presentation.Contracts;
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

        public static PerformanceDebugHarness Create(Transform bootstrapRoot, GameObject cardPrefab)
        {
            var harness = new PerformanceDebugHarness();
            harness.Initialize(bootstrapRoot, cardPrefab);
            return harness;
        }

        private void Initialize(Transform bootstrapRoot, GameObject cardPrefab)
        {
            this.bootstrapRoot = bootstrapRoot;
            ActorsRoot = FindChildRecursive(bootstrapRoot.root, "Actors");
            AnchorsRoot = FindChildRecursive(bootstrapRoot.root, "Anchors");
            PanelsRoot = FindChildRecursive(bootstrapRoot.root, "Panels");
            HandActorsRoot = FindChildRecursive(bootstrapRoot.root, "HandCardActors");

            if (AnchorsRoot != null)
            {
                NineGridAnchors = AnchorsRoot.Find("NineGridAnchors");
                HandCardAnchors = AnchorsRoot.Find("HandCardAnchors");
                CardDeckAnchors = AnchorsRoot.Find("CardDeckAnchors");
                PanelsAnchor = AnchorsRoot.Find("PanelsAnchor");
            }

            ActorFactory = new DebugActorFactory(ActorsRoot, cardPrefab);
            IndexAnchors();
            moduleHost = CreateAndWireModuleHost(bootstrapRoot, cardPrefab);
            ApplyContextPreset(PerformanceDebugContextPreset.BattlePair);
            Log.Info("Harness ready.");
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
            ClearDebugActors();
            Registry.ClearActors();

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

            Log.Info($"Context preset applied: {preset}");
        }

        public void ClearDebugActors()
        {
            ActorFactory?.DestroyAll();
            Registry.ClearActors();
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
            Transform playerAnchor = Registry.ResolveAnchor("slot5_Player");
            Transform enemyAnchor = Registry.ResolveAnchor("slot3");
            Transform player = ActorFactory.SpawnAtAnchor("player", playerAnchor);
            Transform enemy = ActorFactory.SpawnAtAnchor("enemy", enemyAnchor);
            Registry.RegisterActor("player", player);
            Registry.RegisterActor("enemy", enemy);
        }

        private void BuildBoard9()
        {
            for (var i = 1; i <= 9; i++)
            {
                string slotName = i == 5 ? "slot5_Player" : $"slot{i}";
                Transform anchor = Registry.ResolveAnchor(slotName);
                string actorId = $"board{i}";
                Transform actor = ActorFactory.SpawnAtAnchor(actorId, anchor);
                Registry.RegisterActor(actorId, actor);
            }
        }

        private void BuildDeck20()
        {
            if (CardDeckAnchors == null)
            {
                return;
            }

            Vector3 deckOrigin = CardDeckAnchors.position + new Vector3(9.4375f, 3f, 0f);
            for (var i = 0; i < CardDeckAnchors.childCount; i++)
            {
                Transform slot = CardDeckAnchors.GetChild(i);
                string actorId = $"deck{i + 1}";
                Transform actor = ActorFactory.Spawn(actorId, deckOrigin, Quaternion.identity);
                actor.position = deckOrigin + new Vector3(0f, i * 0.02f, 0f);
                Registry.RegisterActor(actorId, actor);
                Registry.RegisterAnchor($"deckSlot{i + 1}", slot);
            }
        }

        private void BuildHand7()
        {
            Transform parent = HandActorsRoot != null ? HandActorsRoot : HandCardAnchors;
            if (HandCardAnchors == null)
            {
                return;
            }

            for (var i = 0; i < HandCardAnchors.childCount; i++)
            {
                Transform anchor = HandCardAnchors.GetChild(i);
                if (anchor.name == "HandcardApplyZone" || anchor.name == "HandCardActors")
                {
                    continue;
                }

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
            Transform anchor = Registry.ResolveAnchor("slot5_Player");
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
            Registry.ClearAll();
            IndexRecursive(AnchorsRoot, "Anchors");
            IndexRecursive(PanelsRoot, "Panels");
        }

        private void IndexRecursive(Transform root, string prefix)
        {
            if (root == null)
            {
                return;
            }

            Registry.RegisterAnchor(prefix, root);
            for (var i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                string childPath = $"{prefix}/{child.name}";
                Registry.RegisterAnchor(childPath, child);
                Registry.RegisterAnchor(child.name, child);
                IndexRecursive(child, childPath);
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
            if (root == null)
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildRecursive(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}

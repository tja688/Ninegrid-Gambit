using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NineGrid.Presentation.Debugging
{
    public static class PerformanceDebugAnchorIndexing
    {
        public static bool TryResolveStagingRoots(
            out Transform anchorsRoot,
            out Transform panelsRoot,
            out Transform nineGridAnchors,
            out Transform cardDeckAnchors,
            out Transform handCardAnchors,
            out Transform panelsAnchor,
            out Transform actorsRoot,
            out Transform handActorsRoot)
        {
            anchorsRoot = null;
            panelsRoot = null;
            nineGridAnchors = null;
            cardDeckAnchors = null;
            handCardAnchors = null;
            panelsAnchor = null;
            actorsRoot = null;
            handActorsRoot = null;

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                return false;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                Transform root = roots[i].transform;
                if (anchorsRoot == null)
                {
                    anchorsRoot = FindChildRecursive(root, "Anchors");
                }

                if (panelsRoot == null)
                {
                    panelsRoot = FindChildRecursive(root, "Panels");
                }

                if (actorsRoot == null)
                {
                    actorsRoot = FindChildRecursive(root, "Actors");
                }

                if (handActorsRoot == null)
                {
                    handActorsRoot = FindChildRecursive(root, "HandCardActors");
                }
            }

            if (anchorsRoot != null)
            {
                nineGridAnchors = anchorsRoot.Find("NineGridAnchors");
                cardDeckAnchors = anchorsRoot.Find("CardDeckAnchors");
                handCardAnchors = anchorsRoot.Find("HandCardAnchors");
                panelsAnchor = anchorsRoot.Find("PanelsAnchor");
            }

            return anchorsRoot != null;
        }

        public static Transform[] CollectHandCardSlotAnchors(Transform handCardAnchors, int maxCount = 5)
        {
            if (handCardAnchors == null || maxCount <= 0)
            {
                return Array.Empty<Transform>();
            }

            var slots = new List<Transform>(maxCount);
            for (var i = 0; i < handCardAnchors.childCount && slots.Count < maxCount; i++)
            {
                Transform child = handCardAnchors.GetChild(i);
                if (!IsHandCardSlotAnchor(child.name))
                {
                    continue;
                }

                slots.Add(child);
            }

            return slots.ToArray();
        }

        public static bool IsHandCardSlotAnchor(string anchorName)
        {
            if (string.IsNullOrEmpty(anchorName))
            {
                return false;
            }

            return anchorName.StartsWith("handcard", StringComparison.OrdinalIgnoreCase);
        }

        public const string DeckEntryPreparationSlotName = "DeckEntryPreparationSlot";
        public const string DeckDealOriginSlotName = "slot1";

        public static Transform FindDeckChild(Transform cardDeckAnchors, string childName)
        {
            if (cardDeckAnchors == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            return cardDeckAnchors.Find(childName);
        }

        public static bool IsDeckPileSlotAnchor(string anchorName)
        {
            if (string.IsNullOrEmpty(anchorName))
            {
                return false;
            }

            if (!anchorName.StartsWith("slot", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return int.TryParse(anchorName.Substring(4), out int slotNumber) && slotNumber >= 1;
        }

        public static Transform[] CollectDeckDealSourceSlots(Transform cardDeckAnchors, int maxCount = 8)
        {
            if (cardDeckAnchors == null || maxCount <= 0)
            {
                return Array.Empty<Transform>();
            }

            var slots = new List<Transform>(maxCount);
            for (var slotNumber = 1; slotNumber <= maxCount; slotNumber++)
            {
                Transform slot = cardDeckAnchors.Find($"slot{slotNumber}");
                if (slot != null)
                {
                    slots.Add(slot);
                }
            }

            return slots.ToArray();
        }
        public static void Reindex(
            PerformanceDebugViewRegistry registry,
            Transform anchorsRoot,
            Transform panelsRoot,
            Transform nineGridAnchors,
            Transform cardDeckAnchors,
            Transform handCardAnchors,
            Transform panelsAnchor)
        {
            registry.ClearAll();
            IndexRecursive(registry, anchorsRoot, "Anchors");
            IndexRecursive(registry, panelsRoot, "Panels");
            RegisterScopedGroup(registry, nineGridAnchors, "Anchors/NineGridAnchors", "grid", registerBareNames: true);
            RegisterScopedGroup(registry, cardDeckAnchors, "Anchors/CardDeckAnchors", "deck", registerBareNames: false);
            RegisterScopedGroup(registry, handCardAnchors, "Anchors/HandCardAnchors", "hand", registerBareNames: false);
            RegisterScopedGroup(registry, panelsAnchor, "Anchors/PanelsAnchor", "panel", registerBareNames: false);
        }

        public static void ResolveAnchorRoots(
            Transform sceneRoot,
            out Transform anchorsRoot,
            out Transform panelsRoot,
            out Transform nineGridAnchors,
            out Transform cardDeckAnchors,
            out Transform handCardAnchors,
            out Transform panelsAnchor)
        {
            anchorsRoot = FindChildRecursive(sceneRoot, "Anchors");
            panelsRoot = FindChildRecursive(sceneRoot, "Panels");
            nineGridAnchors = null;
            cardDeckAnchors = null;
            handCardAnchors = null;
            panelsAnchor = null;

            if (anchorsRoot != null)
            {
                nineGridAnchors = anchorsRoot.Find("NineGridAnchors");
                handCardAnchors = anchorsRoot.Find("HandCardAnchors");
                cardDeckAnchors = anchorsRoot.Find("CardDeckAnchors");
                panelsAnchor = anchorsRoot.Find("PanelsAnchor");
            }
        }

        public static bool ShouldDisplayAnchorKey(string key)
        {
            return key.StartsWith("grid.", StringComparison.Ordinal)
                || key.StartsWith("deck.", StringComparison.Ordinal)
                || key.StartsWith("hand.", StringComparison.Ordinal)
                || key.StartsWith("Anchors/NineGridAnchors/", StringComparison.Ordinal)
                || key.StartsWith("Anchors/CardDeckAnchors/", StringComparison.Ordinal)
                || key.StartsWith("Anchors/HandCardAnchors/", StringComparison.Ordinal);
        }

        private static void RegisterScopedGroup(
            PerformanceDebugViewRegistry registry,
            Transform group,
            string pathPrefix,
            string scope,
            bool registerBareNames)
        {
            if (group == null)
            {
                return;
            }

            registry.RegisterAnchor(pathPrefix, group);
            registry.RegisterAnchor(scope, group, overwrite: false);

            for (var i = 0; i < group.childCount; i++)
            {
                Transform child = group.GetChild(i);
                string path = $"{pathPrefix}/{child.name}";
                registry.RegisterAnchor(path, child);
                registry.RegisterAnchor($"{scope}.{child.name}", child);
                if (registerBareNames)
                {
                    registry.RegisterAnchor(child.name, child, overwrite: true);
                }
            }
        }

        private static void IndexRecursive(PerformanceDebugViewRegistry registry, Transform root, string prefix)
        {
            if (root == null)
            {
                return;
            }

            registry.RegisterAnchor(prefix, root, overwrite: false);
            for (var i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                string childPath = $"{prefix}/{child.name}";
                registry.RegisterAnchor(childPath, child, overwrite: false);
                IndexRecursive(registry, child, childPath);
            }
        }

        internal static Transform FindChildRecursive(Transform root, string name)
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

        public static bool TryReindexActiveScene(string sceneName, PerformanceDebugViewRegistry registry)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name != sceneName)
            {
                return false;
            }

            if (!TryResolveStagingRoots(
                    out Transform anchorsRoot,
                    out Transform panelsRoot,
                    out Transform nineGridAnchors,
                    out Transform cardDeckAnchors,
                    out Transform handCardAnchors,
                    out Transform panelsAnchor,
                    out _,
                    out _))
            {
                return false;
            }

            Reindex(
                registry,
                anchorsRoot,
                panelsRoot,
                nineGridAnchors,
                cardDeckAnchors,
                handCardAnchors,
                panelsAnchor);

            return true;
        }
    }
}

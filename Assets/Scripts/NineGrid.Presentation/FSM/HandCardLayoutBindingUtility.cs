using System;
using UnityEngine;

namespace NineGrid.Presentation.FSM
{
    /// <summary>
    /// 运行时解析 HandCardAnchors / HandCardActors，供 ViewRegistry 与交互 FSM 共用。
    /// </summary>
    internal static class HandCardLayoutBindingUtility
    {
        private const int MaxReferenceAnchors = 5;
        private const string HandCardAnchorsName = "HandCardAnchors";
        private const string HandcardApplyZoneName = "HandcardApplyZone";

        public static bool TryResolve(Transform searchRoot, out Transform handRoot, out Transform[] referenceAnchors)
        {
            handRoot = null;
            referenceAnchors = Array.Empty<Transform>();

            Transform anchorsRoot = FindHandCardAnchorsRoot(searchRoot);
            if (anchorsRoot == null)
            {
                return false;
            }

            handRoot = FindChildByName(anchorsRoot, "HandCardActors") ?? anchorsRoot;
            referenceAnchors = CollectReferenceAnchors(anchorsRoot);
            return referenceAnchors.Length > 0;
        }

        public static bool TryResolveApplyZoneCollider(Transform searchRoot, out Collider applyZoneCollider)
        {
            applyZoneCollider = null;
            Transform anchorsRoot = FindHandCardAnchorsRoot(searchRoot);
            if (anchorsRoot == null)
            {
                return false;
            }

            Transform zone = FindChildByName(anchorsRoot, HandcardApplyZoneName);
            if (zone == null)
            {
                return false;
            }

            applyZoneCollider = zone.GetComponent<Collider>();
            return applyZoneCollider != null;
        }

        private static Transform FindHandCardAnchorsRoot(Transform searchRoot)
        {
            Transform found = FindHandCardAnchorsInHierarchy(searchRoot);
            if (found != null)
            {
                return found;
            }

            // NineGrid Battle 与 HandCardAnchors 常为场景兄弟节点，子树搜索会失败。
            GameObject sceneObject = GameObject.Find(HandCardAnchorsName);
            return sceneObject != null ? sceneObject.transform : null;
        }

        private static Transform FindHandCardAnchorsInHierarchy(Transform searchRoot)
        {
            if (searchRoot == null)
            {
                return null;
            }

            if (searchRoot.name == HandCardAnchorsName)
            {
                return searchRoot;
            }

            Transform[] roots = searchRoot.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < roots.Length; i++)
            {
                Transform candidate = roots[i];
                if (candidate != null && candidate.name == HandCardAnchorsName)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static Transform[] CollectReferenceAnchors(Transform anchorsRoot)
        {
            var anchors = new Transform[MaxReferenceAnchors];
            var found = 0;

            for (var slot = 1; slot <= MaxReferenceAnchors; slot++)
            {
                Transform anchor = FindChildByName(anchorsRoot, "handcard" + slot);
                if (anchor == null)
                {
                    continue;
                }

                anchors[found++] = anchor;
            }

            if (found == 0)
            {
                for (var i = 0; i < anchorsRoot.childCount && found < MaxReferenceAnchors; i++)
                {
                    Transform child = anchorsRoot.GetChild(i);
                    if (child == null || IsNonLayoutChild(child))
                    {
                        continue;
                    }

                    anchors[found++] = child;
                }
            }

            if (found == 0)
            {
                return Array.Empty<Transform>();
            }

            if (found == anchors.Length)
            {
                return anchors;
            }

            var trimmed = new Transform[found];
            Array.Copy(anchors, trimmed, found);
            return trimmed;
        }

        private static bool IsNonLayoutChild(Transform child)
        {
            string name = child.name;
            return name == "HandCardActors"
                || name.StartsWith("Apply", StringComparison.OrdinalIgnoreCase);
        }

        private static Transform FindChildByName(Transform parent, string childName)
        {
            if (parent == null)
            {
                return null;
            }

            for (var i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child != null && child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }
    }
}

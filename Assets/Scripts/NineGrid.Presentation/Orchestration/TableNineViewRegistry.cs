using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Presentation.Bridge;
using UnityEngine;

namespace NineGrid.Presentation.Orchestration
{
    /// <summary>
    /// 生产运行时视图注册表：<c>CardUid→演员</c>、<c>SlotId→锚点</c>。
    /// </summary>
    public sealed class TableNineViewRegistry : IViewRegistry
    {
        private readonly Dictionary<int, Transform> mUidActors = new();
        private readonly Dictionary<int, Transform> mSlotAnchors = new();
        private readonly Dictionary<string, Transform> mNamedAnchors = new();

        public void ClearActors()
        {
            mUidActors.Clear();
        }

        public void ClearAll()
        {
            mUidActors.Clear();
            mSlotAnchors.Clear();
            mNamedAnchors.Clear();
        }

        public void RegisterActor(int cardUid, Transform actor)
        {
            if (cardUid <= 0 || actor == null)
            {
                return;
            }

            mUidActors[cardUid] = actor;
        }

        public void RegisterAnchor(SlotId slot, Transform anchor)
        {
            if (anchor == null || slot.IsNone)
            {
                return;
            }

            mSlotAnchors[slot.Index] = anchor;
            if (slot.IsBoardSlot)
            {
                RegisterNamedAnchor(BoardSlotAnchorKey(slot.Index), anchor, overwrite: false);
            }
        }

        public void RegisterNamedAnchor(string id, Transform anchor, bool overwrite = true)
        {
            if (string.IsNullOrEmpty(id) || anchor == null)
            {
                return;
            }

            if (!overwrite && mNamedAnchors.ContainsKey(id))
            {
                return;
            }

            mNamedAnchors[id] = anchor;
        }

        public Transform ResolveActor(int cardUid)
        {
            return mUidActors.TryGetValue(cardUid, out Transform actor) ? actor : null;
        }

        public void CopyRegisteredUids(List<int> output)
        {
            if (output == null)
            {
                return;
            }

            foreach (int uid in mUidActors.Keys)
            {
                output.Add(uid);
            }
        }

        public void ForEachActor(System.Action<int, Transform> visitor)
        {
            if (visitor == null)
            {
                return;
            }

            foreach (var pair in mUidActors)
            {
                visitor(pair.Key, pair.Value);
            }
        }

        public Transform ResolveAnchor(SlotId slot)
        {
            if (slot.IsNone)
            {
                return null;
            }

            if (mSlotAnchors.TryGetValue(slot.Index, out Transform anchor))
            {
                return anchor;
            }

            if (slot.IsBoardSlot)
            {
                return ResolveNamedAnchor(BoardSlotAnchorKey(slot.Index));
            }

            return null;
        }

        public Transform ResolveNamedAnchor(string id, Transform fallback = null)
        {
            if (!string.IsNullOrEmpty(id) && mNamedAnchors.TryGetValue(id, out Transform anchor))
            {
                return anchor;
            }

            if (!string.IsNullOrEmpty(id) && !id.Contains("."))
            {
                string[] scoped = { $"grid.{id}", $"deck.{id}", $"hand.{id}", $"panel.{id}" };
                for (var i = 0; i < scoped.Length; i++)
                {
                    if (mNamedAnchors.TryGetValue(scoped[i], out anchor))
                    {
                        return anchor;
                    }
                }
            }

            return fallback;
        }

        public void IndexFromScene(SceneStagingRoots roots)
        {
            ClearAll();
            if (roots == null || !roots.IsValid)
            {
                return;
            }

            IndexRecursive(roots.AnchorsRoot, "Anchors");
            RegisterScopedGroup(roots.NineGridAnchors, "Anchors/NineGridAnchors", "grid", registerBareNames: true);
            RegisterScopedGroup(roots.CardDeckAnchors, "Anchors/CardDeckAnchors", "deck", registerBareNames: false);
            RegisterScopedGroup(roots.HandCardAnchors, $"Anchors/{roots.HandCardAnchors.name}", "hand", registerBareNames: false);
            RegisterScopedGroup(roots.PanelsAnchor, "Anchors/PanelsAnchor", "panel", registerBareNames: false);
            RegisterBoardSlotAnchors(roots.NineGridAnchors);
        }

        private void RegisterBoardSlotAnchors(Transform nineGridAnchors)
        {
            if (nineGridAnchors == null)
            {
                return;
            }

            for (var i = 1; i <= SlotId.MaxBoardIndex; i++)
            {
                string slotName = i == 5 ? "slot5_Player" : $"slot{i}";
                Transform anchor = nineGridAnchors.Find(slotName) ?? nineGridAnchors.Find($"slot{i}");
                if (anchor != null)
                {
                    RegisterAnchor(SlotId.Board(i), anchor);
                }
            }
        }

        private void RegisterScopedGroup(
            Transform group,
            string pathPrefix,
            string scope,
            bool registerBareNames)
        {
            if (group == null)
            {
                return;
            }

            RegisterNamedAnchor(pathPrefix, group);
            RegisterNamedAnchor(scope, group, overwrite: false);

            for (var i = 0; i < group.childCount; i++)
            {
                Transform child = group.GetChild(i);
                string path = $"{pathPrefix}/{child.name}";
                RegisterNamedAnchor(path, child);
                RegisterNamedAnchor($"{scope}.{child.name}", child);
                if (registerBareNames)
                {
                    RegisterNamedAnchor(child.name, child, overwrite: true);
                }
            }
        }

        private void IndexRecursive(Transform root, string prefix)
        {
            if (root == null)
            {
                return;
            }

            RegisterNamedAnchor(prefix, root, overwrite: false);
            for (var i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                string childPath = $"{prefix}/{child.name}";
                RegisterNamedAnchor(childPath, child, overwrite: false);
                IndexRecursive(child, childPath);
            }
        }

        public static string BoardSlotAnchorKey(int boardIndex)
        {
            return boardIndex == 5 ? "grid.slot5_Player" : $"grid.slot{boardIndex}";
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Presentation.Debugging
{
    public sealed class PerformanceDebugViewRegistry
    {
        private readonly Dictionary<string, Transform> anchors = new();
        private readonly Dictionary<string, Transform> actors = new();

        public IReadOnlyDictionary<string, Transform> Actors => actors;
        public IReadOnlyDictionary<string, Transform> Anchors => anchors;

        public void ClearActors()
        {
            actors.Clear();
        }

        public void ClearAll()
        {
            actors.Clear();
            anchors.Clear();
        }

        public void RegisterActor(string id, Transform transform)
        {
            if (string.IsNullOrEmpty(id) || transform == null)
            {
                return;
            }

            actors[id] = transform;
        }

        public void RegisterAnchor(string id, Transform transform, bool overwrite = true)
        {
            if (string.IsNullOrEmpty(id) || transform == null)
            {
                return;
            }

            if (!overwrite && anchors.ContainsKey(id))
            {
                return;
            }

            anchors[id] = transform;
        }

        public bool TryGetActor(string id, out Transform transform)
        {
            return actors.TryGetValue(id, out transform);
        }

        public bool TryGetAnchor(string id, out Transform transform)
        {
            return anchors.TryGetValue(id, out transform);
        }

        public Transform ResolveActor(string id, Transform fallback = null)
        {
            return TryGetActor(id, out Transform actor) ? actor : fallback;
        }

        public Transform ResolveAnchor(string id, Transform fallback = null)
        {
            if (TryGetAnchor(id, out Transform anchor))
            {
                return anchor;
            }

            if (!string.IsNullOrEmpty(id) && !id.Contains("."))
            {
                string[] scoped = { $"grid.{id}", $"deck.{id}", $"hand.{id}", $"panel.{id}" };
                for (var i = 0; i < scoped.Length; i++)
                {
                    if (TryGetAnchor(scoped[i], out anchor))
                    {
                        return anchor;
                    }
                }
            }

            return fallback;
        }

        public IReadOnlyList<string> GetAnchorKeysSorted()
        {
            var keys = new List<string>(anchors.Keys);
            keys.Sort();
            return keys;
        }

        public string DescribeAnchor(string id)
        {
            if (!TryGetAnchor(id, out Transform anchor) || anchor == null)
            {
                return id;
            }

            Vector3 position = anchor.position;
            return $"{id} @ ({position.x:0.##}, {position.y:0.##})";
        }

        public void IndexAnchorsUnder(Transform root, string prefix = null)
        {
            if (root == null)
            {
                return;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                string id = string.IsNullOrEmpty(prefix) ? child.name : $"{prefix}/{child.name}";
                RegisterAnchor(id, child);
                RegisterAnchor(child.name, child);
            }
        }
    }
}

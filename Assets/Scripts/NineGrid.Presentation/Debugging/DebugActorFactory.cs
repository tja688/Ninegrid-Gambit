using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Presentation.Debugging
{
    public sealed class DebugActorFactory
    {
        private readonly Transform actorsRoot;
        private readonly GameObject cardPrefab;
        private readonly List<GameObject> spawned = new();
        private int spawnCounter;

        public DebugActorFactory(Transform actorsRoot, GameObject cardPrefab)
        {
            this.actorsRoot = actorsRoot;
            this.cardPrefab = cardPrefab;
        }

        public GameObject CardPrefab => cardPrefab;

        public Transform Spawn(string debugId, Vector3 worldPosition, Quaternion rotation, Transform parent = null)
        {
            Transform parentTransform = parent != null ? parent : actorsRoot;
            GameObject instance = cardPrefab != null
                ? Object.Instantiate(cardPrefab, worldPosition, rotation, parentTransform)
                : CreateFallbackCard(worldPosition, rotation, parentTransform);

            string name = string.IsNullOrEmpty(debugId) ? $"DebugActor_{++spawnCounter:000}" : debugId;
            instance.name = name;
            spawned.Add(instance);
            return instance.transform;
        }

        public Transform SpawnAtAnchor(string debugId, Transform anchor, bool useLocalSpace = false)
        {
            if (anchor == null)
            {
                Debug.LogWarning($"[PerformanceDebug] Missing anchor for '{debugId}', spawning at origin.");
                return Spawn(debugId, Vector3.zero, Quaternion.identity);
            }

            if (useLocalSpace)
            {
                Transform actor = Spawn(debugId, anchor.position, anchor.rotation, anchor.parent);
                actor.localPosition = anchor.localPosition;
                actor.localRotation = anchor.localRotation;
                actor.localScale = Vector3.one;
                return actor;
            }

            return Spawn(debugId, anchor.position, anchor.rotation);
        }

        public void DestroyAll()
        {
            for (var i = spawned.Count - 1; i >= 0; i--)
            {
                GameObject actor = spawned[i];
                if (actor != null)
                {
                    Object.Destroy(actor);
                }
            }

            spawned.Clear();
            spawnCounter = 0;
        }

        private static GameObject CreateFallbackCard(Vector3 worldPosition, Quaternion rotation, Transform parent)
        {
            var go = new GameObject("DebugActor_Fallback");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(worldPosition, rotation);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateFallbackSprite();
            renderer.sortingOrder = 10;
            return go;
        }

        private static Sprite fallbackSprite;

        private static Sprite CreateFallbackSprite()
        {
            if (fallbackSprite != null)
            {
                return fallbackSprite;
            }

            var texture = new Texture2D(4, 6, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
            };

            Color fill = new(0.35f, 0.55f, 0.85f, 1f);
            for (var y = 0; y < texture.height; y++)
            {
                for (var x = 0; x < texture.width; x++)
                {
                    texture.SetPixel(x, y, fill);
                }
            }

            texture.Apply();
            fallbackSprite = Sprite.Create(texture, new Rect(0, 0, 4, 6), new Vector2(0.5f, 0.5f), 16f);
            return fallbackSprite;
        }
    }
}

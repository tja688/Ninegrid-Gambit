using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Presentation.Interaction;
using NineGrid.Presentation.Visuals;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Orchestration
{
    /// <summary>
    /// 生产卡演员工厂：对象池、defId→视觉、生命周期。
    /// </summary>
    public sealed class TableNineActorFactory : IActorFactory
    {
        private readonly Transform mActorsRoot;
        private readonly GameObject mCardPrefab;
        private readonly TableNineViewRegistry mViewRegistry;
        private readonly IArchitecture mArchitecture;
        private readonly Queue<GameObject> mPool = new();
        private readonly Dictionary<int, Transform> mUidActors = new();
        private readonly List<GameObject> mActiveInstances = new();
        private int mSpawnCounter;

        public TableNineActorFactory(
            Transform actorsRoot,
            GameObject cardPrefab,
            TableNineViewRegistry viewRegistry,
            IArchitecture architecture)
        {
            mActorsRoot = actorsRoot;
            mCardPrefab = cardPrefab;
            mViewRegistry = viewRegistry;
            mArchitecture = architecture;
        }

        public Transform Spawn(string defId, int cardUid, Transform parent = null)
        {
            Transform parentTransform = parent != null ? parent : mActorsRoot;
            GameObject instance = AcquireInstance(parentTransform);
            string name = string.IsNullOrEmpty(defId) ? $"Card_{cardUid}" : $"{defId}#{cardUid}";
            instance.name = name;
            instance.SetActive(true);

            ApplyVisual(instance.transform, defId);
            EnsureCardStatusView(instance.transform);
            EnsureActorBinding(instance, cardUid);

            mUidActors[cardUid] = instance.transform;
            mViewRegistry?.RegisterActor(cardUid, instance.transform);
            if (!mActiveInstances.Contains(instance))
            {
                mActiveInstances.Add(instance);
            }

            return instance.transform;
        }

        public Transform SpawnAtAnchor(string defId, int cardUid, Transform anchor, bool useLocalSpace = false)
        {
            Transform actor = Spawn(defId, cardUid);
            PlaceAtAnchor(actor, anchor, useLocalSpace);
            return actor;
        }

        public static void PlaceAtAnchor(Transform actor, Transform anchor, bool useLocalSpace = false)
        {
            if (actor == null || anchor == null)
            {
                return;
            }

            if (useLocalSpace)
            {
                actor.SetParent(anchor.parent, false);
                actor.localPosition = anchor.localPosition;
                actor.localRotation = anchor.localRotation;
                actor.localScale = Vector3.one;
                return;
            }

            actor.SetPositionAndRotation(anchor.position, anchor.rotation);
        }

        public void Despawn(int cardUid)
        {
            if (!mUidActors.TryGetValue(cardUid, out Transform actor))
            {
                return;
            }

            mUidActors.Remove(cardUid);
            if (actor == null)
            {
                return;
            }

            ReleaseInstance(actor.gameObject);
        }

        public void DespawnExcept(IReadOnlyCollection<int> keepUids)
        {
            var keep = keepUids ?? new int[0];
            var toRemove = new List<int>();
            foreach (var pair in mUidActors)
            {
                bool shouldKeep = false;
                foreach (int uid in keep)
                {
                    if (pair.Key == uid)
                    {
                        shouldKeep = true;
                        break;
                    }
                }

                if (!shouldKeep && IsHandItemActor(pair.Value))
                {
                    continue;
                }

                if (!shouldKeep)
                {
                    toRemove.Add(pair.Key);
                }
            }

            for (var i = 0; i < toRemove.Count; i++)
            {
                Despawn(toRemove[i]);
            }
        }

        private static bool IsHandItemActor(Transform actor)
        {
            if (actor == null)
            {
                return false;
            }

            TableNineActorBinding binding = actor.GetComponent<TableNineActorBinding>();
            return binding != null && binding.IsHandItem;
        }

        public bool TryGet(int cardUid, out Transform actor)
        {
            return mUidActors.TryGetValue(cardUid, out actor);
        }

        public void DestroyAll()
        {
            for (var i = mActiveInstances.Count - 1; i >= 0; i--)
            {
                GameObject instance = mActiveInstances[i];
                if (instance != null)
                {
                    Object.Destroy(instance);
                }
            }

            mActiveInstances.Clear();
            mUidActors.Clear();
            while (mPool.Count > 0)
            {
                Object.Destroy(mPool.Dequeue());
            }

            mSpawnCounter = 0;
        }

        private GameObject AcquireInstance(Transform parent)
        {
            while (mPool.Count > 0)
            {
                GameObject pooled = mPool.Dequeue();
                if (pooled != null)
                {
                    pooled.transform.SetParent(parent, false);
                    return pooled;
                }
            }

            if (mCardPrefab != null)
            {
                return Object.Instantiate(mCardPrefab, parent);
            }

            return CreateFallbackCard(parent, ++mSpawnCounter);
        }

        private void ReleaseInstance(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            mActiveInstances.Remove(instance);
            instance.SetActive(false);
            instance.transform.SetParent(mActorsRoot, false);
            mPool.Enqueue(instance);
        }

        private void ApplyVisual(Transform actor, string defId)
        {
            if (string.IsNullOrEmpty(defId) || mArchitecture == null)
            {
                return;
            }

            TableNineCardVisualBinder.TryApplyForDefId(mArchitecture, actor, defId);
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

        private static void EnsureActorBinding(GameObject instance, int cardUid)
        {
            if (instance == null || cardUid <= 0)
            {
                return;
            }

            TableNineActorBinding binding = instance.GetComponent<TableNineActorBinding>();
            if (binding == null)
            {
                binding = instance.AddComponent<TableNineActorBinding>();
            }

            binding.Bind(cardUid);
            Interaction.InteractionColliderUtility.EnsureCollider2D(instance);
        }

        private static GameObject CreateFallbackCard(Transform parent, int counter)
        {
            var go = new GameObject($"FallbackCard_{counter:000}");
            go.transform.SetParent(parent, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateFallbackSprite();
            renderer.sortingOrder = 10;
            return go;
        }

        private static Sprite sFallbackSprite;

        private static Sprite CreateFallbackSprite()
        {
            if (sFallbackSprite != null)
            {
                return sFallbackSprite;
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
            sFallbackSprite = Sprite.Create(texture, new Rect(0, 0, 4, 6), new Vector2(0.5f, 0.5f), 16f);
            return sFallbackSprite;
        }
    }
}

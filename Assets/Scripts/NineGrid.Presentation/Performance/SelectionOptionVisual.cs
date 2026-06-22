using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace NineGrid.Presentation.Performance
{
    /// <summary>
    /// 选择层选项卡的排序与占位卡生成工具。
    /// </summary>
    public static class SelectionOptionVisual
    {
        private static Sprite fallbackPreviewSprite;

        public static void ApplySortingOrder(Transform root, int order)
        {
            if (root == null)
            {
                return;
            }

            var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                renderers[i].sortingOrder = order;
            }
        }

        public static Transform CreatePreviewCard(
            Transform parent,
            int index,
            GameObject cardPrefab,
            Vector3 localPosition,
            float localRotationZ,
            int sortingOrder)
        {
            Transform actor;
            if (cardPrefab != null)
            {
                var instance = Object.Instantiate(cardPrefab, parent);
                instance.name = $"SelectionOption_{index + 1}";
                actor = instance.transform;
            }
            else
            {
                var stub = new GameObject($"SelectionOption_{index + 1}");
                stub.transform.SetParent(parent, false);
                var spriteRenderer = stub.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = GetFallbackPreviewSprite();
                actor = stub.transform;
            }

            actor.localPosition = localPosition;
            actor.localRotation = Quaternion.Euler(0f, 0f, localRotationZ);
            actor.localScale = Vector3.one;
            ApplySortingOrder(actor, sortingOrder);
            return actor;
        }

        public static void DestroyActors(IList<Transform> actors)
        {
            for (var i = actors.Count - 1; i >= 0; i--)
            {
                Transform actor = actors[i];
                if (actor == null)
                {
                    continue;
                }

                actor.DOKill();
                Object.Destroy(actor.gameObject);
            }

            actors.Clear();
        }

        private static Sprite GetFallbackPreviewSprite()
        {
            if (fallbackPreviewSprite != null)
            {
                return fallbackPreviewSprite;
            }

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, new Color(0.92f, 0.88f, 0.78f, 1f));
            texture.Apply();
            fallbackPreviewSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                100f);
            return fallbackPreviewSprite;
        }
    }
}

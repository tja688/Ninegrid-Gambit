using UnityEngine;

namespace NineGrid.Presentation.Interaction
{
    internal static class InteractionColliderUtility
    {
        public static Collider2D EnsureCollider2D(GameObject go)
        {
            if (go == null)
            {
                return null;
            }

            Collider2D existing = go.GetComponent<Collider2D>();
            if (existing != null)
            {
                return existing;
            }

            var box = go.AddComponent<BoxCollider2D>();
            SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
            if (renderer != null && renderer.sprite != null)
            {
                box.size = renderer.sprite.bounds.size;
            }
            else
            {
                box.size = new Vector2(1f, 1.5f);
            }

            return box;
        }
    }
}

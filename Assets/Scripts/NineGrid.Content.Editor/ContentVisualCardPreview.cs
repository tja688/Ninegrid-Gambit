using NineGrid.Content;
using NineGrid.Presentation.Visuals;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    public sealed class ContentVisualCardPreview : System.IDisposable
    {
        private const string StandardCardPrefabPath = "Assets/Prefabs/Standard Card.prefab";
        private const string FrameChild = "Card Frame ";

        private PreviewRenderUtility previewUtility;
        private GameObject previewRoot;
        private GameObject prefabAsset;
        private bool disposed;

        public void Draw(Rect rect, ContentVisualResolvedView view)
        {
            if (rect.width <= 1f || rect.height <= 1f)
            {
                return;
            }

            EnsurePreviewRoot();
            ApplyResolvedView(view);

            previewUtility.BeginPreview(rect, GUIStyle.none);
            previewUtility.camera.transform.position = new Vector3(0f, 0f, -6f);
            previewUtility.camera.transform.rotation = Quaternion.identity;
            previewUtility.camera.orthographic = true;
            previewUtility.camera.orthographicSize = 2.2f;
            previewUtility.camera.backgroundColor = new Color(0.09f, 0.075f, 0.06f, 1f);
            previewUtility.lights[0].intensity = 1.1f;
            previewUtility.lights[0].transform.rotation = Quaternion.Euler(30f, 30f, 0f);
            previewUtility.Render();
            var texture = previewUtility.EndPreview();
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (previewRoot != null)
            {
                Object.DestroyImmediate(previewRoot);
                previewRoot = null;
            }

            if (previewUtility != null)
            {
                previewUtility.Cleanup();
                previewUtility = null;
            }
        }

        private void EnsurePreviewRoot()
        {
            if (previewUtility != null && previewRoot != null)
            {
                return;
            }

            previewUtility = new PreviewRenderUtility();
            prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(StandardCardPrefabPath);
            if (prefabAsset == null)
            {
                return;
            }

            previewRoot = previewUtility.InstantiatePrefabInScene(prefabAsset);
            previewRoot.transform.position = Vector3.zero;
            previewRoot.transform.rotation = Quaternion.identity;
            previewRoot.hideFlags = HideFlags.HideAndDontSave;
        }

        private void ApplyResolvedView(ContentVisualResolvedView view)
        {
            if (previewRoot == null)
            {
                return;
            }

            Sprite iconSprite;
            Sprite faceSprite;
            ContentVisualSpriteLoader.TryLoad(
                view?.IconVisualId,
                view?.IconAssetKey,
                out iconSprite);
            ContentVisualSpriteLoader.TryLoad(
                view?.FaceVisualId,
                view?.FaceAssetKey,
                out faceSprite);

            SetSpriteOnChild(previewRoot.transform, "MainIcon", iconSprite);
            SetSpriteOnChild(previewRoot.transform, "Standard Card", faceSprite);
            SetFrameColor(previewRoot.transform, view != null ? view.FrameColor : ContentColor.White);
        }

        private static void SetFrameColor(Transform root, ContentColor color)
        {
            var child = FindChildRecursive(root, FrameChild);
            if (child == null)
            {
                return;
            }

            var renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                return;
            }

            renderer.color = new Color(color.R, color.G, color.B, color.A);
            renderer.enabled = true;
        }

        private static void SetSpriteOnChild(Transform root, string childName, Sprite sprite)
        {
            if (root == null)
            {
                return;
            }

            var child = FindChildRecursive(root, childName);
            if (child == null)
            {
                return;
            }

            var renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                return;
            }

            if (sprite != null)
            {
                renderer.sprite = sprite;
                renderer.enabled = true;
            }
        }

        private static Transform FindChildRecursive(Transform parent, string childName)
        {
            if (parent.name == childName)
            {
                return parent;
            }

            for (var i = 0; i < parent.childCount; i++)
            {
                var match = FindChildRecursive(parent.GetChild(i), childName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }
    }
}

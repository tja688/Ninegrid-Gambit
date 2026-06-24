using System;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    public sealed class ContentVisualCardPreview : IDisposable
    {
        private const string StandardCardPrefabPath = "Assets/Prefabs/Standard Card.prefab";

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
                UnityEngine.Object.DestroyImmediate(previewRoot);
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
            Sprite frameSprite;
            ContentVisualSpriteKeyCodec.TryDecode(view?.IconKey, out iconSprite);
            ContentVisualSpriteKeyCodec.TryDecode(view?.FaceKey, out faceSprite);
            ContentVisualSpriteKeyCodec.TryDecode(view?.FrameKey, out frameSprite);

            SetSpriteOnChild(previewRoot.transform, "MainIcon", iconSprite);
            SetSpriteOnChild(previewRoot.transform, "Standard Card", faceSprite);
            SetSpriteOnChild(previewRoot.transform, "Card Frame ", frameSprite);
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

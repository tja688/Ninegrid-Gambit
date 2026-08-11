#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using NineGrid.Cards;

namespace NineGrid.Presentation.Editor
{
    /// <summary>
    /// 编辑器内嵌预览宿主：PreviewRenderUtility 渲染底盘+L4 终态，不污染场景选择。
    /// </summary>
    public sealed class CardFacePreviewHost : IDisposable
    {
        private PreviewRenderUtility _previewUtility;
        private CardFacePreviewBuilder.BuildResult _build;
        private string _status = "就绪";
        private readonly List<string> _warnings = new List<string>();
        private bool _disposed;
        private Bounds _framedBounds;

        public string Status => _status;
        public IReadOnlyList<string> Warnings => _warnings;
        public GameObject PreviewRoot => _build?.Root;
        public CardFacePreviewBuilder.BuildResult Build => _build;
        public Camera PreviewCamera => _previewUtility != null ? _previewUtility.camera : null;
        public Bounds FramedBounds => _framedBounds;

        public bool Rebuild(CardFacePreviewRequest request)
        {
            DestroyBuildOnly();
            _warnings.Clear();

            if (request == null)
            {
                _status = "预览请求为空。";
                return false;
            }

            EnsurePreviewUtility();
            if (!CardFacePreviewBuilder.TryBuild(request, out _build, out var error))
            {
                _status = error ?? "预览构建失败。";
                return false;
            }

            _warnings.AddRange(_build.SortingWarnings);
            _previewUtility.AddSingleGO(_build.Root);
            FrameCameraOnRoot();
            _status = "已预览 DefId=" + request.DefId + " Kind=" + request.Kind + "（底盘 + L4 + ApplyPresentation）。";
            return true;
        }

        public void ValidateSortingOrders()
        {
            _warnings.Clear();
            if (_build?.Root == null)
            {
                _status = "无预览实例，请先重建。";
                return;
            }

            CardFacePreviewBuilder.CollectSortingWarnings(_build.Root.transform, _warnings);
            _status = _warnings.Count == 0
                ? "通层 sortingOrder 无重复。"
                : "发现 " + _warnings.Count + " 处通层 sortingOrder 重复。";
        }

        /// <summary>调试：把当前预览根亮到 SceneView（Face Final 薄壳用）。</summary>
        public void FocusInSceneView()
        {
            if (_build?.Root == null)
            {
                return;
            }

            _build.Root.hideFlags = HideFlags.DontSave;
            Selection.activeGameObject = _build.Root;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        public void Draw(Rect rect)
        {
            if (_disposed)
            {
                return;
            }

            EditorGUI.DrawRect(rect, new Color(0.09f, 0.075f, 0.06f, 1f));
            if (_build?.Root == null || _previewUtility == null)
            {
                EditorGUI.LabelField(rect, _status, EditorStyles.centeredGreyMiniLabel);
                return;
            }

            if (rect.width < 8f || rect.height < 8f)
            {
                return;
            }

            _previewUtility.BeginPreview(rect, GUIStyle.none);
            _previewUtility.camera.Render();
            var texture = _previewUtility.EndPreview();
            if (texture != null)
            {
                GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
            }
        }

        /// <summary>离屏渲染当前预览为 PNG（loopback 工作台等非 IMGUI 消费方用）。</summary>
        public bool TryRenderStaticPng(int width, int height, out byte[] png)
        {
            png = null;
            if (_disposed || _build?.Root == null || _previewUtility == null)
            {
                return false;
            }

            width = Mathf.Clamp(width, 8, 2048);
            height = Mathf.Clamp(height, 8, 2048);
            _previewUtility.BeginStaticPreview(new Rect(0f, 0f, width, height));
            _previewUtility.camera.Render();
            var texture = _previewUtility.EndStaticPreview();
            if (texture == null)
            {
                return false;
            }

            try
            {
                png = texture.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            return png != null && png.Length > 0;
        }

        /// <summary>
        /// 将预览相机世界坐标映射到 <see cref="Draw"/> 所用的 GUI 矩形（假定 StretchToFill / RT 与 rect 同宽高比）。
        /// </summary>
        public bool TryWorldToGui(Rect drawRect, Vector3 world, out Vector2 gui)
        {
            gui = default;
            var cam = PreviewCamera;
            if (cam == null || drawRect.width < 1f || drawRect.height < 1f)
                return false;

            float viewH = cam.orthographicSize * 2f;
            float viewW = viewH * (drawRect.width / drawRect.height);
            Vector3 camPos = cam.transform.position;

            float nx = (world.x - camPos.x) / viewW + 0.5f;
            float ny = (world.y - camPos.y) / viewH + 0.5f;
            gui = new Vector2(
                drawRect.x + nx * drawRect.width,
                drawRect.yMax - ny * drawRect.height);
            return true;
        }

        public bool TryWorldBoundsToGui(Rect drawRect, Bounds worldBounds, out Rect guiRect)
        {
            guiRect = default;
            if (!TryWorldToGui(drawRect, worldBounds.min, out Vector2 a) ||
                !TryWorldToGui(drawRect, worldBounds.max, out Vector2 b))
                return false;

            float xMin = Math.Min(a.x, b.x);
            float xMax = Math.Max(a.x, b.x);
            float yMin = Math.Min(a.y, b.y);
            float yMax = Math.Max(a.y, b.y);
            guiRect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            return true;
        }

        public Transform FindChild(string name)
        {
            if (_build?.Root == null || string.IsNullOrEmpty(name))
                return null;
            return FindChildRecursive(_build.Root.transform, name);
        }

        public SpriteRenderer FindSpriteRenderer(string name)
        {
            Transform t = FindChild(name);
            return t != null ? t.GetComponent<SpriteRenderer>() : null;
        }

        public void Reframe()
        {
            FrameCameraOnRoot();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            DestroyBuildOnly();
            if (_previewUtility != null)
            {
                _previewUtility.Cleanup();
                _previewUtility = null;
            }
        }

        private void EnsurePreviewUtility()
        {
            if (_previewUtility != null)
            {
                return;
            }

            _previewUtility = new PreviewRenderUtility();
            _previewUtility.camera.orthographic = true;
            _previewUtility.camera.nearClipPlane = 0.01f;
            _previewUtility.camera.farClipPlane = 50f;
            _previewUtility.camera.clearFlags = CameraClearFlags.SolidColor;
            _previewUtility.camera.backgroundColor = new Color(0.09f, 0.075f, 0.06f, 1f);
            if (_previewUtility.lights != null && _previewUtility.lights.Length > 0)
            {
                _previewUtility.lights[0].intensity = 1.2f;
                _previewUtility.lights[0].transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            }
        }

        private void FrameCameraOnRoot()
        {
            if (_previewUtility == null || _build?.Root == null)
            {
                return;
            }

            _framedBounds = CalculateBounds(_build.Root);
            var cam = _previewUtility.camera;
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(_framedBounds.extents.y, _framedBounds.extents.x * 0.75f) * 1.15f;
            if (cam.orthographicSize < 0.6f)
            {
                cam.orthographicSize = 1.2f;
            }

            cam.transform.position = _framedBounds.center + new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return new Bounds(root.transform.position, Vector3.one * 2f);
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].enabled)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            return bounds;
        }

        private static Transform FindChildRecursive(Transform root, string name)
        {
            if (root.name == name)
                return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildRecursive(root.GetChild(i), name);
                if (found != null)
                    return found;
            }

            return null;
        }

        private void DestroyBuildOnly()
        {
            CardFacePreviewBuilder.DestroyBuild(_build);
            _build = null;
        }
    }
}
#endif

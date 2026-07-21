#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Cards.Editor
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

        public string Status => _status;
        public IReadOnlyList<string> Warnings => _warnings;
        public GameObject PreviewRoot => _build?.Root;
        public CardFacePreviewBuilder.BuildResult Build => _build;

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
                GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, false);
            }
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

            var bounds = CalculateBounds(_build.Root);
            var cam = _previewUtility.camera;
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x * 0.75f) * 1.15f;
            if (cam.orthographicSize < 0.6f)
            {
                cam.orthographicSize = 1.2f;
            }

            cam.transform.position = bounds.center + new Vector3(0f, 0f, -10f);
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

        private void DestroyBuildOnly()
        {
            CardFacePreviewBuilder.DestroyBuild(_build);
            _build = null;
        }
    }
}
#endif

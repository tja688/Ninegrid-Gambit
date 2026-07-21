#if UNITY_EDITOR
using System.Collections.Generic;
using NineGrid.Content;
using NineGrid.Core.Content;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Cards.Editor
{
    /// <summary>
    /// 薄壳调试窗：共享 <see cref="CardFacePreviewBuilder"/>。
    /// 内容装配权威预览在 TableNine/Content/Content Visual Editor。
    /// </summary>
    public sealed class CardFaceFinalPreviewWindow : EditorWindow
    {
        private string _defId = "help.fireball";
        private bool _useManualOverride;
        private Sprite _overrideMainIcon;
        private Sprite _overrideFaceBackground;
        private Sprite _overrideBackBorder;
        private Sprite _overrideBackShirt;
        private Sprite _overrideBackLogo;

        private CardFacePreviewBuilder.BuildResult _build;
        private Vector2 _scroll;
        private string _status = "就绪（调试薄壳；权威预览见 Content Visual Editor）";
        private readonly List<string> _warnings = new List<string>();

        [MenuItem("NineGrid/Cards/Face Final Preview (Chassis + L4)")]
        public static void Open()
        {
            var window = GetWindow<CardFaceFinalPreviewWindow>();
            window.titleContent = new GUIContent("卡面终态预览");
            window.minSize = new Vector2(420f, 520f);
            window.Show();
        }

        private void OnDisable()
        {
            DestroyPreview();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.HelpBox(
                "调试薄壳：与 Content Visual Editor 共用 CardFacePreviewBuilder（ApplyPresentation）。内容配图请用 TableNine/Content/Content Visual Editor。",
                MessageType.None);

            EditorGUILayout.LabelField("假投影来源", EditorStyles.boldLabel);
            _defId = EditorGUILayout.TextField(
                new GUIContent("DefId", "默认从 ContentCatalog / Content Visual 解析；可改。"),
                _defId);

            _useManualOverride = EditorGUILayout.Toggle(
                new GUIContent("手填覆盖直暴露槽", "开启后以下 Sprite 覆盖 Catalog；关闭则用 DefId 解析。"),
                _useManualOverride);

            using (new EditorGUI.DisabledScope(!_useManualOverride))
            {
                _overrideMainIcon = (Sprite)EditorGUILayout.ObjectField("Main_Icon", _overrideMainIcon, typeof(Sprite), false);
                _overrideFaceBackground = (Sprite)EditorGUILayout.ObjectField("Face_Background", _overrideFaceBackground, typeof(Sprite), false);
                _overrideBackBorder = (Sprite)EditorGUILayout.ObjectField("Back_Border", _overrideBackBorder, typeof(Sprite), false);
                _overrideBackShirt = (Sprite)EditorGUILayout.ObjectField("Back_Shirt", _overrideBackShirt, typeof(Sprite), false);
                _overrideBackLogo = (Sprite)EditorGUILayout.ObjectField("Back_Logo", _overrideBackLogo, typeof(Sprite), false);
            }

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("重建终态预览", GUILayout.Height(28f)))
                {
                    RebuildPreview();
                }

                if (GUILayout.Button("校验通层 sortingOrder", GUILayout.Height(28f)))
                {
                    ValidateSortingOrders();
                }
            }

            EditorGUILayout.HelpBox(_status, MessageType.Info);
            if (_warnings.Count > 0)
            {
                EditorGUILayout.HelpBox(string.Join("\n", _warnings), MessageType.Warning);
            }

            if (_build?.Root != null)
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("预览根", _build.Root.name);
                EditorGUILayout.ObjectField("Scene 实例", _build.Root, typeof(GameObject), true);
            }

            EditorGUILayout.EndScrollView();
        }

        private void RebuildPreview()
        {
            DestroyPreview();
            _warnings.Clear();

            if (!TryBuildRequest(out var request, out var note))
            {
                _status = note;
                return;
            }

            if (!CardFacePreviewBuilder.TryBuild(request, out _build, out var error))
            {
                _status = error;
                return;
            }

            _build.Root.hideFlags = HideFlags.DontSave;
            _warnings.AddRange(_build.SortingWarnings);
            Selection.activeGameObject = _build.Root;
            SceneView.lastActiveSceneView?.FrameSelected();
            _status = "已预览 DefId=" + _defId + " Kind=" + request.Kind + "（Builder + ApplyPresentation）。";
        }

        private bool TryBuildRequest(out CardFacePreviewRequest request, out string error)
        {
            request = null;
            if (!CardFacePreviewBuilder.TryResolveDefId(_defId, out var presentationKind, out var contentKind, out error))
            {
                return false;
            }

            request = new CardFacePreviewRequest
            {
                DefId = _defId,
                Kind = presentationKind,
                DisplayName = _defId,
                FaceUp = true,
            };

            if (_useManualOverride)
            {
                request.MainIcon = _overrideMainIcon;
                request.FaceBackground = _overrideFaceBackground;
                request.BackBorder = _overrideBackBorder;
                request.BackShirt = _overrideBackShirt;
                request.BackLogo = _overrideBackLogo;
            }
            else
            {
                CardFacePreviewBuilder.ApplyCatalogDirectSlots(request, contentKind);
            }

            try
            {
                var dataDirectory = ContentVisualBootstrap.ResolveLubanDataDirectory();
                var coreCatalog = TableNineLubanCatalogFactory.CreateFromDirectory(dataDirectory);
                CardFacePreviewBuilder.TryFillSampleStats(request, coreCatalog);
                if (string.IsNullOrEmpty(request.BasicDescription)
                    && TableNineVisualCatalogFactory.CreateFromDirectory(dataDirectory) is { } visual
                    && visual.TryGet(_defId, out var def)
                    && def != null)
                {
                    request.BasicDescription = def.Description ?? string.Empty;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[CardFaceFinalPreview] 样例数值/描述填充失败：" + ex.Message);
            }

            return true;
        }

        private void ValidateSortingOrders()
        {
            _warnings.Clear();
            if (_build?.Root == null)
            {
                RebuildPreview();
            }

            if (_build?.Root == null)
            {
                return;
            }

            CardFacePreviewBuilder.CollectSortingWarnings(_build.Root.transform, _warnings);
            _status = _warnings.Count == 0
                ? "通层 sortingOrder 无重复。"
                : "发现 " + _warnings.Count + " 处通层 sortingOrder 重复（见下方告警）。";
        }

        private void DestroyPreview()
        {
            CardFacePreviewBuilder.DestroyBuild(_build);
            _build = null;
        }
    }
}
#endif

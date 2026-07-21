#if UNITY_EDITOR
using System.Collections.Generic;
using NineGrid.Cards.Convergence;
using NineGrid.Cards.Slots;
using NineGrid.Content;
using NineGrid.Core.Content;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineGrid.Cards.Editor
{
    /// <summary>
    /// 编辑器权威预览：底盘 + L4 卡面终态全显示（收纳槽仍渲染）。
    /// 假投影默认取 ContentCatalog DefId，可选手填覆盖直暴露槽。
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

        private GameObject _previewRoot;
        private Vector2 _scroll;
        private string _status = "就绪";
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

            if (_previewRoot != null)
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("预览根", _previewRoot.name);
                EditorGUILayout.ObjectField("Scene 实例", _previewRoot, typeof(GameObject), true);
            }

            EditorGUILayout.EndScrollView();
        }

        private void RebuildPreview()
        {
            DestroyPreview();
            _warnings.Clear();

            var chassisPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardChassisPaths.ChassisPrefab);
            if (chassisPrefab == null)
            {
                _status = "找不到底盘：" + CardChassisPaths.ChassisPrefab;
                return;
            }

            if (!TryResolveDefId(_defId, out var presentationKind, out _, out var catalogNote))
            {
                _status = catalogNote;
                return;
            }

            var kind = presentationKind;
            var facePrefab = LoadFacePrefab(kind);
            if (facePrefab == null)
            {
                _status = "Kind=" + kind + " 无对应卡面模板（或 DefId 无法映射）。";
                return;
            }

            _previewRoot = (GameObject)PrefabUtility.InstantiatePrefab(chassisPrefab);
            _previewRoot.name = "CardFaceFinalPreview";
            _previewRoot.hideFlags = HideFlags.DontSave;

            var tower = _previewRoot.GetComponent<CardTransformTower>();
            if (tower == null)
            {
                tower = _previewRoot.AddComponent<CardTransformTower>();
            }

            tower.EnsureTower();
            var pivot = tower.FacePivot;
            if (pivot == null)
            {
                _status = "底盘缺少 FacePivot。";
                DestroyPreview();
                return;
            }

            var face = (GameObject)PrefabUtility.InstantiatePrefab(facePrefab, pivot);
            face.name = facePrefab.name;
            StripFaceSortingGroup(face);

            var templateDefaults = CardFaceSlotNodeMap.CaptureTemplateDefaults(face.transform);
            var contentOverrides = ResolveContentOverrides();
            var resolved = new Dictionary<string, Sprite>();
            foreach (var code in new[]
                     {
                         CardFaceSlotCodes.MainIcon,
                         CardFaceSlotCodes.FaceBackground,
                         CardFaceSlotCodes.BackBorder,
                         CardFaceSlotCodes.BackShirt,
                         CardFaceSlotCodes.BackLogo
                     })
            {
                var sprite = CardFaceSlotResolver.ResolveIcon(code, contentOverrides, templateDefaults);
                if (sprite != null)
                {
                    resolved[code] = sprite;
                }
            }

            CardFaceSlotNodeMap.ApplyResolvedSprites(face.transform, resolved);
            CollectSortingWarnings(_previewRoot.transform);

            Selection.activeGameObject = _previewRoot;
            SceneView.lastActiveSceneView?.FrameSelected();
            _status = "已预览 DefId=" + _defId + " Kind=" + kind + "（底盘 + L4 终态）。";
        }

        private Dictionary<string, Sprite> ResolveContentOverrides()
        {
            if (_useManualOverride)
            {
                return CardFaceSlotResolver.BuildContentOverrides(
                    _overrideMainIcon,
                    _overrideFaceBackground,
                    _overrideBackBorder,
                    _overrideBackShirt,
                    _overrideBackLogo);
            }

            if (!TryResolveDefId(_defId, out _, out var contentKind, out _) ||
                !TryLoadSpriteCatalogs(out var catalogs))
            {
                return CardFaceSlotResolver.BuildContentOverrides(null, null, null, null, null);
            }

            ContentVisualDirectSlotSprites slots;
            if (catalogs.TryGetDirectSlots(contentKind, _defId, out slots))
            {
                return CardFaceSlotResolver.BuildContentOverrides(
                    slots.MainIcon,
                    slots.FaceBackground,
                    slots.BackBorder,
                    slots.BackShirt,
                    slots.BackLogo);
            }

            return CardFaceSlotResolver.BuildContentOverrides(null, null, null, null, null);
        }

        /// <summary>
        /// 假投影默认走 ContentCatalog DefId：校验条目存在，并用 Content Visual 的 Kind。
        /// </summary>
        private static bool TryResolveDefId(
            string defId,
            out CardPresentationKind presentationKind,
            out ContentVisualKind contentKind,
            out string error)
        {
            presentationKind = CardPresentationKind.Unknown;
            contentKind = ContentVisualKind.Unknown;
            error = null;

            if (string.IsNullOrEmpty(defId))
            {
                error = "请填写 ContentCatalog DefId。";
                return false;
            }

            var dataDirectory = ContentVisualBootstrap.ResolveLubanDataDirectory();
            GameContentCatalog coreCatalog = null;
            ContentVisualCatalog visualCatalog = null;
            try
            {
                coreCatalog = TableNineLubanCatalogFactory.CreateFromDirectory(dataDirectory);
                visualCatalog = TableNineVisualCatalogFactory.CreateFromDirectory(dataDirectory);
            }
            catch (System.Exception ex)
            {
                error = "无法加载 ContentCatalog：" + ex.Message;
                return false;
            }

            ContentVisualDefinition visual = null;
            var inVisual = visualCatalog != null && visualCatalog.TryGet(defId, out visual) && visual != null;
            if (!DefIdExistsInContentCatalog(coreCatalog, defId) && !inVisual)
            {
                error = "ContentCatalog / Content Visual 中无此 DefId：" + defId;
                return false;
            }

            contentKind = inVisual ? visual.Kind : GuessContentVisualKind(defId);

            presentationKind = ToPresentationKind(contentKind, defId);
            if (presentationKind == CardPresentationKind.Unknown)
            {
                error = "DefId 在 ContentCatalog 中存在，但不映射到四套卡面：" + defId;
                return false;
            }

            return true;
        }

        private static bool DefIdExistsInContentCatalog(GameContentCatalog catalog, string defId)
        {
            if (catalog == null)
            {
                return false;
            }

            return catalog.Cards.ContainsKey(defId)
                   || catalog.Relics.ContainsKey(defId)
                   || catalog.Skills.ContainsKey(defId);
        }

        private static CardPresentationKind ToPresentationKind(ContentVisualKind kind, string defId)
        {
            switch (kind)
            {
                case ContentVisualKind.Avatar:
                    return CardPresentationKind.Avatar;
                case ContentVisualKind.Monster:
                    return CardPresentationKind.Monster;
                case ContentVisualKind.HelpCard:
                    return CardPresentationKind.HelpCard;
                case ContentVisualKind.Relic:
                    return CardPresentationKind.Relic;
                case ContentVisualKind.Skill:
                    return CardPresentationKind.Unknown;
                default:
                    return CardPresentationKindResolver.FromDefId(defId);
            }
        }

        private static bool TryLoadSpriteCatalogs(out ContentVisualSpriteCatalogSet catalogs)
        {
            catalogs = ContentVisualSpriteCatalogBootstrapSO.TryLoadCatalogSet();
            if (catalogs != null)
            {
                return true;
            }

            catalogs = new ContentVisualSpriteCatalogSet
            {
                helpCards = AssetDatabase.LoadAssetAtPath<HelpCardVisualCatalogSO>(
                    "Assets/Arts/ContentVisual/HelpCardVisualCatalog.asset"),
                monsters = AssetDatabase.LoadAssetAtPath<MonsterVisualCatalogSO>(
                    "Assets/Arts/ContentVisual/MonsterVisualCatalog.asset"),
                relics = AssetDatabase.LoadAssetAtPath<RelicVisualCatalogSO>(
                    "Assets/Arts/ContentVisual/RelicVisualCatalog.asset"),
                skills = AssetDatabase.LoadAssetAtPath<SkillVisualCatalogSO>(
                    "Assets/Arts/ContentVisual/SkillVisualCatalog.asset"),
                misc = AssetDatabase.LoadAssetAtPath<MiscVisualCatalogSO>(
                    "Assets/Arts/ContentVisual/MiscVisualCatalog.asset"),
                choiceOptions = AssetDatabase.LoadAssetAtPath<ChoiceOptionVisualCatalogSO>(
                    "Assets/Arts/ContentVisual/ChoiceOptionVisualCatalog.asset")
            };
            return catalogs.helpCards != null || catalogs.monsters != null;
        }

        private static ContentVisualKind GuessContentVisualKind(string defId)
        {
            if (defId.StartsWith("monster."))
            {
                return ContentVisualKind.Monster;
            }

            if (defId.StartsWith("relic."))
            {
                return ContentVisualKind.Relic;
            }

            if (defId.StartsWith("skill."))
            {
                return ContentVisualKind.Skill;
            }

            if (defId.StartsWith("avatar."))
            {
                return ContentVisualKind.Avatar;
            }

            return ContentVisualKind.HelpCard;
        }

        private static GameObject LoadFacePrefab(CardPresentationKind kind)
        {
            string path;
            switch (kind)
            {
                case CardPresentationKind.Avatar:
                    path = CardChassisPaths.AvatarFacePrefab;
                    break;
                case CardPresentationKind.Monster:
                    path = CardChassisPaths.MonsterFacePrefab;
                    break;
                case CardPresentationKind.HelpCard:
                case CardPresentationKind.Item:
                case CardPresentationKind.PlayerCard:
                    path = CardChassisPaths.ItemFacePrefab;
                    break;
                case CardPresentationKind.Relic:
                    path = CardChassisPaths.RelicFacePrefab;
                    break;
                default:
                    return null;
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private void ValidateSortingOrders()
        {
            _warnings.Clear();
            if (_previewRoot == null)
            {
                RebuildPreview();
            }

            if (_previewRoot == null)
            {
                return;
            }

            CollectSortingWarnings(_previewRoot.transform);
            _status = _warnings.Count == 0
                ? "通层 sortingOrder 无重复。"
                : "发现 " + _warnings.Count + " 处通层 sortingOrder 重复（见下方告警）。";
        }

        private void CollectSortingWarnings(Transform root)
        {
            var hits = CardFaceSortingOrderValidator.FindDuplicates(root);
            for (var i = 0; i < hits.Count; i++)
            {
                var line = hits[i].ToString();
                _warnings.Add(line);
                Debug.LogWarning("[CardFaceFinalPreview] 通层 sortingOrder 重复：" + line, root);
            }
        }

        private static void StripFaceSortingGroup(GameObject face)
        {
            var group = face.GetComponent<SortingGroup>();
            if (group != null)
            {
                Object.DestroyImmediate(group);
            }
        }

        private void DestroyPreview()
        {
            if (_previewRoot != null)
            {
                Object.DestroyImmediate(_previewRoot);
                _previewRoot = null;
            }
        }
    }
}
#endif

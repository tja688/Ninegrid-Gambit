#if UNITY_EDITOR
using System.Collections.Generic;
using NineGrid.Cards.Convergence;
using NineGrid.Cards.Presentation;
using NineGrid.Cards.Slots;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Core.Content;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using NineGrid.Cards;

namespace NineGrid.Presentation.Editor
{
    /// <summary>
    /// 编辑器终态预览构建：底盘 + L4 挂面 + ApplyPresentation（与运行时 Commit 同族出口）。
    /// </summary>
    public static class CardFacePreviewBuilder
    {
        public sealed class BuildResult
        {
            public GameObject Root;
            public StandardCardView View;
            public CardFacePresentationBinder Binder;
            public Transform FaceRoot;
            public readonly List<string> SortingWarnings = new List<string>();
        }

        public static bool TryBuild(CardFacePreviewRequest request, out BuildResult result, out string error)
        {
            return TryBuild(request, out result, out error, chassisPrefabOverride: null, facePrefabOverride: null);
        }

        /// <param name="chassisPrefabOverride">EditMode 测试可注入支架底盘；正式预览传 null 走资产路径。</param>
        /// <param name="facePrefabOverride">EditMode 测试可注入支架卡面。</param>
        public static bool TryBuild(
            CardFacePreviewRequest request,
            out BuildResult result,
            out string error,
            GameObject chassisPrefabOverride,
            GameObject facePrefabOverride)
        {
            result = null;
            error = null;

            if (request == null)
            {
                error = "预览请求为空。";
                return false;
            }

            if (request.Kind == CardPresentationKind.Unknown)
            {
                error = "Kind 未映射到四套卡面（如 skill.* 不挂面）：" + request.DefId;
                return false;
            }

            if (request.Kind == CardPresentationKind.Room)
            {
                return TryBuildRoomIcon(request, out result, out error);
            }

            var chassisPrefab = chassisPrefabOverride != null
                ? chassisPrefabOverride
                : AssetDatabase.LoadAssetAtPath<GameObject>(CardChassisPaths.ChassisPrefab);
            if (chassisPrefab == null)
            {
                error = "找不到底盘：" + CardChassisPaths.ChassisPrefab;
                return false;
            }

            var facePrefab = facePrefabOverride != null
                ? facePrefabOverride
                : LoadFacePrefab(request.Kind);
            if (facePrefab == null)
            {
                error = "Kind=" + request.Kind + " 无对应卡面模板。";
                return false;
            }

            GameObject root;
            GameObject face;
            if (chassisPrefabOverride != null || facePrefabOverride != null)
            {
                root = Object.Instantiate(chassisPrefab);
                root.name = "CardFacePreview_" + (string.IsNullOrEmpty(request.DefId) ? request.Kind.ToString() : request.DefId);
                root.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;

                var towerOverride = root.GetComponent<CardTransformTower>();
                if (towerOverride == null)
                {
                    towerOverride = root.AddComponent<CardTransformTower>();
                }

                towerOverride.EnsureTower();
                var pivotOverride = towerOverride.FacePivot;
                if (pivotOverride == null)
                {
                    Object.DestroyImmediate(root);
                    error = "底盘缺少 FacePivot。";
                    return false;
                }

                for (var i = pivotOverride.childCount - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(pivotOverride.GetChild(i).gameObject);
                }

                face = Object.Instantiate(facePrefab, pivotOverride);
            }
            else
            {
                root = (GameObject)PrefabUtility.InstantiatePrefab(chassisPrefab);
                root.name = "CardFacePreview_" + (string.IsNullOrEmpty(request.DefId) ? request.Kind.ToString() : request.DefId);
                root.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;

                var tower = root.GetComponent<CardTransformTower>();
                if (tower == null)
                {
                    tower = root.AddComponent<CardTransformTower>();
                }

                tower.EnsureTower();
                var pivot = tower.FacePivot;
                if (pivot == null)
                {
                    Object.DestroyImmediate(root);
                    error = "底盘缺少 FacePivot。";
                    return false;
                }

                for (var i = pivot.childCount - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(pivot.GetChild(i).gameObject);
                }

                face = (GameObject)PrefabUtility.InstantiatePrefab(facePrefab, pivot);
            }

            face.name = facePrefab.name;
            face.hideFlags = HideFlags.DontSave;
            face.transform.localPosition = Vector3.zero;
            face.transform.localRotation = Quaternion.identity;
            face.transform.localScale = Vector3.one;
            StripFaceSortingGroup(face);

            var binder = face.GetComponent<CardFacePresentationBinder>();
            if (binder == null)
            {
                binder = face.AddComponent<CardFacePresentationBinder>();
            }

            var view = root.GetComponent<StandardCardView>();
            if (view == null)
            {
                view = root.AddComponent<StandardCardView>();
            }

            view.AttachFaceBinder(binder);
            view.ApplyPresentation(request.ToSnapshot());

            result = new BuildResult
            {
                Root = root,
                View = view,
                Binder = binder,
                FaceRoot = face.transform,
            };
            CollectSortingWarnings(root.transform, result.SortingWarnings);
            return true;
        }

        public static void DestroyBuild(BuildResult result)
        {
            if (result?.Root != null)
            {
                Object.DestroyImmediate(result.Root);
                result.Root = null;
            }
        }

        public static GameObject LoadFacePrefab(CardPresentationKind kind)
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
                case CardPresentationKind.Trap:
                    path = CardChassisPaths.TrapFacePrefab;
                    break;
                case CardPresentationKind.HelpCard:
                case CardPresentationKind.Item:
                case CardPresentationKind.PlayerCard:
                    path = CardChassisPaths.ItemFacePrefab;
                    break;
                case CardPresentationKind.Relic:
                    path = CardChassisPaths.RelicFacePrefab;
                    break;
                case CardPresentationKind.ChoiceOption:
                    path = CardChassisPaths.RoomOptionFacePrefab;
                    break;
                default:
                    return null;
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        public static CardPresentationKind ToPresentationKind(ContentVisualKind kind, string defId)
        {
            switch (kind)
            {
                case ContentVisualKind.Avatar:
                    return CardPresentationKind.Avatar;
                case ContentVisualKind.Monster:
                    return CardPresentationKind.Monster;
                case ContentVisualKind.Trap:
                    return CardPresentationKind.Trap;
                case ContentVisualKind.HelpCard:
                    return CardPresentationKind.HelpCard;
                case ContentVisualKind.Relic:
                    return CardPresentationKind.Relic;
                case ContentVisualKind.Room:
                    return CardPresentationKind.Room;
                case ContentVisualKind.ChoiceOption:
                    return CardPresentationKind.ChoiceOption;
                case ContentVisualKind.Skill:
                    return CardPresentationKind.Unknown;
                default:
                    return CardPresentationKindResolver.FromDefId(defId);
            }
        }

        private static bool TryBuildRoomIcon(
            CardFacePreviewRequest request,
            out BuildResult result,
            out string error)
        {
            result = null;
            error = null;
            var path = CardChassisPaths.ResolveRoomIconPrefab(
                request.DefId,
                request.RoomIconPrefabPath);
            var iconPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (iconPrefab == null)
            {
                error = "找不到房间图标预制体：" + path;
                return false;
            }

            var root = (GameObject)PrefabUtility.InstantiatePrefab(iconPrefab);
            root.name = "RoomIconPreview_" + (string.IsNullOrEmpty(request.DefId) ? "Room" : request.DefId);
            root.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;

            if (request.MainIcon != null)
            {
                var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
                for (var i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null && renderers[i].sprite != null)
                    {
                        renderers[i].sprite = request.MainIcon;
                        break;
                    }
                }
            }

            result = new BuildResult
            {
                Root = root,
                View = null,
                Binder = null,
                FaceRoot = root.transform,
            };
            CollectSortingWarnings(root.transform, result.SortingWarnings);
            return true;
        }

        public static ContentVisualKind GuessContentVisualKind(string defId)
        {
            if (string.IsNullOrEmpty(defId))
            {
                return ContentVisualKind.Unknown;
            }

            if (defId.StartsWith("monster."))
            {
                return ContentVisualKind.Monster;
            }

            if (defId.StartsWith("trap."))
            {
                return ContentVisualKind.Trap;
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

        /// <summary>
        /// 从 ContentCatalog / Content Visual 解析 Kind，并可选填 Catalog 直暴露槽与样例数值。
        /// </summary>
        public static bool TryResolveDefId(
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

            GameContentCatalog coreCatalog = null;
            try
            {
                coreCatalog = ContentCatalogBootstrap.Load();
            }
            catch (System.Exception ex)
            {
                error = "无法加载 ContentCatalog：" + ex.Message;
                return false;
            }

            var inPresentation = CardPresentationConfigCatalog.TryGet(defId, out _);
            if (!DefIdExistsInContentCatalog(coreCatalog, defId) && !inPresentation)
            {
                error = "ContentCatalog / CardPresentation JSON 中无此 DefId：" + defId;
                return false;
            }

            contentKind = GuessContentVisualKind(defId);
            presentationKind = ToPresentationKind(contentKind, defId);
            if (presentationKind == CardPresentationKind.Unknown)
            {
                error = "DefId 在 ContentCatalog 中存在，但不映射到四套卡面：" + defId;
                return false;
            }

            return true;
        }

        public static void TryFillSampleStats(CardFacePreviewRequest request, GameContentCatalog coreCatalog)
        {
            if (request == null || coreCatalog == null || string.IsNullOrEmpty(request.DefId))
            {
                return;
            }

            if (coreCatalog.Cards != null && coreCatalog.Cards.TryGetValue(request.DefId, out var card) && card != null)
            {
                request.Attack = Mathf.Max(0, card.Stats.Attack);
                request.Armor = Mathf.Max(0, card.Stats.Armor);
                request.Hp = Mathf.Max(0, card.Stats.Hp > 0 ? card.Stats.Hp : card.Stats.MaxHp);
                if (string.IsNullOrEmpty(request.DisplayName))
                {
                    request.DisplayName = card.DisplayName ?? request.DefId;
                }

                return;
            }

            if (coreCatalog.Relics != null && coreCatalog.Relics.TryGetValue(request.DefId, out var relic) && relic != null)
            {
                if (string.IsNullOrEmpty(request.DisplayName))
                {
                    request.DisplayName = relic.DisplayName ?? request.DefId;
                }
            }
        }

        public static bool TryLoadSpriteCatalogs(out ContentVisualSpriteCatalogSet catalogs)
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

        public static void ApplyCatalogDirectSlots(CardFacePreviewRequest request, ContentVisualKind contentKind)
        {
            if (request == null || string.IsNullOrEmpty(request.DefId))
            {
                return;
            }

            if (!TryLoadSpriteCatalogs(out var catalogs))
            {
                return;
            }

            if (catalogs.TryGetDirectSlots(contentKind, request.DefId, out var slots))
            {
                request.MainIcon = slots.MainIcon;
                request.FaceBackground = slots.FaceBackground;
                request.BackBorder = slots.BackBorder;
                request.BackShirt = slots.BackShirt;
                request.BackLogo = slots.BackLogo;
            }
        }

        public static CardFaceSlotRegistrySO LoadSlotRegistry()
        {
            var registry = AssetDatabase.LoadAssetAtPath<CardFaceSlotRegistrySO>(CardChassisPaths.SlotRegistryAsset);
            if (registry != null)
            {
                return registry;
            }

            registry = ScriptableObject.CreateInstance<CardFaceSlotRegistrySO>();
            registry.ApplyDefaultCatalog();
            return registry;
        }

        public static List<CardFaceSlotDefinition> ListInsertableSlots(CardFaceSlotRegistrySO registry = null)
        {
            registry = registry != null ? registry : LoadSlotRegistry();
            var list = new List<CardFaceSlotDefinition>();
            if (registry?.Slots == null)
            {
                return list;
            }

            for (var i = 0; i < registry.Slots.Count; i++)
            {
                var slot = registry.Slots[i];
                if (slot != null && slot.HasRole(CardFaceSlotRole.InsertableInDescription))
                {
                    list.Add(slot);
                }
            }

            return list;
        }

        public static void CollectSortingWarnings(Transform root, List<string> warnings)
        {
            if (root == null || warnings == null)
            {
                return;
            }

            var hits = CardFaceSortingOrderValidator.FindDuplicates(root);
            for (var i = 0; i < hits.Count; i++)
            {
                var line = hits[i].ToString();
                warnings.Add(line);
                Debug.LogWarning("[CardFacePreview] 通层 sortingOrder 重复：" + line, root);
            }
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

        private static void StripFaceSortingGroup(GameObject face)
        {
            var group = face.GetComponent<SortingGroup>();
            if (group != null)
            {
                Object.DestroyImmediate(group);
            }
        }
    }
}
#endif

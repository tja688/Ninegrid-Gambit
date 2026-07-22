#if UNITY_EDITOR
using System.IO;
using NineGrid.Cards.Convergence;
using NineGrid.Cards.Slots;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using NineGrid.Cards;

namespace NineGrid.Presentation.Editor
{
    public static class StandardCardPrefabSetup
    {
        private const string PrefabPath = CardChassisPaths.ChassisPrefab;
        private const string LibraryPath = "Assets/Scripts/NineGrid.Presentation/Cards/PixelCardPackSpriteLibrary.asset";

        [MenuItem("NineGrid/Cards/Setup Card Chassis Prefab")]
        public static void SetupPrefab()
        {
            // 底盘权威形态：塔 + FacePivot，不再在 L4 重建旧万能视觉（#13/#14）。
            EvolveChassisFacePivot();
            EnsureChassisInteractionAndEffects();
        }

        [MenuItem("NineGrid/Cards/Install Transform Tower On Card Chassis Prefab")]
        public static void InstallTransformTowerOnPrefab()
        {
            InstallTransformTowerAt(PrefabPath);
        }

        [MenuItem("NineGrid/Cards/Evolve Chassis FacePivot (strip legacy L4 visuals)")]
        public static void EvolveChassisFacePivot()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var tower = InstallTransformTower(root);
                StripLegacyL4Visuals(tower);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"Card chassis FacePivot evolved: {PrefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("NineGrid/Cards/Ensure Card Face Slot Registry Asset")]
        public static void EnsureSlotRegistryAsset()
        {
            EnsureFolder("Assets/Arts/Cards");
            var existing = AssetDatabase.LoadAssetAtPath<CardFaceSlotRegistrySO>(CardChassisPaths.SlotRegistryAsset);
            if (existing == null)
            {
                existing = ScriptableObject.CreateInstance<CardFaceSlotRegistrySO>();
                existing.ApplyDefaultCatalog();
                AssetDatabase.CreateAsset(existing, CardChassisPaths.SlotRegistryAsset);
            }
            else
            {
                existing.ApplyDefaultCatalog();
                EditorUtility.SetDirty(existing);
            }

            AssetDatabase.SaveAssets();
            Selection.activeObject = existing;
            Debug.Log("CardFaceSlotRegistry ready: " + CardChassisPaths.SlotRegistryAsset);
        }

        private static void EnsureChassisInteractionAndEffects()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                SetupInteractionComponents(root);
                SetupEffectManager(root);
                var sortingGroup = GetOrAdd<SortingGroup>(root);
                sortingGroup.enabled = true;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"Card chassis interaction/effects ensured: {PrefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            var name = Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }

        /// <summary>
        /// 剥除 L4 旧万能视觉，仅保留 FacePivot 挂载点（#13）。
        /// </summary>
        public static void StripLegacyL4Visuals(CardTransformTower tower)
        {
            if (tower == null)
            {
                return;
            }

            tower.EnsureTower();
            var visual = tower.CardVisual;
            var pivot = tower.FacePivot;
            if (visual == null)
            {
                return;
            }

            for (var i = visual.childCount - 1; i >= 0; i--)
            {
                var child = visual.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                if (pivot != null && child == pivot)
                {
                    continue;
                }

                Object.DestroyImmediate(child.gameObject);
            }

            var cardView = tower.GetComponent<StandardCardView>();
            if (cardView == null)
            {
                return;
            }

            var serialized = new SerializedObject(cardView);
            ClearObjectReference(serialized, "attackAnchor");
            ClearObjectReference(serialized, "lifeAnchor");
            ClearObjectReference(serialized, "armorBlocksAnchor");
            ClearObjectReference(serialized, "armorValueAnchor");
            ClearObjectReference(serialized, "cardBackgroundRenderer");
            ClearObjectReference(serialized, "cardFrameRenderer");
            ClearObjectReference(serialized, "mainIconRenderer");
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ClearObjectReference(SerializedObject serialized, string propertyName)
        {
            var property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = null;
            }
        }

        private static void SetupPrefabAt(string prefabPath)
        {
            PixelCardPackSpriteLibraryMenu.CreateOrReloadLibrary();
            var library = AssetDatabase.LoadAssetAtPath<PixelCardPackSpriteLibrary>(LibraryPath);
            if (library == null)
            {
                Debug.LogError("Failed to create PixelCardPackSpriteLibrary asset.");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                ApplySetup(root, library);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"Standard Card prefab setup complete: {prefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void InstallTransformTowerAt(string prefabPath)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                InstallTransformTower(root);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"Transform tower installed: {prefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// 插入 L1–L4 塔层，把现有视觉子节点迁入 L4 CardVisual，并挂塔组件。
        /// </summary>
        public static CardTransformTower InstallTransformTower(GameObject root)
        {
            var tower = GetOrAdd<CardTransformTower>(root);
            tower.EnsureTower();

            var visual = tower.CardVisual;
            if (visual == null)
            {
                Debug.LogError("[StandardCardPrefabSetup] CardVisual 层创建失败。", root);
                return tower;
            }

            ReparentVisualChildrenUnderCardVisual(root.transform, visual);
            LayerConvergenceDriver.Ensure(root.transform, TowerLayer.SlotFrame);
            LayerConvergenceDriver.Ensure(root.transform, TowerLayer.EffectFrame);

            var cardView = root.GetComponent<StandardCardView>();
            if (cardView != null)
            {
                RewireCardViewAfterTower(cardView, visual);
            }

            return tower;
        }

        private static void ReparentVisualChildrenUnderCardVisual(Transform root, Transform cardVisual)
        {
            for (var i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
                if (child == null || child.name == CardTransformTower.BoardFrameName)
                {
                    continue;
                }

                child.SetParent(cardVisual, false);
            }
        }

        private static void RewireCardViewAfterTower(StandardCardView cardView, Transform visualRoot)
        {
            var serialized = new SerializedObject(cardView);
            AssignIfFound(serialized, "attackAnchor", visualRoot.Find("Attack"));
            AssignIfFound(serialized, "lifeAnchor", visualRoot.Find("Life"));
            AssignIfFound(serialized, "armorBlocksAnchor", visualRoot.Find("ArmorBlocks"));
            AssignIfFound(serialized, "armorValueAnchor", visualRoot.Find("ArmorValue"));
            AssignRendererIfFound(serialized, "cardBackgroundRenderer", visualRoot, "Card Background");
            AssignRendererIfFound(serialized, "cardFrameRenderer", visualRoot, "Card Frame");
            AssignRendererIfFound(serialized, "mainIconRenderer", visualRoot, "MainIcon");
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignIfFound(SerializedObject serialized, string propertyName, Transform value)
        {
            if (value == null)
            {
                return;
            }

            var property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static void AssignRendererIfFound(
            SerializedObject serialized,
            string propertyName,
            Transform visualRoot,
            string childName)
        {
            var renderer = FindRenderer(visualRoot, childName);
            if (renderer == null)
            {
                return;
            }

            var property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = renderer;
            }
        }

        private static void ApplySetup(GameObject root, PixelCardPackSpriteLibrary library)
        {
            var sortingGroup = GetOrAdd<SortingGroup>(root);
            var cardView = GetOrAdd<StandardCardView>(root);
            var tower = InstallTransformTower(root);
            var visualParent = tower.CardVisual != null ? tower.CardVisual : root.transform;

            var attack = EnsureChild(visualParent, "Attack", new Vector3(-0.5939188f, 0.7503282f, 0f));
            var life = EnsureChild(visualParent, "Life", new Vector3(0.5935812f, 0.7503282f, 0f));
            var armorBlocks = EnsureChild(visualParent, "ArmorBlocks", new Vector3(-0.5388546f, -0.6682327f, 0f));
            var armorValue = EnsureChild(visualParent, "ArmorValue", new Vector3(0.5861454f, -0.7307327f, 0f));

            CaptureArmorLayout(visualParent, out var blockScale, out var blockSpacing);
            blockScale = Vector3.one;
            blockSpacing = 0.156f;
            FlattenArmorHierarchy(visualParent);

            DisableSpriteRenderer(attack);
            DisableSpriteRenderer(life);

            NormalizeSorting(root.transform, sortingGroup);
            ApplyPrefabSortingOrders(visualParent);
            MoveFrameToBottom(visualParent);

            AssignCardView(cardView, library, attack, life, armorBlocks, armorValue, blockScale, blockSpacing);
            SetupInteractionComponents(root);
            SetupEffectManager(root);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GetOrAddComponentByTypeName(root, "NineGrid.DevTest.Cards.StandardCardViewDevKeys, NineGrid.DevTest");
#endif
        }

        private static void SetupInteractionComponents(GameObject root)
        {
            var collider = GetOrAdd<BoxCollider2D>(root);
            collider.size = new Vector2(1.6f, 2.2f);
            collider.isTrigger = false;

            GetOrAdd<CardVisualDriver>(root);
            GetOrAdd<GroundCardHitProxy>(root);
            GetOrAdd<HandCardHitProxy>(root);
        }

        private static void SetupEffectManager(GameObject root)
        {
            CardEffectAssetMenu.EnsureDefaultAssetsExist();
            var effectManager = GetOrAdd<CardEffectManager>(root);
            var death = CardEffectAssetMenu.LoadDeathEffect();
            var use = CardEffectAssetMenu.LoadUseEffect();
            var hit = CardEffectAssetMenu.LoadHitEffect();
            var hitFlash = CardEffectAssetMenu.LoadHitFlashEffect();
            var attack = CardEffectAssetMenu.LoadAttackEffect();

            var serialized = new SerializedObject(effectManager);
            var bindings = serialized.FindProperty("bindings");
            if (bindings != null && bindings.isArray)
            {
                AssignBinding(bindings, 0, CardEffectKind.Attack, attack);
                AssignBinding(bindings, 1, CardEffectKind.Hit, hit);
                AssignBinding(bindings, 2, CardEffectKind.Death, death);
                AssignBinding(bindings, 3, CardEffectKind.Use, use);
                AssignBinding(bindings, 4, CardEffectKind.HitFlash, hitFlash);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignBinding(
            SerializedProperty bindings,
            int index,
            CardEffectKind kind,
            CardEffectSO effect)
        {
            while (bindings.arraySize <= index)
            {
                bindings.InsertArrayElementAtIndex(bindings.arraySize);
            }

            var element = bindings.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("kind").enumValueIndex = (int)kind;
            element.FindPropertyRelative("defaultEffect").objectReferenceValue = effect;
        }

        private static void AssignCardView(
            StandardCardView cardView,
            PixelCardPackSpriteLibrary library,
            Transform attack,
            Transform life,
            Transform armorBlocks,
            Transform armorValue,
            Vector3 blockScale,
            float blockSpacing)
        {
            var serialized = new SerializedObject(cardView);
            serialized.FindProperty("spriteLibrary").objectReferenceValue = library;
            serialized.FindProperty("attackAnchor").objectReferenceValue = attack;
            serialized.FindProperty("lifeAnchor").objectReferenceValue = life;
            serialized.FindProperty("armorBlocksAnchor").objectReferenceValue = armorBlocks;
            serialized.FindProperty("armorValueAnchor").objectReferenceValue = armorValue;
            var visualRoot = ResolveVisualRoot(cardView.transform);
            serialized.FindProperty("cardBackgroundRenderer").objectReferenceValue =
                FindRenderer(visualRoot, "Card Background");
            serialized.FindProperty("cardFrameRenderer").objectReferenceValue =
                FindRenderer(visualRoot, "Card Frame");
            serialized.FindProperty("mainIconRenderer").objectReferenceValue = FindRenderer(visualRoot, "MainIcon");
            serialized.FindProperty("armorBlockScale").vector3Value = blockScale;
            serialized.FindProperty("digitSpacing").floatValue = 0.04f;
            serialized.FindProperty("armorDigitSpacing").floatValue = 0.25f;
            serialized.FindProperty("armorDigitScale").vector3Value = Vector3.one;
            serialized.FindProperty("armorDigitColor").colorValue = new Color(249f / 255f, 194f / 255f, 43f / 255f, 1f);
            serialized.FindProperty("armorBlockSpacing").floatValue = blockSpacing;
            serialized.FindProperty("frameSortingOrder").intValue = -10;
            serialized.FindProperty("backgroundSortingOrder").intValue = -9;
            serialized.FindProperty("mainIconSortingOrder").intValue = 0;
            serialized.FindProperty("statSortingOrder").intValue = 1;
            serialized.FindProperty("armorSortingOrder").intValue = 2;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CaptureArmorLayout(Transform root, out Vector3 blockScale, out float blockSpacing)
        {
            blockScale = Vector3.one;
            blockSpacing = 0.156f;

            var armor = root.Find("Armor");
            if (armor == null)
            {
                return;
            }

            var renderers = armor.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            var parentScale = armor.localScale;
            var firstScale = renderers[0].transform.localScale;
            blockScale = new Vector3(
                firstScale.x * parentScale.x,
                firstScale.y * parentScale.y,
                firstScale.z * parentScale.z);

            if (renderers.Length > 1)
            {
                var delta = renderers[1].transform.localPosition.x - renderers[0].transform.localPosition.x;
                blockSpacing = Mathf.Abs(delta) * parentScale.x;
            }
        }

        private static void FlattenArmorHierarchy(Transform root)
        {
            var armor = root.Find("Armor");
            if (armor == null)
            {
                return;
            }

            for (var i = armor.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(armor.GetChild(i).gameObject);
            }

            Object.DestroyImmediate(armor.gameObject);
        }

        private static void NormalizeSorting(Transform root, SortingGroup sortingGroup)
        {
            var visualRoot = ResolveVisualRoot(root);
            var reference = FindRenderer(visualRoot, "MainIcon") ?? root.GetComponent<SpriteRenderer>();
            if (reference != null)
            {
                sortingGroup.sortingLayerID = reference.sortingLayerID;
            }

            sortingGroup.sortingOrder = 0;

            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                renderer.sortingLayerID = sortingGroup.sortingLayerID;
            }
        }

        private static void ApplyPrefabSortingOrders(Transform root)
        {
            const int frameOrder = -10;
            const int backgroundOrder = -9;
            const int mainIconOrder = 0;

            var frame = FindRenderer(root, "Card Frame");
            if (frame != null)
            {
                frame.sortingOrder = frameOrder;
            }

            var background = FindRenderer(root, "Card Background") ?? root.GetComponent<SpriteRenderer>();
            if (background != null)
            {
                background.sortingOrder = backgroundOrder;
            }

            var mainIcon = FindRenderer(root, "MainIcon");
            if (mainIcon != null)
            {
                mainIcon.sortingOrder = mainIconOrder;
            }
        }

        private static void MoveFrameToBottom(Transform root)
        {
            Transform frame = null;
            foreach (Transform child in root)
            {
                if (child.name.Trim() == "Card Frame")
                {
                    frame = child;
                    break;
                }
            }

            if (frame != null)
            {
                frame.SetSiblingIndex(0);
            }
        }

        private static Transform ResolveVisualRoot(Transform root)
        {
            var tower = root.GetComponent<CardTransformTower>();
            if (tower != null)
            {
                tower.EnsureTower();
                if (tower.CardVisual != null)
                {
                    return tower.CardVisual;
                }
            }

            var named = root.Find(
                $"{CardTransformTower.BoardFrameName}/{CardTransformTower.SlotFrameName}/{CardTransformTower.EffectFrameName}/{CardTransformTower.CardVisualName}");
            return named != null ? named : root;
        }

        private static Transform EnsureChild(Transform parent, string childName, Vector3 localPosition)
        {
            var child = parent.Find(childName);
            if (child == null)
            {
                var go = new GameObject(childName);
                child = go.transform;
                child.SetParent(parent, false);
            }

            child.localPosition = localPosition;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            return child;
        }

        private static void DisableSpriteRenderer(Transform target)
        {
            var renderer = target.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }

        private static SpriteRenderer FindRenderer(Transform root, string childName)
        {
            var trimmed = childName.Trim();
            foreach (Transform child in root)
            {
                if (child.name.Trim() == trimmed)
                {
                    return child.GetComponent<SpriteRenderer>();
                }
            }

            var childTransform = root.Find(childName);
            if (childTransform != null)
            {
                return childTransform.GetComponent<SpriteRenderer>();
            }

            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer != null && renderer.name.Trim() == trimmed)
                {
                    return renderer;
                }
            }

            return null;
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        private static Component GetOrAddComponentByTypeName(GameObject go, string typeName)
        {
            var type = System.Type.GetType(typeName);
            if (type == null)
            {
                return null;
            }

            var component = go.GetComponent(type);
            return component != null ? component : go.AddComponent(type);
        }
    }
}
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineGrid.Presentation.Cards.Editor
{
    public static class StandardCardPrefabSetup
    {
        private const string PrefabPath = "Assets/Prefabs/Standard Card.prefab";
        private const string LibraryPath = "Assets/Scripts/NineGrid.Presentation/Cards/PixelCardPackSpriteLibrary.asset";

        [MenuItem("NineGrid/Cards/Setup Standard Card Prefab")]
        public static void SetupPrefab()
        {
            PixelCardPackSpriteLibraryMenu.CreateOrReloadLibrary();
            var library = AssetDatabase.LoadAssetAtPath<PixelCardPackSpriteLibrary>(LibraryPath);
            if (library == null)
            {
                Debug.LogError("Failed to create PixelCardPackSpriteLibrary asset.");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                ApplySetup(root, library);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("Standard Card prefab setup complete.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ApplySetup(GameObject root, PixelCardPackSpriteLibrary library)
        {
            var sortingGroup = GetOrAdd<SortingGroup>(root);
            var cardView = GetOrAdd<StandardCardView>(root);

            var attack = EnsureChild(root.transform, "Attack", new Vector3(-0.5939188f, 0.7503282f, 0f));
            var life = EnsureChild(root.transform, "Life", new Vector3(0.5935812f, 0.7503282f, 0f));
            var armorBlocks = EnsureChild(root.transform, "ArmorBlocks", new Vector3(0.021987915f, -0.73784775f, 0f));
            var armorValue = EnsureChild(root.transform, "ArmorValue", new Vector3(0.021987915f, -0.73784775f, 0f));

            CaptureArmorLayout(root.transform, out var blockScale, out var blockSpacing);
            blockScale = Vector3.one;
            blockSpacing = 0.156f;
            FlattenArmorHierarchy(root.transform);

            DisableSpriteRenderer(attack);
            DisableSpriteRenderer(life);

            NormalizeSorting(root.transform, sortingGroup);
            ApplyPrefabSortingOrders(root.transform);
            MoveFrameToBottom(root.transform);

            AssignCardView(cardView, library, attack, life, armorBlocks, armorValue, blockScale, blockSpacing);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GetOrAddComponentByTypeName(root, "NineGrid.DevTest.Cards.StandardCardViewDevKeys, NineGrid.DevTest");
#endif
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
            serialized.FindProperty("cardBackgroundRenderer").objectReferenceValue = cardView.GetComponent<SpriteRenderer>();
            serialized.FindProperty("cardFrameRenderer").objectReferenceValue =
                FindRenderer(cardView.transform, "Card Frame");
            serialized.FindProperty("mainIconRenderer").objectReferenceValue = FindRenderer(cardView.transform, "MainIcon");
            serialized.FindProperty("armorBlockScale").vector3Value = blockScale;
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
            var reference = FindRenderer(root, "MainIcon") ?? root.GetComponent<SpriteRenderer>();
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

            var background = root.GetComponent<SpriteRenderer>();
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
            return childTransform != null ? childTransform.GetComponent<SpriteRenderer>() : null;
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

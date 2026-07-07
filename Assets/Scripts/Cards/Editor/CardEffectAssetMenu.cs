#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace NineGrid.Cards.Editor
{
    public static class CardEffectAssetMenu
    {
        private const string DefaultsFolder = "Assets/Scripts/Cards/Effects/Defaults";

        [MenuItem("NineGrid/Cards/Effects/Create Default Effect Assets")]
        public static void CreateDefaultEffectAssets()
        {
            EnsureFolder(DefaultsFolder);

            var death = CreateOrLoad<CardTweenScaleOutEffectSO>(
                $"{DefaultsFolder}/CardDeathScaleOutEffect.asset",
                "CardDeathScaleOutEffect",
                CardEffectKind.Death,
                0.18f);
            var use = CreateOrLoad<CardTweenScaleOutEffectSO>(
                $"{DefaultsFolder}/CardUseVanishEffect.asset",
                "CardUseVanishEffect",
                CardEffectKind.Use,
                0.18f);
            var hit = CreateOrLoad<CardTweenDirectionalPunchEffectSO>(
                $"{DefaultsFolder}/CardHitPunchEffect.asset",
                "CardHitPunchEffect",
                CardEffectKind.Hit,
                0.24f,
                supportsDirectionVariants: true);
            var attack = CreateOrLoad<CardTweenDirectionalLungeEffectSO>(
                $"{DefaultsFolder}/CardAttackLungeEffect.asset",
                "CardAttackLungeEffect",
                CardEffectKind.Attack,
                0.2f,
                supportsDirectionVariants: true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CardEffectAssetMenu] Default effect assets ready in " + DefaultsFolder);
        }

        public static void EnsureDefaultAssetsExist()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scripts/Cards/Effects"))
            {
                return;
            }

            var deathPath = $"{DefaultsFolder}/CardDeathScaleOutEffect.asset";
            if (AssetDatabase.LoadAssetAtPath<CardEffectSO>(deathPath) == null)
            {
                CreateDefaultEffectAssets();
            }
        }

        public static CardTweenScaleOutEffectSO LoadDeathEffect()
        {
            return AssetDatabase.LoadAssetAtPath<CardTweenScaleOutEffectSO>(
                $"{DefaultsFolder}/CardDeathScaleOutEffect.asset");
        }

        public static CardTweenScaleOutEffectSO LoadUseEffect()
        {
            return AssetDatabase.LoadAssetAtPath<CardTweenScaleOutEffectSO>(
                $"{DefaultsFolder}/CardUseVanishEffect.asset");
        }

        public static CardTweenDirectionalPunchEffectSO LoadHitEffect()
        {
            return AssetDatabase.LoadAssetAtPath<CardTweenDirectionalPunchEffectSO>(
                $"{DefaultsFolder}/CardHitPunchEffect.asset");
        }

        public static CardTweenDirectionalLungeEffectSO LoadAttackEffect()
        {
            return AssetDatabase.LoadAssetAtPath<CardTweenDirectionalLungeEffectSO>(
                $"{DefaultsFolder}/CardAttackLungeEffect.asset");
        }

        private static T CreateOrLoad<T>(
            string path,
            string assetName,
            CardEffectKind kind,
            float estimatedDuration,
            bool supportsDirectionVariants = false) where T : CardEffectSO
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                ApplyEffectMetadata(existing, kind, estimatedDuration, supportsDirectionVariants);
                return existing;
            }

            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = assetName;
            ApplyEffectMetadata(asset, kind, estimatedDuration, supportsDirectionVariants);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void ApplyEffectMetadata(
            CardEffectSO asset,
            CardEffectKind kind,
            float estimatedDuration,
            bool supportsDirectionVariants)
        {
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("kind").enumValueIndex = (int)kind;
            serialized.FindProperty("estimatedDuration").floatValue = estimatedDuration;
            serialized.FindProperty("supportsDirectionVariants").boolValue = supportsDirectionVariants;
            serialized.FindProperty("displayName").stringValue = asset.name;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            var parts = folderPath.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
#endif

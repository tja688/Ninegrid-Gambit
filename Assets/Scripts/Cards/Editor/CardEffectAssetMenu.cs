#if UNITY_EDITOR
using System.Collections.Generic;
using DG.Tweening;
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
            var hitFlash = CreateOrLoadHitFlash(
                $"{DefaultsFolder}/CardHitFlashEffect.asset",
                "CardHitFlashEffect");
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

        [MenuItem("NineGrid/Cards/Effects/Create Basic Attack/Hit Sequence Assets")]
        public static void CreateBasicAttackHitSequenceAssets()
        {
            EnsureFolder(DefaultsFolder);

            var attack = CreateOrLoadSequence(
                $"{DefaultsFolder}/CardBasicAttackTimelineEffect.asset",
                "CardBasicAttackTimelineEffect",
                CardEffectKind.Attack,
                BuildBasicAttackClips(),
                orchestrationDelay: 0.4f,
                usesBasicAttackComboDelay: true);
            var hit = CreateOrLoadSequence(
                $"{DefaultsFolder}/CardBasicHitTimelineEffect.asset",
                "CardBasicHitTimelineEffect",
                CardEffectKind.Hit,
                BuildBasicHitClips(),
                orchestrationDelay: 0.4f,
                usesBasicAttackComboDelay: true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[CardEffectAssetMenu] Basic sequence assets ready: {attack.name}, {hit.name}");
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

        public static CardSpriteHitFlashEffectSO LoadHitFlashEffect()
        {
            return AssetDatabase.LoadAssetAtPath<CardSpriteHitFlashEffectSO>(
                $"{DefaultsFolder}/CardHitFlashEffect.asset");
        }

        public static CardTweenDirectionalLungeEffectSO LoadAttackEffect()
        {
            return AssetDatabase.LoadAssetAtPath<CardTweenDirectionalLungeEffectSO>(
                $"{DefaultsFolder}/CardAttackLungeEffect.asset");
        }

        public static CardDOTweenSequenceEffectSO LoadBasicAttackSequenceEffect()
        {
            return AssetDatabase.LoadAssetAtPath<CardDOTweenSequenceEffectSO>(
                $"{DefaultsFolder}/CardBasicAttackTimelineEffect.asset");
        }

        public static CardDOTweenSequenceEffectSO LoadBasicHitSequenceEffect()
        {
            return AssetDatabase.LoadAssetAtPath<CardDOTweenSequenceEffectSO>(
                $"{DefaultsFolder}/CardBasicHitTimelineEffect.asset");
        }

        private static List<CardTweenClip> BuildBasicAttackClips()
        {
            return new List<CardTweenClip>
            {
                new()
                {
                    delay = 0.1f,
                    duration = 0.3f,
                    ease = Ease.OutQuad,
                    type = CardTweenClipType.LocalMove,
                    endValue = new Vector3(1.0625f, 0f, 0f),
                    isRelative = false,
                },
                new()
                {
                    delay = 0f,
                    duration = 0.1f,
                    ease = Ease.InQuad,
                    type = CardTweenClipType.LocalMove,
                    endValue = new Vector3(-0.1f, 0f, 0f),
                    isRelative = false,
                },
            };
        }

        private static List<CardTweenClip> BuildBasicHitClips()
        {
            return new List<CardTweenClip>
            {
                new()
                {
                    delay = 0f,
                    duration = 0.3f,
                    ease = Ease.OutBack,
                    type = CardTweenClipType.LocalMove,
                    endValue = new Vector3(3f, 0f, 0f),
                    isRelative = false,
                },
            };
        }

        private static CardDOTweenSequenceEffectSO CreateOrLoadSequence(
            string path,
            string assetName,
            CardEffectKind kind,
            List<CardTweenClip> clips,
            float orchestrationDelay,
            bool usesBasicAttackComboDelay)
        {
            var existing = AssetDatabase.LoadAssetAtPath<CardDOTweenSequenceEffectSO>(path);
            if (existing != null)
            {
                existing.EditorSetClips(clips);
                ApplySequenceMetadata(
                    existing,
                    kind,
                    CardDOTweenSequenceEffectSO.ComputeEstimatedDuration(clips),
                    orchestrationDelay,
                    usesBasicAttackComboDelay);
                return existing;
            }

            var asset = ScriptableObject.CreateInstance<CardDOTweenSequenceEffectSO>();
            asset.name = assetName;
            asset.EditorSetClips(clips);
            ApplySequenceMetadata(
                asset,
                kind,
                CardDOTweenSequenceEffectSO.ComputeEstimatedDuration(clips),
                orchestrationDelay,
                usesBasicAttackComboDelay);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static CardSpriteHitFlashEffectSO CreateOrLoadHitFlash(string path, string assetName)
        {
            var existing = AssetDatabase.LoadAssetAtPath<CardSpriteHitFlashEffectSO>(path);
            if (existing != null)
            {
                ApplyHitFlashMetadata(existing);
                return existing;
            }

            var asset = ScriptableObject.CreateInstance<CardSpriteHitFlashEffectSO>();
            asset.name = assetName;
            ApplyHitFlashMetadata(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void ApplyHitFlashMetadata(CardSpriteHitFlashEffectSO asset)
        {
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("kind").enumValueIndex = (int)CardEffectKind.HitFlash;
            serialized.FindProperty("displayName").stringValue = asset.name;
            serialized.FindProperty("estimatedDuration").floatValue = 0.45f;
            serialized.FindProperty("supportsDirectionVariants").boolValue = false;
            serialized.FindProperty("hitFlashMaterialTemplate").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/Arts/VisualProfiles/TableNineSpriteHitFlash.mat");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
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

        private static void ApplySequenceMetadata(
            CardDOTweenSequenceEffectSO asset,
            CardEffectKind kind,
            float estimatedDuration,
            float orchestrationDelay,
            bool usesBasicAttackComboDelay)
        {
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("kind").enumValueIndex = (int)kind;
            serialized.FindProperty("estimatedDuration").floatValue = estimatedDuration;
            serialized.FindProperty("supportsDirectionVariants").boolValue = true;
            serialized.FindProperty("orchestrationDelay").floatValue = orchestrationDelay;
            serialized.FindProperty("usesBasicAttackComboDelay").boolValue = usesBasicAttackComboDelay;
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

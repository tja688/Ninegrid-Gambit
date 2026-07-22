using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using NineGrid.Cards;

namespace NineGrid.Presentation.Editor
{
    /// <summary>
    /// 创建默认战斗编排 Profile / Catalog，并尝试挂到场景 FieldBattleManager。
    /// </summary>
    public static class BattleEncounterDefaultsSetup
    {
        private const string DefaultsFolder = "Assets/Scripts/NineGrid.Presentation/Cards/Battle/Defaults";
        private const string CatalogAssetName = "Battle_Encounter_Catalog_Default";

        private static readonly IntentSpec[] Specs =
        {
            new(
                "Battle_BasicAttack_Default",
                BattleIntent.Attack,
                "默认玩家进攻：四向 CardAttackBasic Timeline，怪物受击击退系数 1.0。单卡闪白/死亡仍走受击卡 CardEffect。"),
            new(
                "Battle_BasicAttack_Lethal_Default",
                BattleIntent.AttackLethal,
                "默认玩家击杀进攻：绑死亡回调，播完不强制拉回受击者（随后 Vacate/Release）。"),
            new(
                "Battle_Counter_Default",
                BattleIntent.CounterAttack,
                "默认怪物反击：相对位移重绑，玩家受击击退系数 0.5（锁区间 0.3–1.0）。"),
            new(
                "Battle_Counter_Lethal_Default",
                BattleIntent.CounterAttackLethal,
                "默认怪物击杀反击：相对位移 + 死亡回调；玩家受击者不强制拉回。"),
        };

        [MenuItem("NineGrid/Cards/Battle/Create Default Encounter Profiles And Catalog")]
        public static void CreateDefaults()
        {
            EnsureFolder(DefaultsFolder);

            var profiles = new Dictionary<BattleIntent, BattleEncounterProfileSO>();
            foreach (var spec in Specs)
            {
                var profile = LoadOrCreateProfile(spec);
                profiles[spec.Intent] = profile;
            }

            var catalog = LoadOrCreateCatalog(profiles);
            TryAssignCatalogToScene(catalog);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = catalog;
            Debug.Log($"[BattleEncounterDefaultsSetup] 已写入默认战斗编排资产 → {DefaultsFolder}");
        }

        private static BattleEncounterProfileSO LoadOrCreateProfile(IntentSpec spec)
        {
            var path = $"{DefaultsFolder}/{spec.AssetName}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<BattleEncounterProfileSO>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<BattleEncounterProfileSO>();
                AssetDatabase.CreateAsset(profile, path);
            }

            profile.ApplyIntentDefaults(spec.Intent);
            profile.EditorSetIdentity(spec.AssetName, spec.AssetName, spec.Description);
            profile.EditorSetParticipants(BattleParticipantIds.Wildcard, BattleParticipantIds.Wildcard);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static BattleEncounterCatalogSO LoadOrCreateCatalog(
            Dictionary<BattleIntent, BattleEncounterProfileSO> profiles)
        {
            var path = $"{DefaultsFolder}/{CatalogAssetName}.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<BattleEncounterCatalogSO>(path);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<BattleEncounterCatalogSO>();
                AssetDatabase.CreateAsset(catalog, path);
            }

            catalog.EditorSetDescription(
                "默认战斗编排路由表。新增玩家×怪物变体时追加 Entry（精确或怪物通配）；" +
                "不要改场景 Rig 魔法数，不要把战斗 timing 塞进 CardEffect。");

            var entries = new List<BattleEncounterCatalogSO.Entry>();
            foreach (var pair in profiles)
            {
                entries.Add(new BattleEncounterCatalogSO.Entry
                {
                    intent = pair.Key,
                    playerContentId = BattleParticipantIds.Wildcard,
                    monsterContentId = BattleParticipantIds.Wildcard,
                    profile = pair.Value,
                });
            }

            catalog.EditorSetEntries(entries);
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static void TryAssignCatalogToScene(BattleEncounterCatalogSO catalog)
        {
            var managers = Object.FindObjectsByType<FieldBattleManagerSingleton>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (managers == null || managers.Length == 0)
            {
                Debug.LogWarning("[BattleEncounterDefaultsSetup] 场景中未找到 FieldBattleManagerSingleton，请手动拖入 Catalog。");
                return;
            }

            foreach (var manager in managers)
            {
                var so = new SerializedObject(manager);
                var prop = so.FindProperty("encounterCatalog");
                if (prop == null)
                {
                    continue;
                }

                prop.objectReferenceValue = catalog;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(manager);
            }

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.IsValid())
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder))
            {
                return;
            }

            var parts = assetFolder.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }

            if (!Directory.Exists(assetFolder))
            {
                // AssetDatabase folder creation is enough for Unity.
            }
        }

        private readonly struct IntentSpec
        {
            public IntentSpec(string assetName, BattleIntent intent, string description)
            {
                AssetName = assetName;
                Intent = intent;
                Description = description;
            }

            public string AssetName { get; }

            public BattleIntent Intent { get; }

            public string Description { get; }
        }
    }
}

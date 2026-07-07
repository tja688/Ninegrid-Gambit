#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace NineGrid.DevTest.Editor
{
    public static class TestKeyDevTestAssetMenu
    {
        private const string Root = "Assets/Resources/DevTest";
        private const string StackPath = Root + "/TestKeyStack.asset";

        /// <summary>
        /// 将层 Profile 写入 TestKeyStack 栈底（最高优先级）。新建层与置顶均走此入口。
        /// </summary>
        public static bool RegisterLayerAsHighestPriority(TestKeyLayerProfileSO profile)
        {
            if (profile == null)
            {
                return false;
            }

            EnsureFolder("Assets/Resources");
            EnsureFolder(Root);

            var stack = AssetDatabase.LoadAssetAtPath<TestKeyStackConfigSO>(StackPath);
            if (stack == null)
            {
                stack = ScriptableObject.CreateInstance<TestKeyStackConfigSO>();
                AssetDatabase.CreateAsset(stack, StackPath);
            }

            Undo.RecordObject(stack, "Register Test Key Layer As Highest Priority");
            stack.RegisterLayerAsHighestPriority(profile);
            EditorUtility.SetDirty(stack);
            AssetDatabase.SaveAssets();
            return true;
        }

        [MenuItem("NineGrid/DevTest/Register Selected Layer As Highest Priority")]
        public static void RegisterSelectedLayerAsHighestPriority()
        {
            var profile = Selection.activeObject as TestKeyLayerProfileSO;
            if (profile == null)
            {
                EditorUtility.DisplayDialog(
                    "Test Key Stack",
                    "请在 Project 窗口选中一个 TestKeyLayerProfileSO。",
                    "OK");
                return;
            }

            if (RegisterLayerAsHighestPriority(profile))
            {
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<TestKeyStackConfigSO>(StackPath);
            }
        }

        [MenuItem("NineGrid/DevTest/Create Ground Field Manager Test Layer")]
        public static void CreateGroundFieldManagerLayer()
        {
            CreateOrUpdateLayerAsHighestPriority(
                $"{Root}/Layer_GroundFieldManager.asset",
                "ground-field-manager",
                "场地卡管理器测试",
                new[]
                {
                    (KeyCode.Keypad4, "外圈全体顺时针旋转"),
                    (KeyCode.Keypad5, "随机移除场中1张"),
                });

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<TestKeyLayerProfileSO>(
                $"{Root}/Layer_GroundFieldManager.asset");
        }

        [MenuItem("NineGrid/DevTest/Create Default Test Key Stack Assets")]
        public static void CreateDefaultAssets()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder(Root);

            var cardDeckLayer = LoadOrCreateLayer(
                $"{Root}/Layer_CardDeckManager.asset",
                "card-deck-manager",
                "牌组管理器测试",
                new[]
                {
                    (KeyCode.Keypad1, "卡组入场+开局发牌"),
                    (KeyCode.Keypad2, "清场+随机发1张"),
                    (KeyCode.Keypad3, "随机槽位增卡"),
                });

            var standardCardLayer = LoadOrCreateLayer(
                $"{Root}/Layer_StandardCard.asset",
                "standard-card",
                "卡牌调试",
                new[]
                {
                    (KeyCode.Keypad1, "加护甲"),
                    (KeyCode.Keypad2, "减护甲"),
                    (KeyCode.Keypad3, "加攻血"),
                });

            var stackPath = StackPath;
            var stack = AssetDatabase.LoadAssetAtPath<TestKeyStackConfigSO>(stackPath);
            if (stack == null)
            {
                stack = ScriptableObject.CreateInstance<TestKeyStackConfigSO>();
                AssetDatabase.CreateAsset(stack, stackPath);
            }

            var serialized = new SerializedObject(stack);
            var layers = serialized.FindProperty("layers");
            layers.arraySize = 2;
            layers.GetArrayElementAtIndex(0).objectReferenceValue = standardCardLayer;
            layers.GetArrayElementAtIndex(1).objectReferenceValue = cardDeckLayer;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(stack);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = stack;
            EditorUtility.DisplayDialog(
                "Test Key Stack",
                "已创建默认级联栈：\n下方 card-deck-manager 优先级高于 standard-card。\n请在场景 Directors 下添加 TestKeyStackHost 并引用 TestKeyStack。",
                "OK");
        }

        private static TestKeyLayerProfileSO LoadOrCreateLayer(
            string path,
            string layerId,
            string displayName,
            (KeyCode key, string label)[] bindings)
        {
            var layer = AssetDatabase.LoadAssetAtPath<TestKeyLayerProfileSO>(path);
            if (layer == null)
            {
                layer = ScriptableObject.CreateInstance<TestKeyLayerProfileSO>();
                AssetDatabase.CreateAsset(layer, path);
            }

            var serialized = new SerializedObject(layer);
            serialized.FindProperty("layerId").stringValue = layerId;
            serialized.FindProperty("displayName").stringValue = displayName;

            var bindingsProp = serialized.FindProperty("declaredBindings");
            bindingsProp.arraySize = bindings.Length;
            for (var i = 0; i < bindings.Length; i++)
            {
                var element = bindingsProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("key").enumValueIndex = (int)bindings[i].key;
                element.FindPropertyRelative("label").stringValue = bindings[i].label;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(layer);
            return layer;
        }

        /// <summary>
        /// 创建或更新层 Profile，并注册为栈底最高优先级。
        /// </summary>
        public static TestKeyLayerProfileSO CreateOrUpdateLayerAsHighestPriority(
            string assetPath,
            string layerId,
            string displayName,
            (KeyCode key, string label)[] bindings)
        {
            var layer = LoadOrCreateLayer(assetPath, layerId, displayName, bindings);
            RegisterLayerAsHighestPriority(layer);
            return layer;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}

#endif

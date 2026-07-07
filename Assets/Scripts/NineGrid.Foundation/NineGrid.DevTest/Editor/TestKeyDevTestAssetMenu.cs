#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace NineGrid.DevTest.Editor
{
    public static class TestKeyDevTestAssetMenu
    {
        private const string Root = "Assets/Resources/DevTest";

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
                    (KeyCode.Keypad2, "随机发牌到Ground"),
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

            var stackPath = $"{Root}/TestKeyStack.asset";
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

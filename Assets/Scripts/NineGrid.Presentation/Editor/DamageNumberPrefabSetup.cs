#if UNITY_EDITOR

using System.IO;
using DamageNumbersPro;
using UnityEditor;
using UnityEngine;
using NineGrid.Flow;

namespace NineGrid.Presentation.Editor
{
    public static class DamageNumberPrefabSetup
    {
        private const string SourcePrefabPath = "Assets/Plugins/DamageNumbersPro/Demo C#/Demo_Popup.prefab";
        private const string TargetPrefabPath = "Assets/Prefabs/VFX/DamageNumberPopup.prefab";
        private const string SortingLayerName = "Main";
        private const int SortingOrder = 5;

        [MenuItem("NineGrid/VFX/Create Damage Number Popup Prefab")]
        public static void CreateDamageNumberPopupPrefab()
        {
            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Prefabs/VFX");

            if (!File.Exists(SourcePrefabPath))
            {
                EditorUtility.DisplayDialog("Damage Number", $"未找到源预制体：\n{SourcePrefabPath}", "OK");
                return;
            }

            if (!File.Exists(TargetPrefabPath))
            {
                AssetDatabase.CopyAsset(SourcePrefabPath, TargetPrefabPath);
            }

            using (var editScope = new PrefabUtility.EditPrefabContentsScope(TargetPrefabPath))
            {
                var root = editScope.prefabContentsRoot;
                root.name = "DamageNumberPopup";

                var damageNumber = root.GetComponent<DamageNumberMesh>();
                if (damageNumber != null)
                {
                    damageNumber.enable3DGame = false;
                    damageNumber.renderThroughWalls = false;
                }

                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sortingLayerName = SortingLayerName;
                    renderer.sortingOrder = SortingOrder;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(TargetPrefabPath);
            EditorUtility.DisplayDialog(
                "Damage Number",
                $"已创建/更新伤害数字预制体：\n{TargetPrefabPath}\nSorting Layer = {SortingLayerName}, Order = {SortingOrder}",
                "OK");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}

#endif

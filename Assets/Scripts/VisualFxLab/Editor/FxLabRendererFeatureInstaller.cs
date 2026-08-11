using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NineGrid.VisualFxLab.Editor
{
    /// <summary>
    /// 画面实验室渲染特性的安装 / 卸载菜单。
    /// 仓库默认已把 Feature 预装在 Renderer2D.asset 上（未装配时零开销）；
    /// 本菜单用于彻底移除（还原资产原状）或误删后的重新安装。
    /// </summary>
    public static class FxLabRendererFeatureInstaller
    {
        const string RendererDataPath = "Assets/Settings/Renderer2D.asset";
        const string LookShaderPath = "Assets/Arts/VisualProfiles/NineGridFxLabLooks.shader";
        const string FeatureName = "NineGrid FxLab Looks";

        [MenuItem("NineGrid/画面实验室/安装全屏渲染特性 (Renderer2D)")]
        public static void Install()
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererDataPath);
            if (rendererData == null)
            {
                Debug.LogError($"[FxLab] 找不到 {RendererDataPath}");
                return;
            }

            foreach (var existing in rendererData.rendererFeatures)
            {
                if (existing is NineGridFxLabFeature)
                {
                    Debug.Log("[FxLab] 渲染特性已安装，无需重复。");
                    return;
                }
            }

            var feature = ScriptableObject.CreateInstance<NineGridFxLabFeature>();
            feature.name = FeatureName;
            feature.lookShader = AssetDatabase.LoadAssetAtPath<Shader>(LookShaderPath);

            AssetDatabase.AddObjectToAsset(feature, rendererData);
            AssetDatabase.SaveAssets();

            var so = new SerializedObject(rendererData);
            var list = so.FindProperty("m_RendererFeatures");
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;

            var map = so.FindProperty("m_RendererFeatureMap");
            if (map != null
                && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId))
            {
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
            Debug.Log("[FxLab] 渲染特性安装完成（未装配时零开销，F9 装配后 F10 切换全屏 Look）。");
        }

        [MenuItem("NineGrid/画面实验室/移除全屏渲染特性 (还原 Renderer2D)")]
        public static void Uninstall()
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererDataPath);
            if (rendererData == null)
            {
                Debug.LogError($"[FxLab] 找不到 {RendererDataPath}");
                return;
            }

            NineGridFxLabFeature target = null;
            foreach (var existing in rendererData.rendererFeatures)
            {
                if (existing is NineGridFxLabFeature fxLab)
                {
                    target = fxLab;
                    break;
                }
            }

            if (target == null)
            {
                Debug.Log("[FxLab] 渲染特性不在 Renderer2D 上，无需移除。");
                return;
            }

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(target, out _, out long targetLocalId);

            var so = new SerializedObject(rendererData);
            var list = so.FindProperty("m_RendererFeatures");
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == target)
                {
                    list.DeleteArrayElementAtIndex(i);
                }
            }

            var map = so.FindProperty("m_RendererFeatureMap");
            if (map != null && targetLocalId != 0)
            {
                for (int i = map.arraySize - 1; i >= 0; i--)
                {
                    if (map.GetArrayElementAtIndex(i).longValue == targetLocalId)
                    {
                        map.DeleteArrayElementAtIndex(i);
                    }
                }
            }

            so.ApplyModifiedProperties();
            AssetDatabase.RemoveObjectFromAsset(target);
            Object.DestroyImmediate(target, true);
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
            Debug.Log("[FxLab] 渲染特性已移除，Renderer2D 恢复原状。");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace NineGrid.Presentation.Editor
{
    /// <summary>
    /// 在已引用的 SmileySans SDF 上原地补字：不换 GUID、不重绑场景/预制体。
    /// 开启 Multi Atlas，按字符集 TryAddCharacters，并关掉 Clear Dynamic Data On Build。
    /// </summary>
    public static class SmileySansSdfCharsetBaker
    {
        public const string FontAssetPath =
            "Assets/Arts/Fronts/DeYiHei/SmileySans-Oblique-3 SDF.asset";

        public const string CharsetPath =
            "Assets/Arts/Fronts/7000汉字+符号+英文字符集.txt";

        public const string CharsetGuid = "5ea671eb92a17d64396b8b26362b138c";

        const int PreferredAtlasSize = 4096;

        [MenuItem("NineGrid/Fonts/Bake SmileySans SDF Charset (in-place)")]
        public static void BakeFromMenu()
        {
            string report = BakeInPlace();
            Debug.Log(report);
            EditorUtility.DisplayDialog("SmileySans SDF Bake", report, "OK");
        }

        /// <summary>供 Unity CLI eval 调用；返回可读报告字符串。</summary>
        public static string BakeInPlace()
        {
            var sw = Stopwatch.StartNew();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (font == null)
            {
                return $"FAIL: missing font asset at {FontAssetPath}";
            }

            if (!File.Exists(CharsetPath))
            {
                return $"FAIL: missing charset at {CharsetPath}";
            }

            string raw = File.ReadAllText(CharsetPath, Encoding.UTF8);
            var unique = new List<char>(raw.Length);
            var seen = new HashSet<char>();
            foreach (char c in raw)
            {
                if (c == '\r' || c == '\n' || c == '\t')
                {
                    continue;
                }

                if (seen.Add(c))
                {
                    unique.Add(c);
                }
            }

            string characters = new string(unique.ToArray());
            int beforeChars = font.characterTable != null ? font.characterTable.Count : 0;
            int beforeGlyphs = font.glyphTable != null ? font.glyphTable.Count : 0;
            int beforeAtlases = font.atlasTextures != null ? font.atlasTextures.Length : 0;

            if (font.sourceFontFile == null)
            {
                return "FAIL: sourceFontFile is null — cannot rasterize new glyphs. Check SmileySans-Oblique-3.otf (LFS).";
            }

            // TryAddCharacters 需要 Dynamic + 源字体。
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;

            // atlasWidth/Height 与 clearDynamicDataOnBuild 在公开 API 上只读/internal，统一用 SerializedObject。
            var so = new SerializedObject(font);
            so.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
            so.FindProperty("m_IsMultiAtlasTexturesEnabled").boolValue = true;
            so.FindProperty("m_AtlasWidth").intValue = PreferredAtlasSize;
            so.FindProperty("m_AtlasHeight").intValue = PreferredAtlasSize;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 清空后按更大图集重烘，避免旧 1024 单页塞不下 7000 字。
            font.ClearFontAssetData(setAtlasSizeToZero: false);

            so = new SerializedObject(font);
            so.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
            so.FindProperty("m_IsMultiAtlasTexturesEnabled").boolValue = true;
            so.FindProperty("m_AtlasWidth").intValue = PreferredAtlasSize;
            so.FindProperty("m_AtlasHeight").intValue = PreferredAtlasSize;
            so.ApplyModifiedPropertiesWithoutUndo();
            font.isMultiAtlasTexturesEnabled = true;

            int samplingPointSize = font.faceInfo.pointSize > 0
                ? Mathf.RoundToInt(font.faceInfo.pointSize)
                : 90;
            int padding = font.atlasPadding > 0 ? font.atlasPadding : 9;

            var creation = font.creationSettings;
            creation.characterSetSelectionMode = 7; // Characters from File
            creation.referencedTextAssetGUID = CharsetGuid;
            creation.atlasWidth = PreferredAtlasSize;
            creation.atlasHeight = PreferredAtlasSize;
            creation.pointSize = samplingPointSize;
            creation.padding = padding;
            creation.renderMode = (int)font.atlasRenderMode;
            font.creationSettings = creation;

            bool ok = font.TryAddCharacters(characters, out string missing);
            font.ReadFontAssetDefinition();

            so = new SerializedObject(font);
            so.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
            so.FindProperty("m_IsMultiAtlasTexturesEnabled").boolValue = true;
            so.FindProperty("m_AtlasWidth").intValue = PreferredAtlasSize;
            so.FindProperty("m_AtlasHeight").intValue = PreferredAtlasSize;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(font);
            if (font.material != null)
            {
                EditorUtility.SetDirty(font.material);
            }

            if (font.atlasTextures != null)
            {
                foreach (Texture2D atlas in font.atlasTextures)
                {
                    if (atlas != null)
                    {
                        EditorUtility.SetDirty(atlas);
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(FontAssetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();

            // 外部材质（如 TableNineTmpScanlineSdf_SmileySans）仍指向旧 atlas fileID 时，对齐到新主图集。
            int retargeted = RetargetMaterialsToFontAtlas(FontAssetPath);
            sw.Stop();

            // 重新加载拿最终计数（Save 后嵌套 atlas 子资源可能更新）。
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            int afterChars = font != null && font.characterTable != null ? font.characterTable.Count : 0;
            int afterGlyphs = font != null && font.glyphTable != null ? font.glyphTable.Count : 0;
            int afterAtlases = font != null ? font.atlasTextureCount : 0;
            int missingCount = string.IsNullOrEmpty(missing) ? 0 : missing.Length;
            bool clearOnBuild = false;
            if (font != null)
            {
                var so2 = new SerializedObject(font);
                clearOnBuild = so2.FindProperty("m_ClearDynamicDataOnBuild").boolValue;
            }

            var sb = new StringBuilder(512);
            // 源字体本身不含的码点记为 PARTIAL，不算工具失败。
            string status = missingCount == 0 ? "OK" : (afterChars > beforeChars ? "PARTIAL" : "FAIL");
            sb.AppendLine(status);
            sb.AppendLine($"font={FontAssetPath}");
            sb.AppendLine($"guid={AssetDatabase.AssetPathToGUID(FontAssetPath)} (unchanged)");
            sb.AppendLine($"charsetUnique={characters.Length}");
            sb.AppendLine($"chars {beforeChars} -> {afterChars}");
            sb.AppendLine($"glyphs {beforeGlyphs} -> {afterGlyphs}");
            sb.AppendLine(
                $"atlases {beforeAtlases} -> {afterAtlases} ({PreferredAtlasSize}x{PreferredAtlasSize}, multi={(font != null && font.isMultiAtlasTexturesEnabled)})");
            sb.AppendLine($"clearDynamicDataOnBuild={clearOnBuild}");
            sb.AppendLine($"population={(font != null ? font.atlasPopulationMode.ToString() : "?")}");
            sb.AppendLine($"materialsRetargeted={retargeted}");
            sb.AppendLine($"missingCount={missingCount}");
            if (missingCount > 0)
            {
                const int preview = 80;
                sb.AppendLine(
                    missingCount <= preview
                        ? $"missing={missing}"
                        : $"missingPreview={missing.Substring(0, preview)}... (+{missingCount - preview})");
            }

            sb.AppendLine($"elapsedMs={sw.ElapsedMilliseconds}");
            return sb.ToString().TrimEnd();
        }

        static int RetargetMaterialsToFontAtlas(string fontAssetPath)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontAssetPath);
            if (font == null || font.atlasTexture == null)
            {
                return 0;
            }

            string fontGuid = AssetDatabase.AssetPathToGUID(fontAssetPath);
            string[] matGuids = AssetDatabase.FindAssets("t:Material");
            int changed = 0;
            foreach (string matGuid in matGuids)
            {
                string matPath = AssetDatabase.GUIDToAssetPath(matGuid);
                if (string.IsNullOrEmpty(matPath) || !matPath.StartsWith("Assets/", StringComparison.Ordinal))
                {
                    continue;
                }

                // 跳过字体资产自身内嵌材质（由 TMP 维护）。
                if (string.Equals(matPath, fontAssetPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null || !mat.HasProperty(ShaderUtilities.ID_MainTex))
                {
                    continue;
                }

                Texture main = mat.GetTexture(ShaderUtilities.ID_MainTex);
                if (main == null)
                {
                    continue;
                }

                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(main, out string texGuid, out long _))
                {
                    continue;
                }

                if (!string.Equals(texGuid, fontGuid, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (main == font.atlasTexture)
                {
                    continue;
                }

                mat.SetTexture(ShaderUtilities.ID_MainTex, font.atlasTexture);
                if (mat.HasProperty("_TextureWidth"))
                {
                    mat.SetFloat("_TextureWidth", font.atlasWidth);
                }

                if (mat.HasProperty("_TextureHeight"))
                {
                    mat.SetFloat("_TextureHeight", font.atlasHeight);
                }

                if (mat.HasProperty(ShaderUtilities.ID_GradientScale))
                {
                    mat.SetFloat(ShaderUtilities.ID_GradientScale, font.atlasPadding + 1);
                }

                EditorUtility.SetDirty(mat);
                changed++;
            }

            if (changed > 0)
            {
                AssetDatabase.SaveAssets();
            }

            return changed;
        }
    }
}

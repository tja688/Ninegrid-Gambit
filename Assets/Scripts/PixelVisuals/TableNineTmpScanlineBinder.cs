using TMPro;
using UnityEngine;

namespace NineGrid.Presentation.Visuals
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class TableNineTmpScanlineBinder : MonoBehaviour
    {
        [SerializeField] private TableNinePixelSnapController controller;
        [SerializeField] private Material bitmapScanlinePreset;
        [SerializeField] private Material sdfScanlinePreset;

        private TextMeshProUGUI text;
        private Material runtimeMaterial;
        private TMP_FontAsset boundFont;
        private Material boundPreset;

        private void OnEnable()
        {
            text = GetComponent<TextMeshProUGUI>();
            if (controller == null)
            {
                controller = FindFirstObjectByType<TableNinePixelSnapController>();
            }

            RefreshMaterial(forceRebuild: true);
            controller?.ApplyNow();
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
            {
                CleanupRuntimeMaterial();
            }
        }

        private void OnDestroy()
        {
            CleanupRuntimeMaterial();
        }

        private void OnValidate()
        {
            if (text == null)
            {
                text = GetComponent<TextMeshProUGUI>();
            }

            RefreshMaterial(forceRebuild: false);
            controller?.ApplyNow();
        }

        private void LateUpdate()
        {
            if (text == null || text.font == null)
                return;

            if (text.font != boundFont)
            {
                RefreshMaterial(forceRebuild: true);
                return;
            }

            SyncScanlineParamsFromPreset();
            SyncOutlineParams();
        }

        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

        private void SyncOutlineParams()
        {
            if (runtimeMaterial == null || text == null)
                return;

            if (!runtimeMaterial.HasProperty(OutlineWidthId))
                return;

            runtimeMaterial.SetFloat(OutlineWidthId, text.outlineWidth);
            runtimeMaterial.SetColor(OutlineColorId, text.outlineColor);
        }

        private void SyncScanlineParamsFromPreset()
        {
            if (runtimeMaterial == null)
                return;

            Material preset = ResolvePreset(text.font);
            if (preset == null)
                return;

            runtimeMaterial.SetVector("_PixelResolution", preset.GetVector("_PixelResolution"));
            runtimeMaterial.SetFloat("_ScanlineEnabled", preset.GetFloat("_ScanlineEnabled"));
            runtimeMaterial.SetFloat("_ScanlineIntensity", preset.GetFloat("_ScanlineIntensity"));
            runtimeMaterial.SetFloat("_ScanlineSpacing", preset.GetFloat("_ScanlineSpacing"));
        }

        public void RebindNow()
        {
            RefreshMaterial(forceRebuild: true);
            controller?.ApplyNow();
        }

        public void RefreshMaterial(bool forceRebuild = false)
        {
            if (text == null)
                return;

            Material activePreset = ResolvePreset(text.font);
            if (activePreset == null)
                return;

            RepairBrokenTextMaterialReference(activePreset);

            if (!forceRebuild)
            {
                forceRebuild = ShouldReplaceSharedMaterial(activePreset);
            }

            if (!forceRebuild
                && runtimeMaterial != null
                && boundFont == text.font
                && boundPreset == activePreset
                && text.fontSharedMaterial == runtimeMaterial)
            {
                return;
            }

            Material staleSharedMaterial = text.fontSharedMaterial;
            CleanupRuntimeMaterial();
            DetachStaleSharedMaterial(staleSharedMaterial, activePreset);

            runtimeMaterial = new Material(activePreset)
            {
                name = activePreset.name + " (Instance)",
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
            };

            TableNineTmpScanlineUtility.CopyFontMaterial(text.font, runtimeMaterial);
            text.fontSharedMaterial = runtimeMaterial;
            boundFont = text.font;
            boundPreset = activePreset;
            SyncOutlineParams();
        }

        private Material ResolvePreset(TMP_FontAsset font)
        {
            if (font == null)
                return null;

            if (TableNineTmpScanlineUtility.IsBitmapFont(font))
                return bitmapScanlinePreset;

            return sdfScanlinePreset != null ? sdfScanlinePreset : bitmapScanlinePreset;
        }

        private void CleanupRuntimeMaterial()
        {
            if (runtimeMaterial == null)
                return;

            RestoreTextMaterialReference();

            if (Application.isPlaying)
            {
                Destroy(runtimeMaterial);
            }
            else
            {
                DestroyImmediate(runtimeMaterial);
            }

            runtimeMaterial = null;
            boundFont = null;
            boundPreset = null;
        }

        private void RestoreTextMaterialReference()
        {
            if (text == null || text.fontSharedMaterial != runtimeMaterial)
                return;

            Material fallback = boundPreset ?? ResolvePreset(boundFont ?? text.font);
            if (fallback == null && text.font != null)
            {
                fallback = text.font.material;
            }

            if (fallback != null && fallback != runtimeMaterial)
            {
                text.fontSharedMaterial = fallback;
            }
        }

        private void RepairBrokenTextMaterialReference(Material activePreset)
        {
            if (text == null)
                return;

            if (text.fontSharedMaterial != null)
                return;

            if (activePreset != null)
            {
                text.fontSharedMaterial = activePreset;
                return;
            }

            if (text.font != null && text.font.material != null)
            {
                text.fontSharedMaterial = text.font.material;
            }
        }

        private bool ShouldReplaceSharedMaterial(Material activePreset)
        {
            Material shared = text.fontSharedMaterial;
            if (shared == null)
                return true;

            if (shared == runtimeMaterial || shared == activePreset)
                return false;

            if (shared == bitmapScanlinePreset || shared == sdfScanlinePreset)
                return true;

            return shared.name.Contains("(Instance)");
        }

        private void DetachStaleSharedMaterial(Material staleSharedMaterial, Material activePreset)
        {
            if (text == null || staleSharedMaterial == null || staleSharedMaterial == activePreset)
                return;

            if (staleSharedMaterial == bitmapScanlinePreset || staleSharedMaterial == sdfScanlinePreset)
                return;

            if (staleSharedMaterial == runtimeMaterial)
                return;

            if (!staleSharedMaterial.name.Contains("(Instance)"))
                return;

            text.fontSharedMaterial = activePreset;
        }
    }

    internal static class TableNineTmpScanlineUtility
    {
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int FaceTexId = Shader.PropertyToID("_FaceTex");
        private static readonly int FaceColorId = Shader.PropertyToID("_FaceColor");
        private static readonly int TextureWidthId = Shader.PropertyToID("_TextureWidth");
        private static readonly int TextureHeightId = Shader.PropertyToID("_TextureHeight");
        private static readonly int GradientScaleId = Shader.PropertyToID("_GradientScale");
        private static readonly int ScaleXId = Shader.PropertyToID("_ScaleX");
        private static readonly int ScaleYId = Shader.PropertyToID("_ScaleY");
        private static readonly int WeightNormalId = Shader.PropertyToID("_WeightNormal");
        private static readonly int WeightBoldId = Shader.PropertyToID("_WeightBold");
        private static readonly int ScaleRatioAId = Shader.PropertyToID("_ScaleRatioA");
        private static readonly int ScaleRatioBId = Shader.PropertyToID("_ScaleRatioB");
        private static readonly int ScaleRatioCId = Shader.PropertyToID("_ScaleRatioC");

        public static bool IsBitmapFont(TMP_FontAsset font)
        {
            if (font == null || font.material == null || font.material.shader == null)
                return false;

            string shaderName = font.material.shader.name;
            return shaderName.Contains("Bitmap");
        }

        public static void CopyFontMaterial(TMP_FontAsset font, Material target)
        {
            if (font == null || target == null || font.material == null)
                return;

            Material source = font.material;
            if (source.HasProperty(MainTexId) && source.GetTexture(MainTexId) != null)
            {
                target.SetTexture(MainTexId, source.GetTexture(MainTexId));
            }

            CopyTexture(source, target, FaceTexId);
            CopyColor(source, target, FaceColorId);
            CopyFloat(source, target, TextureWidthId);
            CopyFloat(source, target, TextureHeightId);
            CopyFloat(source, target, GradientScaleId);
            CopyFloat(source, target, ScaleXId);
            CopyFloat(source, target, ScaleYId);
            CopyFloat(source, target, WeightNormalId);
            CopyFloat(source, target, WeightBoldId);
            CopyFloat(source, target, ScaleRatioAId);
            CopyFloat(source, target, ScaleRatioBId);
            CopyFloat(source, target, ScaleRatioCId);

            if (font.atlasTexture != null)
            {
                target.SetFloat(TextureWidthId, font.atlasTexture.width);
                target.SetFloat(TextureHeightId, font.atlasTexture.height);
            }
        }

        private static void CopyTexture(Material source, Material target, int propertyId)
        {
            if (!source.HasProperty(propertyId) || !target.HasProperty(propertyId))
                return;

            Texture texture = source.GetTexture(propertyId);
            if (texture != null)
            {
                target.SetTexture(propertyId, texture);
            }
        }

        private static void CopyColor(Material source, Material target, int propertyId)
        {
            if (source.HasProperty(propertyId) && target.HasProperty(propertyId))
            {
                target.SetColor(propertyId, source.GetColor(propertyId));
            }
        }

        private static void CopyFloat(Material source, Material target, int propertyId)
        {
            if (source.HasProperty(propertyId) && target.HasProperty(propertyId))
            {
                target.SetFloat(propertyId, source.GetFloat(propertyId));
            }
        }
    }
}

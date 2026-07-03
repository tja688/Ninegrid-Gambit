using UnityEngine;

namespace NineGrid.Presentation.Visuals
{
    [ExecuteAlways]
    public sealed class TableNinePixelSnapController : MonoBehaviour
    {
        [SerializeField] private Material postProcessMaterial;
        [SerializeField] private Material tmpScanlineMaterial;
        [SerializeField] private Material tmpBitmapScanlineMaterial;
        [SerializeField] private Material tmpSdfScanlineMaterial;
        [SerializeField] private Vector2Int internalResolution = new(480, 270);
        [SerializeField] private bool pixelSnap = true;

        [Header("Scanlines")]
        [SerializeField] private bool scanlines = true;
        [SerializeField, Range(0f, 1f)] private float scanlineIntensity = 0.12f;
        [SerializeField, Range(1f, 8f)] private float scanlineSpacing = 2f;

        private static readonly int PixelResolutionId = Shader.PropertyToID("_PixelResolution");
        private static readonly int PixelSnapId = Shader.PropertyToID("_PixelSnap");
        private static readonly int ScanlineEnabledId = Shader.PropertyToID("_ScanlineEnabled");
        private static readonly int ScanlineIntensityId = Shader.PropertyToID("_ScanlineIntensity");
        private static readonly int ScanlineSpacingId = Shader.PropertyToID("_ScanlineSpacing");

        private void OnEnable()
        {
            ApplyNow();
        }

        private void OnValidate()
        {
            internalResolution.x = Mathf.Max(1, internalResolution.x);
            internalResolution.y = Mathf.Max(1, internalResolution.y);
            ApplyNow();
        }

        public void ApplyNow()
        {
            Vector4 pixelResolution = new Vector4(internalResolution.x, internalResolution.y, 0f, 0f);
            float scanlineEnabled = scanlines ? 1f : 0f;

            if (postProcessMaterial != null)
            {
                postProcessMaterial.SetVector(PixelResolutionId, pixelResolution);
                postProcessMaterial.SetFloat(PixelSnapId, pixelSnap ? 1f : 0f);
                postProcessMaterial.SetFloat(ScanlineEnabledId, scanlineEnabled);
                postProcessMaterial.SetFloat(ScanlineIntensityId, scanlineIntensity);
                postProcessMaterial.SetFloat(ScanlineSpacingId, scanlineSpacing);
            }

            if (tmpScanlineMaterial != null)
            {
                ApplyScanlineMaterial(tmpScanlineMaterial, pixelResolution, scanlineEnabled, scanlineIntensity, scanlineSpacing);
            }

            if (tmpBitmapScanlineMaterial != null)
            {
                ApplyScanlineMaterial(tmpBitmapScanlineMaterial, pixelResolution, scanlineEnabled, scanlineIntensity, scanlineSpacing);
            }

            if (tmpSdfScanlineMaterial != null)
            {
                ApplyScanlineMaterial(tmpSdfScanlineMaterial, pixelResolution, scanlineEnabled, scanlineIntensity, scanlineSpacing);
            }
        }

        private static void ApplyScanlineMaterial(
            Material material,
            Vector4 pixelResolution,
            float scanlineEnabled,
            float intensity,
            float spacing)
        {
            material.SetVector(PixelResolutionId, pixelResolution);
            material.SetFloat(ScanlineEnabledId, scanlineEnabled);
            material.SetFloat(ScanlineIntensityId, intensity);
            material.SetFloat(ScanlineSpacingId, spacing);
        }
    }
}

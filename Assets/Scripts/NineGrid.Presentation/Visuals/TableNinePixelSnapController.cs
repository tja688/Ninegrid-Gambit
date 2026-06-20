using UnityEngine;

namespace NineGrid.Presentation.Visuals
{
    [ExecuteAlways]
    public sealed class TableNinePixelSnapController : MonoBehaviour
    {
        [SerializeField] private Material postProcessMaterial;
        [SerializeField] private Vector2Int internalResolution = new(320, 180);
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
            if (postProcessMaterial == null)
                return;

            postProcessMaterial.SetVector(PixelResolutionId, new Vector4(internalResolution.x, internalResolution.y, 0f, 0f));
            postProcessMaterial.SetFloat(PixelSnapId, pixelSnap ? 1f : 0f);
            postProcessMaterial.SetFloat(ScanlineEnabledId, scanlines ? 1f : 0f);
            postProcessMaterial.SetFloat(ScanlineIntensityId, scanlineIntensity);
            postProcessMaterial.SetFloat(ScanlineSpacingId, scanlineSpacing);
        }
    }
}

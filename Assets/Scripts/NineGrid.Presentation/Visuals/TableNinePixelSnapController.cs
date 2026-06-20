using UnityEngine;

namespace NineGrid.Presentation.Visuals
{
    [ExecuteAlways]
    public sealed class TableNinePixelSnapController : MonoBehaviour
    {
        [SerializeField] private Material postProcessMaterial;
        [SerializeField] private Vector2Int internalResolution = new(320, 180);
        [SerializeField] private bool pixelSnap = true;

        private static readonly int PixelResolutionId = Shader.PropertyToID("_PixelResolution");
        private static readonly int PixelSnapId = Shader.PropertyToID("_PixelSnap");

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
        }
    }
}

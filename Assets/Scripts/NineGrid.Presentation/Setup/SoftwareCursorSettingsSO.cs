using UnityEngine;

namespace NineGrid.Presentation.Setup
{
    /// <summary>
    /// 软件光标设置：避免 Windows 高 DPI 对硬件光标软放大导致又大又糊。
    /// Inspector 里调 <see cref="sizeScale"/> 即可改显示大小（最近邻整数倍）。
    /// </summary>
    [CreateAssetMenu(
        fileName = "SoftwareCursorSettings",
        menuName = "NineGrid/Presentation/Software Cursor Settings")]
    public sealed class SoftwareCursorSettingsSO : ScriptableObject
    {
        public const string ResourcePath = "UI/SoftwareCursor";
        private const int MaxSizeScale = 8;

        [SerializeField] private Texture2D cursorTexture;
        [SerializeField] private Vector2 hotspot = Vector2.zero;
        [SerializeField]
        [Range(1, MaxSizeScale)]
        [Tooltip("相对原图像素的整数倍放大（最近邻，保持像素锐利）。")]
        private int sizeScale = 2;

        public Texture2D CursorTexture => cursorTexture;
        public Vector2 Hotspot => hotspot;
        public int SizeScale => Mathf.Clamp(sizeScale, 1, MaxSizeScale);

        /// <summary>
        /// 按 <see cref="SizeScale"/> 生成用于 SetCursor 的贴图；倍数为 1 时直接返回原贴图。
        /// </summary>
        public Texture2D BuildDisplayTexture()
        {
            if (cursorTexture == null)
            {
                return null;
            }

            var scale = SizeScale;
            if (scale == 1)
            {
                return cursorTexture;
            }

            var srcW = cursorTexture.width;
            var srcH = cursorTexture.height;
            var dstW = srcW * scale;
            var dstH = srcH * scale;
            var srcPixels = cursorTexture.GetPixels32();
            var dstPixels = new Color32[dstW * dstH];

            for (var y = 0; y < dstH; y++)
            {
                var srcY = y / scale;
                var srcRow = srcY * srcW;
                var dstRow = y * dstW;
                for (var x = 0; x < dstW; x++)
                {
                    dstPixels[dstRow + x] = srcPixels[srcRow + (x / scale)];
                }
            }

            var display = new Texture2D(dstW, dstH, TextureFormat.RGBA32, mipChain: false)
            {
                name = cursorTexture.name + "_x" + scale,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            display.SetPixels32(dstPixels);
            display.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            return display;
        }

        public Vector2 BuildDisplayHotspot()
        {
            return hotspot * SizeScale;
        }
    }
}

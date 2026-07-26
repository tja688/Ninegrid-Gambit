using TMPro;
using UnityEngine;

namespace NineGrid.UI
{
    /// <summary>
    /// Bitmap TMP 像素黑边：在 OnPreRenderText 把每个可见字形做八向平移复制上色。
    /// 不采样 atlas 邻域，避免相邻字形串色噪音。
    /// 内联 Sprite（描述图标等）跳过描边，只保留本体。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    [ExecuteAlways]
    public sealed class TmpBitmapPixelOutline : MonoBehaviour
    {
        static readonly Vector2[] OutlineOffsets =
        {
            new Vector2(1f, 0f),
            new Vector2(-1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, -1f),
            new Vector2(1f, 1f),
            new Vector2(1f, -1f),
            new Vector2(-1f, 1f),
            new Vector2(-1f, -1f),
        };

        [SerializeField]
        [Tooltip("黑边颜色。Alpha 参与最终透明度。")]
        Color outlineColor = Color.black;

        [SerializeField]
        [Tooltip("黑边厚度（字体像素）。1 = 扩一圈字库像素；本地偏移 = 该值 × 字符 scale。")]
        float outlineFontPixels = 1f;

        [SerializeField]
        [Tooltip("是否启用八向黑边。关闭则只画原字。")]
        bool outlineEnabled = true;

        TMP_Text _text;
        TextMeshProUGUI _ugui;
        bool _subscribed;

        void Awake()
        {
            CacheText();
        }

        void OnEnable()
        {
            CacheText();
            SuppressAtlasPaddingExpand();
            Subscribe();
            if (_text != null)
            {
                _text.SetVerticesDirty();
            }
        }

        void OnDisable()
        {
            Unsubscribe();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            outlineFontPixels = Mathf.Max(0f, outlineFontPixels);
            CacheText();
            if (_text != null && isActiveAndEnabled)
            {
                SuppressAtlasPaddingExpand();
                _text.SetVerticesDirty();
            }
        }
#endif

        void CacheText()
        {
            _text = GetComponent<TMP_Text>();
            _ugui = _text as TextMeshProUGUI;
        }

        void Subscribe()
        {
            if (_subscribed || _text == null)
            {
                return;
            }

            // TextMeshProUGUI override 了 OnPreRenderText：必须订到派生事件，基类字段不会被 Invoke。
            if (_ugui != null)
            {
                _ugui.OnPreRenderText += HandlePreRenderText;
            }
            else
            {
                _text.OnPreRenderText += HandlePreRenderText;
            }

            _subscribed = true;
        }

        void Unsubscribe()
        {
            if (!_subscribed || _text == null)
            {
                return;
            }

            if (_ugui != null)
            {
                _ugui.OnPreRenderText -= HandlePreRenderText;
            }
            else
            {
                _text.OnPreRenderText -= HandlePreRenderText;
            }

            _subscribed = false;
        }

        void SuppressAtlasPaddingExpand()
        {
            if (_text == null)
            {
                return;
            }

            // 关闭 UV 外扩，避免材质残留 _OutlineWidth 把 mesh 扩进 atlas 邻字。
            _text.extraPadding = false;
            Material mat = _text.fontSharedMaterial;
            if (mat != null && mat.HasProperty(ShaderUtilities.ID_OutlineWidth))
            {
                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
            }

            _text.UpdateMeshPadding();
        }

        void HandlePreRenderText(TMP_TextInfo textInfo)
        {
            if (!outlineEnabled || outlineFontPixels <= 0.001f || outlineColor.a <= 0.001f)
            {
                return;
            }

            int visibleCharCount = 0;
            for (int i = 0; i < textInfo.characterCount; i++)
            {
                if (textInfo.characterInfo[i].isVisible)
                {
                    visibleCharCount++;
                }
            }

            if (visibleCharCount == 0)
            {
                return;
            }

            int copyCount = OutlineOffsets.Length;
            // 每个可见字符 4 顶点；黑边 copy + 本体 1 份
            int layers = copyCount + 1;
            int quadCount = visibleCharCount * layers;
            Color32 outlineC32 = outlineColor;

            for (int materialIndex = 0; materialIndex < textInfo.meshInfo.Length; materialIndex++)
            {
                TMP_MeshInfo src = textInfo.meshInfo[materialIndex];
                if (src.vertices == null || src.vertices.Length == 0)
                {
                    continue;
                }

                // TMP_MeshInfo 是 struct：Resize 后必须写回，否则扩容丢失。
                Vector3[] srcVertices = (Vector3[])src.vertices.Clone();
                Color32[] srcColors = src.colors32 != null ? (Color32[])src.colors32.Clone() : null;
                Vector4[] srcUvs0 = src.uvs0 != null ? (Vector4[])src.uvs0.Clone() : null;
                Vector2[] srcUvs2 = src.uvs2 != null ? (Vector2[])src.uvs2.Clone() : null;

                // ResizeMeshInfo 参数是四边形数量（每个字符 1 个 quad），不是顶点数。
                src.ResizeMeshInfo(quadCount);
                textInfo.meshInfo[materialIndex] = src;
                TMP_MeshInfo dst = textInfo.meshInfo[materialIndex];

                Vector3[] dstVertices = dst.vertices;
                Color32[] dstColors = dst.colors32;
                Vector4[] dstUvs0 = dst.uvs0;
                Vector2[] dstUvs2 = dst.uvs2;

                int vertWrite = 0;

                // 层：先黑边，后本体（后画盖住黑边重合区）
                for (int layer = 0; layer < layers; layer++)
                {
                    bool isOutline = layer < copyCount;
                    Vector2 dir = isOutline ? OutlineOffsets[layer] : Vector2.zero;

                    for (int ci = 0; ci < textInfo.characterCount; ci++)
                    {
                        TMP_CharacterInfo ch = textInfo.characterInfo[ci];
                        if (!ch.isVisible || ch.materialReferenceIndex != materialIndex)
                        {
                            continue;
                        }

                        // 内联 Sprite 不走像素描边：八向复制会在图标外形成随 scale 变大的黑边噪点。
                        if (isOutline && ch.elementType == TMP_TextElementType.Sprite)
                        {
                            continue;
                        }

                        int srcVi = ch.vertexIndex;
                        float shift = outlineFontPixels * ch.scale;
                        float dx = dir.x * shift;
                        float dy = dir.y * shift;

                        for (int v = 0; v < 4; v++)
                        {
                            Vector3 p = srcVertices[srcVi + v];
                            p.x += dx;
                            p.y += dy;
                            dstVertices[vertWrite + v] = p;

                            if (dstColors != null && srcColors != null)
                            {
                                Color32 srcC = srcColors[srcVi + v];
                                if (isOutline)
                                {
                                    dstColors[vertWrite + v] = new Color32(
                                        outlineC32.r,
                                        outlineC32.g,
                                        outlineC32.b,
                                        (byte)((srcC.a * outlineC32.a) / 255));
                                }
                                else
                                {
                                    dstColors[vertWrite + v] = srcC;
                                }
                            }

                            if (dstUvs0 != null && srcUvs0 != null)
                            {
                                dstUvs0[vertWrite + v] = srcUvs0[srcVi + v];
                            }

                            if (dstUvs2 != null && srcUvs2 != null)
                            {
                                dstUvs2[vertWrite + v] = srcUvs2[srcVi + v];
                            }
                        }

                        vertWrite += 4;
                    }
                }

                dst.vertexCount = vertWrite;
                textInfo.meshInfo[materialIndex] = dst;
            }
        }
    }
}

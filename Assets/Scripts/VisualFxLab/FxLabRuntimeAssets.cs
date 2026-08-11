using UnityEngine;
using UnityEngine.Rendering;

namespace NineGrid.VisualFxLab
{
    /// <summary>
    /// 画面实验室运行时生成的贴图 / 材质 / 精灵，全部程序化产出，不依赖新增美术资产。
    /// 生成物 hideFlags = DontSave，只活在会话内存里，不污染场景与资产库。
    /// </summary>
    public static class FxLabRuntimeAssets
    {
        const string ParticleShaderName = "NineGrid/FxLab/Particle";
        const string FoilShaderName = "NineGrid/FxLab/CardFoil";

        static Texture2D _softCircle;
        static Texture2D _sparkle;
        static Texture2D _ring;
        static Material _foilMaterial;
        static Sprite _ringSprite;

        /// <summary>柔和圆形光斑（浮尘 / 余烬用）。</summary>
        public static Texture2D SoftCircle
        {
            get
            {
                if (_softCircle == null)
                {
                    _softCircle = CreateTexture(64, "FxLab_SoftCircle", (x, y, size) =>
                    {
                        float r = Radius(x, y, size);
                        float a = Mathf.Clamp01(1f - r);
                        a = a * a * (3f - 2f * a);
                        return new Color(1f, 1f, 1f, a * a);
                    });
                }

                return _softCircle;
            }
        }

        /// <summary>四芒星闪光（光环轨道火花用）。</summary>
        public static Texture2D Sparkle
        {
            get
            {
                if (_sparkle == null)
                {
                    _sparkle = CreateTexture(64, "FxLab_Sparkle", (x, y, size) =>
                    {
                        float nx = (x + 0.5f) / size * 2f - 1f;
                        float ny = (y + 0.5f) / size * 2f - 1f;
                        float r = Mathf.Sqrt(nx * nx + ny * ny);
                        float cross = Mathf.Max(
                            Mathf.Exp(-Mathf.Abs(nx) * 9f) * Mathf.Exp(-Mathf.Abs(ny) * 2.4f),
                            Mathf.Exp(-Mathf.Abs(ny) * 9f) * Mathf.Exp(-Mathf.Abs(nx) * 2.4f));
                        float glow = Mathf.Exp(-r * 4f);
                        float a = Mathf.Clamp01(cross + glow * 0.45f);
                        return new Color(1f, 1f, 1f, a);
                    });
                }

                return _sparkle;
            }
        }

        /// <summary>柔和光环（机关卡环绕底环用）。</summary>
        public static Texture2D Ring
        {
            get
            {
                if (_ring == null)
                {
                    _ring = CreateTexture(128, "FxLab_Ring", (x, y, size) =>
                    {
                        float r = Radius(x, y, size);
                        float band = Mathf.Exp(-Mathf.Pow((r - 0.8f) * 8.5f, 2f));
                        float inner = 0.16f * Mathf.Exp(-Mathf.Pow((r - 0.55f) * 6f, 2f));
                        float a = Mathf.Clamp01(band + inner) * Mathf.Clamp01((1f - r) * 8f);
                        return new Color(1f, 1f, 1f, a);
                    });
                }

                return _ring;
            }
        }

        /// <summary>光环底环精灵（约 2.4 世界单位直径）。</summary>
        public static Sprite RingSprite
        {
            get
            {
                if (_ringSprite == null)
                {
                    _ringSprite = Sprite.Create(
                        Ring, new Rect(0f, 0f, Ring.width, Ring.height),
                        new Vector2(0.5f, 0.5f), Ring.width / 2.4f);
                    _ringSprite.name = "FxLab_RingSprite";
                    _ringSprite.hideFlags = HideFlags.DontSave;
                }

                return _ringSprite;
            }
        }

        /// <summary>卡面流光共享材质（Blend One One，几何 Pixel Snap 与底卡一致）。</summary>
        public static Material FoilMaterial
        {
            get
            {
                if (_foilMaterial == null)
                {
                    var shader = Shader.Find(FoilShaderName);
                    if (shader == null)
                    {
                        return null;
                    }

                    _foilMaterial = new Material(shader)
                    {
                        name = "FxLab_Foil",
                        hideFlags = HideFlags.DontSave,
                    };
                }

                return _foilMaterial;
            }
        }

        /// <summary>创建一份粒子材质（additive = 加色，否则半透明）。调用方持有生命周期。</summary>
        public static Material CreateParticleMaterial(Texture2D texture, bool additive, string name)
        {
            var shader = Shader.Find(ParticleShaderName);
            if (shader == null)
            {
                return null;
            }

            var mat = new Material(shader)
            {
                name = name,
                hideFlags = HideFlags.DontSave,
                mainTexture = texture,
            };
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            return mat;
        }

        static float Radius(int x, int y, int size)
        {
            float nx = (x + 0.5f) / size * 2f - 1f;
            float ny = (y + 0.5f) / size * 2f - 1f;
            return Mathf.Sqrt(nx * nx + ny * ny);
        }

        static Texture2D CreateTexture(int size, string name, System.Func<int, int, int, Color> fill)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    pixels[y * size + x] = fill(x, y, size);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return tex;
        }
    }
}

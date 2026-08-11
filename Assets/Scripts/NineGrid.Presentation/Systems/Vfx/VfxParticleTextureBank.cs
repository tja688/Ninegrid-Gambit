using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Presentation.Systems.Vfx
{
    public enum VfxParticleTexture
    {
        /// <summary>32px 径向柔光圆点；烟雾 / 光尘 / 辉光。</summary>
        SoftDot,

        /// <summary>8px 硬边白方块（Point 过滤）；像素碎屑，最贴合像素画风。</summary>
        Pixel,

        /// <summary>16px 四角菱形星芒；火花 / 星光。</summary>
        Spark,

        /// <summary>32px 柔边圆环；冲击波 / 符能环。</summary>
        Ring,

        /// <summary>8px 直角三角碎片（Point 过滤）；护甲 / 骨片等碎裂物。</summary>
        Shard,
    }

    public enum VfxParticleBlend
    {
        /// <summary>普通透明混合（Sprites/Default）；实体碎屑与烟尘。</summary>
        Alpha,

        /// <summary>加色混合（NineGrid/VFX/ParticleAdditive）；发光火花与光点。</summary>
        Additive,
    }

    /// <summary>
    /// 程序化粒子贴图与材质库：全部纹理运行时生成，不依赖美术资产；
    /// 按（纹理形状 × 混合模式）缓存共享材质，播放器只读。
    /// </summary>
    internal static class VfxParticleTextureBank
    {
        private const string AdditiveShaderName = "NineGrid/VFX/ParticleAdditive";
        private const string AlphaShaderName = "Sprites/Default";

        private static readonly Dictionary<VfxParticleTexture, Texture2D> sTextures =
            new Dictionary<VfxParticleTexture, Texture2D>();
        private static readonly Dictionary<(VfxParticleTexture, VfxParticleBlend), Material> sMaterials =
            new Dictionary<(VfxParticleTexture, VfxParticleBlend), Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            sTextures.Clear();
            sMaterials.Clear();
        }

        public static Material GetMaterial(VfxParticleTexture texture, VfxParticleBlend blend)
        {
            if (sMaterials.TryGetValue((texture, blend), out var cached) && cached != null)
            {
                return cached;
            }

            var shader = ResolveShader(blend);
            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader)
            {
                name = "VfxParticle-" + texture + "-" + blend,
                mainTexture = GetTexture(texture),
                hideFlags = HideFlags.HideAndDontSave,
            };
            sMaterials[(texture, blend)] = material;
            return material;
        }

        private static Shader ResolveShader(VfxParticleBlend blend)
        {
            if (blend == VfxParticleBlend.Additive)
            {
                var additive = Shader.Find(AdditiveShaderName);
                if (additive != null)
                {
                    return additive;
                }
            }

            return Shader.Find(AlphaShaderName);
        }

        public static Texture2D GetTexture(VfxParticleTexture kind)
        {
            if (sTextures.TryGetValue(kind, out var cached) && cached != null)
            {
                return cached;
            }

            Texture2D texture;
            switch (kind)
            {
                case VfxParticleTexture.Pixel:
                    texture = BakePixel();
                    break;
                case VfxParticleTexture.Spark:
                    texture = BakeSpark();
                    break;
                case VfxParticleTexture.Ring:
                    texture = BakeRing();
                    break;
                case VfxParticleTexture.Shard:
                    texture = BakeShard();
                    break;
                default:
                    texture = BakeSoftDot();
                    break;
            }

            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            sTextures[kind] = texture;
            return texture;
        }

        private static Texture2D BakeSoftDot()
        {
            const int size = 32;
            var texture = NewTexture(size, FilterMode.Bilinear);
            var half = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - half) / half;
                    var dy = (y - half) / half;
                    var r = Mathf.Sqrt(dx * dx + dy * dy);
                    // 二次衰减：核心亮、边缘柔，加色混合下呈自然光斑。
                    var a = Mathf.Clamp01(1f - r);
                    a *= a;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D BakePixel()
        {
            const int size = 8;
            var texture = NewTexture(size, FilterMode.Point);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    texture.SetPixel(x, y, Color.white);
                }
            }

            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D BakeSpark()
        {
            const int size = 16;
            var texture = NewTexture(size, FilterMode.Bilinear);
            var half = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = Mathf.Abs(x - half) / half;
                    var dy = Mathf.Abs(y - half) / half;
                    // 菱形星芒：|x|+|y| 衰减，叠加十字轴向亮线。
                    var diamond = Mathf.Clamp01(1f - (dx + dy));
                    var cross = Mathf.Max(
                        Mathf.Clamp01(1f - dx * 4f) * Mathf.Clamp01(1f - dy),
                        Mathf.Clamp01(1f - dy * 4f) * Mathf.Clamp01(1f - dx));
                    var a = Mathf.Clamp01(Mathf.Max(diamond * diamond, cross * 0.85f));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D BakeRing()
        {
            const int size = 32;
            var texture = NewTexture(size, FilterMode.Bilinear);
            var half = (size - 1) * 0.5f;
            const float ringRadius = 0.72f;
            const float ringWidth = 0.16f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - half) / half;
                    var dy = (y - half) / half;
                    var r = Mathf.Sqrt(dx * dx + dy * dy);
                    var a = Mathf.Clamp01(1f - Mathf.Abs(r - ringRadius) / ringWidth);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            }

            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D BakeShard()
        {
            const int size = 8;
            var texture = NewTexture(size, FilterMode.Point);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    // 直角三角碎片：左下实、右上空。
                    var inside = x + y < size;
                    texture.SetPixel(x, y, inside ? Color.white : Color.clear);
                }
            }

            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D NewTexture(int size, FilterMode filter)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = filter,
            };
        }
    }
}

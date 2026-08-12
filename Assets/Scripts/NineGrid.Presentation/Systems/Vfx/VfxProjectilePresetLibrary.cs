using System;
using System.Collections.Generic;
using NineGrid.Content.Vfx;
using UnityEngine;

namespace NineGrid.Presentation.Systems.Vfx
{
    public enum VfxProjectilePathStyle
    {
        /// <summary>直线飞行；物理弹（飞刀/箭矢）。</summary>
        Line,

        /// <summary>二次贝塞尔弧线；抛射物（火球/毒液/法球）。</summary>
        Arc,

        /// <summary>直线叠加正弦摆动；能量弹（电光/流光）。</summary>
        Wave,
    }

    /// <summary>
    /// 单条弹道预设的完整参数。尺寸/速度/弧高全部为世界单位
    /// （参照：卡面约 3.8×4.9、格距 5×5.5，相邻格弹道距离约 5~7.4）。
    /// 起手 / 命中爆点复用 VfxParticlePresetLibrary 的粒子预设（MuzzlePresetId / ImpactPresetId），
    /// 用 Tint 字段把通用爆点染成弹道同色系。
    /// </summary>
    public sealed class VfxProjectilePreset
    {
        public string Id;
        public string Note;

        // —— 飞行 ——

        /// <summary>飞行速度（世界单位/秒）；实际时长按源靶距离求出后夹紧到 Min/Max。</summary>
        public float FlightSpeed = 10f;

        public float MinFlightSeconds = 0.16f;
        public float MaxFlightSeconds = 0.6f;

        public VfxProjectilePathStyle PathStyle = VfxProjectilePathStyle.Line;

        /// <summary>Arc：弧顶偏离直线的垂距（世界单位，偏向弹道上方）。</summary>
        public float ArcHeight;

        /// <summary>Arc：每发弹道弧高 ± 随机抖动（齐射时避免完全重叠）。</summary>
        public float ArcHeightJitter;

        /// <summary>Wave：摆动幅度（世界单位）。</summary>
        public float WaveAmplitude;

        /// <summary>Wave：全程摆动周期数。</summary>
        public float WaveCycles = 2f;

        /// <summary>时间缓动偏置 -1..1：&lt;0 出手快后减速（射出感），&gt;0 出手慢后加速（俯冲感），0 匀速。</summary>
        public float AccelBias;

        // —— 齐射 ——

        /// <summary>单次 Pulse 的弹道数（绑定 fps 字段 &gt; 0 时覆盖，夹紧 1~8）。</summary>
        public int Count = 1;

        /// <summary>多发时逐发起飞间隔（秒）。</summary>
        public float StaggerSeconds = 0.08f;

        /// <summary>多发时命中点在靶位周围的散布半径（世界单位）。</summary>
        public float TargetJitterRadius;

        // —— 弹头 ——

        public VfxParticleTexture HeadTexture = VfxParticleTexture.Spark;
        public VfxParticleBlend HeadBlend = VfxParticleBlend.Additive;

        /// <summary>弹头主 Sprite 直径（世界单位）。</summary>
        public float HeadSize = 0.3f;

        /// <summary>AlignToVelocity 时的前向/垂直拉伸倍率（x=沿速度，y=垂直）。</summary>
        public Vector2 HeadStretch = Vector2.one;

        /// <summary>弹头颜色：出发 → 到达 全程插值。</summary>
        public Color HeadColorA = Color.white;

        public Color HeadColorB = Color.white;

        /// <summary>true 时弹头朝向速度方向；false 时按 HeadSpinSpeed 自旋。</summary>
        public bool AlignToVelocity = true;

        /// <summary>不对齐速度时的自旋角速度（度/秒，每发随机正负）。</summary>
        public float HeadSpinSpeed;

        /// <summary>&gt;0 时弹头后叠一层柔光（SoftDot Additive，世界单位直径）。</summary>
        public float GlowSize;

        public Color GlowColor = Color.white;

        // —— 拖尾（ParticleSystem rateOverDistance，随弹头移动在世界空间留粒子） ——

        /// <summary>每世界单位路径释放的拖尾粒子数；0 = 无拖尾。</summary>
        public float TrailPerUnit = 6f;

        public VfxParticleTexture TrailTexture = VfxParticleTexture.SoftDot;
        public VfxParticleBlend TrailBlend = VfxParticleBlend.Additive;
        public Vector2 TrailLifetime = new Vector2(0.25f, 0.45f);
        public Vector2 TrailSize = new Vector2(0.08f, 0.16f);
        public VfxParticleSizeCurve TrailSizeCurve = VfxParticleSizeCurve.Shrink;
        public Color TrailColorA = Color.white;
        public Color TrailColorB = Color.white;

        /// <summary>拖尾粒子重力（正值下坠；毒液滴落用）。</summary>
        public float TrailGravity;

        /// <summary>拖尾粒子竖直漂移区间（世界单位/秒）。</summary>
        public Vector2 TrailDrift;

        public float TrailNoiseStrength;
        public float TrailNoiseFrequency = 0.5f;
        public bool TrailRandomRotation;

        // —— 起手 / 命中（复用 VfxParticlePresetLibrary 粒子预设） ——

        /// <summary>出手瞬间在源位置播的粒子预设 ID（VfxParticlePresetIds）；空 = 无起手光。</summary>
        public string MuzzlePresetId = string.Empty;

        public float MuzzleScale = 0.7f;
        public Color MuzzleTint = Color.white;

        /// <summary>命中瞬间在靶位置播的粒子预设 ID（VfxParticlePresetIds）；空 = 无爆点（不建议）。</summary>
        public string ImpactPresetId = string.Empty;

        public float ImpactScale = 1f;
        public Color ImpactTint = Color.white;

        /// <summary>&gt;0 时覆盖命中爆点粒子数。</summary>
        public int ImpactCountOverride;

        /// <summary>相对独立型基准排序（5000）的偏移；弹头应压住拖尾与多数爆点。</summary>
        public int SortingOrderDelta = 3;
    }

    /// <summary>
    /// 程序化弹道预设库：预设 ID 与 Content 侧 VfxProjectilePresetIds 一一对应
    /// （由 VfxProjectilePlayerContractTests 护栏）。配色延续粒子库语义色约定：
    /// 发光类（Additive）表达能量/魔法，像素碎屑类（Alpha）表达实体物。
    /// </summary>
    public static class VfxProjectilePresetLibrary
    {
        private static Dictionary<string, VfxProjectilePreset> sPresets;

        public static bool TryGet(string presetId, out VfxProjectilePreset preset)
        {
            preset = null;
            if (string.IsNullOrWhiteSpace(presetId))
            {
                return false;
            }

            return EnsureBuilt().TryGetValue(presetId.Trim(), out preset);
        }

        public static IReadOnlyCollection<string> AllIds => EnsureBuilt().Keys;

        private static Dictionary<string, VfxProjectilePreset> EnsureBuilt()
        {
            if (sPresets != null)
            {
                return sPresets;
            }

            var presets = new Dictionary<string, VfxProjectilePreset>(StringComparer.Ordinal);
            void Add(VfxProjectilePreset preset) => presets[preset.Id] = preset;

            Add(new VfxProjectilePreset
            {
                Id = VfxProjectilePresetIds.MagicBolt,
                Note = "蓝紫魔法飞弹：发光弹头微弧线加速飞向目标，光尘拖尾，星爆命中；通用效果伤害默认语言。",
                FlightSpeed = 10f,
                MinFlightSeconds = 0.22f,
                MaxFlightSeconds = 0.55f,
                PathStyle = VfxProjectilePathStyle.Arc,
                ArcHeight = 0.55f,
                ArcHeightJitter = 0.2f,
                AccelBias = 0.3f,
                HeadTexture = VfxParticleTexture.Spark,
                HeadBlend = VfxParticleBlend.Additive,
                HeadSize = 0.34f,
                HeadStretch = new Vector2(1.5f, 0.9f),
                HeadColorA = new Color(0.702f, 0.847f, 1f), // #B3D8FF
                HeadColorB = new Color(0.71f, 0.573f, 1f), // #B592FF
                GlowSize = 0.75f,
                GlowColor = new Color(0.498f, 0.573f, 1f, 0.55f), // #7F92FF
                TrailPerUnit = 8f,
                TrailTexture = VfxParticleTexture.SoftDot,
                TrailBlend = VfxParticleBlend.Additive,
                TrailLifetime = new Vector2(0.25f, 0.45f),
                TrailSize = new Vector2(0.08f, 0.18f),
                TrailColorA = new Color(0.498f, 0.698f, 1f), // #7FB2FF
                TrailColorB = new Color(0.71f, 0.573f, 1f), // #B592FF
                MuzzlePresetId = VfxParticlePresetIds.EffectRing,
                MuzzleScale = 0.45f,
                ImpactPresetId = VfxParticlePresetIds.HitSpark,
                ImpactScale = 1f,
                ImpactTint = new Color(0.72f, 0.72f, 1f), // 星爆染蓝紫
            });

            Add(new VfxProjectilePreset
            {
                Id = VfxProjectilePresetIds.ArrowShot,
                Note = "飞刀直射：白亮拉伸弹头极速直线出手，稀疏碎屑尾，命中火花；物理攻击语言。",
                FlightSpeed = 17f,
                MinFlightSeconds = 0.14f,
                MaxFlightSeconds = 0.4f,
                PathStyle = VfxProjectilePathStyle.Line,
                AccelBias = -0.2f,
                HeadTexture = VfxParticleTexture.Spark,
                HeadBlend = VfxParticleBlend.Additive,
                HeadSize = 0.3f,
                HeadStretch = new Vector2(2.4f, 0.55f),
                HeadColorA = Color.white,
                HeadColorB = new Color(1f, 0.925f, 0.702f), // #FFECB3
                TrailPerUnit = 3.5f,
                TrailTexture = VfxParticleTexture.Pixel,
                TrailBlend = VfxParticleBlend.Alpha,
                TrailLifetime = new Vector2(0.16f, 0.3f),
                TrailSize = new Vector2(0.05f, 0.1f),
                TrailColorA = new Color(0.878f, 0.878f, 0.847f), // #E0E0D8
                TrailColorB = new Color(0.627f, 0.627f, 0.596f), // #A0A098
                TrailGravity = 0.4f,
                TrailRandomRotation = true,
                ImpactPresetId = VfxParticlePresetIds.HitSpark,
                ImpactScale = 0.9f,
            });

            Add(new VfxProjectilePreset
            {
                Id = VfxProjectilePresetIds.Fireball,
                Note = "火球：橙红光团低弧线压向目标，火星拖尾，火焰爆燃命中；烈焰主题主击。",
                FlightSpeed = 8f,
                MinFlightSeconds = 0.28f,
                MaxFlightSeconds = 0.7f,
                PathStyle = VfxProjectilePathStyle.Arc,
                ArcHeight = 0.8f,
                ArcHeightJitter = 0.25f,
                AccelBias = 0.2f,
                HeadTexture = VfxParticleTexture.SoftDot,
                HeadBlend = VfxParticleBlend.Additive,
                HeadSize = 0.52f,
                HeadStretch = new Vector2(1.25f, 0.95f),
                HeadColorA = new Color(1f, 0.851f, 0.478f), // #FFD97A
                HeadColorB = new Color(1f, 0.541f, 0.165f), // #FF8A2A
                GlowSize = 1.05f,
                GlowColor = new Color(1f, 0.427f, 0.133f, 0.5f), // #FF6D22
                TrailPerUnit = 10f,
                TrailTexture = VfxParticleTexture.SoftDot,
                TrailBlend = VfxParticleBlend.Additive,
                TrailLifetime = new Vector2(0.28f, 0.5f),
                TrailSize = new Vector2(0.12f, 0.26f),
                TrailColorA = new Color(1f, 0.71f, 0.227f), // #FFB53A
                TrailColorB = new Color(0.89f, 0.231f, 0.118f), // #E33B1E
                TrailDrift = new Vector2(0.3f, 0.7f),
                TrailNoiseStrength = 0.5f,
                TrailNoiseFrequency = 1.2f,
                MuzzlePresetId = VfxParticlePresetIds.FlameBurst,
                MuzzleScale = 0.55f,
                ImpactPresetId = VfxParticlePresetIds.FlameBurst,
                ImpactScale = 1.25f,
                ImpactCountOverride = 20,
            });

            Add(new VfxProjectilePreset
            {
                Id = VfxProjectilePresetIds.VoidOrb,
                Note = "虚空法球：暗紫球体高抛缓落俯冲，暗烟拖尾，虚空爆散命中；决斗/暗系主题。",
                FlightSpeed = 5.5f,
                MinFlightSeconds = 0.4f,
                MaxFlightSeconds = 0.95f,
                PathStyle = VfxProjectilePathStyle.Arc,
                ArcHeight = 1.15f,
                ArcHeightJitter = 0.2f,
                AccelBias = 0.45f,
                HeadTexture = VfxParticleTexture.SoftDot,
                HeadBlend = VfxParticleBlend.Additive,
                HeadSize = 0.42f,
                AlignToVelocity = false,
                HeadSpinSpeed = 90f,
                HeadColorA = new Color(0.722f, 0.573f, 1f), // #B892FF
                HeadColorB = new Color(0.478f, 0.31f, 0.839f), // #7A4FD6
                GlowSize = 0.9f,
                GlowColor = new Color(0.294f, 0.165f, 0.4f, 0.65f), // #4B2A66
                TrailPerUnit = 7f,
                TrailTexture = VfxParticleTexture.SoftDot,
                TrailBlend = VfxParticleBlend.Alpha,
                TrailLifetime = new Vector2(0.4f, 0.7f),
                TrailSize = new Vector2(0.14f, 0.28f),
                TrailSizeCurve = VfxParticleSizeCurve.Grow,
                TrailColorA = new Color(0.294f, 0.165f, 0.4f, 0.8f), // #4B2A66
                TrailColorB = new Color(0.118f, 0.063f, 0.188f, 0.8f), // #1E1030
                TrailDrift = new Vector2(0.05f, 0.25f),
                MuzzlePresetId = VfxParticlePresetIds.VoidBurst,
                MuzzleScale = 0.5f,
                ImpactPresetId = VfxParticlePresetIds.VoidBurst,
                ImpactScale = 1.2f,
            });

            Add(new VfxProjectilePreset
            {
                Id = VfxProjectilePresetIds.SparkZip,
                Note = "电光急蹿：白青光点锯齿疾驰，电花拖尾，脆响火星命中；速攻/雷电语言。",
                FlightSpeed = 20f,
                MinFlightSeconds = 0.12f,
                MaxFlightSeconds = 0.34f,
                PathStyle = VfxProjectilePathStyle.Wave,
                WaveAmplitude = 0.28f,
                WaveCycles = 3f,
                AccelBias = -0.1f,
                HeadTexture = VfxParticleTexture.Spark,
                HeadBlend = VfxParticleBlend.Additive,
                HeadSize = 0.3f,
                HeadStretch = new Vector2(1.9f, 0.7f),
                HeadColorA = Color.white,
                HeadColorB = new Color(0.702f, 0.898f, 1f), // #B3E5FF
                GlowSize = 0.55f,
                GlowColor = new Color(0.416f, 0.804f, 0.894f, 0.6f), // #6ACDE4
                TrailPerUnit = 9f,
                TrailTexture = VfxParticleTexture.Spark,
                TrailBlend = VfxParticleBlend.Additive,
                TrailLifetime = new Vector2(0.12f, 0.26f),
                TrailSize = new Vector2(0.06f, 0.14f),
                TrailColorA = Color.white,
                TrailColorB = new Color(0.416f, 0.804f, 0.894f), // #6ACDE4
                TrailNoiseStrength = 0.8f,
                TrailNoiseFrequency = 4f,
                ImpactPresetId = VfxParticlePresetIds.BlockSpark,
                ImpactScale = 1.1f,
                ImpactTint = new Color(0.78f, 0.95f, 1f), // 电火花染青
            });

            Add(new VfxProjectilePreset
            {
                Id = VfxProjectilePresetIds.BoneShard,
                Note = "骨刺三连：骨白碎片旋转齐射错峰命中，骨屑迸溅；骷髅主题（deck.skeleton_legion 等）。",
                FlightSpeed = 12f,
                MinFlightSeconds = 0.18f,
                MaxFlightSeconds = 0.5f,
                PathStyle = VfxProjectilePathStyle.Arc,
                ArcHeight = 0.3f,
                ArcHeightJitter = 0.35f,
                AccelBias = -0.1f,
                Count = 3,
                StaggerSeconds = 0.09f,
                TargetJitterRadius = 0.4f,
                HeadTexture = VfxParticleTexture.Shard,
                HeadBlend = VfxParticleBlend.Alpha,
                HeadSize = 0.26f,
                AlignToVelocity = false,
                HeadSpinSpeed = 540f,
                HeadColorA = new Color(0.91f, 0.894f, 0.847f), // #E8E4D8
                HeadColorB = new Color(0.725f, 0.706f, 0.643f), // #B9B4A4
                TrailPerUnit = 4f,
                TrailTexture = VfxParticleTexture.Pixel,
                TrailBlend = VfxParticleBlend.Alpha,
                TrailLifetime = new Vector2(0.2f, 0.38f),
                TrailSize = new Vector2(0.05f, 0.1f),
                TrailColorA = new Color(0.91f, 0.894f, 0.847f), // #E8E4D8
                TrailColorB = new Color(0.725f, 0.706f, 0.643f), // #B9B4A4
                TrailGravity = 0.6f,
                TrailRandomRotation = true,
                ImpactPresetId = VfxParticlePresetIds.BoneChips,
                ImpactScale = 0.9f,
            });

            Add(new VfxProjectilePreset
            {
                Id = VfxProjectilePresetIds.VenomGlob,
                Note = "毒液抛射：黄绿液团高抛坠向目标，沿途滴落，毒雾泼溅命中；虫群/中毒主题。",
                FlightSpeed = 7f,
                MinFlightSeconds = 0.32f,
                MaxFlightSeconds = 0.8f,
                PathStyle = VfxProjectilePathStyle.Arc,
                ArcHeight = 1.35f,
                ArcHeightJitter = 0.25f,
                AccelBias = 0.25f,
                HeadTexture = VfxParticleTexture.SoftDot,
                HeadBlend = VfxParticleBlend.Alpha,
                HeadSize = 0.4f,
                HeadStretch = new Vector2(1.15f, 0.9f),
                HeadColorA = new Color(0.667f, 0.788f, 0.29f), // #AAC94A
                HeadColorB = new Color(0.42f, 0.557f, 0.137f), // #6B8E23
                GlowSize = 0.6f,
                GlowColor = new Color(0.525f, 0.627f, 0.271f, 0.45f), // #86A045
                TrailPerUnit = 5f,
                TrailTexture = VfxParticleTexture.SoftDot,
                TrailBlend = VfxParticleBlend.Alpha,
                TrailLifetime = new Vector2(0.3f, 0.55f),
                TrailSize = new Vector2(0.08f, 0.16f),
                TrailColorA = new Color(0.525f, 0.627f, 0.271f, 0.9f), // #86A045
                TrailColorB = new Color(0.298f, 0.42f, 0.133f, 0.9f), // #4C6B22
                TrailGravity = 1.2f,
                ImpactPresetId = VfxParticlePresetIds.SummonPuff,
                ImpactScale = 1.05f,
                ImpactTint = new Color(0.85f, 1f, 0.6f), // 毒雾偏黄绿
            });

            Add(new VfxProjectilePreset
            {
                Id = VfxProjectilePresetIds.GleamStreak,
                Note = "金色流光：暖金光点柔和 S 线掠向目标，星尘拖尾，金光爆点；遗物/道具触发语言。",
                FlightSpeed = 13f,
                MinFlightSeconds = 0.2f,
                MaxFlightSeconds = 0.5f,
                PathStyle = VfxProjectilePathStyle.Wave,
                WaveAmplitude = 0.14f,
                WaveCycles = 1.5f,
                AccelBias = 0.15f,
                HeadTexture = VfxParticleTexture.Spark,
                HeadBlend = VfxParticleBlend.Additive,
                HeadSize = 0.3f,
                HeadStretch = new Vector2(1.6f, 0.85f),
                HeadColorA = new Color(1f, 0.925f, 0.702f), // #FFECB3
                HeadColorB = new Color(1f, 0.784f, 0.302f), // #FFC84D
                GlowSize = 0.6f,
                GlowColor = new Color(1f, 0.851f, 0.478f, 0.5f), // #FFD97A
                TrailPerUnit = 7f,
                TrailTexture = VfxParticleTexture.Spark,
                TrailBlend = VfxParticleBlend.Additive,
                TrailLifetime = new Vector2(0.28f, 0.5f),
                TrailSize = new Vector2(0.07f, 0.15f),
                TrailSizeCurve = VfxParticleSizeCurve.Pulse,
                TrailColorA = Color.white,
                TrailColorB = new Color(1f, 0.851f, 0.478f), // #FFD97A
                ImpactPresetId = VfxParticlePresetIds.GoldBurst,
                ImpactScale = 1.05f,
            });

            sPresets = presets;
            return sPresets;
        }
    }
}

using System;
using System.Collections.Generic;
using NineGrid.Content.Vfx;
using UnityEngine;

namespace NineGrid.Presentation.Systems.Vfx
{
    public enum VfxParticleShape
    {
        /// <summary>原点极小圆，径向（XY 面）飞散。</summary>
        Point,

        /// <summary>实心圆盘内随机出生，径向飞散。</summary>
        CircleFill,

        /// <summary>圆环边缘出生，径向飞散（Speed 为负则向心聚拢）。</summary>
        CircleEdge,

        /// <summary>向上锥形喷射（ConeAngle 半角）。</summary>
        UpCone,

        /// <summary>矩形区域内随机出生（BoxSize 宽高），无出生方向。</summary>
        Box,
    }

    public enum VfxParticleSizeCurve
    {
        Constant,

        /// <summary>1 → 0 线性收缩；火花碎屑。</summary>
        Shrink,

        /// <summary>0.3 → 1 展开；烟雾冲击波。</summary>
        Grow,

        /// <summary>0 → 1 → 0 闪烁脉冲；星光。</summary>
        Pulse,
    }

    /// <summary>
    /// 单个粒子预设的完整发射参数。尺寸/速度/半径全部为世界单位
    /// （参照：卡面约 3.8×4.9、格距 5×5.5），Scale 绑定字段整体缩放。
    /// </summary>
    public sealed class VfxParticlePreset
    {
        public string Id;
        public string Note;
        public bool Loop;

        public VfxParticleTexture Texture;
        public VfxParticleBlend Blend;

        /// <summary>Pulse：单次爆发粒子数；Loop：每秒发射数。绑定 fps 字段 &gt; 0 时覆盖。</summary>
        public int Count;

        /// <summary>Pulse 专用：&gt;0 时把 Count 拆成 4 波在该窗口内错峰发射（秒）。</summary>
        public float EmitWindow;

        public Vector2 Lifetime = new Vector2(0.3f, 0.6f);
        public Vector2 Speed;
        public VfxParticleShape Shape = VfxParticleShape.Point;
        public float ShapeRadius = 0.05f;
        public Vector2 BoxSize = Vector2.one;
        public float ConeAngle = 30f;

        /// <summary>重力系数（正值下坠）。</summary>
        public float Gravity;

        /// <summary>速度阻尼（limitVelocityOverLifetime.dampen 0~1）。</summary>
        public float Dampen;

        /// <summary>额外竖直漂移（世界单位/秒，随机区间；负值下沉）。</summary>
        public Vector2 RiseVelocity;

        public Vector2 StartSize = new Vector2(0.1f, 0.2f);
        public VfxParticleSizeCurve SizeCurve = VfxParticleSizeCurve.Shrink;

        /// <summary>透明度淡入占生命比例（0=出生即不透明）。</summary>
        public float FadeInFraction = 0.05f;

        public Color ColorA = Color.white;
        public Color ColorB = Color.white;

        /// <summary>可选多色盘（胜利彩纸等）；非空时优先于 ColorA/ColorB 随机取色。</summary>
        public Color[] Palette;

        public bool RandomRotation;

        /// <summary>生命周期内自旋角速度区间（度/秒，随机正负）。</summary>
        public Vector2 SpinSpeed;

        public float NoiseStrength;
        public float NoiseFrequency = 0.5f;

        /// <summary>发射器相对锚点的竖直偏移（世界单位）。</summary>
        public float EmitOffsetY;

        /// <summary>相对独立型基准排序（5000）的偏移。</summary>
        public int SortingOrderDelta;
    }

    /// <summary>
    /// 程序化粒子预设库：预设 ID 与 Content 侧 VfxParticlePresetIds 一一对应
    /// （由 VfxParticlePlayerContractTests 护栏）。调色参照项目既有语义色：
    /// 血伤 #CB3834、护甲 #5E7E74（见 DamageNumberManagerSingleton）。
    /// </summary>
    public static class VfxParticlePresetLibrary
    {
        private static Dictionary<string, VfxParticlePreset> sPresets;

        public static bool TryGet(string presetId, out VfxParticlePreset preset)
        {
            preset = null;
            if (string.IsNullOrWhiteSpace(presetId))
            {
                return false;
            }

            return EnsureBuilt().TryGetValue(presetId.Trim(), out preset);
        }

        public static IReadOnlyCollection<string> AllIds => EnsureBuilt().Keys;

        private static Dictionary<string, VfxParticlePreset> EnsureBuilt()
        {
            if (sPresets != null)
            {
                return sPresets;
            }

            var presets = new Dictionary<string, VfxParticlePreset>(StringComparer.Ordinal);
            void Add(VfxParticlePreset preset) => presets[preset.Id] = preset;

            // ================= 战斗反馈 =================

            Add(new VfxParticlePreset
            {
                Id = VfxParticlePresetIds.HitSpark,
                Note = "通用命中火花：白金星芒径向迸射，快出快收，任何打击命中都能叠。",
                Texture = VfxParticleTexture.Spark,
                Blend = VfxParticleBlend.Additive,
                Count = 14,
                Lifetime = new Vector2(0.22f, 0.42f),
                Speed = new Vector2(2.6f, 4.6f),
                Shape = VfxParticleShape.Point,
                Gravity = 0.35f,
                Dampen = 0.55f,
                StartSize = new Vector2(0.1f, 0.22f),
                SizeCurve = VfxParticleSizeCurve.Shrink,
                ColorA = new Color(1f, 0.965f, 0.784f), // #FFF6C8
                ColorB = new Color(1f, 0.694f, 0.239f), // #FFB13D
                SortingOrderDelta = 2,
            });

            Add(new VfxParticlePreset
            {
                Id = VfxParticlePresetIds.BloodSpray,
                Note = "血量受伤红色像素碎屑：向上喷溅后重力下坠，与血伤飘字同色系。",
                Texture = VfxParticleTexture.Pixel,
                Blend = VfxParticleBlend.Alpha,
                Count = 16,
                Lifetime = new Vector2(0.35f, 0.6f),
                Speed = new Vector2(2f, 4f),
                Shape = VfxParticleShape.UpCone,
                ConeAngle = 55f,
                Gravity = 1.2f,
                StartSize = new Vector2(0.08f, 0.16f),
                SizeCurve = VfxParticleSizeCurve.Shrink,
                ColorA = new Color(0.796f, 0.22f, 0.204f), // #CB3834（血伤飘字色）
                ColorB = new Color(0.494f, 0.118f, 0.11f), // #7E1E1C
                RandomRotation = true,
                SortingOrderDelta = 1,
            });

            Add(new VfxParticlePreset
            {
                Id = VfxParticlePresetIds.ArmorShatter,
                Note = "护甲吸收青灰碎片：斜向溅出翻滚下坠，表达「甲挡住了」的碎裂感。",
                Texture = VfxParticleTexture.Shard,
                Blend = VfxParticleBlend.Alpha,
                Count = 12,
                Lifetime = new Vector2(0.35f, 0.62f),
                Speed = new Vector2(1.8f, 3.6f),
                Shape = VfxParticleShape.UpCone,
                ConeAngle = 70f,
                Gravity = 1.1f,
                StartSize = new Vector2(0.1f, 0.18f),
                SizeCurve = VfxParticleSizeCurve.Shrink,
                ColorA = new Color(0.624f, 0.722f, 0.784f), // #9FB8C8
                ColorB = new Color(0.369f, 0.494f, 0.455f), // #5E7E74（护甲飘字色）
                RandomRotation = true,
                SpinSpeed = new Vector2(120f, 420f),
                SortingOrderDelta = 1,
            });

            Add(new VfxParticlePreset
            {
                Id = VfxParticlePresetIds.BlockSpark,
                Note = "完全格挡白色短促火星：从格挡点环缘弹开，硬质金属感。",
                Texture = VfxParticleTexture.Spark,
                Blend = VfxParticleBlend.Additive,
                Count = 10,
                Lifetime = new Vector2(0.14f, 0.3f),
                Speed = new Vector2(3f, 5f),
                Shape = VfxParticleShape.CircleEdge,
                ShapeRadius = 0.25f,
                Dampen = 0.7f,
                StartSize = new Vector2(0.1f, 0.2f),
                SizeCurve = VfxParticleSizeCurve.Shrink,
                ColorA = Color.white,
                ColorB = new Color(0.749f, 0.91f, 1f), // #BFE8FF
                SortingOrderDelta = 2,
            });

            Add(new VfxParticlePreset
            {
                Id = VfxParticlePresetIds.HealMotes,
                Note = "治疗绿色光尘：从卡底浮起缓慢上升渐隐，柔和不抢戏。",
                Texture = VfxParticleTexture.SoftDot,
                Blend = VfxParticleBlend.Additive,
                Count = 10,
                Lifetime = new Vector2(0.7f, 1.1f),
                Speed = new Vector2(0f, 0.15f),
                Shape = VfxParticleShape.Box,
                BoxSize = new Vector2(1.6f, 0.5f),
                RiseVelocity = new Vector2(0.6f, 1.1f),
                StartSize = new Vector2(0.12f, 0.24f),
                SizeCurve = VfxParticleSizeCurve.Shrink,
                FadeInFraction = 0.2f,
                ColorA = new Color(0.561f, 0.89f, 0.69f), // #8FE3B0
                ColorB = new Color(0.247f, 0.749f, 0.498f), // #3FBF7F
                EmitOffsetY = -0.8f,
                SortingOrderDelta = 1,
            });

            Add(new VfxParticlePreset
            {
                Id = VfxParticlePresetIds.ArmorGainRise,
                Note = "获得护甲蓝色光点：自下而上收拢升起，像镀上一层新甲。",
                Texture = VfxParticleTexture.SoftDot,
                Blend = VfxParticleBlend.Additive,
                Count = 12,
                Lifetime = new Vector2(0.5f, 0.85f),
                Speed = new Vector2(-0.5f, -0.2f),
                Shape = VfxParticleShape.CircleEdge,
                ShapeRadius = 0.8f,
                RiseVelocity = new Vector2(0.5f, 0.9f),
                StartSize = new Vector2(0.1f, 0.2f),
                SizeCurve = VfxParticleSizeCurve.Shrink,
                FadeInFraction = 0.15f,
                ColorA = new Color(0.498f, 0.698f, 0.898f), // #7FB2E5
                ColorB = new Color(0.369f, 0.494f, 0.455f), // #5E7E74
                EmitOffsetY = -0.4f,
                SortingOrderDelta = 1,
            });

            Add(new VfxParticlePreset
            {
                Id = VfxParticlePresetIds.DeathPuff,
                Note = "死亡烟尘：灰黑软团缓涨缓散微上飘，衬托单位消失。",
                Texture = VfxParticleTexture.SoftDot,
                Blend = VfxParticleBlend.Alpha,
                Count = 9,
                Lifetime = new Vector2(0.5f, 0.9f),
                Speed = new Vector2(0.6f, 1.2f),
                Shape = VfxParticleShape.CircleFill,
                ShapeRadius = 0.35f,
                Dampen = 0.35f,
                RiseVelocity = new Vector2(0.15f, 0.4f),
                StartSize = new Vector2(0.35f, 0.6f),
                SizeCurve = VfxParticleSizeCurve.Grow,
                FadeInFraction = 0.1f,
                ColorA = new Color(0.29f, 0.29f, 0.322f), // #4A4A52
                ColorB = new Color(0.137f, 0.137f, 0.165f), // #23232A
                SortingOrderDelta = 0,
            });

            Add(new VfxParticlePreset
            {
                Id = VfxParticlePresetIds.BoneChips,
                Note = "骨白碎屑：迸出翻滚下坠；骷髅系死亡/碎裂时叠在烟尘上。",
                Texture = VfxParticleTexture.Pixel,
                Blend = VfxParticleBlend.Alpha,
                Count = 8,
                Lifetime = new Vector2(0.4f, 0.7f),
                Speed = new Vector2(1.6f, 3.2f),
                Shape = VfxParticleShape.UpCone,
                ConeAngle = 60f,
                Gravity = 1.3f,
                StartSize = new Vector2(0.08f, 0.15f),
                SizeCurve = VfxParticleSizeCurve.Constant,
                ColorA = new Color(0.91f, 0.894f, 0.847f), // #E8E4D8
                ColorB = new Color(0.725f, 0.706f, 0.643f), // #B9B4A4
                RandomRotation = true,
                SpinSpeed = new Vector2(90f, 360f),
                SortingOrderDelta = 1,
            });

            // ================= 卡牌生命周期 / 交互 =================

            Add(new VfxParticlePreset
            {
                Id = VfxParticlePresetIds.ExitWisp,
                Note = "卡牌退场轻尘：几缕灰白软点上飘，比精灵表轻烟更便宜的替代/叠加。",
                Texture = VfxParticleTexture.SoftDot,
                Blend = VfxParticleBlend.Alpha,
                Count = 6,
                Lifetime = new Vector2(0.5f, 0.85f),
                Speed = new Vector2(0.2f, 0.5f),
                Shape = VfxParticleShape.CircleFill,
                ShapeRadius = 0.4f,
                RiseVelocity = new Vector2(0.4f, 0.8f),
                StartSize = new Vector2(0.2f, 0.38f),
                SizeCurve = VfxParticleSizeCurve.Grow,
                FadeInFraction = 0.1f,
                ColorA = new Color(0.784f, 0.769f, 0.729f, 0.85f), // #C8C4BA
                ColorB = new Color(0.53f, 0.522f, 0.494f, 0.85f), // #87857E
                SortingOrderDelta = 0,
            });

            Add(new VfxParticlePreset
            {
                Id = VfxParticlePresetIds.ItemFlash,
                Note = "道具使用金色光爆：星芒短促炸开，强调「用掉了一张卡」。",
                Texture = VfxParticleTexture.Spark,
                Blend = VfxParticleBlend.Additive,
                Count = 12,
                Lifetime = new Vector2(0.2f, 0.4f),
                Speed = new Vector2(2.4f, 4f),
                Shape = VfxParticleShape.CircleEdge,
                ShapeRadius = 0.2f,
                Dampen = 0.6f,
                StartSize = new Vector2(0.12f, 0.24f),
                SizeCurve = VfxParticleSizeCurve.Shrink,
                ColorA = new Color(1f, 0.886f, 0.541f), // #FFE28A
                ColorB = new Color(1f, 0.678f, 0.2f), // #FFAD33
                SortingOrderDelta = 2,
            });

            Add(new VfxParticlePreset
            {
                Id = VfxParticlePresetIds.FlipSparkle,
                Note = "翻面星尘：沿卡面竖带浮出白青闪点，提示信息翻转。",
                Texture = VfxParticleTexture.Spark,
                Blend = VfxParticleBlend.Additive,
                Count = 8,
                Lifetime = new Vector2(0.35f, 0.6f),
                Speed = new Vector2(0f, 0.1f),
                Shape = VfxParticleShape.Box,
                BoxSize = new Vector2(0.5f, 2.4f),
                RiseVelocity = new Vector2(0.2f, 0.5f),
                StartSize = new Vector2(0.08f, 0.18f),
                SizeCurve = VfxParticleSizeCurve.Pulse,
                FadeInFraction = 0.25f,
                ColorA = Color.white,
                ColorB = new Color(0.702f, 0.898f, 1f), // #B3E5FF
                SortingOrderDelta = 2,
            });

            Add(new VfxParticlePreset
            {
                Id = VfxParticlePresetIds.GoldBurst,
                Note = "金色闪光爆点：拾金/赏金瞬间的星光，可与 gold-flight 飞币叠加。",
                Texture = VfxParticleTexture.Spark,
                Blend = VfxParticleBlend.Additive,
                Count = 10,
                Lifetime = new Vector2(0.25f, 0.5f),
                Speed = new Vector2(1.5f, 3f),
                Shape = VfxParticleShape.CircleFill,
                ShapeRadius = 0.3f,
                Dampen = 0.5f,
                StartSize = new Vector2(0.1f, 0.2f),
                SizeCurve = VfxParticleSizeCurve.Shrink,
                ColorA = new Color(1f, 0.886f, 0.541f), // #FFE28A
                ColorB = new Color(1f, 0.784f, 0.302f), // #FFC84D
                SortingOrderDelta = 2,
            });

            // ================= 效果 / 技能 / 机关 / 遗物触发 =================

            Add(new VfxParticlePreset
            {
                Id = VfxParticlePresetIds.EffectRing,
                Note = "效果触发符能环：单圈蓝紫冲击波展开消散，通用「有东西生效了」。",
                Texture = VfxParticleTexture.Ring,
                Blend = VfxParticleBlend.Additive,
                Count = 1,
                Lifetime = new Vector2(0.32f, 0.38f),
                Speed = new Vector2(0f, 0f),
                Shape = VfxParticleShape.Point,
                StartSize = new Vector2(1.3f, 1.5f),
                SizeCurve = VfxParticleSizeCurve.Grow,
                ColorA = new Color(0.498f, 0.698f, 1f), // #7FB2FF
                ColorB = new Color(0.71f, 0.573f, 1f), // #B592FF
                SortingOrderDelta = 1,
            });

            Add(new VfxParticlePreset
            {
                Id = VfxParticlePresetIds.SkillSurge,
                Note = "怪物技能紫电碎闪：噪声抖动的紫白星芒，凶一点的能量感。",
                Texture = VfxParticleTexture.Spark,
                Blend = VfxParticleBlend.Additive,
                Count = 12,
                Lifetime = new Vector2(0.2f, 0.4f),
                Speed = new Vector2(2f, 3.5f),
                Shape = VfxParticleShape.CircleFill,
                ShapeRadius = 0.3f,
                Dampen = 0.45f,
                StartSize = new Vector2(0.1f, 0.22f),
                SizeCurve = VfxParticleSizeCurve.Shrink,
                ColorA = new Color(0.722f, 0.573f, 1f), // #B892FF
                ColorB = new Color(0.478f, 0.31f, 0.839f), // #7A4FD6
                NoiseStrength = 1.6f,
                NoiseFrequency = 3f,
                SortingOrderDelta = 2,
            });

            Add(new VfxParticlePreset
            {
                Id = VfxParticlePresetIds.TrapSnap,
                Note = "机关触发金属火花：橙黄锥形上迸带重力，机械「咔哒」感。",
                Texture = VfxParticleTexture.Spark,
                Blend = VfxParticleBlend.Additive,
                Count = 12,
                Lifetime = new Vector2(0.28f, 0.5f),
                Speed = new Vector2(3f, 5f),
                Shape = VfxParticleShape.UpCone,
                ConeAngle = 40f,
                Gravity = 0.9f,
                StartSize = new Vector2(0.09f, 0.18f),
                SizeCurve = VfxParticleSizeCurve.Shrink,
                ColorA = new Color(1f, 0.827f, 0.478f), // #FFD37A
                ColorB = new Color(1f, 0.541f, 0.165f), // #FF8A2A
                SortingOrderDelta = 2,
            });

            Add(new VfxParticlePreset
            {
                Id = VfxParticlePresetIds.RelicGleam,
                Note = "遗物触发星光缓闪：少量大星芒原地脉冲，安静的「被动生效」。",
                Texture = VfxParticleTexture.Spark,
                Blend = VfxParticleBlend.Additive,
                Count = 5,
                Lifetime = new Vector2(0.5f, 0.9f),
                Speed = new Vector2(0.2f, 0.5f),
                Shape = VfxParticleShape.CircleFill,
                ShapeRadius = 0.35f,
                StartSize = new Vector2(0.14f, 0.3f),
                SizeCurve = VfxParticleSizeCurve.Pulse,
                FadeInFraction = 0.3f,
                ColorA = new Color(1f, 0.925f, 0.702f), // #FFECB3
                ColorB = new Color(1f, 0.784f, 0.302f), // #FFC84D
                SortingOrderDelta = 2,
            });

            // ================= 主题元素 =================

            Add(new VfxParticlePreset
            {
                Id = VfxParticlePresetIds.FlameBurst,
                Note = "火焰爆发：橙红光团上蹿快速烧尽；烈焰套/灼烧类效果的主击。",
                Texture = VfxParticleTexture.SoftDot,
                Blend = VfxParticleBlend.Additive,
                Count = 14,
                Lifetime = new Vector2(0.3f, 0.55f),
                Speed = new Vector2(0.5f, 1f),
                Shape = VfxParticleShape.CircleFill,
                ShapeRadius = 0.25f,
                RiseVelocity = new Vector2(1f, 2f),
                StartSize = new Vector2(0.2f, 0.4f),
                SizeCurve = VfxParticleSizeCurve.Shrink,
                ColorA = new Color(1f, 0.71f, 0.227f), // #FFB53A
                ColorB = new Color(0.89f, 0.231f, 0.118f), // #E33B1E
                SortingOrderDelta = 2,
            });

            Add(new VfxParticlePreset
            {
                Id = VfxParticlePresetIds.VoidBurst,
                Note = "虚空爆散：暗紫烟点缓慢逸散微升；决斗套/暗系效果的低语感。",
                Texture = VfxParticleTexture.SoftDot,
                Blend = VfxParticleBlend.Alpha,
                Count = 12,
                Lifetime = new Vector2(0.4f, 0.75f),
                Speed = new Vector2(1f, 2f),
                Shape = VfxParticleShape.CircleFill,
                ShapeRadius = 0.3f,
                Dampen = 0.5f,
                RiseVelocity = new Vector2(0.1f, 0.3f),
                StartSize = new Vector2(0.2f, 0.38f),
                SizeCurve = VfxParticleSizeCurve.Grow,
                FadeInFraction = 0.12f,
                ColorA = new Color(0.294f, 0.165f, 0.4f), // #4B2A66
                ColorB = new Color(0.118f, 0.063f, 0.188f), // #1E1030
                SortingOrderDelta = 1,
            });

            Add(new VfxParticlePreset
            {
                Id = VfxParticlePresetIds.SummonPuff,
                Note = "召唤登场绿雾：黄绿斑点扑散；虫群套生成/入场的生物气息。",
                Texture = VfxParticleTexture.SoftDot,
                Blend = VfxParticleBlend.Alpha,
                Count = 10,
                Lifetime = new Vector2(0.35f, 0.6f),
                Speed = new Vector2(1.2f, 2.2f),
                Shape = VfxParticleShape.CircleFill,
                ShapeRadius = 0.3f,
                Dampen = 0.45f,
                StartSize = new Vector2(0.18f, 0.34f),
                SizeCurve = VfxParticleSizeCurve.Grow,
                FadeInFraction = 0.08f,
                ColorA = new Color(0.498f, 0.796f, 0.416f), // #7FCB6A
                ColorB = new Color(0.243f, 0.478f, 0.227f), // #3E7A3A
                SortingOrderDelta = 1,
            });

            // ================= 流程级 =================

            Add(new VfxParticlePreset
            {
                Id = VfxParticlePresetIds.VictoryConfetti,
                Note = "胜利彩纸：多彩像素纸屑自上方撒落翻滚飘摆，配 YOU WON。",
                Texture = VfxParticleTexture.Pixel,
                Blend = VfxParticleBlend.Alpha,
                Count = 90,
                EmitWindow = 1.2f,
                Lifetime = new Vector2(1.6f, 2.4f),
                Speed = new Vector2(0f, 0.3f),
                Shape = VfxParticleShape.Box,
                BoxSize = new Vector2(7f, 0.5f),
                Gravity = 0.55f,
                Dampen = 0.25f,
                StartSize = new Vector2(0.12f, 0.2f),
                SizeCurve = VfxParticleSizeCurve.Constant,
                Palette = new[]
                {
                    new Color(0.898f, 0.29f, 0.294f), // 红
                    new Color(1f, 0.784f, 0.302f), // 金
                    new Color(0.416f, 0.804f, 0.894f), // 青
                    new Color(0.51f, 0.831f, 0.427f), // 绿
                    new Color(0.902f, 0.545f, 0.78f), // 粉
                },
                RandomRotation = true,
                SpinSpeed = new Vector2(90f, 300f),
                NoiseStrength = 0.7f,
                NoiseFrequency = 0.6f,
                EmitOffsetY = 5.5f,
                SortingOrderDelta = 3,
            });

            Add(new VfxParticlePreset
            {
                Id = VfxParticlePresetIds.DefeatEmbers,
                Note = "战败余烬：暗红光点满场缓浮缓沉，压抑的收场氛围，配 GAME OVER。",
                Texture = VfxParticleTexture.SoftDot,
                Blend = VfxParticleBlend.Additive,
                Count = 18,
                EmitWindow = 1f,
                Lifetime = new Vector2(1.2f, 2f),
                Speed = new Vector2(0f, 0.1f),
                Shape = VfxParticleShape.Box,
                BoxSize = new Vector2(6f, 3f),
                RiseVelocity = new Vector2(-0.35f, -0.1f),
                StartSize = new Vector2(0.08f, 0.18f),
                SizeCurve = VfxParticleSizeCurve.Shrink,
                FadeInFraction = 0.35f,
                ColorA = new Color(0.541f, 0.173f, 0.133f), // #8A2C22
                ColorB = new Color(0.227f, 0.071f, 0.055f), // #3A120E
                NoiseStrength = 0.35f,
                NoiseFrequency = 0.4f,
                SortingOrderDelta = 3,
            });

            // ================= 持续 State（loop） =================

            Add(new VfxParticlePreset
            {
                Id = VfxParticlePresetIds.LoopEmber,
                Note = "持续燃烧余烬：橙红火星不断升腾；灼烧/烈焰驻场状态。",
                Loop = true,
                Texture = VfxParticleTexture.SoftDot,
                Blend = VfxParticleBlend.Additive,
                Count = 6,
                Lifetime = new Vector2(0.6f, 1f),
                Speed = new Vector2(0f, 0.15f),
                Shape = VfxParticleShape.CircleFill,
                ShapeRadius = 0.4f,
                RiseVelocity = new Vector2(0.6f, 1.1f),
                StartSize = new Vector2(0.08f, 0.16f),
                SizeCurve = VfxParticleSizeCurve.Shrink,
                FadeInFraction = 0.15f,
                ColorA = new Color(1f, 0.71f, 0.227f), // #FFB53A
                ColorB = new Color(0.89f, 0.231f, 0.118f), // #E33B1E
                SortingOrderDelta = 1,
            });

            Add(new VfxParticlePreset
            {
                Id = VfxParticlePresetIds.LoopHealSpring,
                Note = "治疗泉气泡光点：绿青光尘持续从底部涌起；治疗泉机关驻场。",
                Loop = true,
                Texture = VfxParticleTexture.SoftDot,
                Blend = VfxParticleBlend.Additive,
                Count = 5,
                Lifetime = new Vector2(0.8f, 1.4f),
                Speed = new Vector2(0f, 0.1f),
                Shape = VfxParticleShape.Box,
                BoxSize = new Vector2(1.4f, 0.3f),
                RiseVelocity = new Vector2(0.4f, 0.8f),
                StartSize = new Vector2(0.1f, 0.2f),
                SizeCurve = VfxParticleSizeCurve.Shrink,
                FadeInFraction = 0.25f,
                ColorA = new Color(0.561f, 0.89f, 0.69f), // #8FE3B0
                ColorB = new Color(0.247f, 0.749f, 0.498f), // #3FBF7F
                EmitOffsetY = -0.8f,
                SortingOrderDelta = 1,
            });

            Add(new VfxParticlePreset
            {
                Id = VfxParticlePresetIds.LoopMiasma,
                Note = "毒瘴气泡：黄绿浊雾缓浮带噪声漂移；中毒/瘴气类持续状态。",
                Loop = true,
                Texture = VfxParticleTexture.SoftDot,
                Blend = VfxParticleBlend.Alpha,
                Count = 4,
                Lifetime = new Vector2(1.2f, 2f),
                Speed = new Vector2(0f, 0.1f),
                Shape = VfxParticleShape.CircleFill,
                ShapeRadius = 0.5f,
                RiseVelocity = new Vector2(0.2f, 0.5f),
                StartSize = new Vector2(0.25f, 0.45f),
                SizeCurve = VfxParticleSizeCurve.Grow,
                FadeInFraction = 0.3f,
                ColorA = new Color(0.525f, 0.627f, 0.271f, 0.6f), // #86A045
                ColorB = new Color(0.298f, 0.42f, 0.133f, 0.6f), // #4C6B22
                NoiseStrength = 0.4f,
                NoiseFrequency = 0.5f,
                SortingOrderDelta = 1,
            });

            Add(new VfxParticlePreset
            {
                Id = VfxParticlePresetIds.LoopSparkle,
                Note = "稀有星光环：卡面范围金白星点偶发闪烁；精英/稀有/强化标记。",
                Loop = true,
                Texture = VfxParticleTexture.Spark,
                Blend = VfxParticleBlend.Additive,
                Count = 3,
                Lifetime = new Vector2(0.5f, 0.9f),
                Speed = new Vector2(0f, 0.05f),
                Shape = VfxParticleShape.Box,
                BoxSize = new Vector2(1.8f, 2.4f),
                StartSize = new Vector2(0.1f, 0.22f),
                SizeCurve = VfxParticleSizeCurve.Pulse,
                FadeInFraction = 0.3f,
                ColorA = Color.white,
                ColorB = new Color(1f, 0.851f, 0.478f), // #FFD97A
                SortingOrderDelta = 2,
            });

            Add(new VfxParticlePreset
            {
                Id = VfxParticlePresetIds.LoopVoidVeil,
                Note = "虚空紫幕：暗紫软点自环缘向心收拢；虚空/诅咒类持续状态。",
                Loop = true,
                Texture = VfxParticleTexture.SoftDot,
                Blend = VfxParticleBlend.Alpha,
                Count = 5,
                Lifetime = new Vector2(1f, 1.8f),
                Speed = new Vector2(-0.6f, -0.3f),
                Shape = VfxParticleShape.CircleEdge,
                ShapeRadius = 0.9f,
                StartSize = new Vector2(0.2f, 0.35f),
                SizeCurve = VfxParticleSizeCurve.Shrink,
                FadeInFraction = 0.3f,
                ColorA = new Color(0.294f, 0.165f, 0.4f, 0.75f), // #4B2A66
                ColorB = new Color(0.118f, 0.063f, 0.188f, 0.75f), // #1E1030
                SortingOrderDelta = 1,
            });

            Add(new VfxParticlePreset
            {
                Id = VfxParticlePresetIds.LoopDust,
                Note = "环境浮尘：全场极淡光尘缓慢漂浮；氛围层，几乎不可察觉才是对的。",
                Loop = true,
                Texture = VfxParticleTexture.SoftDot,
                Blend = VfxParticleBlend.Alpha,
                Count = 2,
                Lifetime = new Vector2(3f, 5f),
                Speed = new Vector2(0f, 0.05f),
                Shape = VfxParticleShape.Box,
                BoxSize = new Vector2(8f, 5f),
                RiseVelocity = new Vector2(0.05f, 0.15f),
                StartSize = new Vector2(0.05f, 0.1f),
                SizeCurve = VfxParticleSizeCurve.Constant,
                FadeInFraction = 0.4f,
                ColorA = new Color(0.784f, 0.753f, 0.667f, 0.35f), // #C8C0AA
                ColorB = new Color(0.784f, 0.753f, 0.667f, 0.18f),
                NoiseStrength = 0.3f,
                NoiseFrequency = 0.3f,
                SortingOrderDelta = 0,
            });

            sPresets = presets;
            return sPresets;
        }
    }
}

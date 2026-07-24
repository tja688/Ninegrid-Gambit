using UnityEngine;

namespace NineGrid.Cards.Vfx
{
    /// <summary>
    /// 卡牌边沿尘雾（落地烟雾）参数。源自 LostForSwords <c>DustManagerNode</c> Place 算法；
    /// 用 <see cref="intensity"/> 总控浓度，满意后再扩展其它触发花样。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CardEdgeDustFxSettings",
        menuName = "NineGrid/Cards/VFX/Card Edge Dust Fx Settings")]
    public sealed class CardEdgeDustFxSettingsSO : ScriptableObject
    {
        public const string ResourcePath = "VFX/CardEdgeDustFx";

        [Header("总控")]
        [Tooltip("总强度。0=关闭；1=LFS 原味；>1 更浓更快更大。")]
        [Range(0f, 3f)]
        [SerializeField] private float intensity = 1f;

        [Tooltip("全局开关；关闭后 Play 直接 no-op。")]
        [SerializeField] private bool enabled = true;

        [Header("贴图 / 排序")]
        [SerializeField] private Sprite dustSprite;

        [SerializeField] private string sortingLayerName = "Main";

        [Tooltip("暗底层 sortingOrder；亮层 = 此值 + 1。需高于场地、略低于或贴近卡牌。")]
        [SerializeField] private int sortingOrder = 40;

        [Header("Place 喷发（沿四边）")]
        [Tooltip("每条边基准粒子数（再乘 intensity）。LFS 原值 10。")]
        [SerializeField] private int particlesPerEdge = 10;

        [SerializeField] private Vector2 ttlSeconds = new(0.6f, 1f);

        [Tooltip("粒子 baseScale 随机范围（相对 particleWorldSize）。")]
        [SerializeField] private Vector2 baseScaleRange = new(0.6f, 1.5f);

        [Tooltip("出射速度（世界单位/秒）随机范围。会再乘 intensity。")]
        [SerializeField] private Vector2 speedRange = new(0.55f, 1.1f);

        [Tooltip("沿边位置抖动（归一化卡面比例）。")]
        [SerializeField] private float edgeJitter = 0.1f;

        [Header("绘制（双层叠色）")]
        [Tooltip("单粒子基准世界尺寸（对应 LFS 16px * scale）。")]
        [SerializeField] private float particleWorldSize = 0.22f;

        [Tooltip("暗底层相对亮层的放大（LFS 1.25）。")]
        [SerializeField] private float darkLayerScale = 1.25f;

        [SerializeField] private Color darkColor = new(0.1f, 0.1f, 0.1f, 1f);

        [SerializeField] private Color lightColor = new(0.96f, 0.96f, 0.96f, 1f);

        [Header("池")]
        [SerializeField] private int prewarmPairs = 64;

        [SerializeField] private int maxLiveParticles = 256;

        public bool Enabled => enabled;
        public float Intensity => intensity;
        public Sprite DustSprite => dustSprite;
        public string SortingLayerName => sortingLayerName;
        public int SortingOrder => sortingOrder;
        public int ParticlesPerEdge => Mathf.Max(1, particlesPerEdge);
        public Vector2 TtlSeconds => ttlSeconds;
        public Vector2 BaseScaleRange => baseScaleRange;
        public Vector2 SpeedRange => speedRange;
        public float EdgeJitter => Mathf.Max(0f, edgeJitter);
        public float ParticleWorldSize => Mathf.Max(0.01f, particleWorldSize);
        public float DarkLayerScale => Mathf.Max(1f, darkLayerScale);
        public Color DarkColor => darkColor;
        public Color LightColor => lightColor;
        public int PrewarmPairs => Mathf.Max(0, prewarmPairs);
        public int MaxLiveParticles => Mathf.Max(8, maxLiveParticles);

        public void SetIntensity(float value) => intensity = Mathf.Clamp(value, 0f, 3f);

        public void SetEnabled(bool value) => enabled = value;

        public void SetDustSprite(Sprite sprite) => dustSprite = sprite;
    }
}

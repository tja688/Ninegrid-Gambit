using UnityEngine;

namespace NineGrid.Cards.Vfx
{
    /// <summary>
    /// 卡牌边沿尘雾参数。Place=落地喷发；Trail=飞牌双尾迹（LFS Move）。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CardEdgeDustFxSettings",
        menuName = "NineGrid/Cards/VFX/Card Edge Dust Fx Settings")]
    public sealed class CardEdgeDustFxSettingsSO : ScriptableObject
    {
        public const string ResourcePath = "VFX/CardEdgeDustFx";

        [Header("总控")]
        [Tooltip("总强度。0=关闭；1=基准；>1 更浓更快更大。")]
        [Range(0f, 3f)]
        [SerializeField] private float intensity = 1f;

        [Tooltip("全局开关；关闭后 Play / Trail 直接 no-op。")]
        [SerializeField] private bool fxEnabled = true;

        [Header("贴图 / 排序")]
        [SerializeField] private Sprite dustSprite;

        [SerializeField] private string sortingLayerName = "Main";

        [Tooltip("暗底层 sortingOrder；亮层 = 此值 + 1。需压过飞牌 SortingGroup（约 80）。")]
        [SerializeField] private int sortingOrder = 120;

        [Header("Place 喷发（沿四边）")]
        [Tooltip("每条边基准粒子数（再乘 intensity）。LFS 原值 10。")]
        [SerializeField] private int particlesPerEdge = 10;

        [SerializeField] private Vector2 placeTtlSeconds = new(0.6f, 1f);

        [Tooltip("Place 粒子 baseScale 随机范围。")]
        [SerializeField] private Vector2 placeBaseScaleRange = new(0.6f, 1.5f);

        [Tooltip("Place 出射速度（世界单位/秒）。")]
        [SerializeField] private Vector2 placeSpeedRange = new(0.55f, 1.1f);

        [Tooltip("沿边位置抖动（归一化卡面比例）。")]
        [SerializeField] private float edgeJitter = 0.1f;

        [Header("Trail 双尾迹（飞牌出场）")]
        [Tooltip("飞牌拖尾总开关。")]
        [SerializeField] private bool trailEnabled = true;

        [Tooltip("相对 Place 的拖尾浓度倍率（再乘总 intensity）。")]
        [Range(0f, 3f)]
        [SerializeField] private float trailIntensity = 1f;

        [Tooltip("累计位移达到该距离（世界单位）才喷一次，控制密度与先后消散节奏。")]
        [SerializeField] private float trailEmitDistance = 0.08f;

        [Tooltip("每个拖尾角每次喷射的粒子数。")]
        [SerializeField] private int trailParticlesPerCorner = 2;

        [Tooltip("拖尾粒子 TTL；拉开区间可强化「先后消失」。")]
        [SerializeField] private Vector2 trailTtlSeconds = new(0.45f, 0.95f);

        [SerializeField] private Vector2 trailBaseScaleRange = new(0.7f, 1.1f);

        [Tooltip("拖尾粒子沿飞行方向的速度（世界单位/秒）。")]
        [SerializeField] private float trailSpeed = 1.2f;

        [Tooltip("角点向卡心收缩的随机量（负插值，LFS -0.2）。")]
        [SerializeField] private float trailInwardJitter = 0.2f;

        [Header("绘制（双层叠色）")]
        [SerializeField] private float particleWorldSize = 0.35f;

        [SerializeField] private float darkLayerScale = 1.25f;

        [SerializeField] private Color darkColor = new(0.1f, 0.1f, 0.1f, 1f);

        [SerializeField] private Color lightColor = new(0.96f, 0.96f, 0.96f, 1f);

        [Header("池")]
        [SerializeField] private int prewarmPairs = 96;

        [SerializeField] private int maxLiveParticles = 512;

        public bool Enabled => fxEnabled;
        public float Intensity => intensity;
        public Sprite DustSprite => dustSprite;
        public string SortingLayerName => sortingLayerName;
        public int SortingOrder => sortingOrder;
        public int ParticlesPerEdge => Mathf.Max(1, particlesPerEdge);
        public Vector2 PlaceTtlSeconds => placeTtlSeconds;
        public Vector2 PlaceBaseScaleRange => placeBaseScaleRange;
        public Vector2 PlaceSpeedRange => placeSpeedRange;
        public float EdgeJitter => Mathf.Max(0f, edgeJitter);
        public bool TrailEnabled => trailEnabled;
        public float TrailIntensity => trailIntensity;
        public float TrailEmitDistance => Mathf.Max(0.02f, trailEmitDistance);
        public int TrailParticlesPerCorner => Mathf.Max(1, trailParticlesPerCorner);
        public Vector2 TrailTtlSeconds => trailTtlSeconds;
        public Vector2 TrailBaseScaleRange => trailBaseScaleRange;
        public float TrailSpeed => Mathf.Max(0f, trailSpeed);
        public float TrailInwardJitter => Mathf.Clamp01(trailInwardJitter);
        public float ParticleWorldSize => Mathf.Max(0.01f, particleWorldSize);
        public float DarkLayerScale => Mathf.Max(1f, darkLayerScale);
        public Color DarkColor => darkColor;
        public Color LightColor => lightColor;
        public int PrewarmPairs => Mathf.Max(0, prewarmPairs);
        public int MaxLiveParticles => Mathf.Max(8, maxLiveParticles);

        // 兼容旧字段名（若外部仍读）
        public Vector2 TtlSeconds => placeTtlSeconds;
        public Vector2 BaseScaleRange => placeBaseScaleRange;
        public Vector2 SpeedRange => placeSpeedRange;

        public void SetIntensity(float value) => intensity = Mathf.Clamp(value, 0f, 3f);

        public void SetEnabled(bool value) => fxEnabled = value;

        public void SetDustSprite(Sprite sprite) => dustSprite = sprite;
    }
}

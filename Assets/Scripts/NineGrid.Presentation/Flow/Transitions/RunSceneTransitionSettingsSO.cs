using UnityEngine;

namespace NineGrid.Flow.Transitions
{
    /// <summary>
    /// 局内节点过场（Feel Round / Directional）参数。Resources 路径见 <see cref="ResourcePath"/>。
    /// </summary>
    [CreateAssetMenu(
        fileName = "RunSceneTransitionSettings",
        menuName = "NineGrid/Flow/Run Scene Transition Settings")]
    public sealed class RunSceneTransitionSettingsSO : ScriptableObject
    {
        public const string ResourcePath = "Transitions/RunSceneTransition";

        public const int RoundFaderId = 100;
        public const int DirectionalFaderId = 101;

        [Header("总控")]
        [Tooltip("关闭后进房硬切不播放过场。")]
        [SerializeField] private bool enabled = true;

        [SerializeField] private bool ignoreTimeScale = true;

        [SerializeField] private bool blockRaycastsDuringFade = true;

        [Header("Directional（同层节点）")]
        [SerializeField] private float directionalFadeInDuration = 0.35f;

        [SerializeField] private float directionalFadeOutDuration = 0.25f;

        [Tooltip("0=Linear … 常用 EaseInOutCubic=6（与 Feel MMTweenCurve 序号一致时可对照）。")]
        [SerializeField] private TransitionTweenCurve directionalTween = TransitionTweenCurve.EaseInOutCubic;

        [Header("Round（跨层）")]
        [SerializeField] private float roundFadeInDuration = 0.45f;

        [SerializeField] private float roundFadeOutDuration = 0.45f;

        [SerializeField] private TransitionTweenCurve roundTween = TransitionTweenCurve.EaseInOutCubic;

        [Tooltip("Mask 缩放：x=合上（小洞/全遮），y=打开（大洞/透明）。对齐 Feel MaskScale。")]
        [SerializeField] private Vector2 roundMaskScale = new Vector2(0f, 15f);

        [Tooltip("Round 洞形精灵（alpha）。默认 Feel 官方圆；可换成自有图案。")]
        [SerializeField] private Sprite roundMaskSprite;

        [SerializeField] private Color roundBackgroundColor = Color.black;

        public bool Enabled => enabled;
        public bool IgnoreTimeScale => ignoreTimeScale;
        public bool BlockRaycastsDuringFade => blockRaycastsDuringFade;

        public float DirectionalFadeInDuration => Mathf.Max(0f, directionalFadeInDuration);
        public float DirectionalFadeOutDuration => Mathf.Max(0f, directionalFadeOutDuration);
        public TransitionTweenCurve DirectionalTween => directionalTween;

        public float RoundFadeInDuration => Mathf.Max(0f, roundFadeInDuration);
        public float RoundFadeOutDuration => Mathf.Max(0f, roundFadeOutDuration);
        public TransitionTweenCurve RoundTween => roundTween;
        public Vector2 RoundMaskScale => roundMaskScale;
        public Sprite RoundMaskSprite => roundMaskSprite;
        public Color RoundBackgroundColor => roundBackgroundColor;

        public void SetEnabled(bool value) => enabled = value;

        public void SetDirectionalFadeInDuration(float seconds) =>
            directionalFadeInDuration = Mathf.Max(0f, seconds);

        public void SetDirectionalFadeOutDuration(float seconds) =>
            directionalFadeOutDuration = Mathf.Max(0f, seconds);

        public void SetDirectionalTween(TransitionTweenCurve curve) => directionalTween = curve;

        public void SetRoundFadeInDuration(float seconds) =>
            roundFadeInDuration = Mathf.Max(0f, seconds);

        public void SetRoundFadeOutDuration(float seconds) =>
            roundFadeOutDuration = Mathf.Max(0f, seconds);

        public void SetRoundTween(TransitionTweenCurve curve) => roundTween = curve;

        public void SetRoundMaskScale(Vector2 scale) => roundMaskScale = scale;

        public void SetRoundMaskSprite(Sprite sprite) => roundMaskSprite = sprite;

        public void SetRoundBackgroundColor(Color color) => roundBackgroundColor = color;

        public void SetIgnoreTimeScale(bool value) => ignoreTimeScale = value;

        public void SetBlockRaycastsDuringFade(bool value) => blockRaycastsDuringFade = value;
    }

    /// <summary>过场缓动；数值对齐 Feel <c>MMTween.MMTweenCurve</c> 常用项。</summary>
    public enum TransitionTweenCurve
    {
        LinearTween = 0,
        EaseInCubic = 4,
        EaseOutCubic = 5,
        EaseInOutCubic = 6,
        EaseInOutQuartic = 9,
    }
}

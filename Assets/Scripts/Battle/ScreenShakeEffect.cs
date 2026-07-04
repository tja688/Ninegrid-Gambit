using UnityEngine;

namespace NineGrid.Battle
{
    /// <summary>
    /// 可独立触发的屏幕震动预设。参数挂在自身，任意脚本 / UnityEvent 调 <see cref="Play()"/>；
    /// 也可走静态 <see cref="Trigger"/> 临时指定参数。底层由 <see cref="CameraJuice"/> 执行。
    /// </summary>
    public sealed class ScreenShakeEffect : MonoBehaviour
    {
        [Header("震动参数")]
        [Tooltip("Play() 无 power 时使用的幅度（世界单位）。")]
        public float amplitude = 0.9f;
        [Tooltip("Play(power) 时最弱幅度。")]
        public float amplitudeMin = 0.5f;
        [Tooltip("Play(power) 时最猛幅度。")]
        public float amplitudeMax = 1.8f;
        [Tooltip("震动时长（真实秒，不受 timeScale 影响）。")]
        public float duration = 0.8f;
        [Tooltip("震动频率。<=0 时用 CameraJuice 默认值。")]
        public float frequency = 32f;
        [Tooltip("true：Play(power) 在 min/max 间插值；false：始终用 amplitude。")]
        public bool scaleAmplitudeByPower = true;

        /// <summary>按预设固定幅度触发。</summary>
        public void Play() => Trigger(amplitude, duration, frequency);

        /// <summary>按 0..1 力度触发（可映射 min/max 幅度）。</summary>
        public void Play(float power01)
        {
            float amp = scaleAmplitudeByPower
                ? Mathf.Lerp(amplitudeMin, amplitudeMax, Mathf.Clamp01(power01))
                : amplitude;
            Trigger(amp, duration, frequency);
        }

        [ContextMenu("Play Screen Shake")]
        void PlayFromContextMenu() => Play();

        /// <summary>用任意参数触发一次屏幕震动（无需挂本组件）。</summary>
        public static bool Trigger(float amplitude, float duration, float frequency = -1f)
            => CameraJuice.TryShake(amplitude, duration, frequency);
    }
}

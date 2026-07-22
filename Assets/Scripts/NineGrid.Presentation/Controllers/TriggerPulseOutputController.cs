using NineGrid.Cards;
using NineGrid.Flow.Presentation;
using UnityEngine;

namespace NineGrid.Presentation.Controllers
{
    /// <summary>
    /// 触发脉冲装配 Controller：持有 TriggerPulseHub 生命周期；Hub 深模块算法不变。
    /// </summary>
    public sealed class TriggerPulseOutputController : PresentationController
    {
        private bool mConfigured;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterInstallHook()
        {
            TriggerPulseOutputHook.ConfigureProduction = () =>
                EnsureInstalled().ConfigureProductionDefaults();
            TriggerPulseOutputHook.ResetToNull = () =>
            {
                var existing = UnityEngine.Object.FindObjectOfType<TriggerPulseOutputController>();
                if (existing != null)
                {
                    existing.ResetHub();
                }
                else
                {
                    TriggerPulseHub.ResetToNull();
                }
            };
        }

        public static TriggerPulseOutputController EnsureInstalled()
        {
            var existing = UnityEngine.Object.FindObjectOfType<TriggerPulseOutputController>();
            if (existing != null)
            {
                return existing;
            }

            var host = new GameObject(nameof(TriggerPulseOutputController));
            return host.AddComponent<TriggerPulseOutputController>();
        }

        protected override void OnUnbind()
        {
            ResetHub();
        }

        public void ConfigureProductionDefaults()
        {
            TriggerPulseHub.Configure(
                new CardEffectTriggerPulseSink(),
                new DebouncingTriggerPulseSink(
                    new AudioTriggerPulseSink(),
                    TriggerPulseHub.DefaultAudioDebounceSeconds));
            mConfigured = true;
        }

        public void ResetHub()
        {
            if (!mConfigured)
            {
                TriggerPulseHub.ResetToNull();
                return;
            }

            TriggerPulseHub.ResetToNull();
            mConfigured = false;
        }
    }
}

using System;

namespace NineGrid.Cards
{
    /// <summary>
    /// 触发脉冲装配入口：由 Presentation TriggerPulseOutputController 注册。
    /// Hub 深模块仍在 Flow.Presentation；此处只统一生命周期接线。
    /// </summary>
    public static class TriggerPulseOutputHook
    {
        public static Action ConfigureProduction;
        public static Action ResetToNull;
        /// <summary>仅重置 FX 通道（局内战斗装配关闭时使用，音频保持应用会话）。</summary>
        public static Action ResetFxToNull;

        public static void RequestConfigureProduction()
        {
            ConfigureProduction?.Invoke();
        }

        public static void RequestReset()
        {
            ResetToNull?.Invoke();
        }

        public static void RequestResetFx()
        {
            ResetFxToNull?.Invoke();
        }
    }
}

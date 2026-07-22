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

        public static void RequestConfigureProduction()
        {
            ConfigureProduction?.Invoke();
        }

        public static void RequestReset()
        {
            ResetToNull?.Invoke();
        }
    }
}

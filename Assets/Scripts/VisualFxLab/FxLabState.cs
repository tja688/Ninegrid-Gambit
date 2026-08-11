namespace NineGrid.VisualFxLab
{
    /// <summary>
    /// 画面实验室全屏 Look 变体。None = 原版画面（Feature 不入队，零开销）。
    /// </summary>
    public enum FxLabLook
    {
        None = 0,
        CrtDeluxe = 1,
        Candlelight = 2,
        BloomGlow = 3,
        DungeonNoir = 4,
        OldFilm = 5,
    }

    /// <summary>
    /// 画面实验室运行时状态（试验性）。唯一控制入口是 <see cref="FxLabHotkeyHost"/>：
    /// F9 一键装配 / 一键还原；F10 循环全屏 Look；F11 循环强度；F6/F7/F8 模块开关。
    /// Installed = false 时渲染 Feature 与所有模块完全不参与，游戏保持原汁原味。
    /// </summary>
    public static class FxLabState
    {
        /// <summary>一键装配总开关。false = 原版画面。</summary>
        public static bool Installed;

        /// <summary>当前全屏 Look（F10 循环）。</summary>
        public static FxLabLook Look = FxLabLook.CrtDeluxe;

        /// <summary>全屏 Look 强度（F11 循环 1.0 / 0.7 / 0.4）。</summary>
        public static float LookStrength = 1f;

        /// <summary>机关卡光环环绕（F6）。</summary>
        public static bool TrapHalo = true;

        /// <summary>棋盘氛围浮尘与余烬（F7）。</summary>
        public static bool AmbientDust = true;

        /// <summary>卡面流光（F8）。</summary>
        public static bool CardFoil = true;

        /// <summary>渲染 Feature 是否已随 Renderer2D 加载（供状态叠层提示）。</summary>
        public static bool FeatureLoaded;

        public static string LookDisplayName(FxLabLook look)
        {
            switch (look)
            {
                case FxLabLook.CrtDeluxe: return "CRT 显像管";
                case FxLabLook.Candlelight: return "烛光酒馆";
                case FxLabLook.BloomGlow: return "辉光绽放";
                case FxLabLook.DungeonNoir: return "深渊地牢";
                case FxLabLook.OldFilm: return "老式胶片";
                default: return "无（原版）";
            }
        }
    }
}

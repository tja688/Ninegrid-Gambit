namespace NineGrid.Core
{
    /// <summary>
    /// 表演锚点：结算指令在表演时间轴上的<strong>表演消费归属</strong>
    ///（卡面数值、旁路装饰如飘字/FX/金币等共用；非仅卡面）。
    /// <para>
    /// v1 只有三个：<see cref="Impact"/>（命中帧）、<see cref="Settled"/>（本批表演收尾）、
    /// <see cref="None"/>（无表演消费，须在映射表写明理由）。
    /// </para>
    /// <para>
    /// 升级路径：若观察型加攻等 Settled 归属在观感上嫌晚，可增加「命中后」锚点（建议名
    /// <c>PostImpact</c>），在既有 <c>onCombatHit</c> 回调内延时后二次报点即可，无需重构排期器。
    /// </para>
    /// </summary>
    public enum PresentationBeat
    {
        /// <summary>无表演消费（排期器不装载）。映射表必须附带非空理由。</summary>
        None = 0,

        /// <summary>命中帧（攻击/反击 onCombatHit）。</summary>
        Impact = 1,

        /// <summary>本批表演通道完成后、就位回执前。</summary>
        Settled = 2
    }
}

using NineGrid.Core;
using NineGrid.Core.Systems;
using UnityEngine;

namespace NineGrid.Flow.Tutorial
{
    /// <summary>
    /// 教学关卡内容计划：五阶段受控发牌。
    /// 开局仅 Bootstrap Avatar；场面由 <see cref="TutorialSetupPhaseCommand"/> 指定格直摆。
    /// </summary>
    public static class TutorialDeckPlan
    {
        public const int PhaseCount = 5;

        /// <summary>空池开局：导演在 Opening 后直摆阶段1。</summary>
        public static NodeDeckOptions BuildOpeningOptions(IContentSystem content)
        {
            _ = content;
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0,
                RequireElite = false,
                PreserveDealOrder = true,
            };
        }
    }
}

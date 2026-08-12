using NineGrid.Core;
using NineGrid.Flow.Tutorial;

namespace NineGrid.Flow
{
    /// <summary>
    /// 开局选项；QuickTest 细节复用 <see cref="QuickTestRunOptions"/>。
    /// 正式开局用 <see cref="CreateFormal"/>；QuickTest 镜像/通道用 <see cref="CreateQuickTest"/>；
    /// 读档恢复用 <see cref="CreateRestore"/>（正式模式 + 恢复快照，从快照战斗节点入场）；
    /// 教学关卡用 <see cref="CreateTutorial"/>（单场受控教学战斗，不进节点循环）。
    /// 模式一律由载荷是否非空推导，调用方不再手拼布尔组合。
    /// </summary>
    public sealed class GameFlowRunOptions
    {
        public QuickTestRunOptions QuickTest { get; private set; }

        /// <summary>读档恢复快照；非空表示本次开局是恢复模式（RunSave）。</summary>
        public RunSaveSnapshot RestoreSnapshot { get; private set; }

        /// <summary>教学关卡载荷；非空表示本次开局是教学模式（单场教学战斗）。</summary>
        public TutorialRunOptions Tutorial { get; private set; }

        public bool QuickTestMode => QuickTest != null;

        public bool RestoreMode => RestoreSnapshot != null;

        public bool TutorialMode => Tutorial != null;

        /// <summary>正式开局：无 QuickTest 标志 / 无作弊 / 无动态装配。</summary>
        public static GameFlowRunOptions CreateFormal()
        {
            return new GameFlowRunOptions();
        }

        /// <summary>QuickTest 开局（主菜单 <c>\0</c>–<c>\9</c> 通道；<c>\0</c> 为带作弊的正式流程镜像）。</summary>
        public static GameFlowRunOptions CreateQuickTest(QuickTestRunOptions quickTest = null)
        {
            return new GameFlowRunOptions
            {
                QuickTest = quickTest ?? new QuickTestRunOptions(),
            };
        }

        /// <summary>读档恢复开局：正式模式，Core 状态与 RNG 由快照覆盖，从快照战斗节点开始。</summary>
        public static GameFlowRunOptions CreateRestore(RunSaveSnapshot snapshot)
        {
            return new GameFlowRunOptions
            {
                RestoreSnapshot = snapshot,
            };
        }

        /// <summary>教学开局：单场受控教学战斗；通关后按载荷决定转正式开局或回主菜单。</summary>
        public static GameFlowRunOptions CreateTutorial(bool continueToFormalRun)
        {
            return new GameFlowRunOptions
            {
                Tutorial = new TutorialRunOptions { ContinueToFormalRun = continueToFormalRun },
            };
        }
    }
}

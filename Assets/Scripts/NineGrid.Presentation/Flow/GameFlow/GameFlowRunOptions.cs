namespace NineGrid.Flow
{
    /// <summary>
    /// 开局选项；QuickTest 细节复用 <see cref="QuickTestRunOptions"/>。
    /// 正式开局用 <see cref="CreateFormal"/>；QuickTest 镜像/通道用 <see cref="CreateQuickTest"/>。
    /// QuickTest 模式由 <see cref="QuickTest"/> 载荷是否非空推导，调用方不再手拼布尔组合。
    /// </summary>
    public sealed class GameFlowRunOptions
    {
        public QuickTestRunOptions QuickTest { get; private set; }

        public bool QuickTestMode => QuickTest != null;

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
    }
}

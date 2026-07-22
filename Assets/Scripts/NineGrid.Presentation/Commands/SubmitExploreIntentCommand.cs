using NineGrid.Core;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Commands
{
    /// <summary>
    /// 玩家探索意图：Core 合法性裁决后经 <see cref="IPresentationIntentRuntime"/> 提交导演。
    /// </summary>
    public sealed class SubmitExploreIntentCommand : AbstractCommand<bool>
    {
        private readonly int mGroundSlot;

        public SubmitExploreIntentCommand(int groundSlot)
        {
            mGroundSlot = groundSlot;
        }

        protected override bool OnExecute()
        {
            var architecture = NineGridArchitecture.Interface;
            string legalityReject;
            if (!BoardIntentLegality.TryExplainExplore(architecture, mGroundSlot, out legalityReject))
            {
                this.SendEvent(new ExploreIntentRejectedEvent
                {
                    GroundSlot = mGroundSlot,
                    Reason = legalityReject
                });
                Debug.LogWarning(
                    "[SubmitExploreIntentCommand] Explore 被 Core 合法性拒绝 slot="
                    + mGroundSlot + ": " + legalityReject);
                return false;
            }

            this.SendEvent(new EnsurePresentationDirectorRequested());

            var runtime = ResolveRuntime();
            if (runtime == null || !runtime.IsStarted)
            {
                Debug.LogWarning("[SubmitExploreIntentCommand] 表现意图运行时未启动。");
                return false;
            }

            bool preview;
            return runtime.TrySubmitIntent(
                new InputIntent(InputIntentKinds.Explore, mGroundSlot),
                out preview);
        }

        private IPresentationIntentRuntime ResolveRuntime()
        {
            // 测试夹具经 CompositionRoot 注册 IPresentationRuntimeSystem；
            // 生产路径由 InBattle 注册 DirectorIntentRuntime。
            var presentation = this.GetSystem<IPresentationRuntimeSystem>();
            if (presentation != null)
            {
                return presentation;
            }

            return this.GetSystem<IPresentationIntentRuntime>();
        }
    }
}

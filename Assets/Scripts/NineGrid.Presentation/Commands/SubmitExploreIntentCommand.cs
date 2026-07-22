using NineGrid.Core;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Commands
{
    /// <summary>
    /// 玩家探索意图：Core 合法性裁决后经 <see cref="IPresentationRuntimeSystem"/> 提交导演。
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

            var runtime = this.GetSystem<IPresentationRuntimeSystem>();
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
    }
}

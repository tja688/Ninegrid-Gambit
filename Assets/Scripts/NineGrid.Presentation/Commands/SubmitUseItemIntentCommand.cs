using NineGrid.Core;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Commands
{
    /// <summary>
    /// 玩家用牌意图：Core 合法性裁决后经 <see cref="IPresentationIntentRuntime"/> 提交导演。
    /// </summary>
    public sealed class SubmitUseItemIntentCommand : AbstractCommand<bool>
    {
        private readonly int mItemUid;
        private readonly int[] mSelectedCardUids;
        private readonly string mSelectedOption;

        public SubmitUseItemIntentCommand(
            int itemUid,
            int[] selectedCardUids,
            string selectedOption)
        {
            mItemUid = itemUid;
            mSelectedCardUids = selectedCardUids;
            mSelectedOption = selectedOption;
        }

        protected override bool OnExecute()
        {
            if (mItemUid <= 0)
            {
                return false;
            }

            var architecture = NineGridArchitecture.Interface;
            string legalityReject;
            if (!BoardIntentLegality.TryExplainUseItem(
                    architecture,
                    mItemUid,
                    mSelectedCardUids,
                    mSelectedOption,
                    out legalityReject))
            {
                this.SendEvent(new UseItemIntentRejectedEvent
                {
                    ItemUid = mItemUid,
                    Reason = legalityReject
                });
                Debug.LogWarning(
                    "[SubmitUseItemIntentCommand] UseItem 被 Core 合法性拒绝 itemUid="
                    + mItemUid + ": " + legalityReject);
                return false;
            }

            this.SendEvent(new EnsurePresentationDirectorRequested());

            var runtime = ResolveRuntime();
            if (runtime == null || !runtime.IsStarted)
            {
                Debug.LogWarning("[SubmitUseItemIntentCommand] 表现意图运行时未启动。");
                return false;
            }

            bool preview;
            return runtime.TrySubmitIntent(
                new InputIntent(
                    InputIntentKinds.UseItem,
                    mItemUid,
                    mSelectedCardUids,
                    mSelectedOption),
                out preview);
        }

        private IPresentationIntentRuntime ResolveRuntime()
        {
            var presentation = this.GetSystem<IPresentationRuntimeSystem>();
            if (presentation != null)
            {
                return presentation;
            }

            return this.GetSystem<IPresentationIntentRuntime>();
        }
    }
}

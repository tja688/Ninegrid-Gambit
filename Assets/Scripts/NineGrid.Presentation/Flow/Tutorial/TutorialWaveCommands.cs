using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Flow.Tutorial
{
    /// <summary>
    /// 教学波次清场：移除场上全部机关与怪物（Avatar 与道具卡不动），
    /// reason=clearResidualBoard（与清关收场同款——不派发 OnRemove/OnCumulative，
    /// 避免遗物 / 效果在换波拍连锁）。经批次门解算，由棋盘 Present 通道消费移除表演。
    /// </summary>
    public sealed class TutorialClearWaveCommand : AbstractCommand<CoreCommandResult>
    {
        protected override CoreCommandResult OnExecute()
        {
            var pipeline = this.GetSystem<IActionPipelineSystem>();
            var board = this.GetModel<BoardModel>();
            var registry = this.GetModel<CardRegistry>();
            var avatarUid = board.AvatarUid.Value;

            var removed = 0;
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var uid = board.GetCardUid(SlotId.Board(i));
                if (uid <= 0
                    || uid == avatarUid
                    || !registry.TryGet(uid, out var card)
                    || card == null)
                {
                    continue;
                }

                if (card.Kind != CardKind.Trap && card.Kind != CardKind.Monster)
                {
                    continue;
                }

                pipeline.Enqueue(new RemoveCardAction(uid, ZoneId.Removed, "clearResidualBoard"));
                removed++;
            }

            if (removed > 0)
            {
                pipeline.RunToCompletion();
            }

            return CoreCommandResult.Accept(removed);
        }
    }

    /// <summary>
    /// 教学波次补发：把下一波卡按抽牌堆序（index 0 = 第一张补出）顶插入抽牌堆。
    /// 逆序顶插保证最终顺序；后续由盘面稳定化按 FillOrder 补满场地。
    /// </summary>
    public sealed class TutorialInjectWaveCommand : AbstractCommand<CoreCommandResult>
    {
        private readonly IReadOnlyList<string> mPileOrderDefIds;

        public TutorialInjectWaveCommand(IReadOnlyList<string> pileOrderDefIds)
        {
            mPileOrderDefIds = pileOrderDefIds ?? new List<string>();
        }

        protected override CoreCommandResult OnExecute()
        {
            var pipeline = this.GetSystem<IActionPipelineSystem>();
            var injected = 0;
            for (var i = mPileOrderDefIds.Count - 1; i >= 0; i--)
            {
                var defId = mPileOrderDefIds[i];
                if (string.IsNullOrEmpty(defId))
                {
                    continue;
                }

                pipeline.Enqueue(new ShuffleIntoDrawPileAction(
                    defId,
                    CardKind.Trap,
                    1,
                    top: true,
                    cause: "tutorialWave"));
                injected++;
            }

            if (injected > 0)
            {
                pipeline.RunToCompletion();
            }

            return CoreCommandResult.Accept(injected);
        }
    }
}

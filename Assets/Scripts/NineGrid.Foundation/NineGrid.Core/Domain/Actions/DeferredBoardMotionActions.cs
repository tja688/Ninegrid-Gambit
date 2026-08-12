namespace NineGrid.Core
{
    /// <summary>
    /// 结算窗口内挂起的效果位移条目（ADR-0044）。
    /// 交战窗 / 敌方行动阶段内由效果 DSL 产出的盘面位移动作不即时结算，
    /// 以本条目（动作 + 挂起时占位快照）暂存于 <see cref="BattleContextModel"/>，
    /// 由收尾锚点经 <see cref="ResolveDeferredBoardMotionAction"/> 就地复验后落地。
    /// </summary>
    public sealed class DeferredBoardMotion
    {
        private enum MotionKind
        {
            Rotate,
            Swap,
            Move
        }

        private MotionKind mKind;
        private GameAction mAction;

        // Swap 复验快照：挂起时两格的占位 uid（0 = 空格）。
        private SlotId mLeftSlot;
        private SlotId mRightSlot;
        private int mLeftUid;
        private int mRightUid;

        private DeferredBoardMotion()
        {
        }

        /// <summary>动作属于可挂起的盘面位移类型时捕获条目（含占位快照），否则返回 null。</summary>
        public static DeferredBoardMotion TryCapture(GameAction action, BoardModel board)
        {
            if (action is RotateBoardClockwiseAction)
            {
                return new DeferredBoardMotion { mKind = MotionKind.Rotate, mAction = action };
            }

            var swap = action as SwapBoardSlotsAction;
            if (swap != null)
            {
                return new DeferredBoardMotion
                {
                    mKind = MotionKind.Swap,
                    mAction = action,
                    mLeftSlot = swap.Left,
                    mRightSlot = swap.Right,
                    mLeftUid = board != null ? board.GetCardUid(swap.Left) : 0,
                    mRightUid = board != null ? board.GetCardUid(swap.Right) : 0,
                };
            }

            if (action is MoveCardAction)
            {
                return new DeferredBoardMotion { mKind = MotionKind.Move, mAction = action };
            }

            return null;
        }

        /// <summary>
        /// 落地时就地复验并产出实际执行的动作；失效返回 false（该条静默丢弃，ADR-0044 §4）。
        /// Rotate 恒有效；Swap 以挂起快照为锚——有卡侧跟随卡的当前格重建，空格侧要求原格仍空；
        /// Move 要求卡仍在盘面（或为 Avatar）且目标格空闲 / 即本卡。
        /// </summary>
        public bool TryBuildFlushAction(GameActionContext context, out GameAction action)
        {
            action = null;
            switch (mKind)
            {
                case MotionKind.Rotate:
                    action = mAction;
                    return true;

                case MotionKind.Swap:
                    return TryBuildSwapFlushAction(context, out action);

                case MotionKind.Move:
                    return TryBuildMoveFlushAction(context, out action);

                default:
                    return false;
            }
        }

        private bool TryBuildSwapFlushAction(GameActionContext context, out GameAction action)
        {
            action = null;
            var board = context.GetModel<BoardModel>();
            var registry = context.GetModel<CardRegistry>();
            SlotId left;
            SlotId right;
            if (!TryResolveSwapSide(board, registry, mLeftSlot, mLeftUid, out left)
                || !TryResolveSwapSide(board, registry, mRightSlot, mRightUid, out right))
            {
                return false;
            }

            if (left == right)
            {
                return false;
            }

            var original = (SwapBoardSlotsAction)mAction;
            if (left == original.Left && right == original.Right)
            {
                action = original;
                return true;
            }

            action = new SwapBoardSlotsAction(left, right, original.SourceDefId, original.Cause);
            return true;
        }

        /// <summary>有卡侧跟随卡的当前格（卡离场则失效）；空格侧要求原格仍空。</summary>
        private static bool TryResolveSwapSide(
            BoardModel board,
            CardRegistry registry,
            SlotId capturedSlot,
            int capturedUid,
            out SlotId slot)
        {
            slot = capturedSlot;
            if (capturedUid == 0)
            {
                return board.GetCardUid(capturedSlot) == 0;
            }

            CardInstance card;
            if (!registry.TryGet(capturedUid, out card)
                || card == null
                || card.Zone.Value != ZoneId.Board
                || !card.Slot.Value.IsBoardSlot)
            {
                return false;
            }

            slot = card.Slot.Value;
            return true;
        }

        private bool TryBuildMoveFlushAction(GameActionContext context, out GameAction action)
        {
            action = null;
            var move = (MoveCardAction)mAction;
            var registry = context.GetModel<CardRegistry>();
            CardInstance card;
            if (!registry.TryGet(move.CardUid, out card) || card == null)
            {
                return false;
            }

            if (card.Kind != CardKind.Avatar && card.Zone.Value != ZoneId.Board)
            {
                return false;
            }

            var occupant = context.GetModel<BoardModel>().GetCardUid(move.ToSlot);
            if (occupant != 0 && occupant != move.CardUid)
            {
                return false;
            }

            action = move;
            return true;
        }
    }

    /// <summary>
    /// 挂起位移落地包装（ADR-0044）：Apply 时就地复验，有效则以 FollowUp 执行原动作
    /// （其事件与 Post 触发照常），失效则静默丢弃（触发已发生、后果失效）。
    /// </summary>
    public sealed class ResolveDeferredBoardMotionAction : GameAction
    {
        private readonly DeferredBoardMotion mMotion;

        public ResolveDeferredBoardMotionAction(DeferredBoardMotion motion)
        {
            mMotion = motion;
        }

        public override string ActionName { get { return "ResolveDeferredBoardMotion"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            GameAction action;
            if (mMotion == null || !mMotion.TryBuildFlushAction(context, out action))
            {
                return GameActionResult.Empty;
            }

            return new GameActionResult().AddFollowUp(action);
        }
    }
}

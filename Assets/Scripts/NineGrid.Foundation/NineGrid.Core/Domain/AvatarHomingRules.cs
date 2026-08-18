namespace NineGrid.Core
{
    /// <summary>
    /// 离巢归位倒计时规则（ADR-0057 / #220）：
    /// 换位落地后进入离巢状态（计数为 0）；之后 Avatar 占格每实际变化一次（旋转带走/置换带走）计数 +1，
    /// 到 3 次时触发归位（回中心格；中心空则移回，有占用者则原子互换）。
    /// 归位造成的位移不计入下一次倒计时，归位后退出离巢状态。
    /// 离巢中再次执行原子换位则重置计数为 0 并重新起算。
    /// 两卡交换未改变 Avatar 占格不累加计数。
    /// 归位倒计时不是卡级节奏源，不打开敌方开火窗口。
    /// </summary>
    public static class AvatarHomingRules
    {
        public const int StepsRequiredForHoming = 3;
        public const string HomingCause = "homing";

        public static void TryTrackAvatarDisplacement(
            GameActionResult result,
            GameActionContext context,
            SlotId fromSlot,
            SlotId toSlot,
            string sourceDefId,
            string cause)
        {
            if (result == null || context == null || fromSlot == toSlot)
            {
                return;
            }

            var board = context.GetModel<BoardModel>();
            if (board == null || !board.IsAvatarOffHome.Value)
            {
                return;
            }

            if (toSlot == SlotId.Center || toSlot == SlotId.Board(5))
            {
                board.ClearAvatarOffHome();
                return;
            }

            board.AvatarHomingSteps.Value += 1;
            if (board.AvatarHomingSteps.Value >= StepsRequiredForHoming)
            {
                result.AddFollowUp(new SwapAvatarWithCardAction(SlotId.Center, sourceDefId, HomingCause));
            }
        }
    }
}

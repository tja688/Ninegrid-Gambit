namespace NineGrid.Flow.RoomIcons
{
    /// <summary>
    /// 表现侧驻留会话：踩上图标后武装，跳走取消；提交标记只成功一次。
    /// 计时由调用方执行（不得进 IntentIntake）。
    /// </summary>
    public sealed class RoomIconDwellSession
    {
        public const float DefaultDwellSeconds = 1f;

        private bool mArmed;
        private bool mSubmitted;
        private int mSlot;
        private int mOptionIndex;

        public bool IsArmed => mArmed;

        public bool HasSubmitted => mSubmitted;

        public int ArmedSlot => mSlot;

        public int ArmedOptionIndex => mOptionIndex;

        public void Begin(int slot, int optionIndex)
        {
            mArmed = true;
            mSubmitted = false;
            mSlot = slot;
            mOptionIndex = optionIndex;
        }

        public void Cancel()
        {
            mArmed = false;
            mSlot = 0;
            mOptionIndex = -1;
        }

        /// <summary>
        /// 驻留到期：取出 optionIndex 并解除武装；在 Intent 成功前不置 HasSubmitted，
        /// 以便 Select/Enter 失败后可重新 Begin。
        /// </summary>
        public bool TryConsumeArmed(out int optionIndex)
        {
            optionIndex = -1;
            if (!mArmed || mSubmitted)
            {
                return false;
            }

            mArmed = false;
            optionIndex = mOptionIndex;
            return true;
        }

        public void MarkSubmitted()
        {
            mSubmitted = true;
            mArmed = false;
        }
    }
}

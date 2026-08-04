namespace NineGrid.Flow.BoardBriefTip
{
    /// <summary>
    /// 简要解释文字框会话：悬停与 Notice 两路文案，Notice 盖住悬停；代数清防脏写。
    /// </summary>
    public sealed class BoardBriefTipSession
    {
        private string mHover = string.Empty;
        private string mNotice = string.Empty;
        private int mHoverGeneration;
        private int mNoticeGeneration;

        public string DisplayText =>
            !string.IsNullOrEmpty(mNotice) ? mNotice : mHover;

        public bool IsVisible => !string.IsNullOrEmpty(DisplayText);

        public bool HasNotice => !string.IsNullOrEmpty(mNotice);

        public int ShowHover(string text)
        {
            mHover = text ?? string.Empty;
            return ++mHoverGeneration;
        }

        public void ClearHover()
        {
            mHover = string.Empty;
            mHoverGeneration++;
        }

        public void ClearHover(int generation)
        {
            if (generation != mHoverGeneration)
            {
                return;
            }

            mHover = string.Empty;
        }

        public int ShowNotice(string text)
        {
            mNotice = text ?? string.Empty;
            return ++mNoticeGeneration;
        }

        public void ClearNotice()
        {
            mNotice = string.Empty;
            mNoticeGeneration++;
        }

        public void ClearNotice(int generation)
        {
            if (generation != mNoticeGeneration)
            {
                return;
            }

            mNotice = string.Empty;
        }

        /// <summary>
        /// 硬清悬停 + Notice。进战 / 场地板撤场兜底，避免「金币不足」等 Notice 或悬停粘连进战斗。
        /// </summary>
        public void HardClear()
        {
            mHover = string.Empty;
            mNotice = string.Empty;
            mHoverGeneration++;
            mNoticeGeneration++;
        }

        public void ResetForTests()
        {
            mHover = string.Empty;
            mNotice = string.Empty;
            mHoverGeneration = 0;
            mNoticeGeneration = 0;
        }
    }
}

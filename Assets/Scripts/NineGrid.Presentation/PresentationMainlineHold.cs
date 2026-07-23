namespace NineGrid.Presentation
{
    /// <summary>
    /// 须阻塞输入的交战/场地表演挂主线租约（#47 / ADR-0004）。
    /// 已有 ExternalHold 时依赖外层；主线已因 PresentStep busy 时 TryBegin 嵌套计数。
    /// </summary>
    public static class PresentationMainlineHold
    {
        /// <summary>
        /// 尝试覆盖主线 busy。已有外层 ExternalHold 时视为已覆盖且 <paramref name="acquiredHere"/> 为 false。
        /// </summary>
        /// <returns>false 仅当无法 Begin 且当前也无外层 hold（例如 Runtime 未启动）。</returns>
        public static bool TryAcquire(string reason, out bool acquiredHere)
        {
            acquiredHere = false;
            if (PresentationInputGates.HasExternalHold)
            {
                return true;
            }

            if (!PresentationInputGates.TryBeginExternalHold(reason))
            {
                return false;
            }

            acquiredHere = true;
            return true;
        }

        public static void Release(bool acquiredHere, string reason)
        {
            if (acquiredHere)
            {
                PresentationInputGates.EndExternalHold(reason);
            }
        }
    }
}

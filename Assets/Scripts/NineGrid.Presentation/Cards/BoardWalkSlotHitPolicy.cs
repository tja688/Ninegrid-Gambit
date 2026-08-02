namespace NineGrid.Cards
{
    /// <summary>
    /// 空槽 HitProxy 启用策略：跳格模式任意空格可点；战斗 Explore 仍仅中心正交邻格。
    /// </summary>
    public static class BoardWalkSlotHitPolicy
    {
        public static bool ShouldEnableEmptySlotHit(bool isEmpty, int slot, bool walkEnabled)
        {
            if (!isEmpty || !GroundSlotTopology.IsValidSlot(slot))
            {
                return false;
            }

            if (walkEnabled)
            {
                // 含角格 1/3/7/9，以及 Avatar 离场后的格 5。
                return true;
            }

            return !GroundSlotTopology.IsAvatarReserved(slot)
                   && GroundSlotTopology.AreOrthogonal(slot, GroundSlotTopology.AvatarReservedSlot);
        }

        public static bool IsWalkEnabledNow()
        {
            return BoardWalkInputHook.IsEnabled != null && BoardWalkInputHook.IsEnabled();
        }
    }
}

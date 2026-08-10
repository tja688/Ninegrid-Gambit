namespace NineGrid.Content.Vfx
{
    /// <summary>视觉域宿主允许的排序层与 order 区间；播放器不得越界改写卡级 SortingGroup。</summary>
    public readonly struct VfxSortingBounds
    {
        public static readonly VfxSortingBounds Empty = new VfxSortingBounds(string.Empty, 0, 0);

        public VfxSortingBounds(string sortingLayerName, int minSortingOrder, int maxSortingOrder)
        {
            SortingLayerName = sortingLayerName ?? string.Empty;
            MinSortingOrder = minSortingOrder;
            MaxSortingOrder = maxSortingOrder;
        }

        public string SortingLayerName { get; }
        public int MinSortingOrder { get; }
        public int MaxSortingOrder { get; }

        public bool HasLayer => !string.IsNullOrEmpty(SortingLayerName);

        public int ClampOrder(int order)
        {
            if (order < MinSortingOrder)
            {
                return MinSortingOrder;
            }

            if (order > MaxSortingOrder)
            {
                return MaxSortingOrder;
            }

            return order;
        }
    }
}

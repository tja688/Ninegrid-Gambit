namespace NineGrid.Core
{
    public sealed class HelpCardStackEntry
    {
        public HelpCardStackEntry(string defId, int count)
        {
            DefId = defId ?? string.Empty;
            Count = count < 1 ? 1 : count;
        }

        public string DefId { get; private set; }
        public int Count { get; private set; }
    }
}

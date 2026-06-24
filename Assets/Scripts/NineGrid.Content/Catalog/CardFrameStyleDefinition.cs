namespace NineGrid.Content
{
    public sealed class CardFrameStyleDefinition
    {
        public CardFrameStyleDefinition(string styleId, ContentColor color)
        {
            StyleId = styleId ?? string.Empty;
            Color = color;
        }

        public string StyleId { get; }
        public ContentColor Color { get; }
    }
}

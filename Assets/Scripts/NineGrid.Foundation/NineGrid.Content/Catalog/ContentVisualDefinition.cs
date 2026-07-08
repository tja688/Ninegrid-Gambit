namespace NineGrid.Content
{
    public sealed class ContentVisualDefinition
    {
        public ContentVisualDefinition(
            string contentId,
            ContentVisualKind kind,
            string description)
        {
            ContentId = contentId ?? string.Empty;
            Kind = kind;
            Description = description ?? string.Empty;
        }

        public string ContentId { get; private set; }
        public ContentVisualKind Kind { get; private set; }
        public string Description { get; private set; }
    }
}

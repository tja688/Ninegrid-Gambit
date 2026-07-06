namespace NineGrid.Content
{
    public sealed class ContentVisualDefinition
    {
        public ContentVisualDefinition(
            string contentId,
            ContentVisualKind kind,
            string description,
            string faceKey,
            string frameKey,
            string iconKey)
        {
            ContentId = contentId ?? string.Empty;
            Kind = kind;
            Description = description ?? string.Empty;
            FaceKey = faceKey ?? string.Empty;
            FrameKey = frameKey ?? string.Empty;
            IconKey = iconKey ?? string.Empty;
        }

        public string ContentId { get; private set; }
        public ContentVisualKind Kind { get; private set; }
        public string Description { get; private set; }
        public string FaceKey { get; private set; }
        public string FrameKey { get; private set; }
        public string IconKey { get; private set; }
    }
}

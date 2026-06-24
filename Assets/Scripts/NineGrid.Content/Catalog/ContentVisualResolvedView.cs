namespace NineGrid.Content
{
    public sealed class ContentVisualResolvedView
    {
        public string ContentId { get; set; }
        public ContentVisualKind Kind { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string FaceKey { get; set; }
        public string FrameKey { get; set; }
        public string IconKey { get; set; }
        public string IconResourcePath { get; set; }
    }
}

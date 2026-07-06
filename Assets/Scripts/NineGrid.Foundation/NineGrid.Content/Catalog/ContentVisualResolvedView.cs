namespace NineGrid.Content
{
    public sealed class ContentVisualResolvedView
    {
        public string ContentId { get; set; }
        public ContentVisualKind Kind { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string IconVisualId { get; set; }
        public string IconAssetKey { get; set; }
        public string FaceVisualId { get; set; }
        public string FaceAssetKey { get; set; }
        public string FrameStyleId { get; set; }
        public ContentColor FrameColor { get; set; } = ContentColor.White;

        public string IconKey
        {
            get { return IconVisualId; }
            set { IconVisualId = value; }
        }

        public string FaceKey
        {
            get { return FaceVisualId; }
            set { FaceVisualId = value; }
        }

        public string FrameKey
        {
            get { return FrameStyleId; }
            set { FrameStyleId = value; }
        }

        public string IconResourcePath
        {
            get { return VisualAssetKeyNaming.ToResourcesPath(IconAssetKey); }
        }
    }
}

using UnityEngine;

namespace NineGrid.Content
{
    public sealed class ContentVisualResolvedView
    {
        public string ContentId { get; set; }
        public ContentVisualKind Kind { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public Sprite Icon { get; set; }
        public Sprite Face { get; set; }
        public string FrameStyleId { get; set; }
        public ContentColor FrameColor { get; set; } = ContentColor.White;
    }
}

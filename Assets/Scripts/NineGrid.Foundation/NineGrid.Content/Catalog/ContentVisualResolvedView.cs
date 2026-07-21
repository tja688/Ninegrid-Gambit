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
        public Sprite BackBorder { get; set; }
        public Sprite BackShirt { get; set; }
        public Sprite BackLogo { get; set; }
        public string FrameStyleId { get; set; }
        public ContentColor FrameColor { get; set; } = ContentColor.White;

        public ContentVisualDirectSlotSprites ToDirectSlots()
        {
            return new ContentVisualDirectSlotSprites
            {
                MainIcon = Icon,
                FaceBackground = Face,
                BackBorder = BackBorder,
                BackShirt = BackShirt,
                BackLogo = BackLogo
            };
        }
    }
}

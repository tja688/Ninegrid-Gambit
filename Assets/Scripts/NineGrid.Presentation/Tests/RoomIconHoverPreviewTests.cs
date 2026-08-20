using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Flow.RoomIcons;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    public sealed class RoomIconHoverPreviewTests
    {
        [Test]
        public void ResolvePreviewDefIds_BossRoom_ReturnsEmpty()
        {
            var room = new RoomDefinition(RoomKind.Boss, "层主");
            var ids = RoomIconHoverPreviewPresenter.ResolvePreviewDefIds(room, arch: null);
            Assert.AreEqual(0, ids.Count);
        }
    }
}

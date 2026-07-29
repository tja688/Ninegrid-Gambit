using NUnit.Framework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// #77 / ADR-0011：对角相邻谓词；正交 <see cref="SlotId.IsAdjacentTo"/> 语义不变。
    /// </summary>
    public sealed class SlotIdDiagonalAdjacencyTests
    {
        [Test]
        public void IsDiagonallyAdjacentTo_Center_TrueForCorners_FalseForEdgesAndSelf()
        {
            var center = SlotId.Board(5);

            Assert.IsTrue(center.IsDiagonallyAdjacentTo(SlotId.Board(1)));
            Assert.IsTrue(center.IsDiagonallyAdjacentTo(SlotId.Board(3)));
            Assert.IsTrue(center.IsDiagonallyAdjacentTo(SlotId.Board(7)));
            Assert.IsTrue(center.IsDiagonallyAdjacentTo(SlotId.Board(9)));

            Assert.IsFalse(center.IsDiagonallyAdjacentTo(SlotId.Board(2)));
            Assert.IsFalse(center.IsDiagonallyAdjacentTo(SlotId.Board(4)));
            Assert.IsFalse(center.IsDiagonallyAdjacentTo(SlotId.Board(6)));
            Assert.IsFalse(center.IsDiagonallyAdjacentTo(SlotId.Board(8)));
            Assert.IsFalse(center.IsDiagonallyAdjacentTo(center));
        }

        [Test]
        public void IsDiagonallyAdjacentTo_NonCenter_SeparatesDiagonalFromOrthogonal()
        {
            // Avatar 可被挪出中心：格 1 的对角是 5；正交是 2/4。
            var corner = SlotId.Board(1);

            Assert.IsTrue(corner.IsDiagonallyAdjacentTo(SlotId.Board(5)));
            Assert.IsFalse(corner.IsDiagonallyAdjacentTo(SlotId.Board(2)));
            Assert.IsFalse(corner.IsDiagonallyAdjacentTo(SlotId.Board(4)));
            Assert.IsFalse(corner.IsDiagonallyAdjacentTo(SlotId.Board(3)));
            Assert.IsFalse(corner.IsDiagonallyAdjacentTo(SlotId.Board(7)));
        }

        [Test]
        public void IsAdjacentTo_RemainsOrthogonalOnly_WhenDiagonalExists()
        {
            var center = SlotId.Board(5);

            Assert.IsTrue(center.IsAdjacentTo(SlotId.Board(2)));
            Assert.IsTrue(center.IsAdjacentTo(SlotId.Board(4)));
            Assert.IsTrue(center.IsAdjacentTo(SlotId.Board(6)));
            Assert.IsTrue(center.IsAdjacentTo(SlotId.Board(8)));

            Assert.IsFalse(center.IsAdjacentTo(SlotId.Board(1)));
            Assert.IsFalse(center.IsAdjacentTo(SlotId.Board(3)));
            Assert.IsFalse(center.IsAdjacentTo(SlotId.Board(7)));
            Assert.IsFalse(center.IsAdjacentTo(SlotId.Board(9)));
        }

        [Test]
        public void IsDiagonallyAdjacentTo_RejectsAvatarAndNone()
        {
            Assert.IsFalse(SlotId.Board(5).IsDiagonallyAdjacentTo(SlotId.Avatar));
            Assert.IsFalse(SlotId.Board(5).IsDiagonallyAdjacentTo(SlotId.None));
            Assert.IsFalse(SlotId.Avatar.IsDiagonallyAdjacentTo(SlotId.Board(5)));
        }
    }
}

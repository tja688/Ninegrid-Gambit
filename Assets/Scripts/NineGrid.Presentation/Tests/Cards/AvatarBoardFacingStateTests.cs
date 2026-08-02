using NUnit.Framework;
using NineGrid.Cards;

namespace NineGrid.Presentation.Tests
{
    public sealed class AvatarBoardFacingStateTests
    {
        [TearDown]
        public void TearDown()
        {
            AvatarBoardFacingState.Reset();
        }

        [Test]
        public void ResolveFromPointerX_LeftOfAvatar_IsLeft()
        {
            Assert.AreEqual(
                AvatarBoardFacing.Left,
                AvatarBoardFacingState.ResolveFromPointerX(pointerX: -1f, avatarCenterX: 0f));
        }

        [Test]
        public void ResolveFromPointerX_UsesAvatarWorldX_NotFixedOrigin()
        {
            // Avatar 已跳到右侧（世界 x=3）：指针在其左侧但仍在盘面正 x，应朝左。
            Assert.AreEqual(
                AvatarBoardFacing.Left,
                AvatarBoardFacingState.ResolveFromPointerX(pointerX: 2f, avatarCenterX: 3f));
            Assert.AreEqual(
                AvatarBoardFacing.Right,
                AvatarBoardFacingState.ResolveFromPointerX(pointerX: 4f, avatarCenterX: 3f));
        }

        [Test]
        public void ResolveFromPointerX_RightOfAvatar_IsRight()
        {
            Assert.AreEqual(
                AvatarBoardFacing.Right,
                AvatarBoardFacingState.ResolveFromPointerX(pointerX: 1f, avatarCenterX: 0f));
        }

        [Test]
        public void ResolveFromPointerX_ExactCenter_IsRight()
        {
            Assert.AreEqual(
                AvatarBoardFacing.Right,
                AvatarBoardFacingState.ResolveFromPointerX(pointerX: 0f, avatarCenterX: 0f));
        }

        [Test]
        public void SetFacing_Left_EnablesMirrorX()
        {
            Assert.IsTrue(AvatarBoardFacingState.SetFacing(AvatarBoardFacing.Left));
            Assert.AreEqual(AvatarBoardFacing.Left, AvatarBoardFacingState.Facing);
            Assert.IsTrue(AvatarBoardFacingState.MirrorX);
        }

        [Test]
        public void SetFacing_SameValue_ReturnsFalse()
        {
            AvatarBoardFacingState.SetFacing(AvatarBoardFacing.Right);
            Assert.IsFalse(AvatarBoardFacingState.SetFacing(AvatarBoardFacing.Right));
        }
    }
}

using NineGrid.Core;
using NineGrid.Flow.BoardBriefTip;
using NineGrid.Flow.Tutorial;
using NineGrid.Presentation.Systems;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace NineGrid.Presentation.Tests.Flow
{
    [TestFixture]
    public sealed class FloorHintTutorialTests
    {
        private GameObject mRoot;
        private GameObject mFloorLevelGo;
        private GameObject mRoomGo;
        private TMP_Text mFloorLevelText;
        private TMP_Text mRoomText;
        private FloorHintPresenter mPresenter;

        [SetUp]
        public void SetUp()
        {
            mRoot = new GameObject(FloorHintPresenter.RootObjectName);

            mFloorLevelGo = new GameObject(FloorHintPresenter.FloorLevelHintObjectName);
            mFloorLevelGo.transform.SetParent(mRoot.transform, false);
            mFloorLevelText = mFloorLevelGo.AddComponent<TextMeshPro>();

            mRoomGo = new GameObject(FloorHintPresenter.RoomHintObjectName);
            mRoomGo.transform.SetParent(mRoot.transform, false);
            mRoomText = mRoomGo.AddComponent<TextMeshPro>();

            mPresenter = mRoot.AddComponent<FloorHintPresenter>();
            mPresenter.EnsureBindings();

            TutorialCoach.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            TutorialCoach.ResetForTests();
            if (mRoot != null)
            {
                Object.DestroyImmediate(mRoot);
            }
        }

        [Test]
        public void Apply_InNormalMode_DisplaysEnvironmentAndRoom()
        {
            TutorialCoach.SetEntryKind(TutorialEntryKind.Natural);

            mPresenter.Apply(2, RoomKind.Attribute, 2, "normal");

            Assert.AreEqual(BoardBriefTipCopy.FormatFloorLevelHint(2, 2, "normal"), mFloorLevelText.text);
            Assert.AreEqual("战斗房间", mRoomText.text);
        }

        [Test]
        public void Apply_InTutorialMenuMode_DisplaysTutorialSpecCopy()
        {
            TutorialCoach.SetEntryKind(TutorialEntryKind.Menu);

            mPresenter.Apply(1, RoomKind.Attribute, 1, "normal");

            Assert.AreEqual("密林_翡翠迷雾", mFloorLevelText.text);
            Assert.AreEqual("教程", mRoomText.text);
        }

        [Test]
        public void BoardBriefTipCopy_TutorialConstants_MatchRequirements()
        {
            Assert.AreEqual("密林_翡翠迷雾", BoardBriefTipCopy.TutorialFloorLevelHint);
            Assert.AreEqual("教程", BoardBriefTipCopy.TutorialRoomHint);
        }
    }
}

using NineGrid.Flow;
using NineGrid.Flow.Tutorial;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.Flow
{
    [TestFixture]
    public sealed class TutorialSpotlightPresenterTests
    {
        private GameObject mTestPanel;
        private GameObject mBoxGo;
        private TutorialPromptBoxPresenter mPresenter;

        [SetUp]
        public void SetUp()
        {
            mTestPanel = new GameObject(TutorialPromptBoxPresenter.PanelObjectName);
            mBoxGo = new GameObject(TutorialPromptBoxPresenter.BoxObjectName);
            mBoxGo.transform.SetParent(mTestPanel.transform, false);

            var spriteRenderer = mBoxGo.AddComponent<SpriteRenderer>();
            spriteRenderer.drawMode = SpriteDrawMode.Sliced;
            spriteRenderer.size = new Vector2(4.078125f, 5.109375f);

            mPresenter = mBoxGo.AddComponent<TutorialPromptBoxPresenter>();
            mPresenter.EnsureBindings();
            mBoxGo.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            TutorialPromptBoxPresenter.ResetForTests();
            TutorialSpotlightPresenter.ResetForTests();
            if (mTestPanel != null)
            {
                Object.DestroyImmediate(mTestPanel);
            }
        }

        [Test]
        public void ShowPrompt_TurnsOnSpotlight_WithoutAcquiringBattleDimmer()
        {
            Assert.IsFalse(TutorialSpotlightPresenter.IsSpotlightVisible);
            Assert.IsFalse(BattleUiDimmerOverlay.IsActive);

            var bounds = new Bounds(new Vector3(1.5f, -0.5f, 0f), new Vector3(2.2f, 3.0f, 0f));
            mPresenter.Show(bounds);

            Assert.IsTrue(mPresenter.IsVisible);
            Assert.IsTrue(TutorialSpotlightPresenter.IsSpotlightVisible);
            Assert.IsFalse(BattleUiDimmerOverlay.IsActive, "教学挖洞不得 Acquire 局内半黑屏覆层");

            var spotlight = TutorialSpotlightPresenter.InstanceOrNull();
            Assert.IsNotNull(spotlight);
            Assert.IsNull(spotlight.GetComponent<Collider2D>(), "教学挖洞层不得带 collider");
            Assert.AreEqual(SpriteMaskInteraction.VisibleOutsideMask, spotlight.GetComponent<SpriteRenderer>().maskInteraction);
        }

        [Test]
        public void HidePrompt_HidesSpotlight()
        {
            mPresenter.Show(new Bounds(Vector3.zero, new Vector3(2f, 3f, 0f)));
            Assert.IsTrue(TutorialSpotlightPresenter.IsSpotlightVisible);

            mPresenter.Hide();

            Assert.IsFalse(mPresenter.IsVisible);
            Assert.IsFalse(TutorialSpotlightPresenter.IsSpotlightVisible);
            Assert.IsFalse(BattleUiDimmerOverlay.IsActive);
        }
    }
}

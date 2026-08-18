using System.Collections;
using Cysharp.Threading.Tasks;
using NineGrid.Flow.Tutorial;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NineGrid.Presentation.Tests.Flow
{
    [TestFixture]
    public sealed class TutorialPromptBoxPresenterTests
    {
        private GameObject mTestPanel;
        private GameObject mBoxGo;
        private SpriteRenderer mSpriteRenderer;
        private TutorialPromptBoxPresenter mPresenter;

        private GameObject mTargetCardGo;
        private SpriteRenderer mTargetRenderer;

        [SetUp]
        public void SetUp()
        {
            mTestPanel = new GameObject(TutorialPromptBoxPresenter.PanelObjectName);
            mBoxGo = new GameObject(TutorialPromptBoxPresenter.BoxObjectName);
            mBoxGo.transform.SetParent(mTestPanel.transform, false);

            mSpriteRenderer = mBoxGo.AddComponent<SpriteRenderer>();
            mSpriteRenderer.drawMode = SpriteDrawMode.Sliced;
            mSpriteRenderer.size = new Vector2(4.078125f, 5.109375f);
            mBoxGo.transform.localScale = Vector3.one;

            mPresenter = mBoxGo.AddComponent<TutorialPromptBoxPresenter>();
            mPresenter.EnsureBindings();
            mBoxGo.SetActive(false);

            // 模拟场地卡目标物体
            mTargetCardGo = new GameObject("TargetCard");
            mTargetCardGo.transform.position = new Vector3(2.5f, 1.2f, 0f);
            mTargetRenderer = mTargetCardGo.AddComponent<SpriteRenderer>();
            mTargetRenderer.size = new Vector2(2.0f, 2.8f);
        }

        [TearDown]
        public void TearDown()
        {
            TutorialPromptBoxPresenter.ResetForTests();
            if (mTestPanel != null)
            {
                Object.DestroyImmediate(mTestPanel);
            }

            if (mTargetCardGo != null)
            {
                Object.DestroyImmediate(mTargetCardGo);
            }
        }

        [Test]
        public void Show_PositionsAtTargetBoundsCenter_AndMatchesSize()
        {
            Assert.IsFalse(mPresenter.IsVisible);

            var customBounds = new Bounds(new Vector3(3.0f, -1.5f, 0f), new Vector3(2.2f, 3.1f, 0f));
            mPresenter.Show(customBounds);

            Assert.IsTrue(mPresenter.IsVisible);
            Assert.IsTrue(mBoxGo.activeSelf);
            Assert.AreEqual(3.0f, mBoxGo.transform.position.x, 0.001f);
            Assert.AreEqual(-1.5f, mBoxGo.transform.position.y, 0.001f);
            Assert.AreEqual(2.2f, mPresenter.BaseSize.x, 0.001f);
            Assert.AreEqual(3.1f, mPresenter.BaseSize.y, 0.001f);
        }

        [Test]
        public void Show_WithGameObjectTarget_CalculatesWorldBounds()
        {
            mPresenter.Show(mTargetCardGo);

            Assert.IsTrue(mPresenter.IsVisible);
            Assert.AreEqual(mTargetCardGo.transform.position.x, mBoxGo.transform.position.x, 0.05f);
            Assert.AreEqual(mTargetCardGo.transform.position.y, mBoxGo.transform.position.y, 0.05f);
            Assert.AreEqual(Vector3.one, mBoxGo.transform.localScale, "物体缩放不应改变");
        }

        [Test]
        public void Hide_DeactivatesPromptBox()
        {
            mPresenter.Show(mTargetCardGo);
            Assert.IsTrue(mPresenter.IsVisible);

            mPresenter.Hide();

            Assert.IsFalse(mPresenter.IsVisible);
            Assert.IsFalse(mBoxGo.activeSelf);
        }

        [Test]
        public void StaticFacade_ShowTarget_And_HidePrompt()
        {
            var bounds = new Bounds(new Vector3(1f, 2f, 0f), new Vector3(2f, 3f, 0f));
            TutorialPromptBoxPresenter.ShowTarget(bounds);

            Assert.IsTrue(TutorialPromptBoxPresenter.IsPromptVisible);

            TutorialPromptBoxPresenter.HidePrompt();
            Assert.IsFalse(TutorialPromptBoxPresenter.IsPromptVisible);
        }

        [UnityTest]
        public IEnumerator BreathingAnimation_ModifiesSlicedSize_LeavesLocalScaleUntouched() => UniTask.ToCoroutine(async () =>
        {
            var bounds = new Bounds(Vector3.zero, new Vector3(3.0f, 4.0f, 0f));
            mPresenter.Show(bounds);

            var initialScale = mBoxGo.transform.localScale;
            Assert.AreEqual(Vector3.one, initialScale);
            Assert.AreEqual(SpriteDrawMode.Sliced, mSpriteRenderer.drawMode);

            // 等待若干帧让 Update 执行呼吸计算
            await UniTask.Delay(System.TimeSpan.FromSeconds(0.2f), ignoreTimeScale: true);

            Assert.AreEqual(initialScale, mBoxGo.transform.localScale, "呼吸期间物体 scale 恒定为 (1,1,1)，四角图案不被放大");
            Assert.AreEqual(SpriteDrawMode.Sliced, mSpriteRenderer.drawMode, "必须为 Sliced 模式");
            // Sliced size 随呼吸在中腹波幅内变动
            Assert.Greater(mSpriteRenderer.size.x, 2.5f);
            Assert.Less(mSpriteRenderer.size.x, 3.5f);
        });
    }
}

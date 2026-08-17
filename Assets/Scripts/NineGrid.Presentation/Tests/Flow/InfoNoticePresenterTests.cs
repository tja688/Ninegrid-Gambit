using System.Collections;
using Cysharp.Threading.Tasks;
using NineGrid.Flow.InfoNotice;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace NineGrid.Presentation.Tests.Flow
{
    [TestFixture]
    public sealed class InfoNoticePresenterTests
    {
        private GameObject mTestRoot;
        private GameObject mWindowGo;
        private SpriteRenderer mSpriteRenderer;
        private TextMeshPro mTmpText;
        private InfoNoticePresenter mPresenter;

        [SetUp]
        public void SetUp()
        {
            mTestRoot = new GameObject(InfoNoticePresenter.PanelObjectName);
            mWindowGo = new GameObject(InfoNoticePresenter.WindowObjectName);
            mWindowGo.transform.SetParent(mTestRoot.transform, false);

            mSpriteRenderer = mWindowGo.AddComponent<SpriteRenderer>();
            mSpriteRenderer.color = Color.white;

            var textGo = new GameObject(InfoNoticePresenter.TextObjectName);
            textGo.transform.SetParent(mWindowGo.transform, false);
            mTmpText = textGo.AddComponent<TextMeshPro>();
            mTmpText.text = "默认文案";
            mTmpText.color = new Color(0.8f, 0.8f, 0.8f, 1f);

            mPresenter = mTestRoot.AddComponent<InfoNoticePresenter>();
            mPresenter.EnsureBindings();

            mWindowGo.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            InfoNoticePresenter.ResetForTests();
            if (mTestRoot != null)
            {
                Object.DestroyImmediate(mTestRoot);
            }
        }

        [Test]
        public void Show_ActivatesWindowAndUpdatesText()
        {
            Assert.IsFalse(mPresenter.IsVisible);

            var gen = mPresenter.ShowNotice("已达到手牌上限", 1.0f);

            Assert.Greater(gen, 0);
            Assert.IsTrue(mPresenter.IsVisible);
            Assert.IsTrue(mWindowGo.activeSelf);
            Assert.AreEqual("已达到手牌上限", mTmpText.text);
            Assert.AreEqual(1f, mSpriteRenderer.color.a, 0.01f);
        }

        [Test]
        public void Show_ConsecutiveCalls_SingleInstanceAndRefreshState()
        {
            var gen1 = mPresenter.ShowNotice("提示1", 1.0f);
            Assert.AreEqual("提示1", mTmpText.text);
            Assert.AreEqual(gen1, 1);

            // 再次调用新拒绝，代数自增，文案立即刷新为新拒绝
            var gen2 = mPresenter.ShowNotice("金币不足", 1.0f);
            Assert.AreEqual("金币不足", mTmpText.text);
            Assert.AreEqual(gen2, 2);
            Assert.IsTrue(mPresenter.IsVisible);
            Assert.AreEqual(1f, mSpriteRenderer.color.a, 0.01f);
        }

        [Test]
        public void Hide_DeactivatesWindowImmediately()
        {
            mPresenter.ShowNotice("短时提示", 1.0f);
            Assert.IsTrue(mPresenter.IsVisible);

            mPresenter.HideNotice();

            Assert.IsFalse(mPresenter.IsVisible);
            Assert.IsFalse(mWindowGo.activeSelf);
            Assert.AreEqual(0f, mSpriteRenderer.color.a, 0.01f);
        }

        [UnityTest]
        public IEnumerator NoticeLifecycle_AutomaticallyFadesOutAndDisappears() => UniTask.ToCoroutine(async () =>
        {
            // 使用超短持续时间加速测试
            var gen = mPresenter.ShowNotice("自动消失测试", 0.05f);
            Assert.IsTrue(mPresenter.IsVisible);

            // 等待停留(0.05s) + 淡出(约0.35s) 完成
            await UniTask.Delay(System.TimeSpan.FromSeconds(0.55f), ignoreTimeScale: true);

            Assert.IsFalse(mPresenter.IsVisible, "提示在指定时间后应自动淡出并隐藏");
            Assert.IsFalse(mWindowGo.activeSelf);
        });
    }
}

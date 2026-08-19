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
            mSpriteRenderer.drawMode = SpriteDrawMode.Sliced;
            mSpriteRenderer.size = new Vector2(2f, 0.8f);

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
        public void Show_AdaptiveSlicedWidth_ScalesWidthByCharacterRatio_PreservesHeightAndScale()
        {
            var initialScale = mWindowGo.transform.localScale;
            var initialHeight = mSpriteRenderer.size.y;

            // 6 个字：宽 = 6 * (2/6) = 2.0
            mPresenter.ShowNotice("六个字符测试", 1.0f);
            Assert.AreEqual(2.0f, mSpriteRenderer.size.x, 0.001f);
            Assert.AreEqual(initialHeight, mSpriteRenderer.size.y, 0.001f);
            Assert.AreEqual(initialScale, mWindowGo.transform.localScale);

            // 12 个字：宽 = 12 * (2/6) = 4.0
            var text12 = new string('字', 12);
            mPresenter.ShowNotice(text12, 1.0f);
            Assert.AreEqual(4.0f, mSpriteRenderer.size.x, 0.001f);
            Assert.AreEqual(initialHeight, mSpriteRenderer.size.y, 0.001f);
            Assert.AreEqual(initialScale, mWindowGo.transform.localScale);

            // 30 个字：宽 = 30 * (2/6) = 10.0
            var text30 = new string('字', 30);
            mPresenter.ShowNotice(text30, 1.0f);
            Assert.AreEqual(10.0f, mSpriteRenderer.size.x, 0.001f);
            Assert.AreEqual(initialHeight, mSpriteRenderer.size.y, 0.001f);
            Assert.AreEqual(initialScale, mWindowGo.transform.localScale);
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
        public void ShowClickToAdvance_HoldsUntilDismiss()
        {
            var gen = InfoNoticePresenter.ShowClickToAdvance("欢迎来到九宫地下城");
            Assert.Greater(gen, 0);
            Assert.IsTrue(mPresenter.IsVisible);
            Assert.IsTrue(mPresenter.IsHoldingTutorialSentence);
            Assert.IsTrue(InfoNoticePresenter.IsHoldingSentence);
            Assert.AreEqual(InfoNoticeHoldMode.ClickToAdvance, mPresenter.CurrentHoldMode);
            Assert.AreEqual("欢迎来到九宫地下城", mTmpText.text);

            // 解除保持
            InfoNoticePresenter.DismissHold();
            Assert.IsFalse(mPresenter.IsVisible);
            Assert.IsFalse(mPresenter.IsHoldingTutorialSentence);
            Assert.IsFalse(InfoNoticePresenter.IsHoldingSentence);
        }

        [Test]
        public void HoldTutorialSentence_BlocksShortRejectionNotices()
        {
            InfoNoticePresenter.ShowClickToAdvance("保持中的教学句");
            Assert.AreEqual("保持中的教学句", mTmpText.text);
            Assert.IsTrue(InfoNoticePresenter.IsHoldingSentence);

            // 教学句正在保持时，短时拒绝提示被拦截，文案不被覆盖，保持不被打断
            var rejectGen = mPresenter.ShowNotice("手牌已满");
            Assert.AreEqual(0, rejectGen);
            Assert.AreEqual("保持中的教学句", mTmpText.text);
            Assert.IsTrue(InfoNoticePresenter.IsHoldingSentence);

            // 解除保持后，短时拒绝提示恢复正常响应
            InfoNoticePresenter.DismissHold();
            Assert.IsFalse(InfoNoticePresenter.IsHoldingSentence);

            var normalGen = mPresenter.ShowNotice("金币不足", 1.0f);
            Assert.Greater(normalGen, 0);
            Assert.AreEqual("金币不足", mTmpText.text);
            Assert.IsTrue(mPresenter.IsVisible);
        }

        [Test]
        public void TypewriterDuration_IsTwoTenthsOfASecond()
        {
            Assert.AreEqual(0.2f, InfoNoticePresenter.DefaultTypewriterDuration, 0.0001f);
            Assert.AreEqual(InfoNoticePresenter.DefaultTypewriterDuration, InfoNoticePresenter.DefaultWidthTweenDuration, 0.0001f);
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

        [Test]
        public void ShowHover_DisplaysTextAndClearHoverHides()
        {
            InfoNoticePresenter.ShowHover("这是血条描述");
            Assert.IsTrue(mPresenter.IsVisible);
            Assert.IsTrue(mPresenter.IsHoverActive);
            Assert.AreEqual("这是血条描述", mTmpText.text);

            InfoNoticePresenter.ClearHover();
            Assert.IsFalse(mPresenter.IsHoverActive);
        }

        [Test]
        public void ShowHover_DoesNotOverrideHoldingTutorialSentence()
        {
            InfoNoticePresenter.ShowClickToAdvance("教学句");
            Assert.IsTrue(InfoNoticePresenter.IsHoldingSentence);

            InfoNoticePresenter.ShowHover("尝试悬停");
            Assert.AreEqual("教学句", mTmpText.text);
            Assert.IsFalse(mPresenter.IsHoverActive);

            InfoNoticePresenter.DismissHold();
        }

        [UnityTest]
        public IEnumerator ShowHoldTwoSeconds_AutomaticallyFinishesAndClearsHold() => UniTask.ToCoroutine(async () =>
        {
            var gen = InfoNoticePresenter.ShowHoldTwoSeconds("两秒教学句");
            Assert.IsTrue(mPresenter.IsVisible);
            Assert.IsTrue(InfoNoticePresenter.IsHoldingSentence);
            Assert.AreEqual(InfoNoticeHoldMode.HoldTwoSeconds, mPresenter.CurrentHoldMode);

            // 约 2.0 秒停留 + 0.35 秒淡出完成
            await UniTask.Delay(System.TimeSpan.FromSeconds(2.45f), ignoreTimeScale: true);

            Assert.IsFalse(mPresenter.IsVisible, "停两秒在约两秒后应自动淡出隐藏");
            Assert.IsFalse(InfoNoticePresenter.IsHoldingSentence, "停两秒结束后应解除教学句保持");
        });
    }
}

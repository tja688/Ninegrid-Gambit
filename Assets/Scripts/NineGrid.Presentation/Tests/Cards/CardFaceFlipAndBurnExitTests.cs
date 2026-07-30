using System.Threading;
using NUnit.Framework;
using NineGrid.Cards;
using NineGrid.Cards.Convergence;
using NineGrid.Cards.Presentation;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    public sealed class CardFaceFlipPresenterTests
    {
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("FlipPresenterTestCard");
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
        }

        [Test]
        public void SampleFlipPose_AtMidpoint_MatchesFlipAnimPeak()
        {
            CardFaceFlipPresenter.SampleFlipPose(
                CardFaceFlipPresenter.FaceSwapTimeSeconds,
                out var y,
                out var scale);

            Assert.AreEqual(-90f, y, 0.01f);
            Assert.AreEqual(1.2f, scale, 0.01f);
        }

        [Test]
        public void PlaybackSpeed_HalvesDuration()
        {
            Assert.AreEqual(2f, CardFaceFlipPresenter.PlaybackSpeed, 0.01f);
            Assert.AreEqual(
                CardFaceFlipPresenter.SourceDurationSeconds / 2f,
                CardFaceFlipPresenter.DurationSeconds,
                0.001f);
        }

        [Test]
        public void SampleFlipPose_AtStartAndEnd_IsIdentity()
        {
            CardFaceFlipPresenter.SampleFlipPose(0f, out var y0, out var s0);
            CardFaceFlipPresenter.SampleFlipPose(
                CardFaceFlipPresenter.DurationSeconds,
                out var y1,
                out var s1);

            Assert.AreEqual(0f, y0, 0.01f);
            Assert.AreEqual(1f, s0, 0.01f);
            Assert.AreEqual(0f, y1, 0.01f);
            Assert.AreEqual(1f, s1, 0.01f);
        }

        [Test]
        public void SnapVisualFace_TogglesFrontAndBack()
        {
            var tower = _root.AddComponent<CardTransformTower>();
            tower.EnsureTower();

            var face = new GameObject("Face");
            face.transform.SetParent(tower.FacePivot, false);
            var front = new GameObject("Front");
            front.transform.SetParent(face.transform, false);
            var back = new GameObject("back");
            back.transform.SetParent(face.transform, false);
            back.SetActive(false);

            var presenter = _root.AddComponent<CardFaceFlipPresenter>();
            Assert.IsTrue(presenter.VisualFaceUp);
            Assert.IsTrue(front.activeSelf);
            Assert.IsFalse(back.activeSelf);

            presenter.SnapVisualFace(faceUp: false);

            Assert.IsFalse(presenter.VisualFaceUp);
            Assert.IsFalse(front.activeSelf);
            Assert.IsTrue(back.activeSelf);
            Assert.AreEqual(1f, tower.FacePivot.localScale.x, 0.01f);
        }
    }

    public sealed class CardSpriteSheetBurnExitEffectTests
    {
        [Test]
        public void PlayAsync_HidesRootAndSpawnsDetachedFx()
        {
            var so = ScriptableObject.CreateInstance<CardSpriteSheetBurnExitEffectSO>();
            var tex = new Texture2D(4, 4);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            var framesField = typeof(CardSpriteSheetBurnExitEffectSO)
                .GetField(
                    "frames",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(framesField);
            framesField.SetValue(so, new[] { sprite, sprite });

            var root = new GameObject("BurnExitRoot");
            root.transform.localScale = Vector3.one;
            try
            {
                var ctx = new CardEffectPlayContext(
                    card: null,
                    root: root.transform,
                    view: null,
                    invoke: CardEffectInvokeContext.ForDeath(0, CardBoardDirection.None),
                    selfWorldPosition: Vector3.one,
                    otherWorldPosition: null,
                    cancellationToken: CancellationToken.None);

                var awaiter = so.PlayAsync(ctx).GetAwaiter();
                Assert.IsTrue(awaiter.IsCompleted);
                Assert.AreEqual(Vector3.zero, root.transform.localScale);

                var fx = GameObject.Find("CardBurnExitFx");
                Assert.IsNotNull(fx);
                Assert.IsTrue(fx.transform.parent == null);
                var sr = fx.GetComponent<SpriteRenderer>();
                Assert.IsNotNull(sr);
                Assert.AreEqual(
                    CardSpriteSheetBurnExitEffectSO.DefaultSortingLayerName,
                    sr.sortingLayerName);
                Object.DestroyImmediate(fx);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(so);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(tex);
            }
        }

        [Test]
        public void PlayAsync_SpawnsFxAtSelfWorldPosition_NotRoot()
        {
            var so = ScriptableObject.CreateInstance<CardSpriteSheetBurnExitEffectSO>();
            var tex = new Texture2D(4, 4);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            var framesField = typeof(CardSpriteSheetBurnExitEffectSO)
                .GetField(
                    "frames",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(framesField);
            framesField.SetValue(so, new[] { sprite });

            var root = new GameObject("BurnExitRoot");
            root.transform.position = new Vector3(1f, 2f, 0f);
            var knockbackVisual = new Vector3(4f, 2f, 0f);
            try
            {
                var ctx = new CardEffectPlayContext(
                    card: null,
                    root: root.transform,
                    view: null,
                    invoke: CardEffectInvokeContext.ForDeath(0, CardBoardDirection.None),
                    selfWorldPosition: knockbackVisual,
                    otherWorldPosition: null,
                    cancellationToken: CancellationToken.None);

                so.PlayAsync(ctx).GetAwaiter().GetResult();

                var fx = GameObject.Find("CardBurnExitFx");
                Assert.IsNotNull(fx);
                Assert.AreEqual(knockbackVisual.x, fx.transform.position.x, 0.001f);
                Assert.AreEqual(knockbackVisual.y, fx.transform.position.y, 0.001f);
                Assert.Greater(
                    Mathf.Abs(root.transform.position.x - fx.transform.position.x),
                    0.1f);
                Object.DestroyImmediate(fx);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(so);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(tex);
            }
        }
    }

    public sealed class CardEffectSelfWorldPositionTests
    {
        [Test]
        public void ResolveSelfWorldPosition_LiveCard_UsesVisualNotRoot()
        {
            var root = new Vector3(1f, 0f, 0f);
            var visual = new Vector3(4f, 0f, 0f);
            var slot = new Vector3(1f, 0f, 0f);

            var resolved = CardEffectManager.ResolveSelfWorldPosition(
                visualWorld: visual,
                rootWorld: root,
                hasCard: true,
                stagedOffAnchor: false,
                selfSlot: 0,
                slotAnchorWorld: slot);

            Assert.AreEqual(visual.x, resolved.x, 0.001f);
            Assert.AreEqual(visual.y, resolved.y, 0.001f);
        }

        [Test]
        public void ResolveSelfWorldPosition_LiveCardWithSlot_StillUsesVisual()
        {
            var root = new Vector3(1f, 0f, 0f);
            var visual = new Vector3(4f, 1f, 0f);
            var slot = new Vector3(1f, 0f, 0f);

            var resolved = CardEffectManager.ResolveSelfWorldPosition(
                visualWorld: visual,
                rootWorld: root,
                hasCard: true,
                stagedOffAnchor: false,
                selfSlot: 5,
                slotAnchorWorld: slot);

            Assert.AreEqual(visual.x, resolved.x, 0.001f);
            Assert.AreEqual(visual.y, resolved.y, 0.001f);
        }

        [Test]
        public void ResolveSelfWorldPosition_StagedCorpseWithSlot_UsesSlotAnchor()
        {
            var root = new Vector3(1f, -80f, 0f);
            var visual = new Vector3(1f, -80f, 0f);
            var slot = new Vector3(1f, 0f, 0f);

            var resolved = CardEffectManager.ResolveSelfWorldPosition(
                visualWorld: visual,
                rootWorld: root,
                hasCard: true,
                stagedOffAnchor: true,
                selfSlot: 5,
                slotAnchorWorld: slot);

            Assert.AreEqual(slot.x, resolved.x, 0.001f);
            Assert.AreEqual(slot.y, resolved.y, 0.001f);
        }

        [Test]
        public void ResolveSelfWorldPosition_NoCard_UsesRoot()
        {
            var root = new Vector3(2f, 3f, 0f);
            var visual = new Vector3(9f, 9f, 0f);

            var resolved = CardEffectManager.ResolveSelfWorldPosition(
                visualWorld: visual,
                rootWorld: root,
                hasCard: false,
                stagedOffAnchor: false,
                selfSlot: 5,
                slotAnchorWorld: new Vector3(1f, 0f, 0f));

            Assert.AreEqual(root.x, resolved.x, 0.001f);
            Assert.AreEqual(root.y, resolved.y, 0.001f);
        }

        [Test]
        public void GetVisualWorldPosition_IncludesEffectFrameOffset()
        {
            var go = new GameObject("VisualKnockbackCard");
            try
            {
                go.transform.position = new Vector3(1f, 2f, 0f);
                var tower = go.AddComponent<CardTransformTower>();
                tower.EnsureTower();
                tower.EffectFrame.localPosition = new Vector3(3f, 0f, 0f);

                // ManagedCard 不便构造：直接断言塔上视觉位含 L3，且不等于 CardRoot。
                Assert.AreEqual(
                    tower.CardVisual.position.x,
                    go.transform.position.x + 3f,
                    0.001f);
                Assert.Greater(
                    Mathf.Abs(tower.CardRoot.position.x - tower.CardVisual.position.x),
                    0.1f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}

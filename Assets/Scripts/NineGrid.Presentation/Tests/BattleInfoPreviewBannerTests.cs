using NineGrid.Core.Content;
using NineGrid.Flow.BattleInfoPreview;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    public sealed class BattleInfoPreviewBannerTests
    {
        [Test]
        public void IsHardDifficulty_IdentifiesHardTier_Correctly()
        {
            Assert.IsTrue(BattleInfoPreviewPresenter.IsHardDifficulty(RunDifficultyIds.Hard));
            Assert.IsTrue(BattleInfoPreviewPresenter.IsHardDifficulty("hard"));
            Assert.IsTrue(BattleInfoPreviewPresenter.IsHardDifficulty("HARD"));
            Assert.IsTrue(BattleInfoPreviewPresenter.IsHardDifficulty("Hard"));

            Assert.IsFalse(BattleInfoPreviewPresenter.IsHardDifficulty(RunDifficultyIds.Normal));
            Assert.IsFalse(BattleInfoPreviewPresenter.IsHardDifficulty(RunDifficultyIds.Advanced));
            Assert.IsFalse(BattleInfoPreviewPresenter.IsHardDifficulty("normal"));
            Assert.IsFalse(BattleInfoPreviewPresenter.IsHardDifficulty("advanced"));
            Assert.IsFalse(BattleInfoPreviewPresenter.IsHardDifficulty(null));
            Assert.IsFalse(BattleInfoPreviewPresenter.IsHardDifficulty(string.Empty));
        }

        [Test]
        public void ResolveBannerSprite_NormalMode_ReturnsTierSpritesByFloor()
        {
            var go = new GameObject("TestBattleInfo");
            try
            {
                var presenter = go.AddComponent<BattleInfoPreviewPresenter>();

                var s1 = presenter.ResolveBannerSprite(1, RunDifficultyIds.Normal);
                var s2 = presenter.ResolveBannerSprite(2, RunDifficultyIds.Normal);
                var s3 = presenter.ResolveBannerSprite(3, RunDifficultyIds.Normal);

                Assert.IsNotNull(s1, "Floor 1 sprite should resolve");
                Assert.IsNotNull(s2, "Floor 2 sprite should resolve");
                Assert.IsNotNull(s3, "Floor 3 sprite should resolve");

                Assert.IsTrue(s1.name.Contains("Green", System.StringComparison.OrdinalIgnoreCase), $"Expected Green banner on Floor 1, got {s1.name}");
                Assert.IsTrue(s2.name.Contains("Blue", System.StringComparison.OrdinalIgnoreCase), $"Expected Blue banner on Floor 2, got {s2.name}");
                Assert.IsTrue(s3.name.Contains("White", System.StringComparison.OrdinalIgnoreCase), $"Expected White banner on Floor 3, got {s3.name}");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ResolveBannerSprite_HardMode_ReturnsRedSpriteOnAllFloors()
        {
            var go = new GameObject("TestBattleInfo");
            try
            {
                var presenter = go.AddComponent<BattleInfoPreviewPresenter>();

                var s1 = presenter.ResolveBannerSprite(1, RunDifficultyIds.Hard);
                var s2 = presenter.ResolveBannerSprite(2, RunDifficultyIds.Hard);
                var s3 = presenter.ResolveBannerSprite(3, RunDifficultyIds.Hard);

                Assert.IsNotNull(s1, "Hard Floor 1 sprite should resolve");
                Assert.IsNotNull(s2, "Hard Floor 2 sprite should resolve");
                Assert.IsNotNull(s3, "Hard Floor 3 sprite should resolve");

                Assert.IsTrue(s1.name.Contains("Red", System.StringComparison.OrdinalIgnoreCase), $"Expected Red banner on Hard Floor 1, got {s1.name}");
                Assert.IsTrue(s2.name.Contains("Red", System.StringComparison.OrdinalIgnoreCase), $"Expected Red banner on Hard Floor 2, got {s2.name}");
                Assert.IsTrue(s3.name.Contains("Red", System.StringComparison.OrdinalIgnoreCase), $"Expected Red banner on Hard Floor 3, got {s3.name}");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ApplyBanner_UpdatesBannerRendererSprite()
        {
            var rootGo = new GameObject("战斗信息展示BG");
            var bannerGo = new GameObject("横幅");
            bannerGo.transform.SetParent(rootGo.transform);
            var sr = bannerGo.AddComponent<SpriteRenderer>();

            try
            {
                var presenter = rootGo.AddComponent<BattleInfoPreviewPresenter>();

                presenter.ApplyBanner(1, RunDifficultyIds.Normal);
                Assert.IsNotNull(sr.sprite);
                Assert.IsTrue(sr.sprite.name.Contains("Green", System.StringComparison.OrdinalIgnoreCase));

                presenter.ApplyBanner(2, RunDifficultyIds.Normal);
                Assert.IsTrue(sr.sprite.name.Contains("Blue", System.StringComparison.OrdinalIgnoreCase));

                presenter.ApplyBanner(3, RunDifficultyIds.Normal);
                Assert.IsTrue(sr.sprite.name.Contains("White", System.StringComparison.OrdinalIgnoreCase));

                presenter.ApplyBanner(1, RunDifficultyIds.Hard);
                Assert.IsTrue(sr.sprite.name.Contains("Red", System.StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                Object.DestroyImmediate(bannerGo);
                Object.DestroyImmediate(rootGo);
            }
        }
    }
}

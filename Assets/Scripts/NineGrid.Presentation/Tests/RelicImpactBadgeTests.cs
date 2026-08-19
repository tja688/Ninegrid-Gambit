using System;
using NineGrid.Cards.Vfx;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    [TestFixture]
    public class RelicImpactBadgeTests
    {
        private GameObject _holderGo;
        private RelicImpactBadgeManager _manager;

        [SetUp]
        public void SetUp()
        {
            _holderGo = new GameObject("[Test_RelicImpactBadgeManager]");
            _manager = _holderGo.AddComponent<RelicImpactBadgeManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_manager != null)
            {
                _manager.ClearAll();
            }

            if (_holderGo != null)
            {
                UnityEngine.Object.DestroyImmediate(_holderGo);
            }
        }

        [Test]
        public void ExtractCanonicalRelicDefId_StandardDefId_ReturnsDirectly()
        {
            var result = RelicImpactBadgeManager.ExtractCanonicalRelicDefId("relic.rotten_cleave_axe");
            Assert.AreEqual("relic.rotten_cleave_axe", result);
        }

        [Test]
        public void ExtractCanonicalRelicDefId_SubAssemblySuffix_ExtractsPrefix()
        {
            var result = RelicImpactBadgeManager.ExtractCanonicalRelicDefId("relic.rotten_cleave_axe.cleave");
            Assert.AreEqual("relic.rotten_cleave_axe", result);

            var result2 = RelicImpactBadgeManager.ExtractCanonicalRelicDefId("relic.thorn_mail.reflect");
            Assert.AreEqual("relic.thorn_mail", result2);
        }

        [Test]
        public void ExtractCanonicalRelicDefId_NonRelic_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, RelicImpactBadgeManager.ExtractCanonicalRelicDefId("monster.slime"));
            Assert.AreEqual(string.Empty, RelicImpactBadgeManager.ExtractCanonicalRelicDefId("skill.slash"));
            Assert.AreEqual(string.Empty, RelicImpactBadgeManager.ExtractCanonicalRelicDefId(""));
            Assert.AreEqual(string.Empty, RelicImpactBadgeManager.ExtractCanonicalRelicDefId(null));
        }

        [Test]
        public void TryExtractRelicDefId_SourceDefIdOrCause_ResolvesCorrectly()
        {
            var ok1 = RelicImpactBadgeHook.TryExtractRelicDefId("relic.blood_shockwave", null, out var id1);
            Assert.IsTrue(ok1);
            Assert.AreEqual("relic.blood_shockwave", id1);

            var ok2 = RelicImpactBadgeHook.TryExtractRelicDefId(null, "relic.muscle_counter.battle", out var id2);
            Assert.IsTrue(ok2);
            Assert.AreEqual("relic.muscle_counter", id2);

            var ok3 = RelicImpactBadgeHook.TryExtractRelicDefId("hero.attack", "card.kill", out var id3);
            Assert.IsFalse(ok3);
            Assert.IsEmpty(id3);
        }

        [Test]
        public void BadgeComponent_PopHoldThenRiseFade_FinishesAfterTotalDuration()
        {
            var badgeGo = new GameObject("TestBadge");
            var badge = badgeGo.AddComponent<RelicImpactBadge>();
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);

            badge.Initialize("relic.rotten_cleave_axe", sprite, Vector3.zero, 1, Vector3.zero, "Main", 12);
            Assert.IsFalse(badge.IsFinished);

            badge.Tick(RelicImpactBadge.PopInDuration + RelicImpactBadge.HoldDuration - 0.01f);
            Assert.IsFalse(badge.IsFinished);

            badge.Tick(RelicImpactBadge.FadeOutDuration + 0.02f);
            Assert.IsTrue(badge.IsFinished);

            UnityEngine.Object.DestroyImmediate(badgeGo);
            UnityEngine.Object.DestroyImmediate(sprite);
        }

        [Test]
        public void BadgeComponent_PopIn_OvershootsRestScale()
        {
            var badgeGo = new GameObject("TestBadge");
            var badge = badgeGo.AddComponent<RelicImpactBadge>();
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);

            badge.Initialize("relic.rotten_cleave_axe", sprite, Vector3.zero, 1, Vector3.zero, "Main", 12);

            // EaseOutBack 过冲峰值约在弹出进度 0.58 处，略高于 1.0
            badge.Tick(RelicImpactBadge.PopInDuration * 0.58f);
            Assert.Greater(badge.transform.localScale.x, RelicImpactBadge.RestScale);

            badge.Tick(RelicImpactBadge.PopInDuration * 0.42f);
            Assert.AreEqual(RelicImpactBadge.RestScale, badge.transform.localScale.x, 0.02f);

            UnityEngine.Object.DestroyImmediate(badgeGo);
            UnityEngine.Object.DestroyImmediate(sprite);
        }

        [Test]
        public void BadgeComponent_FadeOut_RisesUpward()
        {
            var badgeGo = new GameObject("TestBadge");
            var badge = badgeGo.AddComponent<RelicImpactBadge>();
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);

            badge.Initialize("relic.rotten_cleave_axe", sprite, Vector3.zero, 1, Vector3.zero, "Main", 12);
            badge.Tick(RelicImpactBadge.PopInDuration);
            var yAfterPop = badge.transform.position.y;
            Assert.AreEqual(0f, yAfterPop, 0.001f);

            badge.Tick(RelicImpactBadge.HoldDuration);
            Assert.AreEqual(yAfterPop, badge.transform.position.y, 0.001f);

            badge.Tick(RelicImpactBadge.FadeOutDuration * 0.5f);
            Assert.Greater(badge.transform.position.y, yAfterPop);

            UnityEngine.Object.DestroyImmediate(badgeGo);
            UnityEngine.Object.DestroyImmediate(sprite);
        }

        [Test]
        public void BadgeComponent_RefreshLifetime_RestartsPlayback()
        {
            var badgeGo = new GameObject("TestBadge");
            var badge = badgeGo.AddComponent<RelicImpactBadge>();
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);

            badge.Initialize("relic.rotten_cleave_axe", sprite, Vector3.zero, 1, Vector3.zero, "Main", 12);
            badge.Tick(RelicImpactBadge.TotalDuration - 0.04f);
            Assert.IsFalse(badge.IsFinished);

            badge.RefreshLifetime();

            badge.Tick(RelicImpactBadge.TotalDuration - 0.04f);
            Assert.IsFalse(badge.IsFinished);

            badge.Tick(0.08f);
            Assert.IsTrue(badge.IsFinished);

            UnityEngine.Object.DestroyImmediate(badgeGo);
            UnityEngine.Object.DestroyImmediate(sprite);
        }
    }
}

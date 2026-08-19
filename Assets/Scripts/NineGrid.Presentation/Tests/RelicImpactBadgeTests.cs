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
        public void BadgeComponent_FadeInHoldFadeOut_FinishesAfterTotalDuration()
        {
            var badgeGo = new GameObject("TestBadge");
            var badge = badgeGo.AddComponent<RelicImpactBadge>();
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);

            badge.Initialize("relic.rotten_cleave_axe", sprite, Vector3.zero, 1, Vector3.zero, "Main", 12);
            Assert.IsFalse(badge.IsFinished);

            // Tick past fade in (0.12s)
            badge.Tick(0.15f);
            Assert.IsFalse(badge.IsFinished);

            // Tick past hold (0.80s)
            badge.Tick(0.85f);
            Assert.IsFalse(badge.IsFinished);

            // Tick past fade out (0.25s) -> total ~1.17s
            badge.Tick(0.30f);
            Assert.IsTrue(badge.IsFinished);

            UnityEngine.Object.DestroyImmediate(badgeGo);
            UnityEngine.Object.DestroyImmediate(sprite);
        }

        [Test]
        public void BadgeComponent_RefreshLifetime_ResetsHoldTimer()
        {
            var badgeGo = new GameObject("TestBadge");
            var badge = badgeGo.AddComponent<RelicImpactBadge>();
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);

            badge.Initialize("relic.rotten_cleave_axe", sprite, Vector3.zero, 1, Vector3.zero, "Main", 12);
            badge.Tick(0.80f); // well into hold

            badge.RefreshLifetime();

            // Should remain alive for another 0.80s hold + 0.25s fade out
            badge.Tick(0.70f);
            Assert.IsFalse(badge.IsFinished);

            badge.Tick(0.40f);
            Assert.IsTrue(badge.IsFinished);

            UnityEngine.Object.DestroyImmediate(badgeGo);
            UnityEngine.Object.DestroyImmediate(sprite);
        }
    }
}

#if UNITY_EDITOR

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.DevTest.Tests
{
    public sealed class TestKeyManagerTests
    {
        private TestKeyManager _manager;

        [SetUp]
        public void SetUp()
        {
            TestKeyManager.ResetSingletonForTests();
            _manager = TestKeyManager.Instance;
            _manager.Reset();
        }

        [Test]
        public void Cascade_BottomLayerWins_OnOverlappingKeys()
        {
            var handHits = new List<KeyCode>();
            var cardHits = new List<KeyCode>();

            _manager.AttachLayer("hand", new Dictionary<KeyCode, TestKeyBinding>
            {
                { KeyCode.Alpha1, new TestKeyBinding("hand-1", () => handHits.Add(KeyCode.Alpha1)) },
                { KeyCode.Alpha2, new TestKeyBinding("hand-2", () => handHits.Add(KeyCode.Alpha2)) },
                { KeyCode.Alpha4, new TestKeyBinding("hand-4", () => handHits.Add(KeyCode.Alpha4)) },
            }, appendToBottom: false);

            _manager.AttachLayer("card", new Dictionary<KeyCode, TestKeyBinding>
            {
                { KeyCode.Alpha1, new TestKeyBinding("card-1", () => cardHits.Add(KeyCode.Alpha1)) },
                { KeyCode.Alpha2, new TestKeyBinding("card-2", () => cardHits.Add(KeyCode.Alpha2)) },
                { KeyCode.Alpha3, new TestKeyBinding("card-3", () => cardHits.Add(KeyCode.Alpha3)) },
            }, appendToBottom: true);

            Assert.That(_manager.TryGetOwner(KeyCode.Alpha1, out var owner1), Is.True);
            Assert.That(owner1, Is.EqualTo("card"));
            Assert.That(_manager.TryGetOwner(KeyCode.Alpha4, out var owner4), Is.True);
            Assert.That(owner4, Is.EqualTo("hand"));

            _manager.ActiveBindings[KeyCode.Alpha1].Invoke();
            _manager.ActiveBindings[KeyCode.Alpha4].Invoke();

            Assert.That(cardHits, Is.EqualTo(new[] { KeyCode.Alpha1 }));
            Assert.That(handHits, Is.EqualTo(new[] { KeyCode.Alpha4 }));
            Assert.That(_manager.GetOverflowKeysForLayer("hand"), Is.EquivalentTo(new[] { KeyCode.Alpha1, KeyCode.Alpha2 }));
        }

        [Test]
        public void PromoteLayerToTop_MovesLayerToBottomOfStack()
        {
            _manager.AttachLayer("hand", new Dictionary<KeyCode, TestKeyBinding>
            {
                { KeyCode.Alpha1, new TestKeyBinding("hand-1", () => { }) },
            }, appendToBottom: false);

            _manager.AttachLayer("card", new Dictionary<KeyCode, TestKeyBinding>
            {
                { KeyCode.Alpha1, new TestKeyBinding("card-1", () => { }) },
            }, appendToBottom: true);

            Assert.That(_manager.PromoteLayerToTop("hand"), Is.True);
            Assert.That(_manager.TryGetOwner(KeyCode.Alpha1, out var owner), Is.True);
            Assert.That(owner, Is.EqualTo("hand"));
            Assert.That(_manager.StackOrder[^1], Is.EqualTo("hand"));
        }

        [Test]
        public void DetachLayer_ReleasesKeysToLowerLayer()
        {
            _manager.AttachLayer("hand", new Dictionary<KeyCode, TestKeyBinding>
            {
                { KeyCode.Alpha1, new TestKeyBinding("hand-1", () => { }) },
            }, appendToBottom: false);

            _manager.AttachLayer("card", new Dictionary<KeyCode, TestKeyBinding>
            {
                { KeyCode.Alpha1, new TestKeyBinding("card-1", () => { }) },
            }, appendToBottom: true);

            _manager.DetachLayer("card");
            Assert.That(_manager.TryGetOwner(KeyCode.Alpha1, out var owner), Is.True);
            Assert.That(owner, Is.EqualTo("hand"));
        }
    }
}

#endif

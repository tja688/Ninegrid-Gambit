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
        public void Register_PreemptsOverlappingKeys_KeepsUniqueKeys()
        {
            var handHits = new List<KeyCode>();
            var cardHits = new List<KeyCode>();

            _manager.Register("hand", new Dictionary<KeyCode, TestKeyBinding>
            {
                { KeyCode.Alpha1, new TestKeyBinding("hand-1", () => handHits.Add(KeyCode.Alpha1)) },
                { KeyCode.Alpha2, new TestKeyBinding("hand-2", () => handHits.Add(KeyCode.Alpha2)) },
                { KeyCode.Alpha4, new TestKeyBinding("hand-4", () => handHits.Add(KeyCode.Alpha4)) },
            });

            _manager.Register("card", new Dictionary<KeyCode, TestKeyBinding>
            {
                { KeyCode.Alpha1, new TestKeyBinding("card-1", () => cardHits.Add(KeyCode.Alpha1)) },
                { KeyCode.Alpha2, new TestKeyBinding("card-2", () => cardHits.Add(KeyCode.Alpha2)) },
                { KeyCode.Alpha3, new TestKeyBinding("card-3", () => cardHits.Add(KeyCode.Alpha3)) },
            });

            Assert.That(_manager.TryGetOwner(KeyCode.Alpha1, out var owner1), Is.True);
            Assert.That(owner1, Is.EqualTo("card"));
            Assert.That(_manager.TryGetOwner(KeyCode.Alpha2, out var owner2), Is.True);
            Assert.That(owner2, Is.EqualTo("card"));
            Assert.That(_manager.TryGetOwner(KeyCode.Alpha3, out var owner3), Is.True);
            Assert.That(owner3, Is.EqualTo("card"));
            Assert.That(_manager.TryGetOwner(KeyCode.Alpha4, out var owner4), Is.True);
            Assert.That(owner4, Is.EqualTo("hand"));

            _manager.ActiveBindings[KeyCode.Alpha1].Invoke();
            _manager.ActiveBindings[KeyCode.Alpha4].Invoke();

            Assert.That(cardHits, Is.EqualTo(new[] { KeyCode.Alpha1 }));
            Assert.That(handHits, Is.EqualTo(new[] { KeyCode.Alpha4 }));
            Assert.That(_manager.GetInactiveKeys("hand"), Is.EquivalentTo(new[] { KeyCode.Alpha1, KeyCode.Alpha2 }));
        }

        [Test]
        public void ActivateModule_RestoresModuleKeys_WithoutTouchingOthers()
        {
            _manager.Register("hand", new Dictionary<KeyCode, TestKeyBinding>
            {
                { KeyCode.Alpha1, new TestKeyBinding("hand-1", () => { }) },
                { KeyCode.Alpha2, new TestKeyBinding("hand-2", () => { }) },
                { KeyCode.Alpha4, new TestKeyBinding("hand-4", () => { }) },
            });

            _manager.Register("card", new Dictionary<KeyCode, TestKeyBinding>
            {
                { KeyCode.Alpha1, new TestKeyBinding("card-1", () => { }) },
                { KeyCode.Alpha2, new TestKeyBinding("card-2", () => { }) },
                { KeyCode.Alpha3, new TestKeyBinding("card-3", () => { }) },
            });

            Assert.That(_manager.ActivateModule("hand"), Is.True);

            Assert.That(_manager.TryGetOwner(KeyCode.Alpha1, out var owner1), Is.True);
            Assert.That(owner1, Is.EqualTo("hand"));
            Assert.That(_manager.TryGetOwner(KeyCode.Alpha2, out var owner2), Is.True);
            Assert.That(owner2, Is.EqualTo("hand"));
            Assert.That(_manager.TryGetOwner(KeyCode.Alpha3, out var owner3), Is.True);
            Assert.That(owner3, Is.EqualTo("card"));
            Assert.That(_manager.TryGetOwner(KeyCode.Alpha4, out var owner4), Is.True);
            Assert.That(owner4, Is.EqualTo("hand"));
        }

        [Test]
        public void UnregisterModule_RebuildsFromRegistrationOrder()
        {
            _manager.Register("hand", new Dictionary<KeyCode, TestKeyBinding>
            {
                { KeyCode.Alpha1, new TestKeyBinding("hand-1", () => { }) },
            });

            _manager.Register("card", new Dictionary<KeyCode, TestKeyBinding>
            {
                { KeyCode.Alpha1, new TestKeyBinding("card-1", () => { }) },
            });

            Assert.That(_manager.UnregisterModule("card"), Is.True);
            Assert.That(_manager.TryGetOwner(KeyCode.Alpha1, out var owner), Is.True);
            Assert.That(owner, Is.EqualTo("hand"));
        }
    }
}

#endif

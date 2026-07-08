#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
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

            var stack = CreateStack(
                CreateLayerProfile("hand", "hand"),
                CreateLayerProfile("card", "card"));
            _manager.SetStack(stack);

            _manager.AttachLayer("hand", new Dictionary<KeyCode, TestKeyBinding>
            {
                { KeyCode.Alpha1, new TestKeyBinding("hand-1", () => handHits.Add(KeyCode.Alpha1)) },
                { KeyCode.Alpha2, new TestKeyBinding("hand-2", () => handHits.Add(KeyCode.Alpha2)) },
                { KeyCode.Alpha4, new TestKeyBinding("hand-4", () => handHits.Add(KeyCode.Alpha4)) },
            });

            _manager.AttachLayer("card", new Dictionary<KeyCode, TestKeyBinding>
            {
                { KeyCode.Alpha1, new TestKeyBinding("card-1", () => cardHits.Add(KeyCode.Alpha1)) },
                { KeyCode.Alpha2, new TestKeyBinding("card-2", () => cardHits.Add(KeyCode.Alpha2)) },
                { KeyCode.Alpha3, new TestKeyBinding("card-3", () => cardHits.Add(KeyCode.Alpha3)) },
            });

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
        public void PromoteLayerToTop_WritesStackAssetAndReloads()
        {
            var stack = CreateStack(
                CreateLayerProfile("hand", "hand"),
                CreateLayerProfile("card", "card"));
            _manager.SetStack(stack);

            _manager.AttachLayer("hand", new Dictionary<KeyCode, TestKeyBinding>
            {
                { KeyCode.Alpha1, new TestKeyBinding("hand-1", () => { }) },
            });

            _manager.AttachLayer("card", new Dictionary<KeyCode, TestKeyBinding>
            {
                { KeyCode.Alpha1, new TestKeyBinding("card-1", () => { }) },
            });

            Assert.That(_manager.PromoteLayerToTop("hand"), Is.True);
            Assert.That(_manager.TryGetOwner(KeyCode.Alpha1, out var owner), Is.True);
            Assert.That(owner, Is.EqualTo("hand"));
            Assert.That(_manager.StackOrder[^1], Is.EqualTo("hand"));
        }

        [Test]
        public void AttachLayer_RespectsStackConfigOrder_ForConfigLayers()
        {
            var cardProfile = CreateLayerProfile("standard-card", "卡牌");
            var deckProfile = CreateLayerProfile("card-deck-manager", "牌组");
            var stack = CreateStack(cardProfile, deckProfile);

            _manager.SetStack(stack);

            _manager.AttachLayer("standard-card", new Dictionary<KeyCode, TestKeyBinding>
            {
                { KeyCode.Keypad1, new TestKeyBinding("card-1", () => { }) },
            });

            _manager.AttachLayer("card-deck-manager", new Dictionary<KeyCode, TestKeyBinding>
            {
                { KeyCode.Keypad1, new TestKeyBinding("deck-1", () => { }) },
            });

            Assert.That(_manager.StackOrder, Is.EqualTo(new[] { "standard-card", "card-deck-manager" }));
            Assert.That(_manager.TryGetOwner(KeyCode.Keypad1, out var owner), Is.True);
            Assert.That(owner, Is.EqualTo("card-deck-manager"));
        }

        [Test]
        public void AttachLayer_UndeclaredLayer_GetsLowestPriority()
        {
            var stack = CreateStack(CreateLayerProfile("card-deck-manager", "牌组"));
            _manager.SetStack(stack);

            _manager.AttachLayer("card-deck-manager", new Dictionary<KeyCode, TestKeyBinding>
            {
                { KeyCode.Keypad1, new TestKeyBinding("deck-1", () => { }) },
            });

            _manager.AttachLayer("dynamic-layer", new Dictionary<KeyCode, TestKeyBinding>
            {
                { KeyCode.Keypad1, new TestKeyBinding("dynamic-1", () => { }) },
            });

            Assert.That(_manager.StackOrder, Is.EqualTo(new[] { "dynamic-layer", "card-deck-manager" }));
            Assert.That(_manager.TryGetOwner(KeyCode.Keypad1, out var owner), Is.True);
            Assert.That(owner, Is.EqualTo("card-deck-manager"));
        }

        [Test]
        public void DetachLayer_ReleasesKeysToLowerLayer()
        {
            var stack = CreateStack(
                CreateLayerProfile("hand", "hand"),
                CreateLayerProfile("card", "card"));
            _manager.SetStack(stack);

            _manager.AttachLayer("hand", new Dictionary<KeyCode, TestKeyBinding>
            {
                { KeyCode.Alpha1, new TestKeyBinding("hand-1", () => { }) },
            });

            _manager.AttachLayer("card", new Dictionary<KeyCode, TestKeyBinding>
            {
                { KeyCode.Alpha1, new TestKeyBinding("card-1", () => { }) },
            });

            _manager.DetachLayer("card");
            Assert.That(_manager.TryGetOwner(KeyCode.Alpha1, out var owner), Is.True);
            Assert.That(owner, Is.EqualTo("hand"));
        }

        [Test]
        public void GetAllRegisteredActions_IncludesOverflowBindings()
        {
            var stack = CreateStack(
                CreateLayerProfile("hand", "hand"),
                CreateLayerProfile("card", "card"));
            _manager.SetStack(stack);

            _manager.AttachLayer("hand", new Dictionary<KeyCode, TestKeyBinding>
            {
                { KeyCode.Alpha1, new TestKeyBinding("hand-1", () => { }) },
                { KeyCode.Alpha2, new TestKeyBinding("hand-2", () => { }) },
            });

            _manager.AttachLayer("card", new Dictionary<KeyCode, TestKeyBinding>
            {
                { KeyCode.Alpha1, new TestKeyBinding("card-1", () => { }) },
            });

            var actions = _manager.GetAllRegisteredActions();

            Assert.That(actions.Count, Is.EqualTo(3));
            Assert.That(actions.Any(action => action.LayerId == "hand" && action.Key == KeyCode.Alpha1 && !action.IsKeypadActive), Is.True);
            Assert.That(actions.Any(action => action.LayerId == "card" && action.Key == KeyCode.Alpha1 && action.IsKeypadActive), Is.True);
        }

        [Test]
        public void TryInvokeRegisteredAction_InvokesOverflowBinding()
        {
            var handHits = 0;
            var cardHits = 0;

            var stack = CreateStack(
                CreateLayerProfile("hand", "hand"),
                CreateLayerProfile("card", "card"));
            _manager.SetStack(stack);

            _manager.AttachLayer("hand", new Dictionary<KeyCode, TestKeyBinding>
            {
                { KeyCode.Alpha1, new TestKeyBinding("hand-1", () => handHits++) },
            });

            _manager.AttachLayer("card", new Dictionary<KeyCode, TestKeyBinding>
            {
                { KeyCode.Alpha1, new TestKeyBinding("card-1", () => cardHits++) },
            });

            Assert.That(_manager.TryInvokeRegisteredAction("hand", KeyCode.Alpha1), Is.True);
            Assert.That(handHits, Is.EqualTo(1));
            Assert.That(cardHits, Is.EqualTo(0));

            _manager.ActiveBindings[KeyCode.Alpha1].Invoke();
            Assert.That(cardHits, Is.EqualTo(1));
            Assert.That(handHits, Is.EqualTo(1));
        }

        private static TestKeyLayerProfileSO CreateLayerProfile(string layerId, string displayName)
        {
            var profile = ScriptableObject.CreateInstance<TestKeyLayerProfileSO>();
            var serialized = new SerializedObject(profile);
            serialized.FindProperty("layerId").stringValue = layerId;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return profile;
        }

        private static TestKeyStackConfigSO CreateStack(params TestKeyLayerProfileSO[] profiles)
        {
            var stack = ScriptableObject.CreateInstance<TestKeyStackConfigSO>();
            var serialized = new SerializedObject(stack);
            var layers = serialized.FindProperty("layers");
            layers.arraySize = profiles.Length;
            for (var i = 0; i < profiles.Length; i++)
            {
                layers.GetArrayElementAtIndex(i).objectReferenceValue = profiles[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return stack;
        }
    }
}

#endif

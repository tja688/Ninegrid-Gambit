using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Cards.Tests
{
    /// <summary>
    /// UID 复用后陈旧 ManagedCard 句柄不得误删当前注册实例（Bounce 延迟退场 ABA 回归）。
    /// </summary>
    public sealed class CardManagerStaleHandleReleaseTests
    {
        private const int TestUid = 42;

        private GameObject _managerRoot;
        private GameObject _prefab;
        private CardManagerSingleton _manager;

        [SetUp]
        public void SetUp()
        {
            _managerRoot = new GameObject("CardManagerStaleHandleTestRoot");
            _manager = _managerRoot.AddComponent<CardManagerSingleton>();

            _prefab = new GameObject("StandardCardPrefab");
            _prefab.AddComponent<StandardCardView>();
            _manager.RegisterPrefab(CardManagerSingleton.StandardDefId, _prefab);
        }

        [TearDown]
        public void TearDown()
        {
            if (_managerRoot != null)
            {
                Object.DestroyImmediate(_managerRoot);
            }

            if (_prefab != null)
            {
                Object.DestroyImmediate(_prefab);
            }
        }

        [Test]
        public void Release_StaleHandleAfterUidReuse_DoesNotDestroyReplacement()
        {
            var first = _manager.SpawnView(TestUid, CardManagerSingleton.StandardDefId);
            Assert.IsNotNull(first);
            var staleHandle = first;

            _manager.Release(TestUid, "test.evictFirst");
            Assert.IsFalse(_manager.TryGet(TestUid, out _));

            var replacement = _manager.SpawnView(TestUid, CardManagerSingleton.StandardDefId);
            Assert.IsNotNull(replacement);
            Assert.AreNotSame(staleHandle, replacement);

            _manager.Release(staleHandle, "stale.attempt");

            Assert.IsTrue(_manager.TryGet(TestUid, out var stillRegistered));
            Assert.AreSame(replacement, stillRegistered);
            Assert.IsNotNull(replacement.View);
            Assert.IsTrue(replacement.GameObject.activeInHierarchy);
        }

        [Test]
        public void Release_CurrentHandle_StillRemovesRegisteredInstance()
        {
            var card = _manager.SpawnView(TestUid, CardManagerSingleton.StandardDefId);
            Assert.IsNotNull(card);

            _manager.Release(card, "test.validRelease");

            Assert.IsFalse(_manager.TryGet(TestUid, out _));
        }

        [Test]
        public void Release_StaleHandle_WhenUidNotRegistered_IsNoOp()
        {
            var orphan = _manager.SpawnView(TestUid, CardManagerSingleton.StandardDefId);
            var staleHandle = orphan;
            _manager.Release(TestUid, "test.clear");

            Assert.DoesNotThrow(() => _manager.Release(staleHandle, "stale.afterClear"));
            Assert.IsFalse(_manager.TryGet(TestUid, out _));
        }
    }
}

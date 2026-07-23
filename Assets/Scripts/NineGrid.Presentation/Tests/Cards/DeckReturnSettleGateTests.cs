using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using NineGrid.Cards;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 回库 settle 等待与发牌栅栏：ReturnInFlight 期间不得立刻 Deal。
    /// </summary>
    public sealed class DeckReturnSettleGateTests
    {
        private GameObject _prefab;
        private CardManagerSingleton _cardManager;
        private CardDeckManagerSingleton _deckManager;

        [SetUp]
        public void SetUp()
        {
            DestroyAllSingletonsInScene();
            ResetPresentationSingletons();

            var cardGo = new GameObject("CardManagerTest");
            var deckGo = new GameObject("DeckManagerTest");

            _cardManager = cardGo.AddComponent<CardManagerSingleton>();
            _deckManager = deckGo.AddComponent<CardDeckManagerSingleton>();
            EnsureDeckManagerInitialized(_deckManager);

            _prefab = new GameObject("StandardCardPrefab");
            _prefab.AddComponent<StandardCardView>();
            _cardManager.RegisterPrefab(CardManagerSingleton.StandardDefId, _prefab);

            ForceDeckMode(_deckManager, CardDeckMode.InGame);
        }

        [TearDown]
        public void TearDown()
        {
            DestroyManagerObject(_deckManager);
            DestroyManagerObject(_cardManager);

            if (_prefab != null)
            {
                UnityEngine.Object.DestroyImmediate(_prefab);
                _prefab = null;
            }

            ResetPresentationSingletons();
        }

        [Test]
        public void WaitReturnSettledAsync_WhenNotInFlight_CompletesImmediately()
        {
            Assert.IsFalse(_deckManager.IsReturnInFlight(9001));
            var awaiter = _deckManager.WaitReturnSettledAsync(9001).GetAwaiter();
            Assert.IsTrue(awaiter.IsCompleted);
            awaiter.GetResult();
            Assert.IsFalse(_deckManager.IsReturnInFlight(9001));
        }

        [Test]
        public void WaitReturnSettledAsync_WhileInFlight_SuspendsUntilEnd()
        {
            const int uid = 9002;
            InvokeBeginReturnInFlight(uid, "test.claim");
            Assert.IsTrue(_deckManager.IsReturnInFlight(uid));

            var awaiter = _deckManager.WaitReturnSettledAsync(uid).GetAwaiter();
            Assert.IsFalse(awaiter.IsCompleted, "回库途中 Wait 应挂起");

            InvokeEndReturnInFlight(uid, "test.rippleEnd");
            Assert.IsFalse(_deckManager.IsReturnInFlight(uid));
            Assert.IsTrue(awaiter.IsCompleted, "EndReturn 后 Wait 应完成");
            awaiter.GetResult();
        }

        [Test]
        public void WaitReturnsSettledAsync_WaitsAllUids()
        {
            const int a = 9010;
            const int b = 9011;
            InvokeBeginReturnInFlight(a, "test.claim");
            InvokeBeginReturnInFlight(b, "test.claim");

            var awaiter = _deckManager
                .WaitReturnsSettledAsync(new List<int> { a, b })
                .GetAwaiter();
            Assert.IsFalse(awaiter.IsCompleted);

            InvokeEndReturnInFlight(a, "test.rippleEnd");
            Assert.IsFalse(awaiter.IsCompleted, "仅清一张时仍应挂起");

            InvokeEndReturnInFlight(b, "test.rippleEnd");
            Assert.IsTrue(awaiter.IsCompleted);
            awaiter.GetResult();
        }

        [Test]
        public void DealCardByUidWithFlightAsync_WhileReturnInFlight_WaitsUntilSettled()
        {
            const int uid = 9020;
            InvokeBeginReturnInFlight(uid, "test.claim");
            Assert.IsTrue(_deckManager.IsReturnInFlight(uid));

            // 无 ensureCard：settle 后会因找不到槽失败，但必须先等到 EndReturn。
            LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex("卡组中未找到 Uid=9020"));

            var awaiter = _deckManager
                .DealCardByUidWithFlightAsync(uid, groundSlot: 1, cancellationToken: CancellationToken.None)
                .GetAwaiter();
            Assert.IsFalse(awaiter.IsCompleted, "ReturnInFlight 时 Deal 不得立刻返回");

            InvokeEndReturnInFlight(uid, "test.rippleEnd");
            Assert.IsTrue(awaiter.IsCompleted, "settle 后 Deal 应越过栅栏继续");
            var result = awaiter.GetResult();
            Assert.IsFalse(result.ok);
            Assert.IsNull(result.handle);
        }

        private void InvokeBeginReturnInFlight(int uid, string phase)
        {
            var method = typeof(CardDeckManagerSingleton).GetMethod(
                "BeginReturnInFlight",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            method.Invoke(_deckManager, new object[] { uid, phase });
        }

        private void InvokeEndReturnInFlight(int uid, string phase)
        {
            var method = typeof(CardDeckManagerSingleton).GetMethod(
                "EndReturnInFlight",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            method.Invoke(_deckManager, new object[] { uid, phase });
        }

        private static void EnsureDeckManagerInitialized(CardDeckManagerSingleton deckManager)
        {
            var slotField = typeof(CardDeckManagerSingleton).GetField(
                "_slotContainer",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(slotField);
            if (slotField.GetValue(deckManager) != null)
            {
                return;
            }

            var awake = typeof(CardDeckManagerSingleton).GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(awake);
            awake.Invoke(deckManager, null);
            Assert.IsNotNull(slotField.GetValue(deckManager));
        }

        private static void ForceDeckMode(CardDeckManagerSingleton deck, CardDeckMode mode)
        {
            var backing = typeof(CardDeckManagerSingleton).GetField(
                "<CurrentMode>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(backing);
            backing.SetValue(deck, mode);
        }

        private static void DestroyAllSingletonsInScene()
        {
            DestroyAll<CardManagerSingleton>();
            DestroyAll<CardHandManagerSingleton>();
            DestroyAll<CardDeckManagerSingleton>();
        }

        private static void DestroyAll<T>() where T : MonoBehaviour
        {
            var found = UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            for (var i = 0; i < found.Length; i++)
            {
                if (found[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(found[i].gameObject);
                }
            }
        }

        private static void ResetPresentationSingletons()
        {
            ResetStaticInstance(typeof(CardManagerSingleton), "_instance");
            ResetStaticInstance(typeof(CardHandManagerSingleton), "_instance");
            ResetStaticInstance(typeof(CardDeckManagerSingleton), "_instance");
        }

        private static void ResetStaticInstance(Type type, string fieldName)
        {
            var field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
            field?.SetValue(null, null);
        }

        private static void DestroyManagerObject(MonoBehaviour manager)
        {
            if (manager == null)
            {
                return;
            }

            UnityEngine.Object.DestroyImmediate(manager.gameObject);
        }
    }
}

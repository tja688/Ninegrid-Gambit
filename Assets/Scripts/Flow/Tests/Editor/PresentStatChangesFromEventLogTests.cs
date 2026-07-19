using System.Collections.Generic;
using System.Reflection;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Systems;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace NineGrid.Flow.Tests
{
    /// <summary>
    /// 属性变化 EventLog → 卡面 Sync：修复「效果脉冲有、数字晚一轮」的回归钉。
    /// </summary>
    public sealed class PresentStatChangesFromEventLogTests
    {
        private IArchitecture _arch;
        private IActionPipelineSystem _pipeline;
        private CardManagerSingleton _cardManager;
        private GameObject _prefab;
        private readonly List<int> _syncedUids = new List<int>();

        [SetUp]
        public void SetUp()
        {
            DestroyAllSingletonsInScene();
            ResetCardManagerInstance();

            NineGridArchitecture.ResetForTests();
            _arch = NineGridArchitecture.Current;
            InitialGameFactory.Create(_arch, new InitialGameOptions { Seed = 7UL });
            _pipeline = _arch.GetSystem<IActionPipelineSystem>();

            var go = new GameObject("CardManager_PresentStatTest");
            _cardManager = go.AddComponent<CardManagerSingleton>();
            _prefab = new GameObject("StandardCardPrefab_PresentStatTest");
            _prefab.AddComponent<StandardCardView>();
            _cardManager.RegisterPrefab(CardManagerSingleton.StandardDefId, _prefab);

            _syncedUids.Clear();
            CombatHitSink.SyncCardPresentation = card =>
            {
                if (card != null)
                {
                    _syncedUids.Add(card.Uid);
                }
            };
        }

        [TearDown]
        public void TearDown()
        {
            CombatHitSink.SyncCardPresentation = null;

            if (_cardManager != null)
            {
                Object.DestroyImmediate(_cardManager.gameObject);
                _cardManager = null;
            }

            if (_prefab != null)
            {
                Object.DestroyImmediate(_prefab);
                _prefab = null;
            }

            ResetCardManagerInstance();
            DestroyAllSingletonsInScene();
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void PresentStatChanges_BaseStatModified_RequestsSyncForTargetUid()
        {
            var uid = SpawnCoreMonsterAndView();
            var startIndex = _pipeline.EventLog.Entries.Count;

            _pipeline.Enqueue(new ModifyBaseStatAction(uid, StatId.Attack, 1, "test.stat.present"));
            Assert.Greater(_pipeline.RunToCompletion(), 0);
            Assert.IsTrue(ContainsTypeSince(startIndex, CoreEventType.BaseStatModified));

            InBattleManagerSingleton.PresentStatChangesFromEventLog(startIndex);

            CollectionAssert.Contains(_syncedUids, uid);
        }

        [Test]
        public void PresentEffectTriggers_AlsoSyncsStatChanges_SameBeat()
        {
            var uid = SpawnCoreMonsterAndView();
            PlaceOnBoard(uid);
            var startIndex = _pipeline.EventLog.Entries.Count;

            _pipeline.Enqueue(new ModifyBaseStatAction(uid, StatId.Attack, 2, "test.stat.via_effect_path"));
            Assert.Greater(_pipeline.RunToCompletion(), 0);

            InBattleManagerSingleton.PresentEffectTriggersFromEventLog(startIndex);

            CollectionAssert.Contains(
                _syncedUids,
                uid,
                "PresentEffectTriggers 同拍应刷属性变化卡面");
        }

        [Test]
        public void PresentStatChanges_ObserverAndVictim_BothSynced()
        {
            var victimUid = SpawnCoreMonsterAndView();
            var observerUid = SpawnCoreMonsterAndView();
            var startIndex = _pipeline.EventLog.Entries.Count;

            _pipeline.Enqueue(new ModifyBaseStatAction(victimUid, StatId.Armor, -1, "test.victim.armor"));
            _pipeline.Enqueue(new ModifyBaseStatAction(observerUid, StatId.Attack, 1, "test.observer.atk"));
            Assert.Greater(_pipeline.RunToCompletion(), 0);

            InBattleManagerSingleton.PresentStatChangesFromEventLog(startIndex);

            CollectionAssert.Contains(_syncedUids, victimUid);
            CollectionAssert.Contains(_syncedUids, observerUid);
        }

        private int SpawnCoreMonsterAndView()
        {
            var board = _arch.GetModel<BoardModel>();
            var slot = FindEmptyBoardSlot(board);
            _pipeline.Enqueue(
                new SpawnCardAction(
                    "monster.test",
                    CardKind.Monster,
                    ZoneId.Board,
                    slot,
                    1,
                    "test.present.stat"));
            Assert.Greater(_pipeline.RunToCompletion(), 0);

            var uid = board.GetCardUid(slot);
            Assert.Greater(uid, 0);
            var managed = _cardManager.SpawnView(uid, CardManagerSingleton.StandardDefId);
            Assert.IsNotNull(managed);
            return uid;
        }

        private void PlaceOnBoard(int uid)
        {
            var registry = _arch.GetModel<CardRegistry>();
            Assert.IsTrue(registry.TryGet(uid, out var card));
            Assert.AreEqual(ZoneId.Board, card.Zone.Value);
        }

        private static SlotId FindEmptyBoardSlot(BoardModel board)
        {
            for (var i = 1; i <= 9; i++)
            {
                var slot = SlotId.Board(i);
                if (board.IsEmpty(slot))
                {
                    return slot;
                }
            }

            Assert.Fail("no empty board slot");
            return SlotId.None;
        }

        private bool ContainsTypeSince(int startIndex, CoreEventType type)
        {
            var entries = _pipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                if (entries[i].Type == type)
                {
                    return true;
                }
            }

            return false;
        }

        private static void DestroyAllSingletonsInScene()
        {
            var found = Object.FindObjectsByType<CardManagerSingleton>(FindObjectsSortMode.None);
            for (var i = 0; i < found.Length; i++)
            {
                if (found[i] != null)
                {
                    Object.DestroyImmediate(found[i].gameObject);
                }
            }
        }

        private static void ResetCardManagerInstance()
        {
            var field = typeof(CardManagerSingleton).GetField(
                "_instance",
                BindingFlags.Static | BindingFlags.NonPublic);
            field?.SetValue(null, null);
        }
    }
}

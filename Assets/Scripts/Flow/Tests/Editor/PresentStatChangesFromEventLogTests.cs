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
            // 延迟 0：断言脉冲后调度路径立刻落地（非「等下一轮 SyncAll」）。
            SetStatRevealDelay(uid, 0f);
            var startIndex = _pipeline.EventLog.Entries.Count;

            _pipeline.Enqueue(new ModifyBaseStatAction(uid, StatId.Attack, 2, "test.stat.via_effect_path"));
            Assert.Greater(_pipeline.RunToCompletion(), 0);

            InBattleManagerSingleton.PresentEffectTriggersFromEventLog(startIndex);

            CollectionAssert.Contains(
                _syncedUids,
                uid,
                "PresentEffectTriggers 应调度属性 Sync（延迟 0 时立刻执行）");
        }

        [Test]
        public void PresentEffectTriggers_WithDelay_DoesNotSyncBeforeDelayElapses()
        {
            var uid = SpawnCoreMonsterAndView();
            PlaceOnBoard(uid);
            SetStatRevealDelay(uid, 0.5f);
            var startIndex = _pipeline.EventLog.Entries.Count;

            // 需要 EffectTriggered 才会走延迟分支；直接 BaseStatModified 无脉冲时 delay=0。
            // 注入一条 EffectTriggered 事件到同一段，模拟技能触发。
            _pipeline.Enqueue(new ModifyBaseStatAction(uid, StatId.Attack, 1, "test.stat.delayed"));
            Assert.Greater(_pipeline.RunToCompletion(), 0);

            // 无 EffectTriggered 时 PresentEffectTriggers 仍会立刻 Sync（hasEffectTriggers=false）。
            // 用带 EffectTriggered 的 overload 路径：手动构造 effectTrigger set。
            var triggers = new HashSet<int> { uid };
            InBattleManagerSingleton.PresentStatChangesFromEventLog(startIndex, triggers);

            CollectionAssert.DoesNotContain(
                _syncedUids,
                uid,
                "有 EffectTriggered 且延迟>0 时不应在 DelayedCall 前 Sync");
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

        private void SetStatRevealDelay(int uid, float delaySeconds)
        {
            Assert.IsTrue(_cardManager.TryGet(uid, out var managed) && managed != null);
            Assert.IsTrue(managed.TryGetEffectManager(out var effectManager) && effectManager != null);
            effectManager.StatRevealDelayAfterEffectTrigger = delaySeconds;
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

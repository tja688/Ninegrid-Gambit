using System.Collections.Generic;
using System.Text.RegularExpressions;
using NineGrid.Cards;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NineGrid.Presentation.Tests.Cards
{
    /// <summary>
    /// GroundOccupancyIndex 批量原子置换：成功提交更新双向表；失败则快照不变。
    /// </summary>
    public sealed class GroundOccupancyAtomicCommitTests
    {
        private const int UidA = 1001;
        private const int UidB = 1002;
        private const int UidC = 1003;

        private GroundOccupancyIndex _index;

        [SetUp]
        public void SetUp()
        {
            _index = new GroundOccupancyIndex();
        }

        [Test]
        public void TryCommitPermutation_Success_UpdatesBidirectionalMaps()
        {
            Assert.IsTrue(_index.TryRegister(1, UidA));
            Assert.IsTrue(_index.TryRegister(2, UidB));

            var moves = new List<(int uid, int toSlot)>
            {
                (UidA, 2),
                (UidB, 1),
            };

            Assert.IsTrue(_index.TryCommitPermutation(moves, "Test.Swap"));
            Assert.AreEqual(UidB, _index.GetUidAt(1));
            Assert.AreEqual(UidA, _index.GetUidAt(2));
            Assert.IsTrue(_index.TryGetSlotOf(UidA, out var slotA));
            Assert.AreEqual(2, slotA);
            Assert.IsTrue(_index.TryGetSlotOf(UidB, out var slotB));
            Assert.AreEqual(1, slotB);
            Assert.IsFalse(_index.HasOccupancyConflictSinceClear);
        }

        [Test]
        public void TryCommitPermutation_ExternalBlocker_LeavesSnapshotUnchanged()
        {
            Assert.IsTrue(_index.TryRegister(1, UidA));
            Assert.IsTrue(_index.TryRegister(2, UidB));
            Assert.IsTrue(_index.TryRegister(3, UidC));

            var moves = new List<(int uid, int toSlot)>
            {
                (UidA, 3),
            };

            LogAssert.Expect(LogType.Error, new Regex("批量置换失败.*外部"));
            Assert.IsFalse(_index.TryCommitPermutation(moves, "Test.ExternalBlock"));
            Assert.AreEqual(UidA, _index.GetUidAt(1));
            Assert.AreEqual(UidB, _index.GetUidAt(2));
            Assert.AreEqual(UidC, _index.GetUidAt(3));
            Assert.IsTrue(_index.HasOccupancyConflictSinceClear);
        }

        [Test]
        public void TryCommitPermutation_DuplicateTarget_LeavesSnapshotUnchanged()
        {
            Assert.IsTrue(_index.TryRegister(1, UidA));
            Assert.IsTrue(_index.TryRegister(2, UidB));

            var moves = new List<(int uid, int toSlot)>
            {
                (UidA, 3),
                (UidB, 3),
            };

            LogAssert.Expect(LogType.Error, new Regex("批量置换失败.*toSlot=3 重复"));
            Assert.IsFalse(_index.TryCommitPermutation(moves, "Test.DupTarget"));
            Assert.AreEqual(UidA, _index.GetUidAt(1));
            Assert.AreEqual(UidB, _index.GetUidAt(2));
            Assert.IsTrue(_index.HasOccupancyConflictSinceClear);
        }

        [Test]
        public void TryCommitPermutation_MissingUid_LeavesSnapshotUnchanged()
        {
            Assert.IsTrue(_index.TryRegister(1, UidA));

            var moves = new List<(int uid, int toSlot)>
            {
                (UidB, 2),
            };

            LogAssert.Expect(LogType.Error, new Regex("批量置换失败.*不在场地"));
            Assert.IsFalse(_index.TryCommitPermutation(moves, "Test.MissingUid"));
            Assert.AreEqual(UidA, _index.GetUidAt(1));
            Assert.AreEqual(0, _index.GetUidAt(2));
            Assert.IsTrue(_index.HasOccupancyConflictSinceClear);
        }

        [Test]
        public void TryCommitPermutation_ThreeWayCycle_UpdatesBidirectionalMaps()
        {
            Assert.IsTrue(_index.TryRegister(1, UidA));
            Assert.IsTrue(_index.TryRegister(2, UidB));
            Assert.IsTrue(_index.TryRegister(3, UidC));

            var moves = new List<(int uid, int toSlot)>
            {
                (UidA, 2),
                (UidB, 3),
                (UidC, 1),
            };

            Assert.IsTrue(_index.TryCommitPermutation(moves, "Test.Cycle3"));
            Assert.AreEqual(UidC, _index.GetUidAt(1));
            Assert.AreEqual(UidA, _index.GetUidAt(2));
            Assert.AreEqual(UidB, _index.GetUidAt(3));
            Assert.IsFalse(_index.HasOccupancyConflictSinceClear);
        }

        [Test]
        public void TryCommitPermutation_EmptyMoves_IsNoOp()
        {
            Assert.IsTrue(_index.TryRegister(1, UidA));
            Assert.IsTrue(_index.TryCommitPermutation(null));
            Assert.IsTrue(_index.TryCommitPermutation(new List<(int, int)>()));
            Assert.AreEqual(UidA, _index.GetUidAt(1));
            Assert.IsFalse(_index.HasOccupancyConflictSinceClear);
        }
    }
}

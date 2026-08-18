using System.Collections.Generic;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Flow.Presentation;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// Issue #218 验收测试：
    /// 离巢交战与拾取按真实格表演：
    /// 1. 人在外圈时，交战方向与反击方向按当前 Avatar 占格计算，不写死格 5。
    /// 2. 人在外圈与正交邻格怪物交战、拾取帮助卡时，意图合法性成立。
    /// 3. 中心格上的非 Avatar 怪合法认领并按几何判定交战与拒绝。
    /// </summary>
    public class OuterRingEngagementAndPickupPresentationRegressionTests
    {
        private IArchitecture mArch;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Interface;
            mArch.GetModel<RunModel>().SetPhase(GamePhase.InteractionLoop);
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void AvatarOnOuterRing_DirectionResolution_UsesCurrentAvatarSlot()
        {
            var go = new GameObject("TestAttackAdapter");
            try
            {
                var adapter = go.AddComponent<CardAttackBasicAdapter>();
                var board = mArch.GetModel<BoardModel>();

                // 1. Avatar 在格 2（上边格）
                var avatar = CreateAvatarOnBoard(SlotId.Board(2));
                Assert.AreEqual(SlotId.Board(2), board.AvatarSlot.Value);

                // 正交邻格：格 1 (左), 格 3 (右), 格 5 (下)
                Assert.IsTrue(adapter.TryResolveDirectionForVictimSlot(1, 2, out var dirTo1));
                Assert.AreEqual(CardBoardDirection.Left, dirTo1);

                Assert.IsTrue(adapter.TryResolveDirectionForVictimSlot(3, 2, out var dirTo3));
                Assert.AreEqual(CardBoardDirection.Right, dirTo3);

                Assert.IsTrue(adapter.TryResolveDirectionForVictimSlot(5, 2, out var dirTo5));
                Assert.AreEqual(CardBoardDirection.Down, dirTo5);

                // 非正交邻格：格 4, 7, 8, 9, 6
                Assert.IsFalse(adapter.TryResolveDirectionForVictimSlot(4, 2, out _), "格 4 与格 2 非正交邻接");
                Assert.IsFalse(adapter.TryResolveDirectionForVictimSlot(7, 2, out _), "格 7 与格 2 非正交邻接");
                Assert.IsFalse(adapter.TryResolveDirectionForVictimSlot(8, 2, out _), "格 8 与格 2 非正交邻接");

                // 2. Avatar 在格 1（左上角格）
                board.SetAvatar(avatar, SlotId.Board(1));
                Assert.AreEqual(SlotId.Board(1), board.AvatarSlot.Value);

                // 正交邻格：格 2 (右), 格 4 (下)
                Assert.IsTrue(adapter.TryResolveDirectionForVictimSlot(2, 1, out var dir1To2));
                Assert.AreEqual(CardBoardDirection.Right, dir1To2);

                Assert.IsTrue(adapter.TryResolveDirectionForVictimSlot(4, 1, out var dir1To4));
                Assert.AreEqual(CardBoardDirection.Down, dir1To4);

                // 格 5 (中心格) 是格 1 的对角格，非正交邻接，应当返回 false
                Assert.IsFalse(adapter.TryResolveDirectionForVictimSlot(5, 1, out _), "格 5 与角格 1 为对角关系，不可正交交战");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void AvatarOnOuterRing_CounterAttackDirection_UsesCurrentAvatarSlot()
        {
            var go = new GameObject("TestAttackAdapter");
            try
            {
                var adapter = go.AddComponent<CardAttackBasicAdapter>();
                var board = mArch.GetModel<BoardModel>();

                // Avatar 在格 2（上边格）
                CreateAvatarOnBoard(SlotId.Board(2));

                // 怪物在格 1 向玩家（格 2）反击 -> 玩家在格 1 的右方，反击冲刺方向为 Right
                Assert.IsTrue(adapter.TryResolveCounterAttackDirectionForAttackerSlot(1, 2, out var counterFrom1));
                Assert.AreEqual(CardBoardDirection.Right, counterFrom1);

                // 怪物在格 5（中心格）向玩家（格 2）反击 -> 玩家在格 5 的上方，反击冲刺方向为 Up
                Assert.IsTrue(adapter.TryResolveCounterAttackDirectionForAttackerSlot(5, 2, out var counterFrom5));
                Assert.AreEqual(CardBoardDirection.Up, counterFrom5);

                // 怪物在格 8（下边格，非邻接格 2）-> 不可反击
                Assert.IsFalse(adapter.TryResolveCounterAttackDirectionForAttackerSlot(8, 2, out _));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void AvatarOnOuterRing_InteractionLegality_AllowsOrthogonalEngageAndPickup()
        {
            var board = mArch.GetModel<BoardModel>();

            // Avatar 放在格 2
            CreateAvatarOnBoard(SlotId.Board(2));

            // 在格 1 放怪物，在格 3 放帮助卡，在格 5 (中心格) 放怪物，在格 8 放怪物
            var monster1 = CreateMonsterOnBoard("monster.wolf_1", SlotId.Board(1));
            var help3 = CreateHelpCardOnBoard("help.potion_3", SlotId.Board(3));
            var monster5 = CreateMonsterOnBoard("monster.boss_5", SlotId.Center);
            var monster8 = CreateMonsterOnBoard("monster.far_8", SlotId.Board(8));

            // 1. 检验正交交战合法性
            Assert.IsTrue(
                BoardIntentLegality.TryExplainAttack(mArch, 1, out _),
                "Avatar 在格 2 可以与邻格 1 的怪物交战");
            Assert.IsTrue(
                BoardIntentLegality.TryExplainAttack(mArch, 5, out _),
                "Avatar 在格 2 可以与中心格 5 的怪物交战");
            Assert.IsFalse(
                BoardIntentLegality.TryExplainAttack(mArch, 8, out var rejectFar),
                "Avatar 在格 2 不可与远端格 8 的怪物交战");
            Assert.IsTrue(rejectFar.Contains("notAdjacent"), "拒绝原因为不邻接");

            // 2. 检验正交拾取合法性
            Assert.IsTrue(
                BoardIntentLegality.TryExplainPickup(mArch, 3, out _),
                "Avatar 在格 2 可以拾取邻格 3 的帮助卡");
        }

        [Test]
        public void AvatarOnCornerSlot_CenterSlotMonster_CannotEngageDiagonally()
        {
            var board = mArch.GetModel<BoardModel>();

            // Avatar 放在角格 1
            CreateAvatarOnBoard(SlotId.Board(1));

            // 中心格 5 放置怪物
            var monster5 = CreateMonsterOnBoard("monster.center_boss", SlotId.Center);

            // 从角格 1 点中心格 5，因为非正交邻接，被显式拒绝
            Assert.IsFalse(
                BoardIntentLegality.TryExplainAttack(mArch, 5, out var rejectReason),
                "Avatar 在角格 1 与中心格 5 是对角关系，不可直接正交交战");
            Assert.IsTrue(rejectReason.Contains("notAdjacent"), "拒绝原因为不邻接");
        }

        private CardInstance CreateAvatarOnBoard(SlotId slot)
        {
            var registry = mArch.GetModel<CardRegistry>();
            var avatar = registry.Create("avatar.test_warrior", CardKind.Avatar);
            avatar.Stats.SetBase(StatId.MaxHp, 30);
            avatar.Stats.SetBase(StatId.Hp, 30);
            avatar.Stats.SetBase(StatId.Attack, 5);
            mArch.GetModel<BoardModel>().SetAvatar(avatar, slot);
            return avatar;
        }

        private CardInstance CreateMonsterOnBoard(string defId, SlotId slot)
        {
            var registry = mArch.GetModel<CardRegistry>();
            var monster = registry.Create(defId, CardKind.Monster);
            monster.Stats.SetBase(StatId.MaxHp, 10);
            monster.Stats.SetBase(StatId.Hp, 10);
            monster.Stats.SetBase(StatId.Attack, 3);
            monster.FaceUp = true;
            mArch.GetModel<BoardModel>().PlaceCard(monster, slot);
            return monster;
        }

        private CardInstance CreateHelpCardOnBoard(string defId, SlotId slot)
        {
            var registry = mArch.GetModel<CardRegistry>();
            var help = registry.Create(defId, CardKind.Item);
            help.Stats.SetBase(StatId.MaxHp, 0);
            help.Stats.SetBase(StatId.Hp, 0);
            help.FaceUp = true;
            mArch.GetModel<BoardModel>().PlaceCard(help, slot);
            return help;
        }
    }
}

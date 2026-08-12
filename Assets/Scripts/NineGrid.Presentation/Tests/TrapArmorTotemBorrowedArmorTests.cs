using System.Collections.Generic;
using System.Text;
using NineGrid.Core;
using NineGrid.Core.Effects;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 护甲图腾借甲光环回归：邻接怪 +1 借甲且只加一次；离开邻接回收；远处怪永远不涨甲。
    /// 复现玩家报告：相邻的敌人没有护甲，远处的敌人却持续叠了一堆护甲。
    /// </summary>
    public class TrapArmorTotemBorrowedArmorTests
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
        public void Refresh_AdjacentGainsOnce_FarNeverGains()
        {
            var avatar = CreateAvatarOnBoard(SlotId.Board(5));
            var totem = CreateTrapOnBoard("trap.armor_totem", SlotId.Board(8));
            var near = CreateMonsterOnBoard("monster.test.near", SlotId.Board(7));
            var far = CreateMonsterOnBoard("monster.test.far", SlotId.Board(1));
            ActivateTotemEffects(totem);

            // 触发一次刷新（远处两格互换，不改变邻接拓扑）。
            Run(new SwapBoardSlotsAction(SlotId.Board(2), SlotId.Board(3)));
            DumpArmor("刷新1", totem, near, far);
            Assert.AreEqual(1, StatArmorUtility.GetCurrentArmor(near), "邻接怪 +1 借甲");
            Assert.AreEqual(0, StatArmorUtility.GetCurrentArmor(far), "远处怪不加甲");

            // 连续多次刷新：不得叠加。
            for (var i = 0; i < 5; i++)
            {
                Run(new SwapBoardSlotsAction(SlotId.Board(2), SlotId.Board(3)));
            }

            DumpArmor("刷新6", totem, near, far);
            Assert.AreEqual(1, StatArmorUtility.GetCurrentArmor(near), "重复刷新不叠加");
            Assert.AreEqual(0, StatArmorUtility.GetCurrentArmor(far), "远处怪始终 0");
        }

        [Test]
        public void Rotate_BorrowedArmorFollowsAdjacency_NeverAccumulates()
        {
            var avatar = CreateAvatarOnBoard(SlotId.Board(5));
            var totem = CreateTrapOnBoard("trap.armor_totem", SlotId.Board(8));
            var m1 = CreateMonsterOnBoard("monster.test.m1", SlotId.Board(7));
            var m2 = CreateMonsterOnBoard("monster.test.m2", SlotId.Board(1));
            var m3 = CreateMonsterOnBoard("monster.test.m3", SlotId.Board(3));
            ActivateTotemEffects(totem);
            var monsters = new[] { m1, m2, m3 };

            var board = mArch.GetSystem<IBoardSystem>();
            for (var step = 0; step < 16; step++)
            {
                Run(new RotateBoardClockwiseAction(step % 2 == 0));
                DumpArmor("旋转" + step, totem, m1, m2, m3);
                for (var i = 0; i < monsters.Length; i++)
                {
                    var expected = board.AreAdjacent(totem, monsters[i]) ? 1 : 0;
                    Assert.AreEqual(
                        expected,
                        StatArmorUtility.GetCurrentArmor(monsters[i]),
                        "旋转" + step + "：" + monsters[i].DefId + " 借甲必须等于邻接状态（不叠加、不残留）");
                }
            }
        }

        // ==================== 基建 ====================

        private const string RequiresJson = "\"requires\":[\"HasOwnerEntity\",\"CardZoneTriggerable\"],\"containerType\":\"Trap\"";

        private static readonly string AuraJson =
            "{\"id\":\"trap.armor_totem.aura\"," + RequiresJson + ",\"kind\":\"Triggered\","
            + "\"trigger\":{\"atom\":\"OnEnter\"},"
            + "\"target\":{\"atom\":\"FilteredCards\",\"kind\":\"Monster\",\"zone\":\"Board\",\"exclude\":[\"Self\"]},"
            + "\"action\":{\"atom\":\"SyncAdjacentBorrowedArmor\",\"value\":1,\"source\":\"trap.armor_totem\",\"adjacentTo\":\"Self\"}}";

        private static readonly string RefreshJson =
            "{\"id\":\"trap.armor_totem.refresh\"," + RequiresJson + ",\"kind\":\"Triggered\","
            + "\"trigger\":{\"atom\":\"OnEvent\",\"eventTypes\":[\"CardMoved\",\"CardDealt\"]},"
            + "\"target\":{\"atom\":\"FilteredCards\",\"kind\":\"Monster\",\"zone\":\"Board\",\"exclude\":[\"Self\"]},"
            + "\"action\":{\"atom\":\"SyncAdjacentBorrowedArmor\",\"value\":1,\"source\":\"trap.armor_totem\",\"adjacentTo\":\"Self\"}}";

        private void ActivateTotemEffects(CardInstance totem)
        {
            var effects = mArch.GetSystem<IEffectSystem>();
            var owner = new EffectOwner(EffectContainerType.Trap, "trap.armor_totem", totem.Uid);
            effects.Activate(effects.ParseJson(AuraJson), owner);
            effects.Activate(effects.ParseJson(RefreshJson), owner);
        }

        private CardInstance CreateAvatarOnBoard(SlotId slot)
        {
            var avatar = mArch.GetModel<CardRegistry>().Create("avatar.default", CardKind.Avatar);
            avatar.Stats.SetBase(StatId.MaxHp, 20);
            avatar.Stats.SetBase(StatId.Hp, 20);
            avatar.Stats.SetBase(StatId.Attack, 2);
            mArch.GetModel<BoardModel>().SetAvatar(avatar, slot);
            return avatar;
        }

        private CardInstance CreateTrapOnBoard(string defId, SlotId slot)
        {
            var trap = mArch.GetModel<CardRegistry>().Create(defId, CardKind.Trap);
            trap.Stats.SetBase(StatId.MaxHp, 6);
            trap.Stats.SetBase(StatId.Hp, 6);
            mArch.GetModel<BoardModel>().PlaceCard(trap, slot);
            return trap;
        }

        private CardInstance CreateMonsterOnBoard(string defId, SlotId slot)
        {
            var monster = mArch.GetModel<CardRegistry>().Create(defId, CardKind.Monster);
            monster.Stats.SetBase(StatId.MaxHp, 10);
            monster.Stats.SetBase(StatId.Hp, 10);
            monster.Stats.SetBase(StatId.Attack, 1);
            mArch.GetModel<BoardModel>().PlaceCard(monster, slot);
            return monster;
        }

        private void Run(GameAction action)
        {
            mArch.GetSystem<IActionPipelineSystem>().Execute(action);
        }

        private void DumpArmor(string when, CardInstance totem, params CardInstance[] monsters)
        {
            var sb = new StringBuilder();
            sb.Append("[totem-test] ").Append(when)
                .Append(" totem@").Append(totem.Slot.Value);
            for (var i = 0; i < monsters.Length; i++)
            {
                var m = monsters[i];
                sb.Append(" | ").Append(m.DefId)
                    .Append('@').Append(m.Slot.Value)
                    .Append(" armor=").Append(StatArmorUtility.GetCurrentArmor(m))
                    .Append(" counters=[").Append(DumpCounters(m)).Append(']');
            }

            UnityEngine.Debug.Log(sb.ToString());
        }

        private static string DumpCounters(CardInstance card)
        {
            var parts = new List<string>();
            foreach (var pair in card.Counters.Values)
            {
                parts.Add(pair.Key + "=" + pair.Value);
            }

            return string.Join(",", parts);
        }
    }
}

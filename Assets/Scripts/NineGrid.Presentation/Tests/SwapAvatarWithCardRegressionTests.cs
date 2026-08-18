using System.Collections.Generic;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Flow;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// Issue #219 验收测试：
    /// Avatar 与场上卡原子换位（ADR-0056 / ADR-0057）。
    /// </summary>
    public class SwapAvatarWithCardRegressionTests
    {
        private const string SwapAvatarOnBattleJson =
            "{\"id\":\"test.deferred_motion.swap_avatar_battle\",\"kind\":\"Triggered\",\"containerType\":\"MonsterSkill\","
            + "\"requires\":[\"HasOwnerEntity\",\"CardZoneTriggerable\"],"
            + "\"trigger\":{\"atom\":\"OnBattle\",\"sourceAction\":\"DealDamage\",\"targetKind\":\"Monster\",\"maxActionDepth\":0},"
            + "\"conditions\":[{\"atom\":\"EventFilterActorIsPlayerTargetIsSelf\",\"eventType\":\"DamageDealt\"}],"
            + "\"target\":{\"atom\":\"Self\"},"
            + "\"action\":{\"atom\":\"SwapAvatar\"}}";

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
        public void SwapAvatarWithCard_CenterAvatar_OuterMonster_SwapsPositionsAtomically()
        {
            // 夹具：Avatar 在中心（格 5），怪物在外圈（格 2）
            var avatar = CreateAvatarOnBoard(SlotId.Center, hp: 20, attack: 2);
            var monster = CreateMonsterOnBoard("monster.test.swappee", SlotId.Board(2), hp: 10, attack: 2);
            var board = mArch.GetModel<BoardModel>();

            Assert.AreEqual(SlotId.Center, board.AvatarSlot.Value);
            Assert.AreEqual(monster.Uid, board.GetCardUid(SlotId.Board(2)));
            Assert.AreEqual(0, board.GetCardUid(SlotId.Center));

            // 执行原子换位动作（通过目标格指定）
            Run(new SwapAvatarWithCardAction(SlotId.Board(2)));

            // 验收：一次原子动作同时改 Avatar 占格与目标卡占格，不重叠、不把人留在原格
            Assert.AreEqual(SlotId.Board(2), board.AvatarSlot.Value, "Avatar 应迁移至格 2");
            Assert.AreEqual(SlotId.Board(2), avatar.Slot.Value, "Avatar 卡实例 Slot 应为格 2");
            Assert.AreEqual(ZoneId.Avatar, avatar.Zone.Value, "Avatar 卡实例 Zone 仍为 Avatar");
            Assert.AreEqual(0, board.GetCardUid(SlotId.Board(2)), "Avatar 所在格 2 的场上卡 uid 应为 0（双轨不重叠）");

            Assert.AreEqual(monster.Uid, board.GetCardUid(SlotId.Center), "中心格 5 应被怪物占用");
            Assert.AreEqual(SlotId.Center, monster.Slot.Value, "怪物卡实例 Slot 应为格 5");
            Assert.AreEqual(ZoneId.Board, monster.Zone.Value, "怪物卡实例 Zone 应为 Board");

            // 验收事件流
            var movedTargetIndex = FirstIndexOfCardMoved(monster.Uid, SlotId.Board(2), SlotId.Center);
            var movedAvatarIndex = FirstIndexOfCardMoved(avatar.Uid, SlotId.Center, SlotId.Board(2));
            var avatarMovedIndex = FirstIndexOfAvatarMoved(avatar.Uid, SlotId.Center, SlotId.Board(2));
            var swappedIndex = FirstIndexOf(CoreEventType.CardSwapped);

            Assert.GreaterOrEqual(movedTargetIndex, 0, "应发出目标卡的盘面格→盘面格换格事件");
            Assert.GreaterOrEqual(movedAvatarIndex, 0, "应发出 Avatar 的 CardMoved 事件");
            Assert.GreaterOrEqual(avatarMovedIndex, 0, "应发出 AvatarMoved 事件");
            Assert.GreaterOrEqual(swappedIndex, 0, "应发出 CardSwapped 事件");
        }

        [Test]
        public void SwapAvatarWithCard_OuterAvatar_CenterMonster_SwapsPositionsAtomically()
        {
            // 夹具：Avatar 在外圈（格 1），怪物在中心（格 5）
            var avatar = CreateAvatarOnBoard(SlotId.Board(1), hp: 20, attack: 2);
            var monster = CreateMonsterOnBoard("monster.test.center_mon", SlotId.Center, hp: 10, attack: 2);
            var board = mArch.GetModel<BoardModel>();

            // 执行原子换位动作（通过目标卡 UID 指定）
            Run(new SwapAvatarWithCardAction(monster.Uid));

            Assert.AreEqual(SlotId.Center, board.AvatarSlot.Value, "Avatar 回到中心格 5");
            Assert.AreEqual(SlotId.Center, avatar.Slot.Value);
            Assert.AreEqual(0, board.GetCardUid(SlotId.Center));

            Assert.AreEqual(monster.Uid, board.GetCardUid(SlotId.Board(1)), "怪物换到外圈格 1");
            Assert.AreEqual(SlotId.Board(1), monster.Slot.Value);
        }

        [Test]
        public void SwapAvatarWithCard_OuterAvatar_OuterMonster_SwapsPositions()
        {
            // 夹具：Avatar 在外圈格 1，怪物在外圈格 9
            var avatar = CreateAvatarOnBoard(SlotId.Board(1), hp: 20, attack: 2);
            var monster = CreateMonsterOnBoard("monster.test.outer_mon", SlotId.Board(9), hp: 10, attack: 2);
            var board = mArch.GetModel<BoardModel>();

            Run(new SwapAvatarWithCardAction(SlotId.Board(9)));

            Assert.AreEqual(SlotId.Board(9), board.AvatarSlot.Value, "Avatar 应在格 9");
            Assert.AreEqual(0, board.GetCardUid(SlotId.Board(9)));

            Assert.AreEqual(monster.Uid, board.GetCardUid(SlotId.Board(1)), "怪物应在格 1");
            Assert.AreEqual(SlotId.Board(1), monster.Slot.Value);
        }

        [Test]
        public void SwapAvatarWithCard_TargetEmptySlot_MovesAvatarOnly()
        {
            // 夹具：Avatar 在外圈格 1，格 2 为空格
            var avatar = CreateAvatarOnBoard(SlotId.Board(1), hp: 20, attack: 2);
            var board = mArch.GetModel<BoardModel>();

            Assert.IsTrue(board.IsEmpty(SlotId.Board(2)));

            Run(new SwapAvatarWithCardAction(SlotId.Board(2)));

            Assert.AreEqual(SlotId.Board(2), board.AvatarSlot.Value, "Avatar 迁至空格 2");
            Assert.IsTrue(board.IsEmpty(SlotId.Board(1)), "原格 1 保持为空");
            Assert.IsTrue(board.IsEmpty(SlotId.Board(2)), "新格 2 无场上卡");
        }

        [Test]
        public void SwapAvatarWithCard_TargetCardWithMoveRhythm_TicksMoveCountdown()
        {
            // 验收：目标卡发出换格事件，移动源卡因此推进卡级移动计数；不给玩家发起的换位开节奏例外
            var avatar = CreateAvatarOnBoard(SlotId.Center, hp: 20, attack: 2);
            var monster = CreateMonsterOnBoard("monster.test.mover", SlotId.Board(2), hp: 10, attack: 2);
            monster.RhythmSource = CardRhythmSource.Move;
            monster.RhythmPeriod = 3;
            monster.AttackPattern = AttackPattern.OrthogonalMelee;
            monster.Counters.Set(CoreCounterKeys.AttackPatternCountdown, 3);

            Run(new SwapAvatarWithCardAction(SlotId.Board(2)));

            // 换位落地后移动计数应正常推进（3 -> 2）
            Assert.AreEqual(2, monster.Counters.Get(CoreCounterKeys.AttackPatternCountdown), "换位应推进目标卡的移动计数");
        }

        [Test]
        public void SwapAvatarWithCard_MoveCountdownReachingZero_FiresFromNewSlotInEnemyActionPhase()
        {
            // 验收：移动计数归零则按既有开火窗口从新格打 Avatar 当前格
            var avatar = CreateAvatarOnBoard(SlotId.Center, hp: 20, attack: 2);
            var monster = CreateMonsterOnBoard("monster.test.fire_mover", SlotId.Board(2), hp: 10, attack: 3);
            monster.RhythmSource = CardRhythmSource.Move;
            monster.RhythmPeriod = 2;
            monster.AttackPattern = AttackPattern.OrthogonalMelee;
            monster.Counters.Set(CoreCounterKeys.AttackPatternCountdown, 1);

            // 换位：Avatar (5) ↔ Monster (2)，Monster 移动计数从 1 归零
            Run(new SwapAvatarWithCardAction(SlotId.Board(2)));

            Assert.AreEqual(0, monster.Counters.Get(CoreCounterKeys.AttackPatternCountdown), "换位使移动计数归零");
            Assert.AreEqual(SlotId.Center, monster.Slot.Value, "怪物已换入中心格 5");
            Assert.AreEqual(SlotId.Board(2), mArch.GetModel<BoardModel>().AvatarSlot.Value, "Avatar 已换入外圈格 2");

            // 推进敌方行动阶段：怪物从新格（格 5）打 Avatar 当前格（格 2，正交相距 1）
            RunFullEnemyActionPhase(mArch.GetSystem<IPhaseSystem>());

            Assert.AreEqual(17, (int)avatar.Stats.GetBase(StatId.Hp), "怪物应从新格 5 命中 Avatar（20 - 3 = 17）");
            Assert.AreEqual(2, monster.Counters.Get(CoreCounterKeys.AttackPatternCountdown), "开火后倒计时重置为周期 2");
        }

        [Test]
        public void SwapAvatarWithCard_PresentationStepProjection_CreatesCrossSwapCompatibleStep()
        {
            // 验收：表现复用已有交叉换位/置换提交；目标格有卡时不得走 Avatar hop
            var avatar = CreateAvatarOnBoard(SlotId.Center, hp: 20, attack: 2);
            var monster = CreateMonsterOnBoard("monster.test.swappee", SlotId.Board(2), hp: 10, attack: 2);
            var registry = mArch.GetModel<CardRegistry>();

            Run(new SwapAvatarWithCardAction(SlotId.Board(2)));

            var projection = BoardPresentationStepProjector.Project(Events(), 0, registry);

            Assert.AreEqual(1, projection.Steps.Length, "应产出 1 个盘面表现 Step");
            var step = projection.Steps[0];
            Assert.AreEqual(BoardPresentationStepKind.Swap, step.Kind, "步骤类型应为 Swap");
            Assert.AreEqual(2, step.Moves.Length, "Swap 步骤应包含 2 个移动元素（人和卡）");

            var moveTarget = step.Moves[0].Uid == monster.Uid ? step.Moves[0] : step.Moves[1];
            var moveAvatar = step.Moves[0].Uid == avatar.Uid ? step.Moves[0] : step.Moves[1];

            Assert.AreEqual(monster.Uid, moveTarget.Uid);
            Assert.AreEqual(2, moveTarget.FromSlot);
            Assert.AreEqual(5, moveTarget.ToSlot);

            Assert.AreEqual(avatar.Uid, moveAvatar.Uid);
            Assert.AreEqual(5, moveAvatar.FromSlot);
            Assert.AreEqual(2, moveAvatar.ToSlot);

            // 验证满足 TryGetCrossSwapPair 条件（对称起点与终点）
            Assert.AreEqual(moveTarget.FromSlot, moveAvatar.ToSlot);
            Assert.AreEqual(moveTarget.ToSlot, moveAvatar.FromSlot);
        }

        [Test]
        public void SwapAvatarWithCard_InBattleScope_DefersUntilInteractionAdvance()
        {
            // 验收：位移若落在交战窗或道具链窗口，遵守既有位移挂起与收尾落地
            var avatar = CreateAvatarOnBoard(SlotId.Center, hp: 10, attack: 1);
            var monster = CreateMonsterOnBoard("monster.test.swap_reactor", SlotId.Board(4), hp: 5, attack: 2);
            ActivateEffect(SwapAvatarOnBattleJson, monster.Uid);

            var board = mArch.GetModel<BoardModel>();

            // Avatar 击打怪物：OnBattle 触发的 SwapAvatar 应在交战窗内挂起
            Hit(avatar, monster);
            Assert.AreEqual(SlotId.Center, board.AvatarSlot.Value, "交战命中批内换位被挂起，Avatar 仍在原格");
            Assert.AreEqual(SlotId.Board(4), monster.Slot.Value, "交战命中批内换位被挂起，怪物仍在原格");

            // 怪物反击按原位结算
            Hit(monster, avatar);
            Assert.AreEqual(8, (int)avatar.Stats.GetBase(StatId.Hp), "反击按原位正交成立（10 - 2 = 8）");

            // 推进互动（挂起位移落地锚点）
            mArch.GetSystem<IPhaseSystem>().AdvanceInteractionCount();

            Assert.AreEqual(SlotId.Board(4), board.AvatarSlot.Value, "收尾锚点落地后 Avatar 应在格 4");
            Assert.AreEqual(SlotId.Center, monster.Slot.Value, "收尾锚点落地后怪物应在格 5");
        }

        [Test]
        public void SwapAvatarWithCard_InBattleScope_LethalHit_DropsDeferredSwapSilently()
        {
            // 验收：挂起期间目标卡死亡/离场，换位失效静默丢弃（ADR-0044 §4）
            var avatar = CreateAvatarOnBoard(SlotId.Center, hp: 10, attack: 10);
            var monster = CreateMonsterOnBoard("monster.test.swap_reactor", SlotId.Board(4), hp: 1, attack: 2);
            ActivateEffect(SwapAvatarOnBattleJson, monster.Uid);

            var board = mArch.GetModel<BoardModel>();

            // 致命一击：怪物死亡
            Hit(avatar, monster);
            mArch.GetSystem<IPhaseSystem>().AdvanceInteractionCount();

            Assert.AreEqual(SlotId.Center, board.AvatarSlot.Value, "怪物死亡后换位应静默丢弃，Avatar 保持在格 5");
            Assert.AreEqual(-1, FirstIndexOf(CoreEventType.CardSwapped), "不应发出 CardSwapped 事件");
        }

        private void RunFullEnemyActionPhase(IPhaseSystem phase)
        {
            phase.RegisterEnemyActionPhase();
            var guard = 0;
            while (phase.PendingEnemyActionUids != null
                && phase.PendingEnemyActionUids.Count > 0
                && guard++ < 8)
            {
                phase.ResolveNextEnemyAction();
            }

            phase.ResolveEnemyActionFinale();
        }

        private CardInstance CreateAvatarOnBoard(SlotId slot, int hp, int attack)
        {
            var registry = mArch.GetModel<CardRegistry>();
            var avatar = registry.Create("avatar.default", CardKind.Avatar);
            avatar.FaceUp = true;
            avatar.Stats.SetBase(StatId.MaxHp, hp);
            avatar.Stats.SetBase(StatId.Hp, hp);
            avatar.Stats.SetBase(StatId.Attack, attack);
            mArch.GetModel<BoardModel>().SetAvatar(avatar, slot);
            return avatar;
        }

        private CardInstance CreateMonsterOnBoard(string defId, SlotId slot, int hp, int attack)
        {
            var registry = mArch.GetModel<CardRegistry>();
            var monster = registry.Create(defId, CardKind.Monster);
            monster.FaceUp = true;
            monster.Stats.SetBase(StatId.MaxHp, hp);
            monster.Stats.SetBase(StatId.Hp, hp);
            monster.Stats.SetBase(StatId.Attack, attack);
            mArch.GetModel<BoardModel>().PlaceCard(monster, slot);
            return monster;
        }

        private void ActivateEffect(string bodyJson, int ownerUid)
        {
            var effectSystem = mArch.GetSystem<IEffectSystem>();
            var definition = effectSystem.ParseJson(bodyJson);
            effectSystem.Activate(
                definition,
                new EffectOwner(EffectContainerType.MonsterSkill, "test.deferred_motion", ownerUid));
        }

        private void Hit(CardInstance attacker, CardInstance target)
        {
            var result = mArch.GetSystem<IPhaseSystem>().ApplyCombatHit(attacker.Uid, target.Uid);
            Assert.IsTrue(result.Accepted, "ApplyCombatHit 应被接受");
        }

        private void Run(GameAction action)
        {
            mArch.GetSystem<IActionPipelineSystem>().Execute(action);
        }

        private IReadOnlyList<CoreGameEvent> Events()
        {
            return mArch.GetSystem<IActionPipelineSystem>().EventLog.Entries;
        }

        private int FirstIndexOf(CoreEventType type)
        {
            var entries = Events();
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].Type == type)
                {
                    return i;
                }
            }

            return -1;
        }

        private int FirstIndexOfCardMoved(int cardUid, SlotId from, SlotId to)
        {
            var entries = Events();
            for (var i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Type == CoreEventType.CardMoved
                    && e.CardUid == cardUid
                    && e.FromSlot == from
                    && e.ToSlot == to)
                {
                    return i;
                }
            }

            return -1;
        }

        private int FirstIndexOfAvatarMoved(int cardUid, SlotId from, SlotId to)
        {
            var entries = Events();
            for (var i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Type == CoreEventType.AvatarMoved
                    && e.CardUid == cardUid
                    && e.FromSlot == from
                    && e.ToSlot == to)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}

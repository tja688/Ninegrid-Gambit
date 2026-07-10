using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// CombatHit 死亡回归模板。
    /// 样板：分段 ApplyCombatHit（玩家攻未击杀 → 怪反击）可把 Avatar 打进 Defeat。
    ///
    /// 如何把真实死亡 Trace JSON 落成下一条测试：
    /// 1) Play 里复现死亡，Console 取 BattleTrace 路径；
    /// 2) 读末条 reason=CounterAttack 的 Op：seed、attacker/target defId、before hp/atk、DamageDealt.amount；
    /// 3) 用同 seed + CreateSingleMonsterNode(hp, attack) + 必要时 SetBase 降 Avatar 血，复现 ApplyCombatHit 序列；
    /// 4) 断言 HpChanged RemainingHp≤0 与/或 PhaseChanged→Defeat，以及期望 Amount。
    /// </summary>
    public sealed class CombatHitDeathRegressionTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 42UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void ApplyCombatHit_Counter_CanDefeatAvatar()
        {
            // 高攻怪 + 低血 Avatar：玩家一击未杀 → 怪反击致死。
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 20, attack: 99)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var avatarUid = board.AvatarUid.Value;
            var monsterUid = board.GetCardUid(sAdjacentSlot);
            Assert.Greater(avatarUid, 0);
            Assert.Greater(monsterUid, 0);

            var avatar = registry.Get(avatarUid);
            avatar.Stats.SetBase(StatId.MaxHp, 3);
            avatar.Stats.SetBase(StatId.Hp, 3);
            avatar.Stats.SetBase(StatId.Armor, 0);

            var playerHit = mPhase.ApplyCombatHit(avatarUid, monsterUid);
            Assert.IsTrue(playerHit.Accepted, playerHit.Reason);
            Assert.AreNotEqual(GamePhase.Defeat, mPhase.CurrentPhase, "玩家一击不应直接 Defeat");
            Assert.Greater(
                mArch.GetSystem<IStatSystem>().GetEffectiveInt(registry.Get(monsterUid), StatId.Hp),
                0,
                "样板要求怪未被击杀，才能进入反击");

            var startIndex = mPipeline.EventLog.Entries.Count;
            var counterHit = mPhase.ApplyCombatHit(monsterUid, avatarUid);
            Assert.IsTrue(counterHit.Accepted, counterHit.Reason);

            var events = SliceEvents(startIndex);
            Assert.IsTrue(
                HasAvatarHpDepleted(events, avatarUid) || mPhase.CurrentPhase == GamePhase.Defeat,
                "期望反击后 Avatar Hp≤0 或进入 Defeat");
            Assert.AreEqual(GamePhase.Defeat, mPhase.CurrentPhase);
        }

        [Test]
        public void Attack_SameSetup_DocumentsPathDifference()
        {
            // 对照：同初始态走 Attack(slot) 整段路径（含门禁/可能的内建反击语义）。
            // 与分段 ApplyCombatHit 的差异靠人工读 Trace JSON；此处至少跑通并记录相位。
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 20, attack: 99)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var avatarUid = board.AvatarUid.Value;
            var avatar = registry.Get(avatarUid);
            avatar.Stats.SetBase(StatId.MaxHp, 3);
            avatar.Stats.SetBase(StatId.Hp, 3);
            avatar.Stats.SetBase(StatId.Armor, 0);

            var startIndex = mPipeline.EventLog.Entries.Count;
            var result = mPhase.Attack(sAdjacentSlot);
            Assert.IsTrue(result.Accepted, result.Reason);

            var events = SliceEvents(startIndex);
            var phaseAfter = mPhase.CurrentPhase;

            // 模板骨架：允许先绿；后续用真实 Trace 填期望 Amount / 是否 Defeat。
            Assert.IsTrue(events.Count > 0, "Attack 应产生 EventLog 切片");
            Assert.IsTrue(
                ContainsType(events, CoreEventType.DamageDealt)
                || ContainsType(events, CoreEventType.HpChanged)
                || phaseAfter == GamePhase.Defeat
                || phaseAfter == GamePhase.InteractionLoop,
                "Attack 路径应留下伤害或相位变化痕迹；phase=" + phaseAfter);

            // 期望差异（人工对照用，不强制断言）：
            // - 分段 CombatHit：PlayerAttack 未杀 + CounterAttack → Defeat
            // - Attack(slot)：可能一次命令内含反击，或门禁/旋转顺序不同
            Assert.Pass(
                "Attack 对照已跑通 phase=" + phaseAfter
                + " events=" + events.Count
                + "；用 BattleTrace JSON 对比分段路径。");
        }

        private static NodeDeckOptions CreateSingleMonsterNode(int hp, int attack)
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1
            }.AddEnemyCard(new CardDraft("monster.test", CardKind.Monster) { MaxHp = hp, Attack = attack });
        }

        private void PlaceSoleBoardCardAt(SlotId targetSlot)
        {
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            CardInstance sole = null;
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                if (slot == board.AvatarSlot.Value)
                {
                    continue;
                }

                var uid = board.GetCardUid(slot);
                if (uid == 0)
                {
                    continue;
                }

                Assert.IsNull(sole, "Expected at most one non-avatar board card for relocate helper.");
                sole = registry.Get(uid);
            }

            Assert.IsNotNull(sole, "No board card to relocate.");
            if (sole.Slot.Value == targetSlot)
            {
                return;
            }

            board.ClearSlot(sole.Slot.Value);
            board.PlaceCard(sole, targetSlot);
        }

        private IReadOnlyList<CoreGameEvent> SliceEvents(int startIndex)
        {
            var entries = mPipeline.EventLog.Entries;
            var list = new List<CoreGameEvent>(entries.Count - startIndex);
            for (var i = startIndex; i < entries.Count; i++)
            {
                list.Add(entries[i]);
            }

            return list;
        }

        private static bool HasAvatarHpDepleted(IReadOnlyList<CoreGameEvent> events, int avatarUid)
        {
            for (var i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if ((e.Type == CoreEventType.HpChanged || e.Type == CoreEventType.DamageDealt)
                    && e.TargetUid == avatarUid
                    && e.RemainingHp <= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsType(IReadOnlyList<CoreGameEvent> events, CoreEventType type)
        {
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].Type == type)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

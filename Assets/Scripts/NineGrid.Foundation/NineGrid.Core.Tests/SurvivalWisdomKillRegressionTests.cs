using System.Collections.Generic;
using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// 生存智慧：致命伤后不得回血锁 1 血；非致命仍回血；战斗攻加成 UntilBattleEnds。
    /// </summary>
    public sealed class SurvivalWisdomKillRegressionTests
    {
        private static readonly SlotId sPrimarySlot = SlotId.Board(2);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;
        private IStatSystem mStats;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, TableNineContentCatalog.CreateDefault());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 17UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
            mStats = mArch.GetSystem<IStatSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void OldOrc_AtOneHp_LethalHit_IsKilled_NotHealedBack()
        {
            Assert.IsTrue(mPhase.StartNode(CreateOldOrcNode(hp: 1)).Accepted);
            var monsterUid = PlaceSoleBoardCardAt(sPrimarySlot);
            PrepareAvatarAttack(5);

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var startIndex = mPipeline.EventLog.Entries.Count;
            var hit = mPhase.ApplyCombatHit(board.AvatarUid.Value, monsterUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);

            var monster = registry.Get(monsterUid);
            Assert.AreEqual(ZoneId.Graveyard, monster.Zone.Value, "1 血致命一击应击杀，不得被生存智慧回至 1 血");
            Assert.IsTrue(
                ContainsTypeSince(startIndex, CoreEventType.CardKilled),
                "EventLog 应有 CardKilled");
            Assert.IsFalse(
                ContainsHealedForSince(startIndex, monsterUid),
                "致命一击不应触发 Healed");
        }

        [Test]
        public void OldOrc_NonLethalHit_StillHealsOnce()
        {
            Assert.IsTrue(mPhase.StartNode(CreateOldOrcNode(hp: 3)).Accepted);
            var monsterUid = PlaceSoleBoardCardAt(sPrimarySlot);
            PrepareAvatarAttack(1);

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var startIndex = mPipeline.EventLog.Entries.Count;
            var hit = mPhase.ApplyCombatHit(board.AvatarUid.Value, monsterUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);

            Assert.IsTrue(
                ContainsHealedForSince(startIndex, monsterUid),
                "非致命命中仍应触发生存智慧回血");
            Assert.AreEqual(
                3,
                (int)registry.Get(monsterUid).Stats.GetBase(StatId.Hp),
                "3→2 伤后应回至 3 血");
        }

        [Test]
        public void OldOrc_AttackBonus_ClearedAfterBattleEnds()
        {
            Assert.IsTrue(mPhase.StartNode(CreateOldOrcNode(hp: 9)).Accepted);
            var monsterUid = PlaceSoleBoardCardAt(sPrimarySlot);
            PrepareAvatarAttack(1);

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var avatarUid = board.AvatarUid.Value;

            mPipeline.Enqueue(new BeginPlayerMonsterEngagementAction(monsterUid));
            mPipeline.Enqueue(new DealDamageAction(avatarUid, monsterUid, 1));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.AreEqual(
                2,
                mStats.GetEffectiveInt(registry.Get(monsterUid), StatId.Attack),
                "战斗内应 +1 攻（基础 1 + 临时 1）");

            mPipeline.Enqueue(new EndBattleScopeCleanupAction());
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.AreEqual(
                1,
                mStats.GetEffectiveInt(registry.Get(monsterUid), StatId.Attack),
                "EndBattleScopeCleanup 应清掉 UntilBattleEnds +1 攻");
        }

        private static NodeDeckOptions CreateOldOrcNode(int hp)
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1
            }.AddEnemyCard(new CardDraft("monster.old_orc", CardKind.Monster)
            {
                MaxHp = 9,
                Hp = hp,
                Attack = 1
            });
        }

        private void PrepareAvatarAttack(int attack)
        {
            var board = mArch.GetModel<BoardModel>();
            var avatar = mArch.GetModel<CardRegistry>().Get(board.AvatarUid.Value);
            avatar.Stats.SetBase(StatId.MaxHp, 99);
            avatar.Stats.SetBase(StatId.Hp, 99);
            avatar.Stats.SetBase(StatId.Armor, 0);
            avatar.Stats.SetBase(StatId.Attack, attack);
        }

        private int PlaceSoleBoardCardAt(SlotId targetSlot)
        {
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
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

                var card = registry.Get(uid);
                if (card.Slot.Value == targetSlot)
                {
                    return uid;
                }

                board.ClearSlot(card.Slot.Value);
                board.PlaceCard(card, targetSlot);
                return uid;
            }

            Assert.Fail("No board card to relocate.");
            return 0;
        }

        private bool ContainsTypeSince(int startIndex, CoreEventType type)
        {
            var entries = mPipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                if (entries[i].Type == type)
                {
                    return true;
                }
            }

            return false;
        }

        private bool ContainsHealedForSince(int startIndex, int targetUid)
        {
            var entries = mPipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Type == CoreEventType.Healed && entry.TargetUid == targetUid && entry.Delta > 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

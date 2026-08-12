using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Effects;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 结算窗口位移挂起回归（ADR-0044）。
    /// 锁死的 bug 形态：OnBattle 触发的旋转/换位经反应栈插在「玩家命中」与「怪物反击」
    /// 之间真实转格，而反击不验几何——正交怪被转到斜角后仍反击（斜角打人）；
    /// 击杀落地（KillIfDead FollowUp）前尸体跟环转一格；齐射中途旋转破坏盘面冻结、
    /// 让名单后续怪整窗作废。
    /// 正确语义：交战窗 / 敌方行动阶段内效果 DSL 的盘面位移挂起到收尾锚点
    /// （互动计数推进 / 齐射收尾）就地复验后统一落地——先打再转。
    /// </summary>
    public class DeferredBoardMotionRegressionTests
    {
        private const string RotateOnBattleJson =
            "{\"id\":\"test.deferred_motion.rotate_battle\",\"kind\":\"Triggered\",\"containerType\":\"MonsterSkill\","
            + "\"requires\":[\"HasOwnerEntity\",\"CardZoneTriggerable\"],"
            + "\"trigger\":{\"atom\":\"OnBattle\",\"sourceAction\":\"DealDamage\",\"targetKind\":\"Monster\",\"maxActionDepth\":0},"
            + "\"conditions\":[{\"atom\":\"EventFilterActorIsPlayerTargetIsSelf\",\"eventType\":\"DamageDealt\"}],"
            + "\"target\":{\"atom\":\"Self\"},"
            + "\"action\":{\"atom\":\"Rotate\",\"count\":1}}";

        private const string SwapOnBattleJson =
            "{\"id\":\"test.deferred_motion.swap_battle\",\"kind\":\"Triggered\",\"containerType\":\"MonsterSkill\","
            + "\"requires\":[\"HasOwnerEntity\",\"CardZoneTriggerable\"],"
            + "\"trigger\":{\"atom\":\"OnBattle\",\"sourceAction\":\"DealDamage\",\"targetKind\":\"Monster\",\"maxActionDepth\":0},"
            + "\"conditions\":[{\"atom\":\"EventFilterActorIsPlayerTargetIsSelf\",\"eventType\":\"DamageDealt\"}],"
            + "\"target\":{\"atom\":\"Self\"},"
            + "\"action\":{\"atom\":\"Swap\",\"leftSlot\":4,\"rightSlot\":1}}";

        private const string RotateOnRhythmFireJson =
            "{\"id\":\"test.deferred_motion.rotate_fire\",\"kind\":\"Triggered\",\"containerType\":\"MonsterSkill\","
            + "\"requires\":[\"HasOwnerEntity\",\"CardZoneTriggerable\"],"
            + "\"trigger\":{\"atom\":\"OnCardRhythmFire\"},"
            + "\"target\":{\"atom\":\"Self\"},"
            + "\"action\":{\"atom\":\"Rotate\",\"count\":1}}";

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
        public void RotateOnBattle_CounterResolvesBeforeRotation()
        {
            var avatar = CreateAvatarOnBoard(hp: 10, attack: 1);
            var monster = CreateMonsterOnBoard("monster.test.space_mastery", 4, hp: 5, attack: 2);
            ActivateEffect(RotateOnBattleJson, monster.Uid);

            // 导演路径：命中批 → 反击批 → 互动计数（挂起位移的落地锚点）。
            Hit(avatar, monster);
            Assert.AreEqual(
                SlotId.Board(4),
                monster.Slot.Value,
                "命中批内旋转应被挂起，怪仍在原格（盘面未转）");

            Hit(monster, avatar);
            Assert.AreEqual(8, (int)avatar.Stats.GetBase(StatId.Hp), "反击应按原位成立（2 伤）");

            mArch.GetSystem<IPhaseSystem>().AdvanceInteractionCount();

            var counterIndex = LastIndexOfDamage(actorUid: monster.Uid, targetUid: avatar.Uid);
            var rotateIndex = FirstIndexOf(CoreEventType.BoardRotated);
            Assert.GreaterOrEqual(counterIndex, 0, "应存在怪物反击伤害事件");
            Assert.GreaterOrEqual(rotateIndex, 0, "挂起的旋转应在互动计数锚点落地");
            Assert.Greater(rotateIndex, counterIndex, "旋转必须晚于反击结算（先打再转，ADR-0044）");
            Assert.AreEqual(
                SlotId.Board(1),
                monster.Slot.Value,
                "落地后盘面应完成一次顺时针旋转（4→1）");
        }

        [Test]
        public void RotateOnBattle_LethalHit_CorpseDoesNotRideRotation()
        {
            var avatar = CreateAvatarOnBoard(hp: 10, attack: 5);
            var monster = CreateMonsterOnBoard("monster.test.space_mastery", 4, hp: 1, attack: 2);
            ActivateEffect(RotateOnBattleJson, monster.Uid);

            Hit(avatar, monster);
            mArch.GetSystem<IPhaseSystem>().AdvanceInteractionCount();

            var killIndex = FirstIndexOf(CoreEventType.CardKilled);
            var rotateIndex = FirstIndexOf(CoreEventType.BoardRotated);
            Assert.GreaterOrEqual(killIndex, 0, "致死命中应产生 CardKilled");
            Assert.GreaterOrEqual(rotateIndex, 0, "挂起的旋转应在锚点落地");
            Assert.Greater(rotateIndex, killIndex, "旋转必须晚于击杀落地");
            Assert.AreEqual(
                0,
                CountBoardRingMoves(monster.Uid),
                "尸体不得跟环转格（击杀先落地、旋转后行，Issue #207 交战路径）");
        }

        [Test]
        public void SwapOnBattle_DefersUntilAfterCounter()
        {
            var avatar = CreateAvatarOnBoard(hp: 10, attack: 1);
            var monster = CreateMonsterOnBoard("monster.test.evade", 4, hp: 5, attack: 2);
            ActivateEffect(SwapOnBattleJson, monster.Uid);

            Hit(avatar, monster);
            Assert.AreEqual(SlotId.Board(4), monster.Slot.Value, "命中批内换位应被挂起");

            Hit(monster, avatar);
            Assert.AreEqual(8, (int)avatar.Stats.GetBase(StatId.Hp), "反击应按原位成立");

            mArch.GetSystem<IPhaseSystem>().AdvanceInteractionCount();

            var counterIndex = LastIndexOfDamage(actorUid: monster.Uid, targetUid: avatar.Uid);
            var swapIndex = FirstIndexOf(CoreEventType.CardSwapped);
            Assert.GreaterOrEqual(swapIndex, 0, "挂起的换位应在互动计数锚点落地");
            Assert.Greater(swapIndex, counterIndex, "换位必须晚于反击结算");
            Assert.AreEqual(SlotId.Board(1), monster.Slot.Value, "落地后完成 4↔1 换位");
        }

        [Test]
        public void RotateOnRhythmFire_VolleyStaysFrozen_RotationLandsAtFinale()
        {
            var avatar = CreateAvatarOnBoard(hp: 20, attack: 1);

            // M1（先创建，uid 较小，名单在前）：模式=无 + 开火同拍旋转技能。
            var spinner = CreateMonsterOnBoard("monster.test.tide_heart", 4, hp: 5, attack: 0);
            spinner.AttackPattern = AttackPattern.None;
            spinner.HasSyncRhythmSkills = true;
            spinner.RhythmSource = CardRhythmSource.Action;
            spinner.RhythmPeriod = 1;
            ActivateEffect(RotateOnRhythmFireJson, spinner.Uid);

            // M2：普通近战（正交），本拍应在冻结盘面上正常开火。
            var striker = CreateMonsterOnBoard("monster.test.striker", 6, hp: 5, attack: 3);
            striker.AttackPattern = AttackPattern.OrthogonalMelee;
            striker.RhythmSource = CardRhythmSource.Action;
            striker.RhythmPeriod = 1;

            var phase = mArch.GetSystem<IPhaseSystem>();
            phase.RegisterEnemyActionPhase();
            var guard = 0;
            while (phase.PendingEnemyActionUids != null
                && phase.PendingEnemyActionUids.Count > 0
                && guard++ < 8)
            {
                phase.ResolveNextEnemyAction();
            }

            Assert.AreEqual(
                17,
                (int)avatar.Stats.GetBase(StatId.Hp),
                "名单后续怪应在冻结盘面上正常开火（不得因中途旋转 voidPosition）");
            Assert.AreEqual(-1, FirstIndexOf(CoreEventType.BoardRotated), "收尾前旋转不得落地（盘面冻结）");

            phase.ResolveEnemyActionFinale();

            var strikeIndex = LastIndexOfDamage(actorUid: striker.Uid, targetUid: avatar.Uid);
            var rotateIndex = FirstIndexOf(CoreEventType.BoardRotated);
            Assert.GreaterOrEqual(rotateIndex, 0, "挂起的旋转应在齐射收尾落地");
            Assert.Greater(rotateIndex, strikeIndex, "旋转必须晚于全部单向打击");
            Assert.AreEqual(SlotId.Board(1), spinner.Slot.Value, "落地后 4→1");
            Assert.AreEqual(SlotId.Board(9), striker.Slot.Value, "落地后 6→9");
        }

        [Test]
        public void DeferredSwap_PartnerLeftBoard_IsDroppedSilently()
        {
            var avatar = CreateAvatarOnBoard(hp: 10, attack: 1);
            var monster = CreateMonsterOnBoard("monster.test.evade", 4, hp: 5, attack: 0);
            var bystander = CreateMonsterOnBoard("monster.test.bystander", 1, hp: 5, attack: 0);
            ActivateEffect(SwapOnBattleJson, monster.Uid);

            Hit(avatar, monster);

            // 挂起期间换位对象离场（模拟窗口内被其它效果移除）。
            var pipeline = mArch.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new KillAction(avatar.Uid, bystander.Uid));
            pipeline.RunToCompletion();

            mArch.GetSystem<IPhaseSystem>().AdvanceInteractionCount();

            Assert.AreEqual(-1, FirstIndexOf(CoreEventType.CardSwapped), "占位快照失配应静默丢弃换位");
            Assert.AreEqual(SlotId.Board(4), monster.Slot.Value, "换位被丢弃，怪保持原格");
        }

        private CardInstance CreateAvatarOnBoard(int hp, int attack)
        {
            var registry = mArch.GetModel<CardRegistry>();
            var avatar = registry.Create("avatar.default", CardKind.Avatar);
            avatar.Stats.SetBase(StatId.MaxHp, hp);
            avatar.Stats.SetBase(StatId.Hp, hp);
            avatar.Stats.SetBase(StatId.Attack, attack);
            mArch.GetModel<BoardModel>().SetAvatar(avatar, SlotId.Board(5));
            return avatar;
        }

        private CardInstance CreateMonsterOnBoard(string defId, int slotIndex, int hp, int attack)
        {
            var registry = mArch.GetModel<CardRegistry>();
            var monster = registry.Create(defId, CardKind.Monster);
            monster.Stats.SetBase(StatId.MaxHp, hp);
            monster.Stats.SetBase(StatId.Hp, hp);
            monster.Stats.SetBase(StatId.Attack, attack);
            mArch.GetModel<BoardModel>().PlaceCard(monster, SlotId.Board(slotIndex));
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

        private int LastIndexOfDamage(int actorUid, int targetUid)
        {
            var entries = Events();
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i].Type == CoreEventType.DamageDealt
                    && entries[i].ActorUid == actorUid
                    && entries[i].TargetUid == targetUid)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>统计某 uid 的「盘面格→盘面格」移动事件（环移/换位），排除入场与离场。</summary>
        private int CountBoardRingMoves(int cardUid)
        {
            var entries = Events();
            var count = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Type == CoreEventType.CardMoved
                    && e.CardUid == cardUid
                    && e.FromSlot.IsBoardSlot
                    && e.ToSlot.IsBoardSlot)
                {
                    count++;
                }
            }

            return count;
        }
    }
}

using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// #76 / ADR-0013：效果侧「每 N 次」统一为行动倒计时；OnInteract 补 every；
    /// 攻击模式计数前缀与效果前缀隔离；无干预时拍序与旧模运算/阈值语义等价。
    /// Seam：IEffectSystem.Activate + MoveCard / 掉甲 / AdvanceInteractionCount；Counters 可观测。
    /// </summary>
    public sealed class ActionCountdownSemanticsTests
    {
        private const string SelfMoveEvery4Json =
            "{\"id\":\"test.countdown.self_move\",\"typeTag\":\"【类型怪物技能】\",\"containerType\":\"MonsterSkill\","
            + "\"kind\":\"Triggered\","
            + "\"requires\":[\"HasOwnerEntity\",\"CardZoneTriggerable\"],"
            + "\"trigger\":{\"atom\":\"OnSelfMove\",\"every\":4,\"counterKey\":\"test.selfMove.cd\"},"
            + "\"target\":{\"atom\":\"Self\"},"
            + "\"action\":{\"atom\":\"ModifyBaseStat\",\"stat\":\"Attack\",\"delta\":1,\"reason\":\"test.countdown.self_move\"}}";

        private const string SelfMoveEvery1Json =
            "{\"id\":\"test.countdown.self_move1\",\"typeTag\":\"【类型怪物技能】\",\"containerType\":\"MonsterSkill\","
            + "\"kind\":\"Triggered\","
            + "\"requires\":[\"HasOwnerEntity\",\"CardZoneTriggerable\"],"
            + "\"trigger\":{\"atom\":\"OnSelfMove\",\"every\":1},"
            + "\"target\":{\"atom\":\"Self\"},"
            + "\"action\":{\"atom\":\"ModifyBaseStat\",\"stat\":\"Attack\",\"delta\":1,\"reason\":\"test.countdown.self_move1\"}}";

        private const string ArmorLostThreshold3Json =
            "{\"id\":\"test.countdown.cumulative\",\"typeTag\":\"【类型怪物技能】\",\"containerType\":\"MonsterSkill\","
            + "\"kind\":\"Triggered\","
            + "\"requires\":[\"HasOwnerEntity\",\"CardZoneTriggerable\"],"
            + "\"trigger\":{\"atom\":\"OnSelfArmorLostCumulative\",\"threshold\":3,\"counterKey\":\"test.cumulative.cd\"},"
            + "\"target\":{\"atom\":\"Self\"},"
            + "\"action\":{\"atom\":\"ModifyBaseStat\",\"stat\":\"Attack\",\"delta\":1,\"reason\":\"test.countdown.cumulative\"}}";

        private const string OnInteractEvery3Json =
            "{\"id\":\"test.countdown.interact\",\"typeTag\":\"【类型怪物技能】\",\"containerType\":\"MonsterSkill\","
            + "\"kind\":\"Triggered\","
            + "\"requires\":[\"HasOwnerEntity\",\"CardZoneTriggerable\"],"
            + "\"trigger\":{\"atom\":\"OnInteract\",\"every\":3,\"counterKey\":\"test.interact.cd\"},"
            + "\"target\":{\"atom\":\"Self\"},"
            + "\"action\":{\"atom\":\"ModifyBaseStat\",\"stat\":\"Attack\",\"delta\":1,\"reason\":\"test.countdown.interact\"}}";

        private const string OnInteractEvery0Json =
            "{\"id\":\"test.countdown.interact0\",\"typeTag\":\"【类型怪物技能】\",\"containerType\":\"MonsterSkill\","
            + "\"kind\":\"Triggered\","
            + "\"requires\":[\"HasOwnerEntity\",\"CardZoneTriggerable\"],"
            + "\"trigger\":{\"atom\":\"OnInteract\",\"every\":0},"
            + "\"target\":{\"atom\":\"Self\"},"
            + "\"action\":{\"atom\":\"ModifyBaseStat\",\"stat\":\"Attack\",\"delta\":1,\"reason\":\"test.countdown.interact0\"}}";

        private static readonly SlotId sOwnerSlot = SlotId.Board(2);
        private static readonly SlotId sMoveTargetSlot = SlotId.Board(3);
        private static readonly SlotId sAltSlot = SlotId.Board(1);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;
        private IEffectSystem mEffects;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, ContentCatalogBootstrap.Load());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 76UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
            mEffects = mArch.GetSystem<IEffectSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void AttackPatternCounterPrefix_IsIsolatedFromEffectPrefix()
        {
            Assert.IsFalse(
                string.IsNullOrEmpty(CoreCounterKeys.AttackPatternPrefix),
                "攻击模式须有保留前缀");
            Assert.IsFalse(
                CoreCounterKeys.AttackPatternPrefix.StartsWith(CoreCounterKeys.EffectCounterPrefix),
                "攻击模式前缀不得落在 effect. 之下");
            Assert.IsFalse(
                CoreCounterKeys.EffectCounterPrefix.StartsWith(CoreCounterKeys.AttackPatternPrefix),
                "效果前缀不得落在攻击模式前缀之下");
            Assert.AreNotEqual(CoreCounterKeys.AttackPatternPrefix, CoreCounterKeys.EffectCounterPrefix);
        }

        [Test]
        public void OnSelfMove_Every4_TriggersOn4th8th_WithoutIntervention()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            var ownerUid = SpawnOnBoardReturnUid("monster.big_skeleton", sOwnerSlot);
            ActivateEffect(SelfMoveEvery4Json, ownerUid, "test.countdown.self_move");
            var registry = mArch.GetModel<CardRegistry>();
            var atk0 = ReadAttack(ownerUid);

            MoveOwnerPingPong(ownerUid, 1);
            Assert.AreEqual(atk0, ReadAttack(ownerUid), "第 1 次移动不触发");
            Assert.AreEqual(3, registry.Get(ownerUid).Counters.Get("test.selfMove.cd"), "倒计时剩余 3");

            MoveOwnerPingPong(ownerUid, 1);
            Assert.AreEqual(atk0, ReadAttack(ownerUid), "第 2 次移动不触发");
            Assert.AreEqual(2, registry.Get(ownerUid).Counters.Get("test.selfMove.cd"));

            MoveOwnerPingPong(ownerUid, 1);
            Assert.AreEqual(atk0, ReadAttack(ownerUid), "第 3 次移动不触发");
            Assert.AreEqual(1, registry.Get(ownerUid).Counters.Get("test.selfMove.cd"));

            MoveOwnerPingPong(ownerUid, 1);
            Assert.AreEqual(atk0 + 1, ReadAttack(ownerUid), "第 4 次移动触发（与旧模运算等价）");
            Assert.AreEqual(4, registry.Get(ownerUid).Counters.Get("test.selfMove.cd"), "触发后重置为 N");

            MoveOwnerPingPong(ownerUid, 3);
            Assert.AreEqual(atk0 + 1, ReadAttack(ownerUid), "第 5–7 次不触发");
            MoveOwnerPingPong(ownerUid, 1);
            Assert.AreEqual(atk0 + 2, ReadAttack(ownerUid), "第 8 次再次触发");
        }

        [Test]
        public void OnSelfMove_Every1_TriggersEveryMove()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            var ownerUid = SpawnOnBoardReturnUid("monster.big_skeleton", sOwnerSlot);
            ActivateEffect(SelfMoveEvery1Json, ownerUid, "test.countdown.self_move1");
            var atk0 = ReadAttack(ownerUid);

            MoveOwnerPingPong(ownerUid, 3);
            Assert.AreEqual(atk0 + 3, ReadAttack(ownerUid));
        }

        [Test]
        public void OnSelfMove_ExternalDelay_DoesNotPermanentlyShiftPhase()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            var ownerUid = SpawnOnBoardReturnUid("monster.big_skeleton", sOwnerSlot);
            ActivateEffect(SelfMoveEvery4Json, ownerUid, "test.countdown.self_move");
            var registry = mArch.GetModel<CardRegistry>();
            var atk0 = ReadAttack(ownerUid);

            MoveOwnerPingPong(ownerUid, 1);
            Assert.AreEqual(3, registry.Get(ownerUid).Counters.Get("test.selfMove.cd"));

            // 效果加减速：+1 延后一拍（倒计时自然加法）
            registry.Get(ownerUid).Counters.Add("test.selfMove.cd", 1);
            Assert.AreEqual(4, registry.Get(ownerUid).Counters.Get("test.selfMove.cd"));

            MoveOwnerPingPong(ownerUid, 3);
            Assert.AreEqual(atk0, ReadAttack(ownerUid), "延后期间前 3 拍不触发");
            MoveOwnerPingPong(ownerUid, 1);
            Assert.AreEqual(atk0 + 1, ReadAttack(ownerUid), "延后一整期后触发一次");
            Assert.AreEqual(4, registry.Get(ownerUid).Counters.Get("test.selfMove.cd"));

            // 相位恢复：此后仍按每 4 次触发，不会永久错位
            MoveOwnerPingPong(ownerUid, 3);
            Assert.AreEqual(atk0 + 1, ReadAttack(ownerUid));
            MoveOwnerPingPong(ownerUid, 1);
            Assert.AreEqual(atk0 + 2, ReadAttack(ownerUid), "下一周期仍为整期 4，无永久错相");
        }

        [Test]
        public void OnCumulative_Threshold3_TriggersEvery3ArmorLost()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var ownerUid = SpawnOnBoardReturnUid("monster.big_skeleton", sOwnerSlot);
            registry.Get(ownerUid).Stats.SetBase(StatId.Armor, 20);
            registry.Get(ownerUid).Stats.SetBase(StatId.MaxHp, 99);
            registry.Get(ownerUid).Stats.SetBase(StatId.Hp, 99);
            ActivateEffect(ArmorLostThreshold3Json, ownerUid, "test.countdown.cumulative");
            PrepareAvatarAttack(1);
            var atk0 = ReadAttack(ownerUid);

            // 固定 DealDamage 量，避免开局遗物 Attack Modifier 改变掉甲计量。
            mPipeline.Enqueue(new DealDamageAction(board.AvatarUid.Value, ownerUid, 1, "test.countdown"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.AreEqual(atk0, ReadAttack(ownerUid), "掉甲 1 不触发");
            Assert.AreEqual(2, registry.Get(ownerUid).Counters.Get("test.cumulative.cd"));

            mPipeline.Enqueue(new DealDamageAction(board.AvatarUid.Value, ownerUid, 1, "test.countdown"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.AreEqual(atk0, ReadAttack(ownerUid), "掉甲 2 不触发");

            mPipeline.Enqueue(new DealDamageAction(board.AvatarUid.Value, ownerUid, 1, "test.countdown"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.AreEqual(atk0 + 1, ReadAttack(ownerUid), "累计掉甲 3 触发");
            Assert.AreEqual(3, registry.Get(ownerUid).Counters.Get("test.cumulative.cd"), "触发后重置为 threshold");

            mPipeline.Enqueue(new DealDamageAction(board.AvatarUid.Value, ownerUid, 1, "test.countdown"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            mPipeline.Enqueue(new DealDamageAction(board.AvatarUid.Value, ownerUid, 1, "test.countdown"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.AreEqual(atk0 + 1, ReadAttack(ownerUid));
            mPipeline.Enqueue(new DealDamageAction(board.AvatarUid.Value, ownerUid, 1, "test.countdown"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.AreEqual(atk0 + 2, ReadAttack(ownerUid), "第二轮累计 3 再触发");
        }

        [Test]
        public void OnInteract_Every3_TriggersOn3rd6thInteraction()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            var ownerUid = SpawnOnBoardReturnUid("monster.big_skeleton", sOwnerSlot);
            ActivateEffect(OnInteractEvery3Json, ownerUid, "test.countdown.interact");
            var registry = mArch.GetModel<CardRegistry>();
            var atk0 = ReadAttack(ownerUid);

            Assert.IsTrue(mPhase.AdvanceInteractionCount().Accepted);
            Assert.AreEqual(atk0, ReadAttack(ownerUid));
            Assert.AreEqual(2, registry.Get(ownerUid).Counters.Get("test.interact.cd"));

            Assert.IsTrue(mPhase.AdvanceInteractionCount().Accepted);
            Assert.AreEqual(atk0, ReadAttack(ownerUid));

            Assert.IsTrue(mPhase.AdvanceInteractionCount().Accepted);
            Assert.AreEqual(atk0 + 1, ReadAttack(ownerUid), "第 3 次互动触发");
            Assert.AreEqual(3, registry.Get(ownerUid).Counters.Get("test.interact.cd"));

            Assert.IsTrue(mPhase.AdvanceInteractionCount().Accepted);
            Assert.IsTrue(mPhase.AdvanceInteractionCount().Accepted);
            Assert.AreEqual(atk0 + 1, ReadAttack(ownerUid));
            Assert.IsTrue(mPhase.AdvanceInteractionCount().Accepted);
            Assert.AreEqual(atk0 + 2, ReadAttack(ownerUid), "第 6 次互动再触发");
        }

        [Test]
        public void Validate_OnInteractEveryBelowOne_ReportsRangeError()
        {
            var definition = mEffects.ParseJson(OnInteractEvery0Json);
            var validation = mEffects.Validate(definition);
            Assert.IsFalse(validation.IsValid);
            Assert.IsTrue(HasIssue(validation, "schema.range.every"));
        }

        private void ActivateEffect(string json, int ownerUid, string sourceDefId)
        {
            var definition = mEffects.ParseJson(json);
            Assert.IsTrue(mEffects.Validate(definition).IsValid, json);
            mEffects.Activate(definition, new EffectOwner(EffectContainerType.MonsterSkill, sourceDefId, ownerUid));
        }

        private static NodeDeckOptions CreateEmptyEnemyNode()
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            };
        }

        private int SpawnOnBoardReturnUid(string defId, SlotId slot)
        {
            mPipeline.Enqueue(new SpawnCardAction(defId, CardKind.Monster, ZoneId.Board, slot, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            return mArch.GetModel<BoardModel>().GetCardUid(slot);
        }

        private void MoveOwnerPingPong(int ownerUid, int times)
        {
            var registry = mArch.GetModel<CardRegistry>();
            for (var i = 0; i < times; i++)
            {
                var card = registry.Get(ownerUid);
                var from = card.Slot.Value;
                var to = from.Equals(sOwnerSlot) ? sMoveTargetSlot : sOwnerSlot;
                // 若目标被占，绕到备用格
                var board = mArch.GetModel<BoardModel>();
                if (board.GetCardUid(to) != 0 && board.GetCardUid(to) != ownerUid)
                {
                    to = sAltSlot;
                }

                mPipeline.Enqueue(new MoveCardAction(ownerUid, to, "test", "test"));
                Assert.Greater(mPipeline.RunToCompletion(), 0);
            }
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

        private int ReadAttack(int uid)
        {
            return (int)mArch.GetModel<CardRegistry>().Get(uid).Stats.GetBase(StatId.Attack);
        }

        private static bool HasIssue(EffectValidationResult validation, string code)
        {
            for (var i = 0; i < validation.Issues.Count; i++)
            {
                if (validation.Issues[i].Code == code)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

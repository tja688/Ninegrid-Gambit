using System.Collections.Generic;
using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// #73 / ADR-0010 Phase D：未触发探查区分 requires vs conditions；与正向诊断层并存。
    /// </summary>
    public sealed class EffectNonTriggerProbeTests
    {
        private const string ZoneRestrictedJson =
            "{\"id\":\"test.probe.zone\",\"containerType\":\"MonsterSkill\",\"kind\":\"Triggered\","
            + "\"requires\":[\"HasOwnerEntity\",\"CardZoneTriggerable\",\"CardZone:Board\"],"
            + "\"trigger\":{\"atom\":\"OnEvent\",\"eventType\":\"ArmorChanged\"},"
            + "\"conditions\":[{\"atom\":\"EventFilter\",\"eventType\":\"ArmorChanged\",\"maxDelta\":-1}],"
            + "\"target\":{\"atom\":\"Self\"},"
            + "\"action\":{\"atom\":\"ModifyBaseStat\",\"stat\":\"Attack\",\"delta\":1,\"reason\":\"test.probe.zone\"}}";

        private const string ConditionFilteredJson =
            "{\"id\":\"test.probe.condition\",\"containerType\":\"MonsterSkill\",\"kind\":\"Triggered\","
            + "\"requires\":[\"HasOwnerEntity\",\"CardZoneTriggerable\"],"
            + "\"trigger\":{\"atom\":\"OnDamageTaken\"},"
            + "\"conditions\":[{\"atom\":\"EventFilterTargetIsSelf\",\"eventType\":\"HpChanged\",\"maxDelta\":-1}],"
            + "\"target\":{\"atom\":\"Self\"},"
            + "\"action\":{\"atom\":\"ModifyBaseStat\",\"stat\":\"Attack\",\"delta\":1,\"reason\":\"test.probe.condition\"}}";

        private static readonly SlotId sOwnerSlot = SlotId.Board(2);
        private static readonly SlotId sOtherSlot = SlotId.Board(4);

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
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 29UL });
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
        public void Probe_DrawPileOwner_ReportsRequiresFailure()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyNode()).Accepted);
            var ownerUid = SpawnMonster("monster.tank", sOwnerSlot);
            MoveToDrawPile(ownerUid);
            var instance = Activate(ZoneRestrictedJson, ownerUid, "test.probe.zone");

            var events = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.ArmorChanged, 1, "probe")
                    .WithCard(ownerUid)
                    .WithDelta(-1)
            };
            var ctx = new TriggerContext(TriggerPoint.AfterAction, TriggerTiming.Post, null, events, null);
            var result = mEffects.ProbeWhyNotTriggered(instance.InstanceId, ctx);

            Assert.AreEqual(EffectNonTriggerFailureKind.Requires, result.Kind);
            Assert.AreEqual("requires.failed", result.Code);
            StringAssert.Contains("CardZone", result.Message);
        }

        [Test]
        public void Probe_OtherCardHpLoss_ReportsConditionsFailure()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyNode()).Accepted);
            var ownerUid = SpawnMonster("monster.tank", sOwnerSlot);
            var otherUid = SpawnMonster("monster.big_skeleton", sOtherSlot);
            var instance = Activate(ConditionFilteredJson, ownerUid, "test.probe.condition");

            var events = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.HpChanged, 1, "probe")
                    .WithCard(otherUid)
                    .WithDelta(-2)
                    .WithRemaining(5, 0)
            };
            var ctx = new TriggerContext(TriggerPoint.OnDamageTaken, TriggerTiming.Post, null, events, null);
            var result = mEffects.ProbeWhyNotTriggered(instance.InstanceId, ctx);

            Assert.AreEqual(EffectNonTriggerFailureKind.Conditions, result.Kind);
            Assert.AreEqual("conditions.failed", result.Code);
        }

        [Test]
        public void Probe_SelfHpLoss_WouldHaveTriggered()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyNode()).Accepted);
            var ownerUid = SpawnMonster("monster.tank", sOwnerSlot);
            var instance = Activate(ConditionFilteredJson, ownerUid, "test.probe.condition");

            var events = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.HpChanged, 1, "probe")
                    .WithCard(ownerUid)
                    .WithDelta(-2)
                    .WithRemaining(5, 0)
            };
            var ctx = new TriggerContext(TriggerPoint.OnDamageTaken, TriggerTiming.Post, null, events, null);
            var result = mEffects.ProbeWhyNotTriggered(instance.InstanceId, ctx);

            Assert.IsTrue(result.WouldHaveTriggered, result.Code + ": " + result.Message);
            Assert.AreEqual(EffectNonTriggerFailureKind.None, result.Kind);
        }

        [Test]
        public void Probe_MissingInstance_ReportsOther()
        {
            var result = mEffects.ProbeWhyNotTriggered(
                "missing-instance",
                new TriggerContext(TriggerPoint.AfterAction, TriggerTiming.Post, null, new CoreGameEvent[0], null));
            Assert.AreEqual(EffectNonTriggerFailureKind.Other, result.Kind);
            Assert.AreEqual("instance.missing", result.Code);
        }

        private EffectInstance Activate(string json, int ownerUid, string sourceDefId)
        {
            var definition = mEffects.ParseJson(json);
            Assert.IsTrue(mEffects.Validate(definition).IsValid, json);
            return mEffects.Activate(
                definition,
                new EffectOwner(EffectContainerType.MonsterSkill, sourceDefId, ownerUid));
        }

        private int SpawnMonster(string defId, SlotId slot)
        {
            mPipeline.Enqueue(new SpawnCardAction(defId, CardKind.Monster, ZoneId.Board, slot, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            return mArch.GetModel<BoardModel>().GetCardUid(slot);
        }

        private void MoveToDrawPile(int uid)
        {
            var board = mArch.GetModel<BoardModel>();
            var deck = mArch.GetModel<DeckModel>();
            var card = mArch.GetModel<CardRegistry>().Get(uid);
            if (card.Zone.Value == ZoneId.Board)
            {
                board.ClearSlot(card.Slot.Value);
            }

            deck.AddToDrawPile(card, false);
        }

        private static NodeDeckOptions CreateEmptyNode()
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            };
        }
    }
}

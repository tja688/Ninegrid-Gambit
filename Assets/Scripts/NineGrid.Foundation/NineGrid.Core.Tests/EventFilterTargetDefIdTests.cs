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
    /// EventFilter.targetDefId：按事件卡 DefId 过滤（生生不息等）。
    /// </summary>
    public sealed class EventFilterTargetDefIdTests
    {
        private const string EndlessFlameJson =
            "{\"id\":\"test.endless_flame\",\"containerType\":\"MonsterSkill\",\"kind\":\"Triggered\","
            + "\"requires\":[\"HasOwnerEntity\",\"CardZoneTriggerable\"],"
            + "\"trigger\":{\"atom\":\"OnAnyCardRemoved\"},"
            + "\"conditions\":[{\"atom\":\"EventFilter\",\"eventType\":\"CardRemoved\",\"targetDefId\":\"help.flame\"}],"
            + "\"target\":{\"atom\":\"Self\"},"
            + "\"action\":{\"atom\":\"ShuffleInto\",\"defId\":\"help.flame\",\"kind\":\"HelpCard\",\"count\":1,\"top\":false}}";

        private static readonly SlotId sOwnerSlot = SlotId.Board(5);
        private static readonly SlotId sFlameSlot = SlotId.Board(2);
        private static readonly SlotId sOtherHelpSlot = SlotId.Board(8);

        private IArchitecture mArch;
        private IActionPipelineSystem mPipeline;
        private IEffectSystem mEffects;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, ContentCatalogBootstrap.Load());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 29UL });
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
            mEffects = mArch.GetSystem<IEffectSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void EventFilter_TargetDefId_AcceptsMatchingRemovedCard()
        {
            var definition = mEffects.ParseJson(EndlessFlameJson);
            Assert.IsTrue(mEffects.Validate(definition).IsValid, "template must validate");

            mArch.GetSystem<IPhaseSystem>().StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            });

            SpawnOnBoard("monster.beggar", sOwnerSlot);
            var ownerUid = mArch.GetModel<BoardModel>().GetCardUid(sOwnerSlot);
            mEffects.Activate(definition, new EffectOwner(EffectContainerType.MonsterSkill, "test.endless_flame", ownerUid));

            SpawnOnBoard("help.flame", sFlameSlot);
            var before = CountDefInDrawPile("help.flame");
            mPipeline.Enqueue(new RemoveCardAction(
                mArch.GetModel<BoardModel>().GetCardUid(sFlameSlot),
                ZoneId.Removed,
                "test",
                "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            Assert.AreEqual(before + 1, CountDefInDrawPile("help.flame"));
        }

        [Test]
        public void EventFilter_TargetDefId_IgnoresOtherRemovedCard()
        {
            var definition = mEffects.ParseJson(EndlessFlameJson);
            Assert.IsTrue(mEffects.Validate(definition).IsValid);

            mArch.GetSystem<IPhaseSystem>().StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            });

            SpawnOnBoard("monster.beggar", sOwnerSlot);
            var ownerUid = mArch.GetModel<BoardModel>().GetCardUid(sOwnerSlot);
            mEffects.Activate(definition, new EffectOwner(EffectContainerType.MonsterSkill, "test.endless_flame", ownerUid));

            SpawnOnBoard("help.bomb", sOtherHelpSlot);
            var before = CountDefInDrawPile("help.flame");
            mPipeline.Enqueue(new RemoveCardAction(
                mArch.GetModel<BoardModel>().GetCardUid(sOtherHelpSlot),
                ZoneId.Removed,
                "test",
                "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            Assert.AreEqual(before, CountDefInDrawPile("help.flame"));
        }

        private void SpawnOnBoard(string defId, SlotId slot)
        {
            var kind = defId.StartsWith("help.") ? CardKind.HelpCard : CardKind.Monster;
            mPipeline.Enqueue(new SpawnCardAction(defId, kind, ZoneId.Board, slot, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
        }

        private int CountDefInDrawPile(string defId)
        {
            var deck = mArch.GetModel<DeckModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var count = 0;
            for (var i = 0; i < deck.DrawPileUids.Count; i++)
            {
                CardInstance card;
                if (registry.TryGet(deck.DrawPileUids[i], out card) && card.DefId == defId)
                {
                    count++;
                }
            }

            return count;
        }
    }
}

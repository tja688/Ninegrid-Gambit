using System.Collections.Generic;
using NineGrid.Core.Systems;
using NUnit.Framework;

namespace NineGrid.Core.Tests
{
    public sealed class P3ActionPipelineTests
    {
        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            InitialGameFactory.Create(NineGridArchitecture.Current);
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void ReactionStackResolvesDepthFirstAndKeepsOrderedEventLog()
        {
            var architecture = NineGridArchitecture.Current;
            var registry = architecture.GetModel<CardRegistry>();
            var board = architecture.GetModel<BoardModel>();
            var pipeline = architecture.GetSystem<IActionPipelineSystem>();
            var triggerSystem = architecture.GetSystem<ITriggerSystem>();
            var avatar = registry.Get(board.AvatarUid.Value);
            var first = CreateMonster("monster.first", 1, SlotId.Board(2));
            var second = CreateMonster("monster.second", 1, SlotId.Board(4));

            triggerSystem.Register(TriggerPoint.OnKill, TriggerTiming.Post, new DelegateTriggerReaction(
                "kill.chain",
                context =>
                {
                    for (var i = 0; i < context.Events.Count; i++)
                    {
                        if (context.Events[i].Type == CoreEventType.CardKilled && context.Events[i].CardUid == first.Uid)
                        {
                            return new[] { new DealDamageAction(avatar.Uid, second.Uid, 5) };
                        }
                    }

                    return null;
                }));

            pipeline.Execute(new DealDamageAction(avatar.Uid, first.Uid, 5));

            var killEvents = EventsOfType(pipeline.EventLog, CoreEventType.CardKilled);
            Assert.AreEqual(2, killEvents.Count);
            Assert.AreEqual(first.Uid, killEvents[0].CardUid);
            Assert.AreEqual(second.Uid, killEvents[1].CardUid);

            var secondDamage = FirstEventForCard(pipeline.EventLog, CoreEventType.DamageDealt, second.Uid);
            Assert.Less(killEvents[0].Sequence, secondDamage.Sequence);
            Assert.Less(secondDamage.Sequence, killEvents[1].Sequence);
        }

        [Test]
        public void DamageConsumesArmorBeforeHpAndNeverDropsBelowZero()
        {
            var architecture = NineGridArchitecture.Current;
            var registry = architecture.GetModel<CardRegistry>();
            var board = architecture.GetModel<BoardModel>();
            var pipeline = architecture.GetSystem<IActionPipelineSystem>();
            var avatar = registry.Get(board.AvatarUid.Value);
            var monster = CreateMonster("monster.armored", 10, SlotId.Board(2));
            monster.Stats.SetBase(StatId.Armor, 3);

            pipeline.Execute(new DealDamageAction(avatar.Uid, monster.Uid, 5));

            Assert.AreEqual(0, monster.Stats.GetBase(StatId.Armor));
            Assert.AreEqual(8, monster.Stats.GetBase(StatId.Hp));

            pipeline.Execute(new DealDamageAction(avatar.Uid, monster.Uid, 50));

            Assert.AreEqual(0, monster.Stats.GetBase(StatId.Hp));
            Assert.AreEqual(ZoneId.Graveyard, monster.Zone.Value);
            Assert.IsTrue(pipeline.EventLog.Contains(CoreEventType.CardKilled));
        }

        private static CardInstance CreateMonster(string defId, int hp, SlotId slot)
        {
            var registry = NineGridArchitecture.Current.GetModel<CardRegistry>();
            var board = NineGridArchitecture.Current.GetModel<BoardModel>();
            var monster = registry.Create(defId, CardKind.Monster);
            monster.Stats.SetBase(StatId.MaxHp, hp);
            monster.Stats.SetBase(StatId.Hp, hp);
            board.PlaceCard(monster, slot);
            return monster;
        }

        private static List<CoreGameEvent> EventsOfType(EventLog eventLog, CoreEventType type)
        {
            var result = new List<CoreGameEvent>();
            for (var i = 0; i < eventLog.Entries.Count; i++)
            {
                if (eventLog.Entries[i].Type == type)
                {
                    result.Add(eventLog.Entries[i]);
                }
            }

            return result;
        }

        private static CoreGameEvent FirstEventForCard(EventLog eventLog, CoreEventType type, int cardUid)
        {
            for (var i = 0; i < eventLog.Entries.Count; i++)
            {
                if (eventLog.Entries[i].Type == type && eventLog.Entries[i].CardUid == cardUid)
                {
                    return eventLog.Entries[i];
                }
            }

            Assert.Fail("Event not found: " + type + " card=" + cardUid);
            return null;
        }
    }
}

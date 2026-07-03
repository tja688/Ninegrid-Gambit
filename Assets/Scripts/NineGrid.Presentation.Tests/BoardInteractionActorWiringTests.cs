using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Presentation.Interaction;
using NineGrid.Presentation.Orchestration;
using NineGrid.Presentation.Tests.Support;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    public sealed class BoardInteractionActorWiringTests
    {
        [Test]
        public void WireAllBoardActors_AddsBoardCardInputRelayToRegisteredActors()
        {
            NineGridArchitecture.ResetForTests();
            InitialGameFactory.Create(NineGridArchitecture.Current);

            var snapshot = CoreViewSnapshotFactory.Capture(NineGridArchitecture.Current);
            var registry = new TableNineViewRegistry();
            var root = new GameObject("ActorsRoot").transform;
            var factory = new TableNineActorFactory(root, null, registry, NineGridArchitecture.Current);

            InitialActorsBuilder.BuildBoardCards(snapshot, registry, factory);
            BoardInteractionActorWiring.WireAllBoardActors(registry, snapshot);

            IReadOnlyList<BoardSlotView> slots = snapshot.Board.Slots;
            for (var i = 0; i < slots.Count; i++)
            {
                BoardSlotView slotView = slots[i];
                if (slotView.CardUid <= 0)
                {
                    continue;
                }

                Transform actor = registry.ResolveActor(slotView.CardUid);
                Assert.IsNotNull(actor, $"Actor for uid {slotView.CardUid} should exist.");
                Assert.IsNotNull(actor.GetComponent<BoardCardInputRelay>(), "Board card should have input relay.");
                Assert.IsNotNull(actor.GetComponent<Collider2D>(), "Board card should have collider.");
            }

            Object.DestroyImmediate(root.gameObject);
        }
    }
}

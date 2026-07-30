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
    /// ADR-0016：牌面朝向权威、Flip/Reveal 事件、背面惰性与 ActiveWhileFaceDown 豁免。
    /// </summary>
    public sealed class CardFaceOrientationTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;
        private IEffectSystem mEffects;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(
                ContentConfigKeys.DefaultCatalog,
                ContentCatalogBootstrap.Load());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 42UL });
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
        public void FlipCardAction_TogglesFaceUp_AndEmitsCardFaceChanged()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 5, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);
            var card = GetSoleBoardCard();
            Assert.IsTrue(card.FaceUp);

            var startIndex = mPipeline.EventLog.Entries.Count;
            mPipeline.Enqueue(new FlipCardAction(card.Uid));
            mPipeline.RunToCompletion();

            Assert.IsFalse(card.FaceUp);
            var faceEvt = FindLast(startIndex, CoreEventType.CardFaceChanged);
            Assert.IsNotNull(faceEvt);
            Assert.AreEqual(card.Uid, faceEvt.CardUid);
            Assert.AreEqual(0, faceEvt.ResultValue);
        }

        [Test]
        public void RevealFace_OnlyAcceptsFaceDownAdjacent()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 5, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);
            var card = GetSoleBoardCard();
            Assert.IsFalse(mPhase.RevealFace(sAdjacentSlot).Accepted, "Already face-up");

            card.FaceUp = false;
            Assert.IsTrue(mPhase.RevealFace(sAdjacentSlot).Accepted);
            Assert.IsTrue(card.FaceUp);
        }

        [Test]
        public void FaceOrientationAtoms_AndActiveWhileFaceDownToken_AreRegistered()
        {
            Assert.IsTrue(mEffects.AtomRegistry.Triggers.ContainsKey("OnFlip"));
            Assert.IsTrue(mEffects.AtomRegistry.Actions.ContainsKey("Flip"));
            Assert.IsTrue(mEffects.AtomRegistry.Conditions.ContainsKey("IsFaceUp"));
            Assert.IsTrue(EffectRequiresValidator.IsKnownToken(EffectRequireTokens.ActiveWhileFaceDown));
            Assert.IsTrue(EffectRequireTokens.HasActiveWhileFaceDown(
                mEffects.ParseJson(
                    @"{""id"":""test.flip.exempt"",""kind"":""Triggered"",""containerType"":""MonsterSkill"","
                    + @"""requires"":[""HasOwnerEntity"",""CardZone:Board"",""ActiveWhileFaceDown""],"
                    + @"""trigger"":{""atom"":""OnFlip""},"
                    + @"""target"":{""atom"":""Self""},"
                    + @"""action"":{""atom"":""Flip""}}")));
        }

        [Test]
        public void Attack_RejectsFaceDownTarget()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 5, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);
            var card = GetSoleBoardCard();
            card.FaceUp = false;

            var result = mPhase.Attack(sAdjacentSlot);
            Assert.IsFalse(result.Accepted);
            StringAssert.Contains("face-down", result.Reason);
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

        private CardInstance GetSoleBoardCard()
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
                if (uid != 0)
                {
                    return registry.Get(uid);
                }
            }

            Assert.Fail("No board card");
            return null;
        }

        private CoreGameEvent FindLast(int startIndex, CoreEventType type)
        {
            var entries = mPipeline.EventLog.Entries;
            CoreGameEvent found = null;
            for (var i = startIndex; i < entries.Count; i++)
            {
                if (entries[i].Type == type)
                {
                    found = entries[i];
                }
            }

            return found;
        }
    }
}

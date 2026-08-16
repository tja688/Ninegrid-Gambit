using System.Collections.Generic;
using System.Reflection;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Flow.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 卡面生成提交回归：生成事件在 Settled 才消费时，不得覆盖 Impact 已提交的血甲。
    /// </summary>
    public sealed class CardFaceSpawnCommitTests
    {
        private GameObject mCardManagerGo;
        private GameObject mViewGo;

        [SetUp]
        public void SetUp()
        {
            mViewGo = new GameObject("TestCardView");
            var view = mViewGo.AddComponent<StandardCardView>();

            mCardManagerGo = new GameObject("TestCardManager");
            var manager = mCardManagerGo.AddComponent<CardManagerSingleton>();
            var cards = (Dictionary<int, ManagedCard>)typeof(CardManagerSingleton)
                .GetField("_cardsByUid", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(manager);
            cards[413] = new ManagedCard(413, "monster.ringleader", view);

            CardEntityLifecycleHook.ResolveCards = () => manager;
            CardEntityLifecycleHook.TryGet = manager.TryGet;
        }

        [TearDown]
        public void TearDown()
        {
            CardEntityLifecycleHook.Reset();
            Object.DestroyImmediate(mViewGo);
            Object.DestroyImmediate(mCardManagerGo);
        }

        [Test]
        public void SpawnAfterImpact_PreservesCommittedHpAndArmor()
        {
            var handler = new CardFaceStatHandler();
            handler.Apply(BuildHpChanged(hp: 1, armor: 0));
            handler.Apply(BuildCardDealt(hp: 11, armor: 0, attack: 4));

            var card = CardEntityLifecycleHook.CardsOrNull().CardsByUid[413];
            Assert.IsNotNull(card.CommittedPresentation, "Impact 应先创建卡面提交快照");
            Assert.AreEqual(1, card.CommittedPresentation.Hp, "CardDealt 不得把捕熊后的残血覆盖回满血");
            Assert.AreEqual(0, card.CommittedPresentation.Armor, "CardDealt 应保留已提交当前甲");
            Assert.AreEqual(4, card.CommittedPresentation.Attack, "CardDealt 仍应补齐尚未提交的攻击");
            Assert.IsTrue(card.CommittedPresentation.HasHp, "血量通道应标记为已提交");
            Assert.IsTrue(card.CommittedPresentation.HasArmor, "护甲通道应标记为已提交");
            Assert.IsTrue(card.CommittedPresentation.HasAttack, "攻击通道应标记为已提交");
        }

        private static PresentationInstruction BuildHpChanged(int hp, int armor)
        {
            var gameEvent = new CoreGameEvent(CoreEventType.HpChanged, 1, "Damage")
                .WithCard(413)
                .WithTarget(413)
                .WithRemaining(hp, armor)
                .WithDelta(-9);
            return new PresentationInstruction(gameEvent, PresentationEventMap.Get(gameEvent.Type));
        }

        private static PresentationInstruction BuildCardDealt(int hp, int armor, int attack)
        {
            var gameEvent = new CoreGameEvent(CoreEventType.CardDealt, 2, "Deal")
                .WithCard(413)
                .WithResultValue(attack)
                .WithRemaining(hp, armor);
            return new PresentationInstruction(gameEvent, PresentationEventMap.Get(gameEvent.Type));
        }
    }
}

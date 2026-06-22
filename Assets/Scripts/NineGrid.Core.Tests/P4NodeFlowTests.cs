using System;
using System.Collections.Generic;
using NineGrid.Core.Commands;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    public sealed class P4NodeFlowTests
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
        public void BoardRotationAndFillUseStableSlotOrder()
        {
            var architecture = NineGridArchitecture.Current;
            var registry = architecture.GetModel<CardRegistry>();
            var board = architecture.GetModel<BoardModel>();
            var deck = architecture.GetModel<DeckModel>();
            var boardSystem = architecture.GetSystem<IBoardSystem>();
            var first = registry.Create("player.first", CardKind.PlayerCard);
            var second = registry.Create("player.second", CardKind.PlayerCard);
            deck.AddToDrawPile(first, false);
            deck.AddToDrawPile(second, false);

            boardSystem.FillEmptySlots();

            Assert.AreEqual(first.Uid, board.GetCardUid(SlotId.Board(2)));
            Assert.AreEqual(second.Uid, board.GetCardUid(SlotId.Board(4)));

            boardSystem.RotateClockwise();

            Assert.AreEqual(first.Uid, board.GetCardUid(SlotId.Board(3)));
            Assert.AreEqual(second.Uid, board.GetCardUid(SlotId.Board(1)));
        }

        [Test]
        public void NoSkillNodeCanClearAfterKillAndAwardGold()
        {
            var architecture = NineGridArchitecture.Current;
            var board = architecture.GetModel<BoardModel>();
            var player = architecture.GetModel<PlayerModel>();
            var phaseSystem = architecture.GetSystem<IPhaseSystem>();
            var deckSystem = architecture.GetSystem<IDeckSystem>();
            var options = new NodeDeckOptions { PlayerOpeningCount = 0, EnemyOpeningCount = 1 }
                .AddEnemyCard(new CardDraft("monster.coin_slime", CardKind.Monster)
                {
                    MaxHp = 1,
                    Attack = 0,
                    GoldReward = 4
                });

            var startResult = architecture.SendCommand(new StartNodeCommand(options));
            Assert.IsTrue(startResult.Accepted);
            Assert.AreEqual(GamePhase.InteractionLoop, phaseSystem.CurrentPhase);

            var targetSlot = FindFirstMonsterSlot();
            Assert.AreNotEqual(SlotId.None, targetSlot);

            var attackResult = architecture.SendCommand(new AttackCommand(targetSlot));

            Assert.IsTrue(attackResult.Accepted);
            Assert.AreEqual(4, player.Coins.Value);
            Assert.AreEqual(1, player.InteractionCount.Value);
            Assert.AreEqual(0, board.GetCardUid(targetSlot));
            Assert.IsFalse(deckSystem.HasEnemyOnBoard());
            Assert.AreEqual(GamePhase.RewardItemChoice, phaseSystem.CurrentPhase);
        }

        [Test]
        public void FirstStrikeMonsterDealsDamageBeforePlayerAttack()
        {
            NineGridArchitecture.ResetForTests();
            InitialGameFactory.Create(
                NineGridArchitecture.Current,
                new InitialGameOptions { AvatarAttack = 3 });

            var architecture = NineGridArchitecture.Current;
            var registry = architecture.GetModel<CardRegistry>();
            var board = architecture.GetModel<BoardModel>();
            var statSystem = architecture.GetSystem<IStatSystem>();
            var avatar = registry.Get(board.AvatarUid.Value);
            var options = new NodeDeckOptions { PlayerOpeningCount = 0, EnemyOpeningCount = 1 }
                .AddEnemyCard(new CardDraft("monster.first_striker", CardKind.Monster)
                {
                    MaxHp = 2,
                    Attack = 5
                });

            architecture.SendCommand(new StartNodeCommand(options));
            var targetSlot = FindFirstMonsterSlot();
            var monster = registry.Get(board.GetCardUid(targetSlot));
            statSystem.RuleModifiers.Add(new RuleModifier(
                RuleId.FirstStrike,
                ModifierOp.Override,
                1,
                ModifierLayer.Persistent,
                new ModifierSource("test.first_strike"),
                ModifierScope.Permanent,
                new TargetUidCondition(monster.Uid)));

            var result = architecture.SendCommand(new AttackCommand(targetSlot));

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(25, avatar.Stats.GetBase(StatId.Hp));
            Assert.AreEqual(ZoneId.Graveyard, monster.Zone.Value);
        }

        [Test]
        public void IllegalCommandIsRejectedAndBroadcast()
        {
            var architecture = NineGridArchitecture.Current;
            var pipeline = architecture.GetSystem<IActionPipelineSystem>();
            Evt_ActionRejected rejected = null;
            architecture.RegisterEvent<Evt_ActionRejected>(evt => rejected = evt);

            var options = new NodeDeckOptions { PlayerOpeningCount = 0, EnemyOpeningCount = 1 }
                .AddEnemyCard(new CardDraft("monster.guard", CardKind.Monster)
                {
                    MaxHp = 5,
                    Attack = 0
                });
            architecture.SendCommand(new StartNodeCommand(options));

            var result = architecture.SendCommand(new AttackCommand(SlotId.Board(4)));

            Assert.IsFalse(result.Accepted);
            Assert.IsNotNull(rejected);
            Assert.AreEqual(GameCommandKind.Attack, rejected.Command);
            Assert.IsTrue(pipeline.EventLog.Contains(CoreEventType.ActionRejected));
        }

        [Test]
        public void OpeningDealDrainsStagingPoolsIntoDrawPile()
        {
            var architecture = NineGridArchitecture.Current;
            var deck = architecture.GetModel<DeckModel>();
            var options = new NodeDeckOptions { PlayerOpeningCount = 1, EnemyOpeningCount = 1 }
                .AddPlayerCard(new CardDraft("player.a", CardKind.PlayerCard))
                .AddPlayerCard(new CardDraft("player.b", CardKind.PlayerCard))
                .AddEnemyCard(new CardDraft("monster.a", CardKind.Monster) { MaxHp = 2, Attack = 0 })
                .AddEnemyCard(new CardDraft("monster.b", CardKind.Monster) { MaxHp = 2, Attack = 0 })
                .AddEnemyCard(new CardDraft("monster.c", CardKind.Monster) { MaxHp = 2, Attack = 0 });

            var startResult = architecture.SendCommand(new StartNodeCommand(options));

            Assert.IsTrue(startResult.Accepted);
            Assert.AreEqual(0, deck.PlayerCardPoolUids.Count);
            Assert.AreEqual(0, deck.EnemyCardPoolUids.Count);
        }

        [Test]
        public void NodeStaysActiveWhileEnemiesRemainAfterStagingPoolIsDrained()
        {
            var architecture = NineGridArchitecture.Current;
            var deck = architecture.GetModel<DeckModel>();
            var deckSystem = architecture.GetSystem<IDeckSystem>();
            var phaseSystem = architecture.GetSystem<IPhaseSystem>();
            var options = new NodeDeckOptions { PlayerOpeningCount = 0, EnemyOpeningCount = 1 }
                .AddEnemyCard(new CardDraft("monster.one", CardKind.Monster) { MaxHp = 1, Attack = 0 })
                .AddEnemyCard(new CardDraft("monster.two", CardKind.Monster) { MaxHp = 1, Attack = 0 });

            var startResult = architecture.SendCommand(new StartNodeCommand(options));
            Assert.IsTrue(startResult.Accepted);
            Assert.AreEqual(0, deck.EnemyCardPoolUids.Count);
            Assert.IsTrue(deckSystem.HasPendingEnemyCards());
            Assert.AreEqual(GamePhase.InteractionLoop, phaseSystem.CurrentPhase);

            var targetSlot = FindFirstMonsterSlot();
            Assert.AreNotEqual(SlotId.None, targetSlot);

            var attackResult = architecture.SendCommand(new AttackCommand(targetSlot));
            Assert.IsTrue(attackResult.Accepted);
            Assert.IsTrue(deckSystem.HasPendingEnemyCards());
            Assert.AreEqual(GamePhase.InteractionLoop, phaseSystem.CurrentPhase);
        }

        [Test]
        public void UseItemIsRejectedWhenPhaseDoesNotAllowIt()
        {
            var architecture = NineGridArchitecture.Current;
            var registry = architecture.GetModel<CardRegistry>();
            var deck = architecture.GetModel<DeckModel>();
            var item = registry.Create("item.potion", CardKind.Item);
            deck.AddToItemSlots(item);

            AssertUseItemRejected(
                architecture,
                new UseItemCommand(item.Uid),
                GameCommandKind.UseItem,
                item.Uid);
        }

        [Test]
        public void UseItemIsRejectedForUnknownUid()
        {
            var architecture = NineGridArchitecture.Current;
            EnterInteractionLoop(architecture);

            AssertUseItemRejected(
                architecture,
                new UseItemCommand(9999),
                GameCommandKind.UseItem,
                9999);
        }

        [Test]
        public void UseItemIsRejectedWhenCardIsNotInItemSlots()
        {
            var architecture = NineGridArchitecture.Current;
            var registry = architecture.GetModel<CardRegistry>();
            var deck = architecture.GetModel<DeckModel>();
            EnterInteractionLoop(architecture);

            var item = registry.Create("item.off_deck", CardKind.Item);
            deck.AddToDrawPile(item, false);

            AssertUseItemRejected(
                architecture,
                new UseItemCommand(item.Uid),
                GameCommandKind.UseItem,
                item.Uid);
        }

        [Test]
        public void UseItemIsRejectedWhenCardKindIsNotUsable()
        {
            var architecture = NineGridArchitecture.Current;
            var registry = architecture.GetModel<CardRegistry>();
            var deck = architecture.GetModel<DeckModel>();
            EnterInteractionLoop(architecture);

            var playerCard = registry.Create("player.wrong_slot", CardKind.PlayerCard);
            deck.AddToItemSlots(playerCard);

            AssertUseItemRejected(
                architecture,
                new UseItemCommand(playerCard.Uid),
                GameCommandKind.UseItem,
                playerCard.Uid);
        }

        [Test]
        public void FullNodeReplayScriptAssertsAllInteractionPaths()
        {
            NineGridArchitecture.ResetForTests();
            InitialGameFactory.Create(
                NineGridArchitecture.Current,
                new InitialGameOptions { Seed = 42, AvatarAttack = 1 });

            var architecture = NineGridArchitecture.Current;
            var registry = architecture.GetModel<CardRegistry>();
            var board = architecture.GetModel<BoardModel>();
            var deck = architecture.GetModel<DeckModel>();
            var player = architecture.GetModel<PlayerModel>();
            var phaseSystem = architecture.GetSystem<IPhaseSystem>();
            var deckSystem = architecture.GetSystem<IDeckSystem>();
            var pipeline = architecture.GetSystem<IActionPipelineSystem>();
            var avatarSlot = board.AvatarSlot.Value;

            var options = new NodeDeckOptions { PlayerOpeningCount = 1, EnemyOpeningCount = 2 }
                .AddPlayerCard(new CardDraft("player.loot", CardKind.PlayerCard) { GoldReward = 3 })
                .AddEnemyCard(new CardDraft("monster.tank", CardKind.Monster)
                {
                    MaxHp = 5,
                    Attack = 0
                })
                .AddEnemyCard(new CardDraft("monster.weak", CardKind.Monster)
                {
                    MaxHp = 1,
                    Attack = 0,
                    GoldReward = 5
                });

            var startResult = architecture.SendCommand(new StartNodeCommand(options));
            Assert.IsTrue(startResult.Accepted);
            Assert.AreEqual(GamePhase.InteractionLoop, phaseSystem.CurrentPhase);
            Assert.IsTrue(pipeline.EventLog.Contains(CoreEventType.NodeStarted));

            var coinsBefore = player.Coins.Value;
            var interactionBefore = player.InteractionCount.Value;
            Assert.AreEqual(0, interactionBefore);

            var illegalAttackSlot = FindNonAdjacentOccupiedOrEmptySlot(avatarSlot, board);
            AssertUseCommandRejected(
                architecture,
                new AttackCommand(illegalAttackSlot),
                GameCommandKind.Attack);

            var tankSlot = FindAdjacentSlot(
                avatarSlot,
                board,
                slot =>
                {
                    var uid = board.GetCardUid(slot);
                    if (uid == 0)
                    {
                        return false;
                    }

                    var card = registry.Get(uid);
                    return card.Kind == CardKind.Monster && card.Stats.GetBase(StatId.Hp) > 1;
                },
                "adjacent monster for non-kill attack");
            var tankUid = board.GetCardUid(tankSlot);
            var tank = registry.Get(tankUid);
            Assert.Greater(tank.Stats.GetBase(StatId.Hp), 1);

            var logBeforeNonKillAttack = pipeline.EventLog.Entries.Count;
            var attackNonKill = architecture.SendCommand(new AttackCommand(tankSlot));
            Assert.IsTrue(attackNonKill.Accepted);
            Assert.AreEqual(tank.Stats.GetBase(StatId.MaxHp) - 1, tank.Stats.GetBase(StatId.Hp));
            Assert.AreEqual(interactionBefore, player.InteractionCount.Value);
            Assert.IsFalse(HasEventSince(pipeline.EventLog, logBeforeNonKillAttack, CoreEventType.BoardRotated));
            Assert.IsTrue(HasEventSince(pipeline.EventLog, logBeforeNonKillAttack, CoreEventType.DamageDealt));

            AssertUseCommandRejected(
                architecture,
                new PickupItemCommand(tankSlot),
                GameCommandKind.PickupItem);

            AssertUseCommandRejected(
                architecture,
                new ClickEmptyCommand(tankSlot),
                GameCommandKind.ClickEmpty);

            var weakUid = FindBoardCardUid(
                board,
                registry,
                card => card.DefId == "monster.weak");
            var lootUid = FindBoardCardUid(
                board,
                registry,
                card => card.DefId == "player.loot");

            var item = registry.Create("item.heal", CardKind.HelpCard);
            deck.AddToItemSlots(item);
            var interactionBeforeItem = player.InteractionCount.Value;
            var logBeforeItem = pipeline.EventLog.Entries.Count;
            var useItemResult = architecture.SendCommand(new UseItemCommand(item.Uid));
            Assert.IsTrue(useItemResult.Accepted);
            Assert.AreEqual(interactionBeforeItem, player.InteractionCount.Value);
            Assert.IsTrue(HasEventSince(pipeline.EventLog, logBeforeItem, CoreEventType.ItemUsed));
            Assert.IsFalse(HasEventSince(pipeline.EventLog, logBeforeItem, CoreEventType.BoardRotated));

            RotateUntilAdjacent(architecture, avatarSlot, board, weakUid);
            var weakSlot = FindSlotOfCard(board, weakUid);
            var interactionBeforeKill = player.InteractionCount.Value;
            var logBeforeKill = pipeline.EventLog.Entries.Count;
            var killAttack = architecture.SendCommand(new AttackCommand(weakSlot));
            Assert.IsTrue(killAttack.Accepted);
            Assert.AreEqual(0, board.GetCardUid(weakSlot));
            Assert.AreEqual(interactionBeforeKill + 1, player.InteractionCount.Value);
            Assert.IsTrue(HasEventSince(pipeline.EventLog, logBeforeKill, CoreEventType.CardKilled));
            Assert.IsTrue(HasEventSince(pipeline.EventLog, logBeforeKill, CoreEventType.GoldModified));
            Assert.IsTrue(HasEventSince(pipeline.EventLog, logBeforeKill, CoreEventType.BoardRotated));
            Assert.IsTrue(HasEventSince(pipeline.EventLog, logBeforeKill, CoreEventType.SlotsFilled));

            RotateUntilAdjacent(architecture, avatarSlot, board, lootUid);
            var pickupSlot = FindSlotOfCard(board, lootUid);
            var interactionBeforePickup = player.InteractionCount.Value;
            var logBeforePickup = pipeline.EventLog.Entries.Count;
            var pickupResult = architecture.SendCommand(new PickupItemCommand(pickupSlot));
            Assert.IsTrue(pickupResult.Accepted);
            Assert.AreEqual(coinsBefore + 3 + 5, player.Coins.Value);
            Assert.AreEqual(interactionBeforePickup + 1, player.InteractionCount.Value);
            Assert.IsTrue(HasEventSince(pipeline.EventLog, logBeforePickup, CoreEventType.ItemPicked));
            Assert.IsTrue(HasEventSince(pipeline.EventLog, logBeforePickup, CoreEventType.BoardRotated));
            Assert.IsTrue(HasEventSince(pipeline.EventLog, logBeforePickup, CoreEventType.SlotsFilled));

            var emptySlot = FindAdjacentSlot(
                avatarSlot,
                board,
                slot => board.IsEmpty(slot),
                "adjacent empty slot");
            var interactionBeforeClick = player.InteractionCount.Value;
            var logBeforeClick = pipeline.EventLog.Entries.Count;
            var clickResult = architecture.SendCommand(new ClickEmptyCommand(emptySlot));
            Assert.IsTrue(clickResult.Accepted);
            Assert.AreEqual(interactionBeforeClick + 1, player.InteractionCount.Value);
            Assert.IsTrue(HasEventSince(pipeline.EventLog, logBeforeClick, CoreEventType.EmptyClicked));
            Assert.IsTrue(HasEventSince(pipeline.EventLog, logBeforeClick, CoreEventType.BoardRotated));

            while (deckSystem.HasEnemyOnBoard())
            {
                var remainingMonsterSlot = TryFindAdjacentSlot(
                    avatarSlot,
                    board,
                    slot =>
                    {
                        var uid = board.GetCardUid(slot);
                        return uid != 0 && registry.Get(uid).Kind == CardKind.Monster;
                    });
                if (remainingMonsterSlot != SlotId.None)
                {
                    var cleanupAttack = architecture.SendCommand(new AttackCommand(remainingMonsterSlot));
                    Assert.IsTrue(cleanupAttack.Accepted);
                    continue;
                }

                var rotateSlot = TryFindAdjacentSlot(avatarSlot, board, slot => board.IsEmpty(slot));
                if (rotateSlot == SlotId.None)
                {
                    Assert.Fail("Could not rotate board to reach remaining monsters.");
                }

                var rotateClick = architecture.SendCommand(new ClickEmptyCommand(rotateSlot));
                Assert.IsTrue(rotateClick.Accepted);
            }

            Assert.IsTrue(pipeline.EventLog.Contains(CoreEventType.CardDealt));
            if (deck.DrawPileUids.Count == 0)
            {
                Assert.IsTrue(pipeline.EventLog.Contains(CoreEventType.DrawPileExhausted));
            }
            Assert.IsFalse(deckSystem.HasEnemyOnBoard());
            Assert.IsTrue(deckSystem.IsNodeCleared());
            Assert.AreEqual(GamePhase.RewardItemChoice, phaseSystem.CurrentPhase);
            Assert.IsTrue(phaseSystem.CanExecute(GameCommandKind.SkipHelpChoice));
            Assert.IsTrue(phaseSystem.CanExecute(GameCommandKind.SelectReward));
            Assert.IsTrue(phaseSystem.CanExecute(GameCommandKind.PickupItem));
            Assert.IsTrue(phaseSystem.CanExecute(GameCommandKind.UseItem));
            Assert.IsFalse(phaseSystem.CanExecute(GameCommandKind.StartNode));
            Assert.IsFalse(phaseSystem.CanExecute(GameCommandKind.Attack));
            Assert.IsFalse(phaseSystem.CanExecute(GameCommandKind.ClickEmpty));

            Assert.IsTrue(pipeline.EventLog.Contains(CoreEventType.ActionRejected));
            Assert.IsTrue(pipeline.EventLog.Contains(CoreEventType.NodeCompleted));
            Assert.IsTrue(pipeline.EventLog.Contains(CoreEventType.RewardOffered));
            Assert.IsTrue(pipeline.EventLog.Contains(CoreEventType.PhaseChanged));
            Assert.IsTrue(pipeline.EventLog.Contains(CoreEventType.InteractionChanged));
            Assert.GreaterOrEqual(EventsOfType(pipeline.EventLog, CoreEventType.BoardRotated).Count, 3);
            Assert.GreaterOrEqual(player.InteractionCount.Value, interactionBefore + 3);
            Assert.AreEqual(coinsBefore + 3 + 5, player.Coins.Value);
        }

        [Test]
        public void UseItemAcceptsValidItemInItemSlots()
        {
            var architecture = NineGridArchitecture.Current;
            var registry = architecture.GetModel<CardRegistry>();
            var deck = architecture.GetModel<DeckModel>();
            var pipeline = architecture.GetSystem<IActionPipelineSystem>();
            var player = architecture.GetModel<PlayerModel>();
            EnterInteractionLoop(architecture);

            var item = registry.Create("item.heal", CardKind.HelpCard);
            deck.AddToItemSlots(item);
            var interactionBefore = player.InteractionCount.Value;

            var result = architecture.SendCommand(new UseItemCommand(item.Uid));

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(interactionBefore, player.InteractionCount.Value);
            Assert.IsTrue(pipeline.EventLog.Contains(CoreEventType.ItemUsed));
        }

        [Test]
        public void UseItemCommandPassesSelectionToEffectsAndConsumesItem()
        {
            var architecture = NineGridArchitecture.Current;
            P5CatalogTestSupport.RegisterCatalog(architecture);

            var registry = architecture.GetModel<CardRegistry>();
            var board = architecture.GetModel<BoardModel>();
            var deck = architecture.GetModel<DeckModel>();
            var content = architecture.GetSystem<IContentSystem>();
            var avatar = registry.Get(board.AvatarUid.Value);
            avatar.Stats.SetBase(StatId.Attack, 5);
            EnterInteractionLoop(architecture);

            var unselectedSlot = FindFirstMonsterSlot();
            var unselected = registry.Get(board.GetCardUid(unselectedSlot));
            var selected = registry.Create("monster.command.selected", CardKind.Monster);
            selected.Stats.SetBase(StatId.MaxHp, 20);
            selected.Stats.SetBase(StatId.Hp, 20);
            board.PlaceCard(selected, FindEmptyBoardSlot(board));

            var item = content.CreateDraft("help.fireball").Create(registry);
            content.ApplyContentToCard(item);
            deck.AddToItemSlots(item);

            var result = architecture.SendCommand(new UseItemCommand(item.Uid, new[] { selected.Uid }, "Armor"));

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(15, selected.Stats.GetBase(StatId.Hp));
            Assert.AreEqual(5, unselected.Stats.GetBase(StatId.Hp));
            Assert.AreEqual(ZoneId.Removed, item.Zone.Value);
            Assert.IsFalse(ContainsUid(deck.ItemSlotUids, item.Uid));

            var itemUsed = LastEventOfType(architecture.GetSystem<IActionPipelineSystem>().EventLog, CoreEventType.ItemUsed);
            Assert.AreEqual(item.Uid, itemUsed.CardUid);
            Assert.AreEqual(selected.Uid, itemUsed.TargetUid);
            StringAssert.Contains("option=Armor", itemUsed.Message);
            StringAssert.Contains(selected.Uid.ToString(), itemUsed.Message);

            var removed = LastEventOfType(architecture.GetSystem<IActionPipelineSystem>().EventLog, CoreEventType.CardRemoved);
            Assert.AreEqual(item.Uid, removed.CardUid);
            Assert.AreEqual("useItem", removed.Message);
        }

        [Test]
        public void NodeTailSkipRewardSelectRoomAndEnterAdvancesNode()
        {
            var architecture = NineGridArchitecture.Current;
            P5CatalogTestSupport.RegisterCatalog(architecture);

            var player = architecture.GetModel<PlayerModel>();
            var run = architecture.GetModel<RunModel>();
            var pending = architecture.GetModel<PendingChoiceModel>();
            var phaseSystem = architecture.GetSystem<IPhaseSystem>();
            var pipeline = architecture.GetSystem<IActionPipelineSystem>();
            var options = new NodeDeckOptions { PlayerOpeningCount = 0, EnemyOpeningCount = 1 }
                .AddEnemyCard(new CardDraft("monster.tail", CardKind.Monster)
                {
                    MaxHp = 1,
                    Attack = 0
                });

            architecture.SendCommand(new StartNodeCommand(options));
            var attackResult = architecture.SendCommand(new AttackCommand(FindFirstMonsterSlot()));
            Assert.IsTrue(attackResult.Accepted);
            Assert.AreEqual(GamePhase.RewardItemChoice, phaseSystem.CurrentPhase);
            Assert.AreEqual(PendingChoiceKind.Reward, pending.Kind.Value);
            Assert.AreEqual("help.choice", pending.PoolId.Value);
            Assert.AreEqual(3, pending.RewardOptions.Count);

            var coinsBeforeSkip = player.Coins.Value;
            var skipResult = architecture.SendCommand(new SkipHelpChoiceCommand());
            Assert.IsTrue(skipResult.Accepted);
            Assert.AreEqual(coinsBeforeSkip + 10, player.Coins.Value);
            Assert.AreEqual(GamePhase.RoomChoice, phaseSystem.CurrentPhase);
            Assert.AreEqual(PendingChoiceKind.Room, pending.Kind.Value);
            Assert.AreEqual(2, pending.RoomOptions.Count);
            Assert.IsTrue(phaseSystem.CanExecute(GameCommandKind.SelectRoom));
            Assert.IsFalse(phaseSystem.CanExecute(GameCommandKind.StartNode));

            var selectedRoom = pending.RoomOptions[0];
            var selectRoomResult = architecture.SendCommand(new SelectRoomCommand(0));
            Assert.IsTrue(selectRoomResult.Accepted);
            Assert.AreEqual(GamePhase.RoomEvent, phaseSystem.CurrentPhase);
            Assert.AreEqual(selectedRoom, pending.SelectedRoom.Value);
            Assert.IsTrue(phaseSystem.CanExecute(GameCommandKind.EnterRoom));

            var enterRoomResult = architecture.SendCommand(new EnterRoomCommand());
            Assert.IsTrue(enterRoomResult.Accepted);
            Assert.AreEqual(GamePhase.NodeCompleted, phaseSystem.CurrentPhase);
            Assert.AreEqual(1, run.NodeIndex.Value);
            Assert.AreEqual(PendingChoiceKind.None, pending.Kind.Value);
            Assert.IsTrue(phaseSystem.CanExecute(GameCommandKind.StartNode));
            Assert.IsTrue(pipeline.EventLog.Contains(CoreEventType.RewardSkipped));
            Assert.IsTrue(pipeline.EventLog.Contains(CoreEventType.RoomChoicesOffered));
            Assert.IsTrue(pipeline.EventLog.Contains(CoreEventType.RoomSelected));
            Assert.IsTrue(pipeline.EventLog.Contains(CoreEventType.RoomResolved));
            Assert.IsTrue(pipeline.EventLog.Contains(CoreEventType.NodeAdvanced));
        }

        [Test]
        public void RewardChoiceOverlayStillAllowsBoardPickupWithoutRepeatingCompletion()
        {
            var architecture = NineGridArchitecture.Current;
            var board = architecture.GetModel<BoardModel>();
            var registry = architecture.GetModel<CardRegistry>();
            var player = architecture.GetModel<PlayerModel>();
            var phaseSystem = architecture.GetSystem<IPhaseSystem>();
            var pipeline = architecture.GetSystem<IActionPipelineSystem>();
            var options = new NodeDeckOptions { PlayerOpeningCount = 0, EnemyOpeningCount = 1 }
                .AddEnemyCard(new CardDraft("monster.tail", CardKind.Monster)
                {
                    MaxHp = 1,
                    Attack = 0
                });

            architecture.SendCommand(new StartNodeCommand(options));
            var attackResult = architecture.SendCommand(new AttackCommand(FindFirstMonsterSlot()));
            Assert.IsTrue(attackResult.Accepted);
            Assert.AreEqual(GamePhase.RewardItemChoice, phaseSystem.CurrentPhase);
            var help = new CardDraft("help.leftover", CardKind.HelpCard) { GoldReward = 3 }.Create(registry);
            var helpSlot = FindAdjacentSlot(
                board.AvatarSlot.Value,
                board,
                slot => board.IsEmpty(slot),
                "adjacent slot for post-clear help pickup");
            board.PlaceCard(help, helpSlot);
            var rewardOfferCount = EventsOfType(pipeline.EventLog, CoreEventType.RewardOffered).Count;
            var interactionBefore = player.InteractionCount.Value;
            var pickupResult = architecture.SendCommand(new PickupItemCommand(helpSlot));

            Assert.IsTrue(pickupResult.Accepted);
            Assert.AreEqual(GamePhase.RewardItemChoice, phaseSystem.CurrentPhase);
            Assert.AreEqual(0, board.GetCardUid(helpSlot));
            Assert.AreEqual(3, player.Coins.Value);
            Assert.AreEqual(interactionBefore, player.InteractionCount.Value);
            Assert.AreEqual(rewardOfferCount, EventsOfType(pipeline.EventLog, CoreEventType.RewardOffered).Count);
        }

        [Test]
        public void ChestCardMidFightTransitionsToRewardItemChoiceAndAllowsSelection()
        {
            var architecture = NineGridArchitecture.Current;
            P5CatalogTestSupport.RegisterCatalog(architecture);

            var registry = architecture.GetModel<CardRegistry>();
            var deck = architecture.GetModel<DeckModel>();
            var content = architecture.GetSystem<IContentSystem>();
            var pending = architecture.GetModel<PendingChoiceModel>();
            var phaseSystem = architecture.GetSystem<IPhaseSystem>();
            var pipeline = architecture.GetSystem<IActionPipelineSystem>();
            var player = architecture.GetModel<PlayerModel>();

            EnterInteractionLoop(architecture);
            Assert.AreEqual(GamePhase.InteractionLoop, phaseSystem.CurrentPhase);

            var chest = content.CreateDraft("help.common_chest_card").Create(registry);
            content.ApplyContentToCard(chest);
            deck.AddToItemSlots(chest);

            var useResult = architecture.SendCommand(new UseItemCommand(chest.Uid));
            Assert.IsTrue(useResult.Accepted);
            Assert.AreEqual(GamePhase.RewardItemChoice, phaseSystem.CurrentPhase);
            Assert.AreEqual(PendingChoiceKind.Reward, pending.Kind.Value);
            Assert.AreEqual(3, pending.RewardOptions.Count);
            Assert.IsTrue(phaseSystem.CanExecute(GameCommandKind.SelectReward));
            Assert.IsTrue(phaseSystem.CanExecute(GameCommandKind.SkipHelpChoice));

            var relicDefId = pending.RewardOptions[0].DefId;
            var selectResult = architecture.SendCommand(new SelectRewardCommand(0));
            Assert.IsTrue(selectResult.Accepted);
            Assert.AreEqual(GamePhase.RoomChoice, phaseSystem.CurrentPhase);
            Assert.AreEqual(PendingChoiceKind.Room, pending.Kind.Value);
            var relicGranted = false;
            var relics = player.RelicDefIds;
            for (var i = 0; i < relics.Count; i++)
            {
                if (relics[i] == relicDefId) { relicGranted = true; break; }
            }
            Assert.IsTrue(relicGranted, "Expected relic " + relicDefId + " to be granted.");
            Assert.IsTrue(pipeline.EventLog.Contains(CoreEventType.RewardSelected));
            Assert.IsTrue(pipeline.EventLog.Contains(CoreEventType.RoomChoicesOffered));
        }

        private static void EnterInteractionLoop(IArchitecture architecture)
        {
            var options = new NodeDeckOptions { PlayerOpeningCount = 0, EnemyOpeningCount = 1 }
                .AddEnemyCard(new CardDraft("monster.blocker", CardKind.Monster)
                {
                    MaxHp = 5,
                    Attack = 0
                });
            var startResult = architecture.SendCommand(new StartNodeCommand(options));
            Assert.IsTrue(startResult.Accepted);
            Assert.AreEqual(
                GamePhase.InteractionLoop,
                architecture.GetSystem<IPhaseSystem>().CurrentPhase);
        }

        private static void AssertUseItemRejected(
            IArchitecture architecture,
            UseItemCommand command,
            GameCommandKind expectedCommand,
            int expectedCardUid)
        {
            AssertUseCommandRejected(architecture, command, expectedCommand, expectedCardUid);
        }

        private static void AssertUseCommandRejected(
            IArchitecture architecture,
            ICommand<CoreCommandResult> command,
            GameCommandKind expectedCommand,
            int expectedCardUid = 0)
        {
            var pipeline = architecture.GetSystem<IActionPipelineSystem>();
            Evt_ActionRejected rejected = null;
            architecture.RegisterEvent<Evt_ActionRejected>(evt => rejected = evt);

            var result = architecture.SendCommand(command);

            Assert.IsFalse(result.Accepted);
            Assert.IsNotNull(rejected);
            Assert.AreEqual(expectedCommand, rejected.Command);
            if (expectedCardUid != 0)
            {
                Assert.AreEqual(expectedCardUid, rejected.CardUid);
            }

            Assert.IsTrue(pipeline.EventLog.Contains(CoreEventType.ActionRejected));
        }

        private static int FindBoardCardUid(
            BoardModel board,
            CardRegistry registry,
            Func<CardInstance, bool> predicate)
        {
            foreach (var uid in board.BoardCardUids())
            {
                if (predicate(registry.Get(uid)))
                {
                    return uid;
                }
            }

            Assert.Fail("Could not find board card matching predicate.");
            return 0;
        }

        private static SlotId FindSlotOfCard(BoardModel board, int cardUid)
        {
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                if (board.GetCardUid(slot) == cardUid)
                {
                    return slot;
                }
            }

            Assert.Fail("Card uid " + cardUid + " is not on the board.");
            return SlotId.None;
        }

        private static void RotateUntilAdjacent(
            IArchitecture architecture,
            SlotId avatarSlot,
            BoardModel board,
            int cardUid)
        {
            var cardSlot = FindSlotOfCard(board, cardUid);
            if (avatarSlot.IsAdjacentTo(cardSlot))
            {
                return;
            }

            var probeSlots = new[]
            {
                SlotId.Board(2),
                SlotId.Board(4),
                SlotId.Board(6),
                SlotId.Board(8)
            };

            for (var attempt = 0; attempt < 8; attempt++)
            {
                cardSlot = FindSlotOfCard(board, cardUid);
                if (avatarSlot.IsAdjacentTo(cardSlot))
                {
                    return;
                }

                SlotId rotateSlot = SlotId.None;
                for (var i = 0; i < probeSlots.Length; i++)
                {
                    if (probeSlots[i] != avatarSlot && board.IsEmpty(probeSlots[i]))
                    {
                        rotateSlot = probeSlots[i];
                        break;
                    }
                }

                if (rotateSlot == SlotId.None)
                {
                    Assert.Fail(
                        "Could not find adjacent empty slot to rotate toward card #"
                        + cardUid
                        + " at "
                        + cardSlot
                        + ".");
                }

                var clickResult = architecture.SendCommand(new ClickEmptyCommand(rotateSlot));
                Assert.IsTrue(clickResult.Accepted);
            }

            Assert.Fail("Card #" + cardUid + " never became adjacent to avatar.");
        }

        private static SlotId TryFindAdjacentSlot(
            SlotId avatarSlot,
            BoardModel board,
            Func<SlotId, bool> predicate)
        {
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                if (slot == avatarSlot || !avatarSlot.IsAdjacentTo(slot))
                {
                    continue;
                }

                if (predicate(slot))
                {
                    return slot;
                }
            }

            return SlotId.None;
        }

        private static SlotId FindAdjacentSlot(
            SlotId avatarSlot,
            BoardModel board,
            Func<SlotId, bool> predicate,
            string description)
        {
            var slot = TryFindAdjacentSlot(avatarSlot, board, predicate);
            if (slot == SlotId.None)
            {
                Assert.Fail("Could not find " + description + ".");
            }

            return slot;
        }

        private static SlotId FindNonAdjacentOccupiedOrEmptySlot(SlotId avatarSlot, BoardModel board)
        {
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                if (slot == avatarSlot || avatarSlot.IsAdjacentTo(slot))
                {
                    continue;
                }

                return slot;
            }

            Assert.Fail("Could not find a non-adjacent board slot.");
            return SlotId.None;
        }

        private static bool HasEventSince(EventLog eventLog, int startIndex, CoreEventType type)
        {
            var entries = eventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                if (entries[i].Type == type)
                {
                    return true;
                }
            }

            return false;
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

        private static CoreGameEvent LastEventOfType(EventLog eventLog, CoreEventType type)
        {
            for (var i = eventLog.Entries.Count - 1; i >= 0; i--)
            {
                if (eventLog.Entries[i].Type == type)
                {
                    return eventLog.Entries[i];
                }
            }

            Assert.Fail("Missing event: " + type);
            return null;
        }

        private static SlotId FindEmptyBoardSlot(BoardModel board)
        {
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                if (slot != board.AvatarSlot.Value && board.IsEmpty(slot))
                {
                    return slot;
                }
            }

            Assert.Fail("Could not find empty board slot.");
            return SlotId.None;
        }

        private static bool ContainsUid(IReadOnlyList<int> values, int uid)
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] == uid)
                {
                    return true;
                }
            }

            return false;
        }

        private static SlotId FindFirstMonsterSlot()
        {
            var registry = NineGridArchitecture.Current.GetModel<CardRegistry>();
            var board = NineGridArchitecture.Current.GetModel<BoardModel>();
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                var uid = board.GetCardUid(slot);
                if (uid == 0)
                {
                    continue;
                }

                if (registry.Get(uid).Kind == CardKind.Monster)
                {
                    return slot;
                }
            }

            return SlotId.None;
        }

    }
}

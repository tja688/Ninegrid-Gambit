using System.Collections.Generic;
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
    /// 局内宝箱 / 属性提升 / 通关奖励：PendingChoice 与相位门禁契约。
    /// </summary>
    public sealed class MidBattleRewardChoiceContractTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, BuildTestCatalog());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 7UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void UseItem_Chest_StaysInInteractionLoop_WithPendingRewardGate()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 5, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);
            var chestUid = SpawnHelpIntoItemSlots("help.common_chest_card");

            var result = mPhase.ApplyUseItem(chestUid, null, null);
            Assert.IsTrue(result.Accepted, result.Reason);

            Assert.AreEqual(GamePhase.InteractionLoop, mPhase.CurrentPhase);
            Assert.AreEqual(PendingChoiceKind.Reward, mArch.GetModel<PendingChoiceModel>().Kind.Value);
            Assert.Greater(mArch.GetModel<PendingChoiceModel>().RewardOptions.Count, 0);

            Assert.IsTrue(mPhase.CanExecute(GameCommandKind.SelectReward));
            Assert.IsTrue(mPhase.CanExecute(GameCommandKind.SkipHelpChoice));
            Assert.IsFalse(mPhase.CanExecute(GameCommandKind.ClickEmpty));
            Assert.IsFalse(mPhase.CanExecute(GameCommandKind.Attack));
            Assert.IsFalse(mPhase.CanExecute(GameCommandKind.UseItem));

            Assert.IsFalse(mPhase.ClickEmpty(sAdjacentSlot).Accepted);
        }

        [Test]
        public void SelectReward_AfterMidBattleChest_RestoresInteractionCommands()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 5, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);
            var chestUid = SpawnHelpIntoItemSlots("help.common_chest_card");
            Assert.IsTrue(mPhase.ApplyUseItem(chestUid, null, null).Accepted);

            var select = mPhase.SelectReward(0);
            Assert.IsTrue(select.Accepted, select.Reason);

            Assert.AreEqual(GamePhase.InteractionLoop, mPhase.CurrentPhase);
            Assert.AreEqual(PendingChoiceKind.None, mArch.GetModel<PendingChoiceModel>().Kind.Value);
            Assert.IsTrue(mPhase.CanExecute(GameCommandKind.ClickEmpty));
            Assert.IsTrue(mPhase.CanExecute(GameCommandKind.Attack));
            Assert.IsFalse(mPhase.CanExecute(GameCommandKind.SelectReward));
        }

        [Test]
        public void SelectReward_WhilePresentationInputLocked_StillLegal_ForMidBattlePending()
        {
            // 导演锁步：UseItem Present 未 Ack 时 IsInputLocked=true，Bounce 点选必须仍能 SelectReward。
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 5, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);
            var chestUid = SpawnHelpIntoItemSlots("help.common_chest_card");
            Assert.IsTrue(mPhase.ApplyUseItem(chestUid, null, null).Accepted);

            var sync = mArch.GetSystem<IPresentationSyncSystem>();
            var map = PresentationEventMap.Get(CoreEventType.ItemUsed);
            Assert.IsTrue(map.LocksInput);
            var blocking = new[]
            {
                new PresentationInstruction(new CoreGameEvent(CoreEventType.ItemUsed, 1, "UseItem"), map),
            };
            sync.OpenBatch(new PresentationBatch(42, blocking, null));
            Assert.IsTrue(sync.IsInputLocked);

            Assert.IsTrue(mPhase.CanExecute(GameCommandKind.PresentationFinished));
            Assert.IsTrue(mPhase.CanExecute(GameCommandKind.SelectReward));
            Assert.IsTrue(mPhase.CanExecute(GameCommandKind.SkipHelpChoice));
            Assert.IsFalse(mPhase.CanExecute(GameCommandKind.ClickEmpty));

            var select = mPhase.SelectReward(0);
            Assert.IsTrue(select.Accepted, select.Reason);
            Assert.AreEqual(PendingChoiceKind.None, mArch.GetModel<PendingChoiceModel>().Kind.Value);

            Assert.IsTrue(sync.FinishBatch(42).Accepted);
        }

        [Test]
        public void UseItem_StatBoost_WithSelectedOption_ModifiesBaseStat_NoRewardPending()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 5, attack: 0)).Accepted);
            var avatar = mArch.GetModel<CardRegistry>().Get(mArch.GetModel<BoardModel>().AvatarUid.Value);
            var attackBefore = (int)System.Math.Round(avatar.Stats.GetBase(StatId.Attack));
            var boostUid = SpawnHelpIntoItemSlots("help.stat_boost_card");
            var startIndex = mPipeline.EventLog.Entries.Count;

            var result = mPhase.ApplyUseItem(boostUid, null, "Attack");
            Assert.IsTrue(result.Accepted, result.Reason);

            Assert.AreEqual(PendingChoiceKind.None, mArch.GetModel<PendingChoiceModel>().Kind.Value);
            Assert.AreEqual(GamePhase.InteractionLoop, mPhase.CurrentPhase);
            Assert.AreEqual(attackBefore + 1, (int)System.Math.Round(avatar.Stats.GetBase(StatId.Attack)));
            Assert.IsTrue(ContainsTypeSince(startIndex, CoreEventType.BaseStatModified));
        }

        [Test]
        public void UseItem_StatBoost_WithoutOption_DoesNotModifyAttack()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 5, attack: 0)).Accepted);
            var avatar = mArch.GetModel<CardRegistry>().Get(mArch.GetModel<BoardModel>().AvatarUid.Value);
            var attackBefore = (int)System.Math.Round(avatar.Stats.GetBase(StatId.Attack));
            var boostUid = SpawnHelpIntoItemSlots("help.stat_boost_card");
            var startIndex = mPipeline.EventLog.Entries.Count;

            var result = mPhase.ApplyUseItem(boostUid, null, null);
            Assert.IsTrue(result.Accepted, result.Reason);

            Assert.AreEqual(attackBefore, (int)System.Math.Round(avatar.Stats.GetBase(StatId.Attack)));
            Assert.IsFalse(ContainsTypeSince(startIndex, CoreEventType.BaseStatModified));
            Assert.AreEqual(PendingChoiceKind.None, mArch.GetModel<PendingChoiceModel>().Kind.Value);
        }

        [Test]
        public void CompleteNodeIfCleared_EntersRoomChoice_WithoutHelpChoice()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);

            var result = mPhase.Attack(sAdjacentSlot);
            Assert.IsTrue(result.Accepted, result.Reason);

            Assert.AreEqual(GamePhase.RoomChoice, mPhase.CurrentPhase);
            Assert.AreEqual(PendingChoiceKind.Room, mArch.GetModel<PendingChoiceModel>().Kind.Value);
            Assert.AreNotEqual("help.choice", mArch.GetModel<PendingChoiceModel>().PoolId.Value);
            Assert.IsTrue(mPhase.CanExecute(GameCommandKind.SelectRoom));
            Assert.IsFalse(mPhase.CanExecute(GameCommandKind.SelectReward));
            Assert.IsFalse(mPhase.CanExecute(GameCommandKind.ClickEmpty));
        }

        [Test]
        public void SelectReward_AfterMidBattleChest_PersistsRelicAcrossStartNode()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);
            var chestUid = SpawnHelpIntoItemSlots("help.common_chest_card");
            Assert.IsTrue(mPhase.ApplyUseItem(chestUid, null, null).Accepted);

            var pending = mArch.GetModel<PendingChoiceModel>();
            Assert.Greater(pending.RewardOptions.Count, 0);
            var relicDefId = pending.RewardOptions[0].DefId;
            Assert.IsTrue(mPhase.SelectReward(0).Accepted);

            var player = mArch.GetModel<PlayerModel>();
            Assert.IsTrue(ContainsRelic(player, relicDefId), "局内宝箱选中后应写入 PlayerModel");

            // 清场 → 选房 → 入房 → 下一节点，遗物必须仍在。
            PlaceSoleBoardCardAt(sAdjacentSlot);
            var kill = mPhase.Attack(sAdjacentSlot);
            Assert.IsTrue(kill.Accepted, kill.Reason);
            Assert.AreEqual(GamePhase.RoomChoice, mPhase.CurrentPhase);
            Assert.IsTrue(mPhase.SelectRoom(0).Accepted);
            Assert.IsTrue(mPhase.EnterRoom().Accepted);
            Assert.AreEqual(GamePhase.NodeCompleted, mPhase.CurrentPhase);

            Assert.IsTrue(
                ContainsRelic(player, relicDefId),
                "跨关后局内宝箱遗物不得因 Bootstrap/清场丢失");

            var options = mArch.GetSystem<IRewardSystem>().BuildNodeDeckOptions(1, null);
            options.AddEnemyCard(new CardDraft("monster.test", CardKind.Monster) { MaxHp = 20, Attack = 0 });
            options.EnemyOpeningCount = 1;
            Assert.IsTrue(mPhase.StartNode(options).Accepted);
            Assert.IsTrue(
                ContainsRelic(player, relicDefId),
                "下一节点 StartNode 后遗物仍应保留");
        }

        private static bool ContainsRelic(PlayerModel player, string defId)
        {
            var relics = player.RelicDefIds;
            for (var i = 0; i < relics.Count; i++)
            {
                if (relics[i] == defId)
                {
                    return true;
                }
            }

            return false;
        }

        private int SpawnHelpIntoItemSlots(string defId)
        {
            var pipeline = mPipeline;
            pipeline.Enqueue(new SpawnCardAction(defId, CardKind.HelpCard, ZoneId.ItemSlots, SlotId.None, 1, "test"));
            Assert.Greater(pipeline.RunToCompletion(), 0);

            var deck = mArch.GetModel<DeckModel>();
            Assert.Greater(deck.ItemSlotUids.Count, 0);
            return deck.ItemSlotUids[deck.ItemSlotUids.Count - 1];
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

        private static GameContentCatalog BuildTestCatalog()
        {
            var catalog = new GameContentCatalog();
            catalog.AddEffect(new ContentEffectDefinition(
                "help.common_chest_card.use",
                EffectContainerType.HelpCard,
                TriggeredJson(
                    "help.common_chest_card.use",
                    "{\"atom\":\"OnSelfUsed\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"OfferRewardChoice\",\"poolId\":\"relic.common_chest\"}"),
                ContentImplementationState.Implemented,
                "chest"));
            catalog.AddEffect(new ContentEffectDefinition(
                "help.stat_boost_card.use",
                EffectContainerType.HelpCard,
                TriggeredJson(
                    "help.stat_boost_card.use",
                    "{\"atom\":\"OnSelfUsed\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"Conditional\",\"condition\":{\"atom\":\"SelectedOption\",\"option\":\"Attack\"},"
                    + "\"action\":{\"atom\":\"ModifyBaseStat\",\"stat\":\"Attack\",\"delta\":1,\"reason\":\"statBoost.attack\"},"
                    + "\"elseAction\":{\"atom\":\"Conditional\",\"condition\":{\"atom\":\"SelectedOption\",\"option\":\"Armor\"},"
                    + "\"action\":{\"atom\":\"ModifyBaseStat\",\"stat\":\"Armor\",\"delta\":1,\"reason\":\"statBoost.armor\"},"
                    + "\"elseAction\":{\"atom\":\"Conditional\",\"condition\":{\"atom\":\"SelectedOption\",\"option\":\"Hp\"},"
                    + "\"action\":{\"atom\":\"ModifyBaseStat\",\"stat\":\"MaxHp\",\"delta\":2,\"reason\":\"statBoost.hp\"}}}}}"),
                ContentImplementationState.Implemented,
                "stat boost"));

            catalog.AddCard(new CardContentDefinition("help.common_chest_card", "普通宝箱卡", CardKind.HelpCard)
                .AddEffect("help.common_chest_card.use"));
            catalog.AddCard(new CardContentDefinition("help.stat_boost_card", "属性提升卡", CardKind.HelpCard)
                .AddEffect("help.stat_boost_card.use"));
            catalog.AddCard(new CardContentDefinition("help.gold_card", "金币卡", CardKind.HelpCard));
            catalog.AddCard(new CardContentDefinition("help.throwing_knife", "飞刀", CardKind.HelpCard));
            catalog.AddCard(new CardContentDefinition("help.healing_potion", "恢复药水", CardKind.HelpCard));

            catalog.AddRelic(new RelicContentDefinition("relic.wood_shield", "木盾", ContentRarity.White, "test"));
            catalog.AddRelic(new RelicContentDefinition("relic.vitality_amulet", "活力护符", ContentRarity.Blue, "test"));
            catalog.AddRelic(new RelicContentDefinition("relic.dragon_scale_armor", "龙鳞甲", ContentRarity.Gold, "test"));

            catalog.Rewards
                .AddPool(new RewardPoolDefinition("relic.common_chest", 3)
                    .Add("relic.wood_shield", CardKind.Relic, 65)
                    .Add("relic.vitality_amulet", CardKind.Relic, 30)
                    .Add("relic.dragon_scale_armor", CardKind.Relic, 5))
                .AddPool(new RewardPoolDefinition("help.choice", 3)
                    .Add("help.gold_card", CardKind.HelpCard, 40)
                    .Add("help.throwing_knife", CardKind.HelpCard, 30)
                    .Add("help.healing_potion", CardKind.HelpCard, 30))
                .AddRoom(new RoomDefinition(RoomKind.Gold, "金币房") { Weight = 1 })
                .AddRoom(new RoomDefinition(RoomKind.Elite, "困难房") { Weight = 1 })
                .AddRoom(new RoomDefinition(RoomKind.Fountain, "恢复房") { Weight = 1 });

            return catalog;
        }

        private static string TriggeredJson(string id, string trigger, string target, string action)
        {
            return "{"
                + "\"id\":\"" + id + "\","
                + "\"typeTag\":\"【类型帮助卡】\","
                + "\"containerType\":\"HelpCard\","
                + "\"kind\":\"Triggered\","
                + "\"requires\":[\"ActivatedByUse\",\"HasOwnerEntity\"],"
                + "\"trigger\":" + trigger + ","
                + "\"target\":" + target + ","
                + "\"action\":" + action
                + "}";
        }
    }
}

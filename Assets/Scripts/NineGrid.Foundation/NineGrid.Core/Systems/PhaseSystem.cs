using System.Collections.Generic;
using NineGrid.Core.Content;
using NineGrid.Core.Stats;
using QFramework;

namespace NineGrid.Core.Systems
{
        public interface IPhaseSystem : ISystem
        {
            GamePhase CurrentPhase { get; }
            IReadOnlyList<GameCommandKind> LegalCommands { get; }
            bool CanExecute(GameCommandKind command);
            CoreCommandResult StartNode(NodeDeckOptions options);
            CoreCommandResult Attack(SlotId targetSlot);
            /// <summary>
            /// 表现层可信命中：仅一段伤害（含致死 Kill/Defeat），无门禁、无反击、无旋转。
            /// </summary>
            CoreCommandResult ApplyCombatHit(int attackerUid, int targetUid);
            /// <summary>
            /// 击杀后盘面：交互计数 + 旋转 + 补牌 + 清场判定。
            /// </summary>
            CoreCommandResult ResolvePostKillBoard();
            CoreCommandResult PickupItem(SlotId targetSlot);
            /// <summary>
            /// 表现层可信拾取：无相邻门禁；InteractionLoop 下仍会旋转补牌。
            /// </summary>
            CoreCommandResult ApplyPickupItem(SlotId targetSlot);
            /// <summary>
            /// 表现层可信使用道具：无相位门禁（仍校验 ItemSlots / 种类）。
            /// </summary>
            CoreCommandResult ApplyUseItem(int itemUid, IReadOnlyList<int> selectedCardUids, string selectedOption);
            CoreCommandResult ClickEmpty(SlotId targetSlot);
            CoreCommandResult UseItem(int itemUid);
            CoreCommandResult UseItem(int itemUid, IReadOnlyList<int> selectedCardUids, string selectedOption);
            CoreCommandResult SelectReward(int optionIndex);
            CoreCommandResult SkipHelpChoice();
            CoreCommandResult SelectRoom(int optionIndex);
            CoreCommandResult EnterRoom();
        }

    public sealed class PhaseSystem : AbstractSystem, IPhaseSystem
    {
        private readonly List<GameCommandKind> mLegalCommands = new List<GameCommandKind>();
        private bool mInRoomRewardContext;

        public GamePhase CurrentPhase
        {
            get { return this.GetModel<RunModel>().Phase.Value; }
        }

        public IReadOnlyList<GameCommandKind> LegalCommands
        {
            get
            {
                RefreshLegalCommands();
                return mLegalCommands;
            }
        }

        protected override void OnInit()
        {
            RefreshLegalCommands();
        }

        public bool CanExecute(GameCommandKind command)
        {
            RefreshLegalCommands();
            return mLegalCommands.Contains(command);
        }

        public CoreCommandResult StartNode(NodeDeckOptions options)
        {
            if (!CanExecute(GameCommandKind.StartNode))
            {
                return Reject(GameCommandKind.StartNode, "Command is not legal in phase " + CurrentPhase, SlotId.None, 0);
            }

            mInRoomRewardContext = false;
            options = options ?? NodeDeckOptions.CreateDefaultBattle();
            var pipeline = this.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new ClearPendingChoicesAction());
            pipeline.Enqueue(new ChangePhaseAction(GamePhase.BuildEnemyPool));
            pipeline.Enqueue(new SetupNodeDeckAction(options));
            pipeline.Enqueue(new ChangePhaseAction(GamePhase.ResetNode));
            pipeline.Enqueue(new ChangePhaseAction(GamePhase.DealOpeningCards));
            pipeline.Enqueue(new OpeningDealAction(options));
            pipeline.Enqueue(new FillEmptySlotsAction());
            pipeline.Enqueue(new ResetCurrentArmorAction());
            pipeline.Enqueue(new NodeStartedAction());
            pipeline.Enqueue(new ChangePhaseAction(GamePhase.InteractionLoop));
            var resolved = pipeline.RunToCompletion();
            resolved += CompleteNodeIfCleared();
            return CoreCommandResult.Accept(resolved);
        }

        public CoreCommandResult Attack(SlotId targetSlot)
        {
            if (!CanExecute(GameCommandKind.Attack))
            {
                return Reject(GameCommandKind.Attack, "Command is not legal in phase " + CurrentPhase, targetSlot, 0);
            }

            var registry = this.GetModel<CardRegistry>();
            var board = this.GetModel<BoardModel>();
            if (!targetSlot.IsBoardSlot || targetSlot == board.AvatarSlot.Value)
            {
                return Reject(GameCommandKind.Attack, "Attack target is not a board card slot.", targetSlot, 0);
            }

            var targetUid = board.GetCardUid(targetSlot);
            if (targetUid == 0)
            {
                return Reject(GameCommandKind.Attack, "Attack target slot is empty.", targetSlot, 0);
            }

            var target = registry.Get(targetUid);
            if (target.Kind != CardKind.Monster)
            {
                return Reject(GameCommandKind.Attack, "Attack target is not a monster.", targetSlot, targetUid);
            }

            if (!this.GetSystem<IBoardSystem>().AreAdjacent(board.AvatarSlot.Value, targetSlot))
            {
                return Reject(GameCommandKind.Attack, "Attack target is outside interaction range.", targetSlot, targetUid);
            }

            if (!CanAttackTargetUnderRules(target))
            {
                return Reject(GameCommandKind.Attack, "Attack target is restricted by taunt.", targetSlot, targetUid);
            }

            var avatar = registry.Get(board.AvatarUid.Value);
            var statSystem = this.GetSystem<IStatSystem>();
            var pipeline = this.GetSystem<IActionPipelineSystem>();
            var startIndex = pipeline.EventLog.Entries.Count;
            pipeline.Enqueue(new BeginPlayerMonsterEngagementAction(targetUid));
            var avatarFirstStrike = HasFirstStrike(statSystem, avatar);
            var targetFirstStrike = HasFirstStrike(statSystem, target);
            if (targetFirstStrike && !avatarFirstStrike)
            {
                pipeline.Enqueue(new DealDamageAction(target.Uid, avatar.Uid, GetAttackDamage(statSystem, target)));
                pipeline.Enqueue(new ConditionalDealDamageIfAliveAction(avatar.Uid, target.Uid, GetAttackDamage(statSystem, avatar)));
            }
            else
            {
                pipeline.Enqueue(new DealDamageAction(avatar.Uid, targetUid, GetAttackDamage(statSystem, avatar)));
                pipeline.Enqueue(new ConditionalDealDamageIfAliveAction(target.Uid, avatar.Uid, GetAttackDamage(statSystem, target)));
            }

            pipeline.Enqueue(new EndBattleScopeCleanupAction());
            var resolved = pipeline.RunToCompletion();

            if (ContainsEventSince(startIndex, CoreEventType.CardKilled, targetUid))
            {
                resolved += ResolveInteractiveRotation();
            }

            return CoreCommandResult.Accept(resolved);
        }

        public CoreCommandResult ApplyCombatHit(int attackerUid, int targetUid)
        {
            var registry = this.GetModel<CardRegistry>();
            if (attackerUid <= 0 || !registry.TryGet(attackerUid, out var attacker))
            {
                return CoreCommandResult.Reject("Combat hit attacker uid is invalid.");
            }

            if (targetUid <= 0 || !registry.TryGet(targetUid, out var target))
            {
                return CoreCommandResult.Reject("Combat hit target uid is invalid.");
            }

            if (target.Zone.Value == ZoneId.Graveyard || target.Zone.Value == ZoneId.Removed)
            {
                return CoreCommandResult.Reject("Combat hit target is already removed.");
            }

            var statSystem = this.GetSystem<IStatSystem>();
            var pipeline = this.GetSystem<IActionPipelineSystem>();
            var engagedMonsterUid = 0;
            if (TryGetPlayerMonsterEngagement(registry, attackerUid, targetUid, out engagedMonsterUid))
            {
                pipeline.Enqueue(new BeginPlayerMonsterEngagementAction(engagedMonsterUid));
            }

            pipeline.Enqueue(new DealDamageAction(attackerUid, targetUid, GetAttackDamage(statSystem, attacker)));
            if (engagedMonsterUid > 0)
            {
                pipeline.Enqueue(new EndBattleScopeCleanupAction());
            }

            var resolved = pipeline.RunToCompletion();
            return CoreCommandResult.Accept(resolved);
        }

        public CoreCommandResult ResolvePostKillBoard()
        {
            return CoreCommandResult.Accept(ResolveInteractiveRotation());
        }

        // 九宫格互动范围：当前 = Avatar 槽正交邻接（IBoardSystem.AreAdjacent）。
        // StatId.InteractionRange / 职业范围尚未接入；多职业时从此处与表现层共用判定源扩展。
        public CoreCommandResult PickupItem(SlotId targetSlot)
        {
            if (!CanExecute(GameCommandKind.PickupItem))
            {
                return Reject(GameCommandKind.PickupItem, "Command is not legal in phase " + CurrentPhase, targetSlot, 0);
            }

            var board = this.GetModel<BoardModel>();
            if (!targetSlot.IsBoardSlot || targetSlot == board.AvatarSlot.Value)
            {
                return Reject(GameCommandKind.PickupItem, "Pickup target is not a board card slot.", targetSlot, 0);
            }

            if (!this.GetSystem<IBoardSystem>().AreAdjacent(board.AvatarSlot.Value, targetSlot))
            {
                return Reject(GameCommandKind.PickupItem, "Pickup target is outside interaction range.", targetSlot, 0);
            }

            return ExecutePickupItem(targetSlot, requireInteractionLoopRotate: true);
        }

        // 表现可信拾取入口：邻接规则与 PickupItem 一致（见上互动范围备注）。
        public CoreCommandResult ApplyPickupItem(SlotId targetSlot)
        {
            var board = this.GetModel<BoardModel>();
            if (!targetSlot.IsBoardSlot || targetSlot == board.AvatarSlot.Value)
            {
                return Reject(GameCommandKind.PickupItem, "Pickup target is not a board card slot.", targetSlot, 0);
            }

            if (!this.GetSystem<IBoardSystem>().AreAdjacent(board.AvatarSlot.Value, targetSlot))
            {
                return Reject(GameCommandKind.PickupItem, "Pickup target is outside interaction range.", targetSlot, 0);
            }

            return ExecutePickupItem(targetSlot, requireInteractionLoopRotate: true);
        }

        public CoreCommandResult ApplyUseItem(int itemUid, IReadOnlyList<int> selectedCardUids, string selectedOption)
        {
            if (itemUid <= 0)
            {
                return Reject(GameCommandKind.UseItem, "Item uid is invalid.", SlotId.None, itemUid);
            }

            var registry = this.GetModel<CardRegistry>();
            CardInstance card;
            if (!registry.TryGet(itemUid, out card))
            {
                return Reject(GameCommandKind.UseItem, "Item card uid does not exist.", SlotId.None, itemUid);
            }

            if (card.Zone.Value != ZoneId.ItemSlots)
            {
                return Reject(GameCommandKind.UseItem, "Item is not in item slots.", SlotId.None, itemUid);
            }

            if (!IsRegisteredInItemSlots(this.GetModel<DeckModel>(), itemUid))
            {
                return Reject(GameCommandKind.UseItem, "Item is not registered in item slots.", SlotId.None, itemUid);
            }

            if (!IsUsableItemKind(card.Kind))
            {
                return Reject(GameCommandKind.UseItem, "Card is not a usable item.", SlotId.None, itemUid);
            }

            return ExecuteUseItem(itemUid, card, selectedCardUids, selectedOption);
        }

        private CoreCommandResult ExecutePickupItem(SlotId targetSlot, bool requireInteractionLoopRotate)
        {
            var registry = this.GetModel<CardRegistry>();
            var board = this.GetModel<BoardModel>();
            var cardUid = board.GetCardUid(targetSlot);
            if (cardUid == 0)
            {
                return Reject(GameCommandKind.PickupItem, "Pickup target slot is empty.", targetSlot, 0);
            }

            var card = registry.Get(cardUid);
            if (card.Kind == CardKind.Monster)
            {
                return Reject(GameCommandKind.PickupItem, "Monsters must be attacked, not picked up.", targetSlot, cardUid);
            }

            var shouldRotate = requireInteractionLoopRotate && CurrentPhase == GamePhase.InteractionLoop;
            var pipeline = this.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new PickupCardAction(cardUid));
            var resolved = pipeline.RunToCompletion();
            if (shouldRotate)
            {
                resolved += ResolveInteractiveRotation();
            }

            return CoreCommandResult.Accept(resolved);
        }

        public CoreCommandResult ClickEmpty(SlotId targetSlot)
        {
            if (!CanExecute(GameCommandKind.ClickEmpty))
            {
                return Reject(GameCommandKind.ClickEmpty, "Command is not legal in phase " + CurrentPhase, targetSlot, 0);
            }

            var board = this.GetModel<BoardModel>();
            if (!targetSlot.IsBoardSlot || targetSlot == board.AvatarSlot.Value || !board.IsEmpty(targetSlot))
            {
                return Reject(GameCommandKind.ClickEmpty, "Clicked slot is not empty.", targetSlot, 0);
            }

            if (!this.GetSystem<IBoardSystem>().AreAdjacent(board.AvatarSlot.Value, targetSlot))
            {
                return Reject(GameCommandKind.ClickEmpty, "Clicked slot is outside interaction range.", targetSlot, 0);
            }

            var pipeline = this.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new ClickEmptySlotAction(targetSlot));
            var resolved = pipeline.RunToCompletion();
            resolved += ResolveInteractiveRotation();
            return CoreCommandResult.Accept(resolved);
        }

        public CoreCommandResult UseItem(int itemUid)
        {
            return UseItem(itemUid, null, null);
        }

        public CoreCommandResult UseItem(int itemUid, IReadOnlyList<int> selectedCardUids, string selectedOption)
        {
            if (!CanExecute(GameCommandKind.UseItem))
            {
                return Reject(GameCommandKind.UseItem, "Command is not legal in phase " + CurrentPhase, SlotId.None, itemUid);
            }

            if (itemUid <= 0)
            {
                return Reject(GameCommandKind.UseItem, "Item uid is invalid.", SlotId.None, itemUid);
            }

            var registry = this.GetModel<CardRegistry>();
            CardInstance card;
            if (!registry.TryGet(itemUid, out card))
            {
                return Reject(GameCommandKind.UseItem, "Item card uid does not exist.", SlotId.None, itemUid);
            }

            if (card.Zone.Value != ZoneId.ItemSlots)
            {
                return Reject(GameCommandKind.UseItem, "Item is not in item slots.", SlotId.None, itemUid);
            }

            if (!IsRegisteredInItemSlots(this.GetModel<DeckModel>(), itemUid))
            {
                return Reject(GameCommandKind.UseItem, "Item is not registered in item slots.", SlotId.None, itemUid);
            }

            if (!IsUsableItemKind(card.Kind))
            {
                return Reject(GameCommandKind.UseItem, "Card is not a usable item.", SlotId.None, itemUid);
            }

            return ExecuteUseItem(itemUid, card, selectedCardUids, selectedOption);
        }

        private CoreCommandResult ExecuteUseItem(
            int itemUid,
            CardInstance card,
            IReadOnlyList<int> selectedCardUids,
            string selectedOption)
        {
            var pipeline = this.GetSystem<IActionPipelineSystem>();
            var startIndex = pipeline.EventLog.Entries.Count;
            pipeline.Enqueue(new UseItemAction(itemUid, selectedCardUids, selectedOption));
            var resolved = pipeline.RunToCompletion();
            resolved += ConsumeUsedItemIfStillInItemSlots(itemUid, card.DefId);

            if (ContainsAnyEventSince(startIndex, CoreEventType.CardKilled)
                && CurrentPhase == GamePhase.InteractionLoop)
            {
                resolved += ResolveInteractiveRotation();
            }

            // 局内 OfferRewardChoice（宝箱等）只写 PendingChoice，保持 InteractionLoop；
            // 通关奖励仍由 CompleteNodeIfCleared 进入 RewardItemChoice。
            resolved += CompleteNodeIfCleared();
            return CoreCommandResult.Accept(resolved);
        }

        private int ConsumeUsedItemIfStillInItemSlots(int itemUid, string sourceDefId)
        {
            var registry = this.GetModel<CardRegistry>();
            CardInstance card;
            if (!registry.TryGet(itemUid, out card) || card.Zone.Value != ZoneId.ItemSlots)
            {
                return 0;
            }

            var pipeline = this.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new RemoveCardAction(itemUid, ZoneId.Removed, "useItem", sourceDefId));
            return pipeline.RunToCompletion();
        }

        public CoreCommandResult SelectReward(int optionIndex)
        {
            if (!CanExecute(GameCommandKind.SelectReward))
            {
                return Reject(GameCommandKind.SelectReward, "Command is not legal in phase " + CurrentPhase, SlotId.None, 0);
            }

            var pending = this.GetModel<PendingChoiceModel>();
            if (pending.Kind.Value != PendingChoiceKind.Reward)
            {
                return Reject(GameCommandKind.SelectReward, "No pending reward choice.", SlotId.None, 0);
            }

            if (optionIndex < 0 || optionIndex >= pending.RewardOptions.Count)
            {
                return Reject(GameCommandKind.SelectReward, "Reward option index is out of range.", SlotId.None, 0);
            }

            var entry = pending.RewardOptions[optionIndex];
            var poolId = pending.PoolId.Value ?? string.Empty;
            var pipeline = this.GetSystem<IActionPipelineSystem>();

            // 商店购买：按卡牌 Price 扣金；通关/宝箱等免费池不扣。
            if (poolId == ShopHelpCardsPoolId)
            {
                var price = ResolveShopPrice(entry != null ? entry.DefId : null);
                if (price > 0)
                {
                    var coins = this.GetModel<PlayerModel>().Coins.Value;
                    if (coins < price)
                    {
                        return Reject(
                            GameCommandKind.SelectReward,
                            "Not enough gold",
                            SlotId.None,
                            0);
                    }

                    pipeline.Enqueue(new ModifyGoldAction(
                        -price,
                        "shopBuy:" + (entry.DefId ?? string.Empty),
                        entry.DefId));
                }
            }

            pipeline.Enqueue(new GrantRewardChoiceAction(entry, optionIndex));
            pipeline.Enqueue(new ClearPendingRewardChoiceAction());
            var resolved = ResolvePostRewardChoiceFlow(pipeline, 0);
            return CoreCommandResult.Accept(resolved);
        }

        public CoreCommandResult SkipHelpChoice()
        {
            if (!CanExecute(GameCommandKind.SkipHelpChoice))
            {
                return Reject(GameCommandKind.SkipHelpChoice, "Command is not legal in phase " + CurrentPhase, SlotId.None, 0);
            }

            var pending = this.GetModel<PendingChoiceModel>();
            var isShop = (pending.PoolId.Value ?? string.Empty) == ShopHelpCardsPoolId;
            // 商店离开：不发跳过帮助卡选择的 +金币；通关帮助三选一跳过仍发。
            var resolved = isShop ? 0 : this.GetSystem<IEconomySystem>().AwardSkipHelpChoice();
            var pipeline = this.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new SkipRewardChoiceAction());
            pipeline.Enqueue(new ClearPendingRewardChoiceAction());
            resolved = ResolvePostRewardChoiceFlow(pipeline, resolved);
            return CoreCommandResult.Accept(resolved);
        }

        public CoreCommandResult SelectRoom(int optionIndex)
        {
            if (!CanExecute(GameCommandKind.SelectRoom))
            {
                return Reject(GameCommandKind.SelectRoom, "Command is not legal in phase " + CurrentPhase, SlotId.None, 0);
            }

            var pending = this.GetModel<PendingChoiceModel>();
            if (pending.Kind.Value != PendingChoiceKind.Room)
            {
                return Reject(GameCommandKind.SelectRoom, "No pending room choice.", SlotId.None, 0);
            }

            if (optionIndex < 0 || optionIndex >= pending.RoomOptions.Count)
            {
                return Reject(GameCommandKind.SelectRoom, "Room option index is out of range.", SlotId.None, 0);
            }

            var room = pending.RoomOptions[optionIndex];
            var pipeline = this.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new SelectRoomChoiceAction(optionIndex, room));
            pipeline.Enqueue(new ChangePhaseAction(GamePhase.RoomEvent));
            var resolved = pipeline.RunToCompletion();
            return CoreCommandResult.Accept(resolved);
        }

        public CoreCommandResult EnterRoom()
        {
            if (!CanExecute(GameCommandKind.EnterRoom))
            {
                return Reject(GameCommandKind.EnterRoom, "Command is not legal in phase " + CurrentPhase, SlotId.None, 0);
            }

            var room = this.GetModel<PendingChoiceModel>().SelectedRoom.Value;
            if (room == RoomKind.None)
            {
                return Reject(GameCommandKind.EnterRoom, "No selected room to enter.", SlotId.None, 0);
            }

            var resolved = this.GetSystem<IRewardSystem>().ResolveRoom(room);
            var pipeline = this.GetSystem<IActionPipelineSystem>();

            if (this.GetModel<PendingChoiceModel>().Kind.Value == PendingChoiceKind.Reward)
            {
                mInRoomRewardContext = true;
                pipeline.Enqueue(new ChangePhaseAction(GamePhase.RewardItemChoice));
                resolved += pipeline.RunToCompletion();
                return CoreCommandResult.Accept(resolved);
            }

            pipeline.Enqueue(new ClearPendingChoicesAction());
            pipeline.Enqueue(new AdvanceNodeAction());
            resolved += pipeline.RunToCompletion();
            if (!IsTerminalPhase(CurrentPhase))
            {
                pipeline.Enqueue(new ChangePhaseAction(GamePhase.NodeCompleted));
                resolved += pipeline.RunToCompletion();
            }

            return CoreCommandResult.Accept(resolved);
        }

        private static bool IsRegisteredInItemSlots(DeckModel deck, int itemUid)
        {
            var itemSlots = deck.ItemSlotUids;
            for (var i = 0; i < itemSlots.Count; i++)
            {
                if (itemSlots[i] == itemUid)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsUsableItemKind(CardKind kind)
        {
            return kind == CardKind.Item || kind == CardKind.HelpCard;
        }

        private int ResolveInteractiveRotation()
        {
            if (IsTerminalPhase(CurrentPhase))
            {
                return 0;
            }

            // R1：先补牌再旋转（Fill → Rotate）。
            var pipeline = this.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new ModifyInteractionCountAction(1));
            pipeline.Enqueue(new FillEmptySlotsAction());
            pipeline.Enqueue(new RotateBoardClockwiseAction());
            var resolved = pipeline.RunToCompletion();
            resolved += CompleteNodeIfCleared();
            return resolved;
        }

        private int CompleteNodeIfCleared()
        {
            if (CurrentPhase != GamePhase.InteractionLoop)
            {
                return 0;
            }

            if (!this.GetSystem<IDeckSystem>().IsNodeCleared())
            {
                return 0;
            }

            var pipeline = this.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new ChangePhaseAction(GamePhase.ClearCheck));
            pipeline.Enqueue(new ChangePhaseAction(GamePhase.NodeCompleted));
            pipeline.Enqueue(new NodeCompletedAction());
            var resolved = pipeline.RunToCompletion();
            // 对局结束（通关判定成立）当拍立即结算残留帮助卡；
            // 先结算再 OfferReward，三选一新获得的帮助卡不参与本次结算。
            resolved += this.GetSystem<IEconomySystem>().SettleUnusedHelpCards();
            pipeline.Enqueue(new ChangePhaseAction(GamePhase.RewardItemChoice));
            pipeline.Enqueue(new OfferRewardChoiceAction("help.choice", 3));
            return resolved + pipeline.RunToCompletion();
        }

        private const string ShopHelpCardsPoolId = "shop.helpCards";

        private int ResolveShopPrice(string defId)
        {
            if (string.IsNullOrEmpty(defId))
            {
                return 0;
            }

            var content = this.GetSystem<IContentSystem>();
            content.TryReloadFromConfig();
            if (!content.HasCatalog)
            {
                return 0;
            }

            CardContentDefinition card;
            if (!content.Catalog.Cards.TryGetValue(defId, out card) || card == null)
            {
                return 0;
            }

            return card.Price > 0 ? card.Price : 0;
        }

        private int ResolvePostRewardChoiceFlow(IActionPipelineSystem pipeline, int resolvedSoFar)
        {
            if (mInRoomRewardContext)
            {
                mInRoomRewardContext = false;
                pipeline.Enqueue(new AdvanceNodeAction());
                var resolved = resolvedSoFar + pipeline.RunToCompletion();
                if (!IsTerminalPhase(CurrentPhase))
                {
                    pipeline.Enqueue(new ChangePhaseAction(GamePhase.NodeCompleted));
                    resolved += pipeline.RunToCompletion();
                }

                return resolved;
            }

            if (!this.GetSystem<IDeckSystem>().IsNodeCleared())
            {
                pipeline.Enqueue(new ChangePhaseAction(GamePhase.InteractionLoop));
                return resolvedSoFar + pipeline.RunToCompletion();
            }

            pipeline.Enqueue(new ChangePhaseAction(GamePhase.RoomChoice));
            pipeline.Enqueue(new OfferRoomChoicesAction(RollRoomChoicesOrFallback()));
            return resolvedSoFar + pipeline.RunToCompletion();
        }

        private IReadOnlyList<RoomKind> RollRoomChoicesOrFallback()
        {
            var choices = this.GetSystem<IRewardSystem>().RollRoomChoices(2);
            if (choices.Count > 0)
            {
                return choices;
            }

            return new[]
            {
                RoomKind.Gold,
                RoomKind.Fountain
            };
        }

        private CoreCommandResult Reject(GameCommandKind command, string reason, SlotId slot, int cardUid)
        {
            this.GetSystem<IActionPipelineSystem>().RejectCommand(command, reason, slot, cardUid);
            return CoreCommandResult.Reject(reason);
        }

        private static bool IsTerminalPhase(GamePhase phase)
        {
            return phase == GamePhase.Victory || phase == GamePhase.Defeat;
        }

        private bool ContainsEventSince(int startIndex, CoreEventType eventType, int cardUid)
        {
            var entries = this.GetSystem<IActionPipelineSystem>().EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Type == eventType && entry.CardUid == cardUid)
                {
                    return true;
                }
            }

            return false;
        }

        private bool ContainsAnyEventSince(int startIndex, CoreEventType eventType)
        {
            var entries = this.GetSystem<IActionPipelineSystem>().EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                if (entries[i].Type == eventType)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasFirstStrike(IStatSystem statSystem, CardInstance card)
        {
            return statSystem.EvaluateRule(RuleId.FirstStrike, 0f, statSystem.CreateContext(card)) > 0f;
        }

        private bool CanAttackTargetUnderRules(CardInstance target)
        {
            var statSystem = this.GetSystem<IStatSystem>();
            var restrictedUid = (int)System.Math.Round(statSystem.EvaluateRule(RuleId.AttackTargetRestriction, 0f, statSystem.CreateContext(target)));
            return restrictedUid == 0 || restrictedUid == target.Uid;
        }

        private static int GetAttackDamage(IStatSystem statSystem, CardInstance card)
        {
            var damage = statSystem.GetEffectiveInt(card, StatId.Attack);
            if (card.Kind == CardKind.Monster)
            {
                damage += (int)System.Math.Round(statSystem.EvaluateRule(RuleId.EnemyAttackDelta, 0f, statSystem.CreateContext(card)));
            }

            return System.Math.Max(0, damage);
        }

        private static bool TryGetPlayerMonsterEngagement(
            CardRegistry registry,
            int attackerUid,
            int targetUid,
            out int monsterUid)
        {
            monsterUid = 0;
            CardInstance attacker;
            CardInstance target;
            if (!registry.TryGet(attackerUid, out attacker) || !registry.TryGet(targetUid, out target))
            {
                return false;
            }

            if (attacker.Kind != CardKind.Avatar || target.Kind != CardKind.Monster)
            {
                return false;
            }

            monsterUid = targetUid;
            return true;
        }

        private void RefreshLegalCommands()
        {
            mLegalCommands.Clear();
            if (this.GetSystem<IPresentationSyncSystem>().IsInputLocked)
            {
                mLegalCommands.Add(GameCommandKind.PresentationFinished);
                return;
            }

            switch (CurrentPhase)
            {
                case GamePhase.None:
                case GamePhase.BuildEnemyPool:
                case GamePhase.NodeCompleted:
                    mLegalCommands.Add(GameCommandKind.StartNode);
                    break;
                case GamePhase.InteractionLoop:
                    if (this.GetModel<PendingChoiceModel>().Kind.Value == PendingChoiceKind.Reward)
                    {
                        // 局内宝箱等：覆盖层待选，锁死战场交互。
                        mLegalCommands.Add(GameCommandKind.SelectReward);
                        mLegalCommands.Add(GameCommandKind.SkipHelpChoice);
                    }
                    else
                    {
                        mLegalCommands.Add(GameCommandKind.Attack);
                        mLegalCommands.Add(GameCommandKind.PickupItem);
                        mLegalCommands.Add(GameCommandKind.ClickEmpty);
                        mLegalCommands.Add(GameCommandKind.UseItem);
                    }

                    break;
                case GamePhase.RewardItemChoice:
                    mLegalCommands.Add(GameCommandKind.SelectReward);
                    mLegalCommands.Add(GameCommandKind.SkipHelpChoice);
                    break;
                case GamePhase.RoomChoice:
                    mLegalCommands.Add(GameCommandKind.SelectRoom);
                    mLegalCommands.Add(GameCommandKind.PickupItem);
                    mLegalCommands.Add(GameCommandKind.UseItem);
                    break;
                case GamePhase.RoomEvent:
                    mLegalCommands.Add(GameCommandKind.EnterRoom);
                    break;
            }
        }
    }
}

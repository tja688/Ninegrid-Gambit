using System.Collections.Generic;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
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
            /// 玩家攻击解析：嘲讽等 AttackTargetRestriction 生效时返回嘲讽者 UID，否则返回 intendedTargetUid。
            /// </summary>
            int ResolvePlayerAttackTargetUid(int intendedTargetUid);
            /// <summary>
            /// 只读：该交战是否应由怪物先出手（仅怪物有 FirstStrike 且玩家无）。
            /// </summary>
            bool MonsterStrikesFirst(int avatarUid, int monsterUid);
            /// <summary>
            /// 表现层可信命中：仅一段伤害（含致死 Kill/Defeat），无门禁、无反击、无旋转。
            /// </summary>
            CoreCommandResult ApplyCombatHit(int attackerUid, int targetUid);
            /// <summary>
            /// 击杀后盘面：交互计数 + 补牌 + 旋转 + 清场判定（兼容整拍；导演路径请用分拍）。
            /// </summary>
            CoreCommandResult ResolvePostKillBoard();
            /// <summary>
            /// 九宫格互动分拍：全局 interactionCount +1（触发 OnInteract）。不含补牌/旋转。
            /// </summary>
            CoreCommandResult AdvanceInteractionCount();
            /// <summary>
            /// 击杀后分拍：仅补牌（Fill）。不含交互计数、不旋转。
            /// </summary>
            CoreCommandResult ResolvePostKillFill();
            /// <summary>
            /// 击杀后分拍：顺时针旋转 + 清场判定。
            /// </summary>
            CoreCommandResult ResolvePostKillRotate();
            /// <summary>
            /// 敌方行动阶段报名：非「无」怪倒计时 −1；归零者按 uid 升序冻结名单（ADR-0012）。
            /// </summary>
            CoreCommandResult RegisterEnemyActionPhase();
            /// <summary>
            /// 敌方行动阶段逐条：复核资格后单向打击或窗口作废重置；每调用结算一条。
            /// </summary>
            CoreCommandResult ResolveNextEnemyAction();
            /// <summary>
            /// 敌方行动阶段收尾：补牌 + 通关检查，不旋转。
            /// </summary>
            CoreCommandResult ResolveEnemyActionFinale();
            /// <summary>当前冻结行动名单中尚未结算的 uid（升序快照的后缀）。</summary>
            IReadOnlyList<int> PendingEnemyActionUids { get; }
            /// <summary>
            /// 融合伴随补牌分拍：仅 FillEmptySlots。skipFill 时不写 Core，仅供导演打开空批。
            /// </summary>
            CoreCommandResult ResolveFusionRefill(bool skipFill = false);
            /// <summary>
            /// drain 退场补牌分拍：仅 FillEmptySlots。skipFill 时不写 Core，仅供导演打开空批。
            /// </summary>
            CoreCommandResult ResolveDrainRefill(bool skipFill = false);
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
            /// <summary>
            /// 主动翻开：场上邻接背面卡 → FaceUp；消耗互动由表现剧本分拍推进。
            /// </summary>
            CoreCommandResult RevealFace(SlotId targetSlot);
            CoreCommandResult MoveAvatar(SlotId targetSlot);
            CoreCommandResult SelectReward(int optionIndex);
            CoreCommandResult SkipHelpChoice();
            /// <summary>商店刷新货架：扣本次进店刷新价并翻倍。</summary>
            CoreCommandResult RefreshShop();
            CoreCommandResult SelectRoom(int optionIndex);
            CoreCommandResult EnterRoom();
        }

    public sealed class PhaseSystem : AbstractSystem, IPhaseSystem
    {
        private readonly List<GameCommandKind> mLegalCommands = new List<GameCommandKind>();
        private readonly List<int> mEnemyActionRoster = new List<int>();
        private int mEnemyActionCursor;
        private bool mInRoomRewardContext;

        public GamePhase CurrentPhase
        {
            get { return this.GetModel<RunModel>().Phase.Value; }
        }

        public IReadOnlyList<int> PendingEnemyActionUids
        {
            get
            {
                if (mEnemyActionCursor >= mEnemyActionRoster.Count)
                {
                    return System.Array.Empty<int>();
                }

                return mEnemyActionRoster.GetRange(
                    mEnemyActionCursor,
                    mEnemyActionRoster.Count - mEnemyActionCursor);
            }
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
            var run = this.GetModel<RunModel>();
            if (!MapNodeProgression.EntersInteractionLoop(run.NodeIndex.Value))
            {
                return StartNonCombatNode();
            }

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

        private CoreCommandResult StartNonCombatNode()
        {
            var pipeline = this.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new ClearPendingChoicesAction());
            pipeline.Enqueue(new ChangePhaseAction(GamePhase.ResetNode));
            pipeline.Enqueue(new ResetCurrentArmorAction());
            pipeline.Enqueue(new NodeStartedAction());
            pipeline.Enqueue(new ChangePhaseAction(GamePhase.RoomChoice));
            var navigation = MapNodeProgression.GetScheduleOrDefault(this.GetModel<RunModel>().NodeIndex.Value)
                .NavigationOffer;
            if (navigation == NavigationKind.None)
            {
                navigation = NavigationKind.Leave;
            }

            pipeline.Enqueue(new OfferNavigationAction(navigation));
            return CoreCommandResult.Accept(pipeline.RunToCompletion());
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
            if (!CardCombatRules.IsBoardCombatTarget(target.Kind))
            {
                return Reject(GameCommandKind.Attack, "Attack target is not a combat target.", targetSlot, targetUid);
            }

            if (!this.GetSystem<IBoardSystem>().AreAdjacent(board.AvatarSlot.Value, targetSlot))
            {
                return Reject(GameCommandKind.Attack, "Attack target is outside interaction range.", targetSlot, targetUid);
            }

            if (!CanAttackTargetUnderRules(target))
            {
                return Reject(GameCommandKind.Attack, "Attack target is restricted by taunt.", targetSlot, targetUid);
            }

            if (!target.FaceUp)
            {
                return Reject(GameCommandKind.Attack, "Attack target is face-down.", targetSlot, targetUid);
            }

            var avatar = registry.Get(board.AvatarUid.Value);
            var statSystem = this.GetSystem<IStatSystem>();
            var pipeline = this.GetSystem<IActionPipelineSystem>();
            var startIndex = pipeline.EventLog.Entries.Count;
            pipeline.Enqueue(new BeginPlayerMonsterEngagementAction(targetUid));
            ApplyHolyDuelMark(avatar, target, pipeline);
            // 远程武器：玩家交战该怪时怪不先手也不反击（齐射不受影响）。
            var monsterStrikes = !HasRule(target, RuleId.CounterAttackBanned)
                && CombatEngagementOrder.MonsterStrikesFirst(statSystem, avatar, target);
            if (monsterStrikes)
            {
                pipeline.Enqueue(new DealDamageAction(target.Uid, avatar.Uid, GetAttackDamage(statSystem, target)));
                pipeline.Enqueue(new ConditionalDealDamageIfAliveAction(avatar.Uid, target.Uid, GetAttackDamage(statSystem, avatar)));
            }
            else
            {
                pipeline.Enqueue(new DealDamageAction(avatar.Uid, targetUid, GetAttackDamage(statSystem, avatar)));
                if (!HasRule(target, RuleId.CounterAttackBanned))
                {
                    pipeline.Enqueue(new ConditionalDealDamageIfAliveAction(target.Uid, avatar.Uid, GetAttackDamage(statSystem, target)));
                }
            }

            pipeline.Enqueue(new EndBattleScopeCleanupAction());
            var resolved = pipeline.RunToCompletion();

            // 九宫格互动：交战无论是否击杀都推进互动计数（ADR-0012 / #75）。
            resolved += AdvanceInteractionCountInternal();
            if (ContainsEventSince(startIndex, CoreEventType.CardKilled, targetUid))
            {
                resolved += ResolvePostKillFillInternal();
                resolved += ResolvePostKillRotateInternal();
            }

            resolved += RunEnemyActionPhaseInternal();
            return CoreCommandResult.Accept(resolved);
        }

        public int ResolvePlayerAttackTargetUid(int intendedTargetUid)
        {
            if (intendedTargetUid <= 0)
            {
                return intendedTargetUid;
            }

            var registry = this.GetModel<CardRegistry>();
            if (!registry.TryGet(intendedTargetUid, out var intendedTarget))
            {
                return intendedTargetUid;
            }

            var restrictedUid = GetAttackTargetRestrictionUid(intendedTarget);
            return restrictedUid != 0 ? restrictedUid : intendedTargetUid;
        }

        public bool MonsterStrikesFirst(int avatarUid, int monsterUid)
        {
            if (avatarUid <= 0 || monsterUid <= 0)
            {
                return false;
            }

            var registry = this.GetModel<CardRegistry>();
            if (!registry.TryGet(avatarUid, out var avatar)
                || !registry.TryGet(monsterUid, out var monster))
            {
                return false;
            }

            // 远程武器：玩家交战时该怪不先手（也不反击，见 ApplyCombatHit 短路）。
            if (HasRule(monster, RuleId.CounterAttackBanned))
            {
                return false;
            }

            return CombatEngagementOrder.MonsterStrikesFirst(
                this.GetSystem<IStatSystem>(),
                avatar,
                monster);
        }

        private bool HasRule(CardInstance card, RuleId rule)
        {
            if (card == null)
            {
                return false;
            }

            var statSystem = this.GetSystem<IStatSystem>();
            return statSystem.EvaluateRule(rule, 0f, statSystem.CreateContext(card)) > 0f;
        }

        /// <summary>
        /// 神圣决斗（skill.holy_duel）标记结算（玩家主动交战入口）：
        /// 目标为持有者 → 记录标记；否则若标记仍有效（持有者在场正面）→ 对玩家 2 伤；
        /// 持有者已离场/翻面 → 清标记不惩罚。
        /// </summary>
        private void ApplyHolyDuelMark(
            CardInstance avatar,
            CardInstance target,
            IActionPipelineSystem pipeline)
        {
            if (avatar == null || target == null || target.Kind != CardKind.Monster)
            {
                return;
            }

            var player = this.GetModel<PlayerModel>();
            if (HasRule(target, RuleId.HolyDuel))
            {
                player.SetDuelMark(target.Uid);
                return;
            }

            var markedUid = player.DuelMarkMonsterUid;
            if (markedUid == 0 || markedUid == target.Uid)
            {
                return;
            }

            var registry = this.GetModel<CardRegistry>();
            CardInstance holder;
            if (!registry.TryGet(markedUid, out holder)
                || holder == null
                || holder.Zone.Value != ZoneId.Board
                || !holder.FaceUp
                || !IsCardAlive(holder))
            {
                player.ClearDuelMark();
                return;
            }

            // 走 ExecuteEffectAction（带预备动作）而非裸 DealDamageAction：
            // 该包装会先发 EffectTriggered 事件 → 表现层在持有者上播效果触发脉冲（缩放）。
            var holyDuelInstanceId = FindHolyDuelActivateInstanceId(markedUid);
            if (holyDuelInstanceId == null)
            {
                // ADR-0018：无 activate 实例时仍须留下等价 EffectTriggered，禁止 silent 裸伤。
                pipeline.Enqueue(new EmitEffectTriggeredAction(
                    markedUid,
                    "skill.holy_duel.activate",
                    "skill.holy_duel"));
                pipeline.Enqueue(new DealDamageAction(
                    markedUid,
                    avatar.Uid,
                    2,
                    "skill.holy_duel",
                    "skill.holy_duel"));
                return;
            }

            pipeline.Enqueue(new ExecuteEffectAction(
                holyDuelInstanceId,
                null,
                new GameAction[]
                {
                    new DealDamageAction(markedUid, avatar.Uid, 2, "skill.holy_duel", "skill.holy_duel"),
                }));
        }

        private string FindHolyDuelActivateInstanceId(int holderUid)
        {
            if (holderUid == 0)
            {
                return null;
            }

            var effectSystem = this.GetSystem<IEffectSystem>();
            var ids = effectSystem.GetInstanceIdsByOwner(holderUid);
            for (var i = 0; i < ids.Count; i++)
            {
                EffectInstance instance;
                if (!effectSystem.TryGetInstance(ids[i], out instance)
                    || instance.Definition == null)
                {
                    continue;
                }

                if (instance.Definition.Id == "skill.holy_duel.activate")
                {
                    return ids[i];
                }
            }

            return null;
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

            if (attacker.Kind == CardKind.Avatar && CardCombatRules.IsBoardCombatTarget(target.Kind))
            {
                if (!target.FaceUp)
                {
                    return CoreCommandResult.Reject("Combat hit target is face-down.");
                }

                var resolvedTargetUid = ResolvePlayerAttackTargetUid(targetUid);
                if (resolvedTargetUid != targetUid)
                {
                    return CoreCommandResult.Reject("Combat hit target is restricted by taunt.");
                }
            }

            // 远程武器：怪作为攻击方的交战命中（先手/反击段）短路，不造成伤害；齐射不经此入口。
            if (attacker.Kind == CardKind.Monster
                && target.Kind == CardKind.Avatar
                && HasRule(attacker, RuleId.CounterAttackBanned))
            {
                return CoreCommandResult.Accept(0);
            }

            var statSystem = this.GetSystem<IStatSystem>();
            var pipeline = this.GetSystem<IActionPipelineSystem>();
            var engagedMonsterUid = 0;
            if (TryGetPlayerMonsterEngagement(registry, attackerUid, targetUid, out engagedMonsterUid))
            {
                pipeline.Enqueue(new BeginPlayerMonsterEngagementAction(engagedMonsterUid));
                ApplyHolyDuelMark(attacker, target, pipeline);
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
            // 兼容整拍：计数 + 补牌 + 旋转 + 敌方行动（单次语义由分步合成）。
            return CoreCommandResult.Accept(ResolveInteractiveRotation());
        }

        public CoreCommandResult AdvanceInteractionCount()
        {
            return CoreCommandResult.Accept(AdvanceInteractionCountInternal());
        }

        public CoreCommandResult ResolvePostKillFill()
        {
            return CoreCommandResult.Accept(ResolvePostKillFillInternal());
        }

        public CoreCommandResult ResolvePostKillRotate()
        {
            return CoreCommandResult.Accept(ResolvePostKillRotateInternal());
        }

        public CoreCommandResult RegisterEnemyActionPhase()
        {
            return CoreCommandResult.Accept(RegisterEnemyActionPhaseInternal());
        }

        public CoreCommandResult ResolveNextEnemyAction()
        {
            return CoreCommandResult.Accept(ResolveNextEnemyActionInternal());
        }

        public CoreCommandResult ResolveEnemyActionFinale()
        {
            return CoreCommandResult.Accept(ResolveEnemyActionFinaleInternal());
        }

        public CoreCommandResult ResolveFusionRefill(bool skipFill = false)
        {
            return CoreCommandResult.Accept(ResolveFillEmptySlotsBatchInternal(skipFill));
        }

        public CoreCommandResult ResolveDrainRefill(bool skipFill = false)
        {
            return CoreCommandResult.Accept(ResolveFillEmptySlotsBatchInternal(skipFill));
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
            if (CardCombatRules.IsBoardCombatTarget(card.Kind))
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

        public CoreCommandResult RevealFace(SlotId targetSlot)
        {
            if (!CanExecute(GameCommandKind.RevealFace))
            {
                return Reject(GameCommandKind.RevealFace, "Command is not legal in phase " + CurrentPhase, targetSlot, 0);
            }

            var registry = this.GetModel<CardRegistry>();
            var board = this.GetModel<BoardModel>();
            if (!targetSlot.IsBoardSlot || targetSlot == board.AvatarSlot.Value)
            {
                return Reject(GameCommandKind.RevealFace, "Reveal target is not a board card slot.", targetSlot, 0);
            }

            var targetUid = board.GetCardUid(targetSlot);
            if (targetUid == 0)
            {
                return Reject(GameCommandKind.RevealFace, "Reveal target slot is empty.", targetSlot, 0);
            }

            var target = registry.Get(targetUid);
            if (target.Kind == CardKind.Avatar)
            {
                return Reject(GameCommandKind.RevealFace, "Cannot reveal avatar.", targetSlot, targetUid);
            }

            if (target.FaceUp)
            {
                return Reject(GameCommandKind.RevealFace, "Target is already face-up.", targetSlot, targetUid);
            }

            if (!this.GetSystem<IBoardSystem>().AreAdjacent(board.AvatarSlot.Value, targetSlot))
            {
                return Reject(GameCommandKind.RevealFace, "Reveal target is outside interaction range.", targetSlot, targetUid);
            }

            var pipeline = this.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new RevealFaceAction(targetUid));
            return CoreCommandResult.Accept(pipeline.RunToCompletion());
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
            // 旋转/补牌留给 ResolvePostKillBoard，供表演导演按批锁步。
            return CoreCommandResult.Accept(resolved);
        }

        public CoreCommandResult MoveAvatar(SlotId targetSlot)
        {
            if (!CanExecute(GameCommandKind.MoveAvatar))
            {
                return Reject(GameCommandKind.MoveAvatar, "Command is not legal in phase " + CurrentPhase, targetSlot, 0);
            }

            var board = this.GetModel<BoardModel>();
            var avatarUid = board.AvatarUid.Value;
            if (avatarUid <= 0)
            {
                return Reject(GameCommandKind.MoveAvatar, "Avatar is missing.", targetSlot, 0);
            }

            var from = board.AvatarSlot.Value;
            if (!from.IsBoardSlot)
            {
                return Reject(GameCommandKind.MoveAvatar, "Avatar is not on the board.", targetSlot, avatarUid);
            }

            if (!targetSlot.IsBoardSlot)
            {
                return Reject(GameCommandKind.MoveAvatar, "Target is not a board slot.", targetSlot, avatarUid);
            }

            if (targetSlot == from)
            {
                return Reject(GameCommandKind.MoveAvatar, "Target is the current avatar slot.", targetSlot, avatarUid);
            }

            // BoardWalk 相位允许踩非空格（寻路软占回退）；AvatarSlot 与 card uid 独立，不清除格上卡。
            // InteractionLoop 等相位本就不合法 MoveAvatar；RoomChoice/RoomEvent/商店 RewardItemChoice 可踩软占。
            if (!board.IsEmpty(targetSlot)
                && CurrentPhase != GamePhase.RoomChoice
                && CurrentPhase != GamePhase.RoomEvent
                && CurrentPhase != GamePhase.RewardItemChoice)
            {
                return Reject(GameCommandKind.MoveAvatar, "Target slot is occupied.", targetSlot, avatarUid);
            }

            if (!from.IsAdjacentTo(targetSlot))
            {
                return Reject(GameCommandKind.MoveAvatar, "Target is not orthogonally adjacent.", targetSlot, avatarUid);
            }

            var pipeline = this.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new MoveAvatarAction(targetSlot));
            return CoreCommandResult.Accept(pipeline.RunToCompletion());
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

            // 击杀后补牌/旋转/清场留给导演 ResolvePostKillFill / ResolvePostKillRotate。
            // 非击杀路径仍可立即 CompleteNodeIfCleared（宝箱等只写 PendingChoice，本调用通常 no-op）。
            if (!ContainsAnyEventSince(startIndex, CoreEventType.CardKilled))
            {
                resolved += CompleteNodeIfCleared();
            }

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
            if (IsRelicRewardPool(poolId))
            {
                this.GetSystem<IRewardSystem>().RememberUnselectedRelics(
                    pending.RewardOptions,
                    entry != null ? entry.DefId : null);
            }

            var pipeline = this.GetSystem<IActionPipelineSystem>();

            // 卡店「道具卡固定」二级确认：扣费 + 写入固定卡 + 回到三项服务面。
            if (PendingChoiceModel.IsTavernFixItemPool(poolId))
            {
                return SelectTavernFixItemConfirm(entry, pipeline);
            }

            // 卡店三项服务：扣费写 Model 留店；FixItem 进入二级选择不扣费。
            if (PendingChoiceModel.IsTavernPool(poolId))
            {
                return SelectTavernService(entry, pipeline);
            }

            // 商店购买：按卡牌 Price 扣金；通关/宝箱等免费池不扣。
            var isShop = PendingChoiceModel.IsShopPool(poolId);
            var isSpecialReward = PendingChoiceModel.IsSpecialRewardPool(poolId);
            if (isShop)
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
            if (isShop || isSpecialReward)
            {
                // 货架拿走后留在房内；离开另走 SkipHelpChoice。
                var resolvedStay = pipeline.RunToCompletion();
                pending.RemoveRewardOptionAt(optionIndex);
                return CoreCommandResult.Accept(resolvedStay);
            }

            pipeline.Enqueue(new ClearPendingRewardChoiceAction());
            var resolved = ResolvePostRewardChoiceFlow(pipeline, 0);
            return CoreCommandResult.Accept(resolved);
        }

        private CoreCommandResult SelectTavernService(RewardEntry entry, IActionPipelineSystem pipeline)
        {
            var defId = entry != null ? entry.DefId : null;
            if (string.Equals(defId, RewardSystem.TavernFixItemDefId, System.StringComparison.Ordinal))
            {
                var candidates = BuildTavernFixItemCandidates();
                if (candidates.Count == 0)
                {
                    return Reject(GameCommandKind.SelectReward, "No item source pool", SlotId.None, 0);
                }

                pipeline.Enqueue(new OfferTavernFixItemAction(candidates));
                return CoreCommandResult.Accept(pipeline.RunToCompletion());
            }

            var price = ResolveTavernServicePrice(defId);
            if (price > 0)
            {
                var coins = this.GetModel<PlayerModel>().Coins.Value;
                if (coins < price)
                {
                    return Reject(GameCommandKind.SelectReward, "Not enough gold", SlotId.None, 0);
                }

                pipeline.Enqueue(new ModifyGoldAction(-price, "tavernBuy:" + defId, defId));
            }

            var resolved = pipeline.RunToCompletion();
            ApplyTavernServicePurchase(defId);
            return CoreCommandResult.Accept(resolved);
        }

        private CoreCommandResult SelectTavernFixItemConfirm(RewardEntry entry, IActionPipelineSystem pipeline)
        {
            var defId = entry != null ? entry.DefId : null;
            if (string.IsNullOrEmpty(defId))
            {
                return Reject(GameCommandKind.SelectReward, "Reward option index is out of range.", SlotId.None, 0);
            }

            var price = ResolveTavernServicePrice(RewardSystem.TavernFixItemDefId);
            if (price > 0)
            {
                var coins = this.GetModel<PlayerModel>().Coins.Value;
                if (coins < price)
                {
                    return Reject(GameCommandKind.SelectReward, "Not enough gold", SlotId.None, 0);
                }

                pipeline.Enqueue(new ModifyGoldAction(-price, "tavernBuy:FixItem", defId));
            }

            var refresh = this.GetModel<PendingChoiceModel>().ShopRefreshPriceGold.Value;
            pipeline.Enqueue(new OfferTavernSessionAction(
                this.GetSystem<IRewardSystem>().BuildTavernServices(),
                refresh));
            var resolved = pipeline.RunToCompletion();
            this.GetModel<PlayerModel>().AddFixedItemCard(defId);
            return CoreCommandResult.Accept(resolved);
        }

        private void ApplyTavernServicePurchase(string defId)
        {
            var player = this.GetModel<PlayerModel>();
            if (string.Equals(defId, RewardSystem.TavernExpandDefId, System.StringComparison.Ordinal))
            {
                player.SetItemDeckCapacity(player.ItemDeckCapacity + 1);
                return;
            }

            if (string.Equals(defId, RewardSystem.TavernUpgradeDefId, System.StringComparison.Ordinal))
            {
                player.AddItemStatBonus(RewardSystem.TavernUpgradeStatDelta);
            }
        }

        private List<RewardEntry> BuildTavernFixItemCandidates()
        {
            // 表现侧最多铺 6 个空格（格 1/3/4/6/7/9）；超出截断，避免 Pending 索引与点击索引错位。
            const int maxCandidates = 6;
            var pool = this.GetModel<PlayerModel>().ItemSourcePoolDefIds;
            var list = new List<RewardEntry>(maxCandidates);
            for (var i = 0; i < pool.Count && list.Count < maxCandidates; i++)
            {
                var id = pool[i];
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                list.Add(new RewardEntry(id, CardKind.HelpCard, 1, 1));
            }

            return list;
        }

        private int ResolveTavernServicePrice(string defId)
        {
            var catalogPrice = ResolveShopPrice(defId);
            if (catalogPrice > 0)
            {
                return catalogPrice;
            }

            return RewardSystem.TavernServicePriceGold;
        }

        public CoreCommandResult RefreshShop()
        {
            if (!CanExecute(GameCommandKind.RefreshShop))
            {
                return Reject(GameCommandKind.RefreshShop, "Command is not legal in phase " + CurrentPhase, SlotId.None, 0);
            }

            var pending = this.GetModel<PendingChoiceModel>();
            var poolId = pending.PoolId.Value;
            if (pending.Kind.Value != PendingChoiceKind.Reward
                || !PendingChoiceModel.IsConsumerRefreshPool(poolId))
            {
                return Reject(GameCommandKind.RefreshShop, "No active shop session.", SlotId.None, 0);
            }

            var price = pending.ShopRefreshPriceGold.Value;
            if (price < 0)
            {
                price = 0;
            }

            var player = this.GetModel<PlayerModel>();
            if (price > 0 && player.Coins.Value < price)
            {
                return Reject(GameCommandKind.RefreshShop, "Not enough gold", SlotId.None, 0);
            }

            var pipeline = this.GetSystem<IActionPipelineSystem>();
            if (price > 0)
            {
                var reason = PendingChoiceModel.IsTavernPool(poolId) ? "tavernRefresh" : "shopRefresh";
                pipeline.Enqueue(new ModifyGoldAction(-price, reason, null));
            }

            var nextPrice = price <= 0 ? OfferShopSessionAction.DefaultRefreshPriceGold : price * 2;
            if (PendingChoiceModel.IsTavernPool(poolId))
            {
                pipeline.Enqueue(new OfferTavernSessionAction(
                    this.GetSystem<IRewardSystem>().BuildTavernServices(),
                    nextPrice));
            }
            else
            {
                var shelves = this.GetSystem<IRewardSystem>().BuildShopShelves();
                pipeline.Enqueue(new OfferShopSessionAction(shelves, nextPrice));
            }

            return CoreCommandResult.Accept(pipeline.RunToCompletion());
        }

        public CoreCommandResult SkipHelpChoice()
        {
            if (!CanExecute(GameCommandKind.SkipHelpChoice))
            {
                return Reject(GameCommandKind.SkipHelpChoice, "Command is not legal in phase " + CurrentPhase, SlotId.None, 0);
            }

            var pending = this.GetModel<PendingChoiceModel>();
            var poolId = pending.PoolId.Value ?? string.Empty;

            // 卡店二级选择取消：回到三项服务面，不离店、不扣费。
            if (PendingChoiceModel.IsTavernFixItemPool(poolId))
            {
                var refresh = pending.ShopRefreshPriceGold.Value;
                var pipelineCancel = this.GetSystem<IActionPipelineSystem>();
                pipelineCancel.Enqueue(new OfferTavernSessionAction(
                    this.GetSystem<IRewardSystem>().BuildTavernServices(),
                    refresh));
                return CoreCommandResult.Accept(pipelineCancel.RunToCompletion());
            }

            if (IsRelicRewardPool(poolId))
            {
                this.GetSystem<IRewardSystem>().RememberUnselectedRelics(pending.RewardOptions, null);
            }

            // 商店/卡店/特殊奖励房离开：不发跳过帮助卡选择的 +金币；通关帮助三选一跳过仍发。
            var isConsumerLeave = PendingChoiceModel.IsConsumerLeavePool(poolId);
            var resolved = isConsumerLeave ? 0 : this.GetSystem<IEconomySystem>().AwardSkipHelpChoice();
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
            if (pending.Kind.Value == PendingChoiceKind.Navigation)
            {
                if (optionIndex != 0 || pending.NavigationOffer.Value == NavigationKind.None)
                {
                    return Reject(GameCommandKind.SelectRoom, "Navigation option index is out of range.", SlotId.None, 0);
                }

                var pipelineNav = this.GetSystem<IActionPipelineSystem>();
                pipelineNav.Enqueue(new SelectNavigationAction(pending.NavigationOffer.Value));
                pipelineNav.Enqueue(new ChangePhaseAction(GamePhase.RoomEvent));
                return CoreCommandResult.Accept(pipelineNav.RunToCompletion());
            }

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

            var pending = this.GetModel<PendingChoiceModel>();
            if (pending.SelectedNavigation.Value != NavigationKind.None)
            {
                var pipelineNav = this.GetSystem<IActionPipelineSystem>();
                pipelineNav.Enqueue(new ClearPendingChoicesAction());
                pipelineNav.Enqueue(new AdvanceNodeAction());
                var resolvedNav = pipelineNav.RunToCompletion();
                if (!IsTerminalPhase(CurrentPhase))
                {
                    pipelineNav.Enqueue(new ChangePhaseAction(GamePhase.NodeCompleted));
                    resolvedNav += pipelineNav.RunToCompletion();
                }

                return CoreCommandResult.Accept(resolvedNav);
            }

            var room = pending.SelectedRoom.Value;
            if (room == RoomKind.None)
            {
                return Reject(GameCommandKind.EnterRoom, "No selected room to enter.", SlotId.None, 0);
            }

            var resolved = this.GetSystem<IRewardSystem>().ResolveRoom(room);
            var pipeline = this.GetSystem<IActionPipelineSystem>();

            if (pending.Kind.Value == PendingChoiceKind.Reward)
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

            // 旧整拍入口（Pickup / ResolvePostKillBoard）：计数 + 补牌 + 旋转 + 敌方行动。
            // 导演分拍请用 AdvanceInteractionCount / ResolvePostKillFill / ResolvePostKillRotate /
            // RegisterEnemyActionPhase / ResolveNextEnemyAction / ResolveEnemyActionFinale。
            var resolved = AdvanceInteractionCountInternal();
            resolved += ResolvePostKillFillInternal();
            resolved += ResolvePostKillRotateInternal();
            resolved += RunEnemyActionPhaseInternal();
            return resolved;
        }

        private int RunEnemyActionPhaseInternal()
        {
            var resolved = RegisterEnemyActionPhaseInternal();
            while (mEnemyActionCursor < mEnemyActionRoster.Count)
            {
                resolved += ResolveNextEnemyActionInternal();
            }

            resolved += ResolveEnemyActionFinaleInternal();
            return resolved;
        }

        private int RegisterEnemyActionPhaseInternal()
        {
            mEnemyActionRoster.Clear();
            mEnemyActionCursor = 0;
            if (IsTerminalPhase(CurrentPhase))
            {
                return 0;
            }

            var board = this.GetModel<BoardModel>();
            var registry = this.GetModel<CardRegistry>();
            var pipeline = this.GetSystem<IActionPipelineSystem>();
            var candidates = new List<int>();
            foreach (var uid in board.BoardCardUids())
            {
                CardInstance card;
                if (!registry.TryGet(uid, out card) || card.Kind != CardKind.Monster)
                {
                    continue;
                }

                // ADR-0016：背面冻结攻击倒计时，不报名、不 −1。
                if (!card.FaceUp)
                {
                    continue;
                }

                if (!AttackPatternRules.ParticipatesInEnemyAction(card.AttackPattern))
                {
                    continue;
                }

                var frequency = AttackPatternRules.Frequency(card.AttackPattern);
                if (frequency <= 0)
                {
                    continue;
                }

                var remaining = card.Counters.Get(CoreCounterKeys.AttackPatternCountdown);
                if (remaining <= 0)
                {
                    // 已被加速到 0（或仍停在开火窗）：本拍直接进入名单，不钳回频率再 −1。
                    pipeline.Enqueue(new SetAttackPatternCountdownAction(uid, 0));
                    candidates.Add(uid);
                    continue;
                }

                remaining -= 1;
                pipeline.Enqueue(new SetAttackPatternCountdownAction(uid, remaining));
                if (remaining <= 0)
                {
                    candidates.Add(uid);
                }
            }

            candidates.Sort();
            mEnemyActionRoster.AddRange(candidates);
            var resolved = pipeline.RunToCompletion();

            // 独立背面 Tick：仅已 Register 的 faceDownTick.*，与 AttackPatternCountdown 解耦。
            FaceDownTickCounters.TickFaceDownBoard(board, registry, null);
            return resolved;
        }

        private int ResolveNextEnemyActionInternal()
        {
            if (mEnemyActionCursor >= mEnemyActionRoster.Count)
            {
                return 0;
            }

            if (IsTerminalPhase(CurrentPhase) || IsAvatarDefeated())
            {
                mEnemyActionCursor = mEnemyActionRoster.Count;
                return 0;
            }

            var monsterUid = mEnemyActionRoster[mEnemyActionCursor];
            mEnemyActionCursor++;

            var registry = this.GetModel<CardRegistry>();
            CardInstance monster;
            if (!registry.TryGet(monsterUid, out monster)
                || monster.Kind != CardKind.Monster
                || monster.Zone.Value != ZoneId.Board
                || !IsCardAlive(monster))
            {
                return 0;
            }

            var board = this.GetModel<BoardModel>();
            var avatarUid = board.AvatarUid.Value;
            CardInstance avatar;
            if (avatarUid <= 0 || !registry.TryGet(avatarUid, out avatar) || !IsCardAlive(avatar))
            {
                mEnemyActionCursor = mEnemyActionRoster.Count;
                return 0;
            }

            var pipeline = this.GetSystem<IActionPipelineSystem>();
            var statSystem = this.GetSystem<IStatSystem>();
            // ADR-0016：结算时已背面 → 不开火、不 Reset（倒计时保持冻结）。
            if (!monster.FaceUp)
            {
                return 0;
            }

            if (IsActionBanned(statSystem, monster)
                || !AttackPatternRules.MeetsPositionRequirement(
                    monster.AttackPattern,
                    monster.Slot.Value,
                    board.AvatarSlot.Value))
            {
                return EnqueueResetAttackPatternCountdown(pipeline, monster);
            }

            // 单向打击：不开交战作用域；标准伤害管线；玩家不反击（ADR-0012）。
            pipeline.Enqueue(new DealDamageAction(
                monster.Uid,
                avatar.Uid,
                GetAttackDamage(statSystem, monster)));
            var resolved = pipeline.RunToCompletion();
            resolved += EnqueueResetAttackPatternCountdown(pipeline, monster);

            if (IsTerminalPhase(CurrentPhase) || IsAvatarDefeated())
            {
                mEnemyActionCursor = mEnemyActionRoster.Count;
            }

            return resolved;
        }

        private int ResolveEnemyActionFinaleInternal()
        {
            mEnemyActionRoster.Clear();
            mEnemyActionCursor = 0;
            if (IsTerminalPhase(CurrentPhase))
            {
                return 0;
            }

            // 收尾：补牌 + 通关检查，不旋转（ADR-0012）。
            var resolved = ResolvePostKillFillInternal();
            resolved += CompleteNodeIfCleared();
            return resolved;
        }

        private static int EnqueueResetAttackPatternCountdown(
            IActionPipelineSystem pipeline,
            CardInstance monster)
        {
            if (pipeline == null
                || monster == null
                || !AttackPatternRules.ParticipatesInEnemyAction(monster.AttackPattern))
            {
                return 0;
            }

            var frequency = AttackPatternRules.Frequency(monster.AttackPattern);
            if (frequency <= 0)
            {
                return 0;
            }

            pipeline.Enqueue(new SetAttackPatternCountdownAction(monster.Uid, frequency));
            return pipeline.RunToCompletion();
        }

        private static bool IsCardAlive(CardInstance card)
        {
            if (card == null)
            {
                return false;
            }

            if (card.Zone.Value == ZoneId.Graveyard || card.Zone.Value == ZoneId.Removed)
            {
                return false;
            }

            return (int)System.Math.Round(card.Stats.GetBase(StatId.Hp)) > 0;
        }

        private bool IsAvatarDefeated()
        {
            var board = this.GetModel<BoardModel>();
            var avatarUid = board.AvatarUid.Value;
            if (avatarUid <= 0)
            {
                return true;
            }

            CardInstance avatar;
            if (!this.GetModel<CardRegistry>().TryGet(avatarUid, out avatar))
            {
                return true;
            }

            return !IsCardAlive(avatar);
        }

        private static bool IsActionBanned(IStatSystem statSystem, CardInstance card)
        {
            return statSystem.EvaluateRule(
                RuleId.ActionBanned,
                0f,
                statSystem.CreateContext(card)) > 0f;
        }

        private int AdvanceInteractionCountInternal()
        {
            // 即使本拍交战已把相位推到 Defeat，仍计一次九宫格互动（ADR-0012）。
            var pipeline = this.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new ModifyInteractionCountAction(1));
            return pipeline.RunToCompletion();
        }

        private int ResolvePostKillFillInternal()
        {
            if (IsTerminalPhase(CurrentPhase))
            {
                return 0;
            }

            var pipeline = this.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new FillEmptySlotsAction());
            return pipeline.RunToCompletion();
        }

        private int ResolvePostKillRotateInternal()
        {
            if (IsTerminalPhase(CurrentPhase))
            {
                return 0;
            }

            var pipeline = this.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new RotateBoardClockwiseAction());
            var resolved = pipeline.RunToCompletion();
            resolved += CompleteNodeIfCleared();
            return resolved;
        }

        private int ResolveFillEmptySlotsBatchInternal(bool skipFill)
        {
            if (skipFill || IsTerminalPhase(CurrentPhase))
            {
                return 0;
            }

            var pipeline = this.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new FillEmptySlotsAction());
            return pipeline.RunToCompletion();
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
            // 清关当拍结算全部未使用道具卡，并清掉残留机关；不再走 help.choice 三选一。
            resolved += this.GetSystem<IEconomySystem>().SettleUnusedHelpCards();
            resolved += this.GetSystem<IEconomySystem>().ClearResidualTraps();
            pipeline.Enqueue(new ChangePhaseAction(GamePhase.RoomChoice));
            EnqueuePostClearOffers(pipeline);
            return resolved + pipeline.RunToCompletion();
        }

        private void EnqueuePostClearOffers(IActionPipelineSystem pipeline)
        {
            var nodeIndex = this.GetModel<RunModel>().NodeIndex.Value;
            var schedule = MapNodeProgression.GetScheduleOrDefault(nodeIndex);
            if (schedule.NavigationOffer != NavigationKind.None)
            {
                pipeline.Enqueue(new OfferNavigationAction(schedule.NavigationOffer));
                return;
            }

            pipeline.Enqueue(new OfferRoomChoicesAction(RollRoomChoicesOrFallback(nodeIndex)));
        }

        private const string ShopHelpCardsPoolId = PendingChoiceModel.ShopPoolId;

        private static bool IsRelicRewardPool(string poolId)
        {
            return !string.IsNullOrEmpty(poolId)
                && poolId.StartsWith("relic.", System.StringComparison.Ordinal);
        }

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
            EnqueuePostClearOffers(pipeline);
            return resolvedSoFar + pipeline.RunToCompletion();
        }

        private IReadOnlyList<RoomKind> RollRoomChoicesOrFallback(int nodeIndex)
        {
            var choices = this.GetSystem<IRewardSystem>().RollPostClearRoomChoices(nodeIndex);
            if (choices.Count > 0)
            {
                return choices;
            }

            var family = MapNodeProgression.GetPostClearOfferFamily(nodeIndex);
            switch (family)
            {
                case NodeOfferFamily.ConsumerRooms:
                    return new[] { RoomKind.Shop, RoomKind.Tavern };
                case NodeOfferFamily.SpecialRooms:
                    return new[] { RoomKind.TreasureReward, RoomKind.ItemReward };
                default:
                    return new[] { RoomKind.Gold, RoomKind.Fountain };
            }
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

        private bool CanAttackTargetUnderRules(CardInstance target)
        {
            var restrictedUid = GetAttackTargetRestrictionUid(target);
            return restrictedUid == 0 || restrictedUid == target.Uid;
        }

        private int GetAttackTargetRestrictionUid(CardInstance target)
        {
            if (target == null)
            {
                return 0;
            }

            var statSystem = this.GetSystem<IStatSystem>();
            return (int)System.Math.Round(
                statSystem.EvaluateRule(RuleId.AttackTargetRestriction, 0f, statSystem.CreateContext(target)));
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

            if (attacker.Kind != CardKind.Avatar || !CardCombatRules.IsBoardCombatTarget(target.Kind))
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
                // 导演 Present 未 Ack 时批次仍锁输入；宝箱 Bounce 在 Present 内等待点选，
                // 必须仍能 Select/Skip，否则会留下孤儿 PendingChoice 并卡死战场。
                AppendPendingChoiceCommandsWhilePresentationLocked();
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
                        mLegalCommands.Add(GameCommandKind.RevealFace);
                    }

                    break;
                case GamePhase.RewardItemChoice:
                    mLegalCommands.Add(GameCommandKind.SelectReward);
                    mLegalCommands.Add(GameCommandKind.SkipHelpChoice);
                    AppendShopRefreshIfActive();
                    // 商店离开图标需 BoardWalk；其它奖励覆盖层不走格。
                    if (PendingChoiceModel.IsConsumerBoardPool(this.GetModel<PendingChoiceModel>().PoolId.Value))
                    {
                        mLegalCommands.Add(GameCommandKind.MoveAvatar);
                    }

                    break;
                case GamePhase.RoomChoice:
                    mLegalCommands.Add(GameCommandKind.SelectRoom);
                    mLegalCommands.Add(GameCommandKind.PickupItem);
                    mLegalCommands.Add(GameCommandKind.UseItem);
                    mLegalCommands.Add(GameCommandKind.MoveAvatar);
                    break;
                case GamePhase.RoomEvent:
                    mLegalCommands.Add(GameCommandKind.EnterRoom);
                    mLegalCommands.Add(GameCommandKind.MoveAvatar);
                    break;
            }
        }

        private void AppendPendingChoiceCommandsWhilePresentationLocked()
        {
            var pending = this.GetModel<PendingChoiceModel>();
            if (pending.Kind.Value == PendingChoiceKind.Reward)
            {
                mLegalCommands.Add(GameCommandKind.SelectReward);
                mLegalCommands.Add(GameCommandKind.SkipHelpChoice);
                AppendShopRefreshIfActive();
            }
            else if (pending.Kind.Value == PendingChoiceKind.Room
                     || pending.Kind.Value == PendingChoiceKind.Navigation)
            {
                mLegalCommands.Add(GameCommandKind.SelectRoom);
            }
        }

        private void AppendShopRefreshIfActive()
        {
            var pending = this.GetModel<PendingChoiceModel>();
            if (pending.Kind.Value == PendingChoiceKind.Reward
                && PendingChoiceModel.IsConsumerRefreshPool(pending.PoolId.Value))
            {
                mLegalCommands.Add(GameCommandKind.RefreshShop);
            }
        }
    }
}

using System;

namespace NineGrid.Flow.Presentation
{
    [Flags]
    public enum AudioCueContexts
    {
        None = 0,
        CardDefId = 1 << 0,
        SkillId = 1 << 1,
        RoomId = 1 << 2,
        ItemDefId = 1 << 3,
        ContentId = 1 << 4,
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class AudioCueAttribute : Attribute
    {
        public AudioCueAttribute(
            string cueId,
            string note,
            string module,
            string authoritativeEmitter,
            AudioCueContexts allowedContexts)
        {
            CueId = cueId ?? string.Empty;
            Note = note ?? string.Empty;
            Module = module ?? string.Empty;
            AuthoritativeEmitter = authoritativeEmitter ?? string.Empty;
            AllowedContexts = allowedContexts;
        }

        public string CueId { get; }
        public string Note { get; }
        public string Module { get; }
        public string AuthoritativeEmitter { get; }
        public AudioCueContexts AllowedContexts { get; }
    }

    /// <summary>通用 UI 与卡牌交互的稳定声音提示声明；发射仍由各声明的权威所有者负责。</summary>
    public static class InteractionAudioCues
    {
        [AudioCue("ui.main_menu.hover", "主菜单按钮悬停", "MainMenu", "GameFlowController.UpdateMenuHover", AudioCueContexts.ContentId)]
        public const string MainMenuHover = "ui.main_menu.hover";

        [AudioCue("ui.main_menu.press", "主菜单按钮按下", "MainMenu", "GameFlowController.Update", AudioCueContexts.ContentId)]
        public const string MainMenuPress = "ui.main_menu.press";

        [AudioCue("ui.main_menu.cancel", "主菜单退出确认", "MainMenu", "GameFlowController.Update", AudioCueContexts.ContentId)]
        public const string MainMenuCancel = "ui.main_menu.cancel";

        [AudioCue("ui.main_menu.reject", "主菜单操作被忙碌门禁拒绝", "MainMenu", "GameFlowController.Update", AudioCueContexts.ContentId)]
        public const string MainMenuReject = "ui.main_menu.reject";

        [AudioCue("ui.action.press", "通用界面按钮按下", "UI", "UiAudioFeedback.OnPointerDown", AudioCueContexts.ContentId)]
        public const string UiPress = "ui.action.press";

        [AudioCue("ui.action.confirm", "通用界面操作确认", "UI", "UiAudioFeedback.PulseAccepted", AudioCueContexts.ContentId)]
        public const string UiConfirm = "ui.action.confirm";

        [AudioCue("ui.action.cancel", "通用界面操作取消或关闭", "UI", "UiAudioFeedback.PulseAccepted", AudioCueContexts.ContentId)]
        public const string UiCancel = "ui.action.cancel";

        [AudioCue("ui.action.reject", "通用界面操作被拒绝", "UI", "UiAudioFeedback.OnPointerDown", AudioCueContexts.ContentId)]
        public const string UiReject = "ui.action.reject";

        [AudioCue("ui.player_audio.escape", "声音设置按 Escape 关闭", "UI", "PlayerAudioSettingsPanel.Update", AudioCueContexts.None)]
        public const string PlayerAudioEscape = "ui.player_audio.escape";

        [AudioCue("card.hand.hover", "手牌悬停聚焦", "Cards", "CardHandManagerSingleton.ApplyHandHoverTarget", AudioCueContexts.CardDefId)]
        public const string HandCardHover = "card.hand.hover";

        [AudioCue("card.ground.hover", "场地卡牌悬停聚焦", "Cards", "GroundCardHitProxy.HoverEnterClaim", AudioCueContexts.CardDefId)]
        public const string GroundCardHover = "card.ground.hover";

        [AudioCue("card.deck.hover", "牌组顶牌悬停聚焦", "Cards", "CardDeckManagerSingleton.ApplyDeckHoverTarget", AudioCueContexts.CardDefId)]
        public const string DeckCardHover = "card.deck.hover";

        [AudioCue("card.interaction.select", "卡牌选中", "Cards", "BoardCardSelectModeController.TryToggleSelection", AudioCueContexts.CardDefId)]
        public const string CardSelect = "card.interaction.select";

        [AudioCue("card.drag.pickup", "卡牌拖起", "Cards", "CardHandManagerSingleton.TryBeginDragFromHand", AudioCueContexts.CardDefId)]
        public const string CardDragPickup = "card.drag.pickup";

        [AudioCue("card.drag.drop", "卡牌放下", "Cards", "CardHandManagerSingleton.RunDragLoopAsync", AudioCueContexts.CardDefId)]
        public const string CardDragDrop = "card.drag.drop";

        [AudioCue("card.drag.valid", "卡牌落到合法落点", "Cards", "CardHandManagerSingleton.CompleteDragApplyInternalAsync", AudioCueContexts.CardDefId)]
        public const string CardDragValid = "card.drag.valid";

        [AudioCue("card.drag.return", "卡牌无效落点退回手牌", "Cards", "CardHandManagerSingleton.FinishDragWithReturnAsync", AudioCueContexts.CardDefId)]
        public const string CardDragReturn = "card.drag.return";

        [AudioCue("ui.action.hover", "通用界面控件悬停", "UI", "UiAudioFeedback.OnPointerEnter", AudioCueContexts.ContentId)]
        public const string UiHover = "ui.action.hover";

        public static void Pulse(string cueId, string diagnosticSource, string contentId = null)
        {
            TriggerPulseHub.PulseAudio(new NineGrid.Content.Audio.AudioCueRequest(
                cueId,
                diagnosticSource,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                contentId));
        }

        public static void PulseCard(string cueId, string diagnosticSource, string cardDefId)
        {
            TriggerPulseHub.PulseAudio(new NineGrid.Content.Audio.AudioCueRequest(
                cueId,
                diagnosticSource,
                cardDefId,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty));
        }
    }

    /// <summary>卡牌生命周期声音提示；发射点对齐发牌/运动落地与可见退场，不旁路 Core 事件。</summary>
    public static class CardLifecycleAudioCues
    {
        [AudioCue("card.lifecycle.deal", "发牌起飞", "Cards", "DealFlightCoordinator.LaunchDrainFlight", AudioCueContexts.CardDefId)]
        public const string Deal = "card.lifecycle.deal";

        [AudioCue("card.lifecycle.draw", "抽牌离组", "Cards", "CardDeckManagerSingleton.DealCardToHandAsync", AudioCueContexts.CardDefId)]
        public const string Draw = "card.lifecycle.draw";

        [AudioCue("card.lifecycle.shuffle", "洗牌入组", "Cards", "BoardPresentationPlayer.PresentOneShuffleIntoDeckAsync", AudioCueContexts.CardDefId)]
        public const string Shuffle = "card.lifecycle.shuffle";

        [AudioCue("card.lifecycle.into_hand", "卡牌入手就位", "Cards", "CardDeckManagerSingleton.DealCardToHandAsync", AudioCueContexts.CardDefId)]
        public const string IntoHand = "card.lifecycle.into_hand";

        [AudioCue("card.lifecycle.into_field", "卡牌入场落地", "Cards", "DealFlightCoordinator.NotifyLanded", AudioCueContexts.CardDefId)]
        public const string IntoField = "card.lifecycle.into_field";

        [AudioCue("card.lifecycle.flip", "卡牌翻面", "Cards", "FlipPlaybackCoordinator.RunPumpAsync", AudioCueContexts.None)]
        public const string Flip = "card.lifecycle.flip";

        [AudioCue("card.lifecycle.move", "卡牌移位", "Cards", "BoardMotionStepScheduler.ExecuteMotionStepAsync", AudioCueContexts.None)]
        public const string Move = "card.lifecycle.move";

        [AudioCue("card.lifecycle.swap", "卡牌交换", "Cards", "BoardMotionStepScheduler.ExecuteMotionStepAsync", AudioCueContexts.None)]
        public const string Swap = "card.lifecycle.swap";

        [AudioCue("card.lifecycle.rotate", "外圈旋转", "Cards", "BoardMotionStepScheduler.ExecuteMotionStepAsync", AudioCueContexts.None)]
        public const string Rotate = "card.lifecycle.rotate";

        [AudioCue("card.lifecycle.item_use", "道具卡使用", "Cards", "CardEffectManager.PlayUseAsync", AudioCueContexts.CardDefId)]
        public const string ItemUse = "card.lifecycle.item_use";

        [AudioCue("card.lifecycle.recycle", "卡牌回收兑金", "Cards", "CardHandManagerSingleton.OnRecycleItemIntentFlushed", AudioCueContexts.CardDefId)]
        public const string Recycle = "card.lifecycle.recycle";

        [AudioCue("card.lifecycle.exit", "卡牌退场", "Cards", "CardEffectManager.PlayDeathAsync", AudioCueContexts.CardDefId)]
        public const string Exit = "card.lifecycle.exit";

        [AudioCue("card.lifecycle.shatter", "卡牌碎裂", "Cards", "CardEffectManager.PlayDeathAsync", AudioCueContexts.CardDefId)]
        public const string Shatter = "card.lifecycle.shatter";

        public static void Pulse(string cueId, string diagnosticSource, string cardDefId = null)
        {
            if (!string.IsNullOrEmpty(cardDefId))
            {
                InteractionAudioCues.PulseCard(cueId, diagnosticSource, cardDefId);
                return;
            }

            InteractionAudioCues.Pulse(cueId, diagnosticSource);
        }

        public static void PulseMotion(NineGrid.Cards.BoardPresentationStepKind kind, string diagnosticSource)
        {
            switch (kind)
            {
                case NineGrid.Cards.BoardPresentationStepKind.Rotate:
                    Pulse(Rotate, diagnosticSource);
                    break;
                case NineGrid.Cards.BoardPresentationStepKind.Swap:
                    Pulse(Swap, diagnosticSource);
                    break;
                case NineGrid.Cards.BoardPresentationStepKind.Move:
                    Pulse(Move, diagnosticSource);
                    break;
            }
        }
    }

    /// <summary>基础战斗声音提示；交战结果仅由 Impact 装饰处理器发射，预备对齐 lunge 起手。</summary>
    public static class BattleCombatAudioCues
    {
        [AudioCue("battle.attack.prepare", "攻击准备起手", "Battle", "CardAttackBasicAdapter.BindLungeBegin", AudioCueContexts.CardDefId)]
        public const string AttackPrepare = "battle.attack.prepare";

        [AudioCue("battle.attack.hit", "攻击命中", "Battle", "DamageFloaterBeatHandler.TryApply", AudioCueContexts.CardDefId)]
        public const string AttackHit = "battle.attack.hit";

        [AudioCue("battle.combat.block", "护甲完全格挡", "Battle", "DamageFloaterBeatHandler.TryApply", AudioCueContexts.CardDefId)]
        public const string Block = "battle.combat.block";

        [AudioCue("battle.combat.armor_absorb", "护甲吸收伤害", "Battle", "DamageFloaterBeatHandler.TryApply", AudioCueContexts.CardDefId)]
        public const string ArmorAbsorb = "battle.combat.armor_absorb";

        [AudioCue("battle.combat.hp_damage", "血量受伤", "Battle", "DamageFloaterBeatHandler.TryApply", AudioCueContexts.CardDefId)]
        public const string HpDamage = "battle.combat.hp_damage";

        [AudioCue("battle.combat.heal", "治疗生效", "Battle", "DamageFloaterBeatHandler.TryApply", AudioCueContexts.CardDefId)]
        public const string Heal = "battle.combat.heal";

        [AudioCue("battle.combat.death", "单位死亡退场", "Battle", "CardEffectManager.PlayDeathAsync", AudioCueContexts.CardDefId)]
        public const string Death = "battle.combat.death";

        public static void Pulse(string cueId, string diagnosticSource, string cardDefId = null)
        {
            CardLifecycleAudioCues.Pulse(cueId, diagnosticSource, cardDefId);
        }
    }

    /// <summary>
    /// Impact 交战结果 → 声音提示映射。与飘字同缝，保证可见事实与声音一致且不由 View/Core 重复发射。
    /// </summary>
    public static class CombatOutcomeAudio
    {
        public static void PulseShowDamage(NineGrid.Core.CoreGameEvent gameEvent, string diagnosticSource)
        {
            if (gameEvent == null || gameEvent.Amount <= 0)
            {
                return;
            }

            var cardDefId = gameEvent.SourceDefId;
            BattleCombatAudioCues.Pulse(BattleCombatAudioCues.AttackHit, diagnosticSource, cardDefId);

            var armorDamage = gameEvent.ArmorDamage;
            var hpDamage = gameEvent.HpDamage;
            if (armorDamage > 0 && hpDamage <= 0)
            {
                BattleCombatAudioCues.Pulse(BattleCombatAudioCues.Block, diagnosticSource, cardDefId);
                return;
            }

            if (armorDamage > 0)
            {
                BattleCombatAudioCues.Pulse(BattleCombatAudioCues.ArmorAbsorb, diagnosticSource, cardDefId);
            }

            if (hpDamage > 0)
            {
                BattleCombatAudioCues.Pulse(BattleCombatAudioCues.HpDamage, diagnosticSource, cardDefId);
            }
        }

        public static void PulseHeal(NineGrid.Core.CoreGameEvent gameEvent, string diagnosticSource)
        {
            if (gameEvent == null || gameEvent.Delta <= 0)
            {
                return;
            }

            BattleCombatAudioCues.Pulse(
                BattleCombatAudioCues.Heal,
                diagnosticSource,
                gameEvent.SourceDefId);
        }
    }

    /// <summary>
    /// 技能 / 可见效果 / 机关 / 遗物声音提示。权威发射为
    /// <see cref="EffectTriggerPulseBeatHandler"/>（触发）与
    /// <see cref="NineGrid.Cards.CardAttackBasicAdapter"/>（可取消蓄力排期）。
    /// 内容覆盖靠 cardDefId+skillId 解析，禁止动态 <c>sfx.effect.&lt;CardUid&gt;</c> 绑定主键。
    /// </summary>
    public static class SkillEffectTrapRelicAudioCues
    {
        public const float DefaultChargeScheduleSeconds = 0.35f;

        [AudioCue(
            "sfx.effect.trigger",
            "可见效果触发",
            "Battle",
            "EffectTriggerPulseBeatHandler.TryApply",
            AudioCueContexts.CardDefId | AudioCueContexts.SkillId | AudioCueContexts.ContentId)]
        public const string EffectTrigger = "sfx.effect.trigger";

        [AudioCue(
            "sfx.skill.trigger",
            "怪物技能触发",
            "Battle",
            "EffectTriggerPulseBeatHandler.TryApply",
            AudioCueContexts.CardDefId | AudioCueContexts.SkillId)]
        public const string SkillTrigger = "sfx.skill.trigger";

        [AudioCue(
            "sfx.trap.trigger",
            "机关触发",
            "Battle",
            "EffectTriggerPulseBeatHandler.TryApply",
            AudioCueContexts.CardDefId | AudioCueContexts.SkillId | AudioCueContexts.ContentId)]
        public const string TrapTrigger = "sfx.trap.trigger";

        [AudioCue(
            "sfx.relic.trigger",
            "遗物触发",
            "Battle",
            "EffectTriggerPulseBeatHandler.TryApply",
            AudioCueContexts.CardDefId | AudioCueContexts.SkillId | AudioCueContexts.ContentId)]
        public const string RelicTrigger = "sfx.relic.trigger";

        [AudioCue(
            "battle.attack.charge",
            "攻击蓄力可取消预备",
            "Battle",
            "CardAttackBasicAdapter.PlayBoundRigAsync",
            AudioCueContexts.CardDefId)]
        public const string AttackCharge = "battle.attack.charge";

        public static string ResolveCueId(string sourceDefId, string cause)
        {
            if (StartsWithToken(sourceDefId, "trap.") || StartsWithToken(cause, "trap."))
            {
                return TrapTrigger;
            }

            if (StartsWithToken(sourceDefId, "relic.") || StartsWithToken(cause, "relic."))
            {
                return RelicTrigger;
            }

            if (StartsWithToken(cause, "skill."))
            {
                return SkillTrigger;
            }

            return EffectTrigger;
        }

        public static bool IsLegacyDynamicEffectCueId(string cueId)
        {
            if (string.IsNullOrEmpty(cueId) || !cueId.StartsWith("sfx.effect.", StringComparison.Ordinal))
            {
                return false;
            }

            var suffix = cueId.Substring("sfx.effect.".Length);
            if (string.Equals(suffix, "trigger", StringComparison.Ordinal))
            {
                return false;
            }

            for (var i = 0; i < suffix.Length; i++)
            {
                if (!char.IsDigit(suffix[i]))
                {
                    return false;
                }
            }

            return suffix.Length > 0;
        }

        public static void PulseTrigger(
            string sourceDefId,
            string cause,
            string diagnosticSource,
            int diagnosticCardUid = 0)
        {
            var cueId = ResolveCueId(sourceDefId, cause);
            TriggerPulseHub.PulseAudio(new NineGrid.Content.Audio.AudioCueRequest(
                cueId,
                diagnosticSource,
                sourceDefId ?? string.Empty,
                cause ?? string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                diagnosticCardUid));
        }

        public static NineGrid.Presentation.Systems.AudioScheduleKey ScheduleAttackCharge(
            string cardDefId,
            float delaySeconds = DefaultChargeScheduleSeconds)
        {
            var audio = NineGrid.Presentation.Systems.AudioSystem.EnsureRegistered();
            return audio.ScheduleCue(
                new NineGrid.Content.Audio.AudioCueRequest(
                    AttackCharge,
                    "CardAttackBasicAdapter.PlayBoundRigAsync",
                    cardDefId ?? string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty),
                Math.Max(0f, delaySeconds));
        }

        public static bool CancelScheduled(NineGrid.Presentation.Systems.AudioScheduleKey key)
        {
            if (!key.IsValid)
            {
                return false;
            }

            try
            {
                return NineGrid.Presentation.Systems.AudioSystem.EnsureRegistered()
                    .CancelScheduledCue(key);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool StartsWithToken(string value, string prefix)
        {
            return !string.IsNullOrEmpty(value)
                && value.StartsWith(prefix, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// 房间、经济与跑图流程声音提示；发射对齐进房/离房、商店/卡店、奖励/属性、金币演出与胜负回菜单，
    /// 不经 FlowTrace 旁路，也不由 Presenter 提交 Music。
    /// </summary>
    public static class FlowRoomEconomyAudioCues
    {
        [AudioCue("flow.room.enter", "进入房间", "Flow", "RoomIconBoardPresenter.ExecuteSelectEnterHardCut", AudioCueContexts.RoomId)]
        public const string RoomEnter = "flow.room.enter";

        [AudioCue("flow.room.leave", "离开房间", "Flow", "ShopBoardPresenter.TryLeave", AudioCueContexts.None)]
        public const string RoomLeave = "flow.room.leave";

        [AudioCue("flow.run.floor_cross", "上下楼过场", "Flow", "RunSceneTransitionService.BeginCoverAsync", AudioCueContexts.None)]
        public const string FloorCross = "flow.run.floor_cross";

        [AudioCue("flow.run.transition", "同层导航过场", "Flow", "RunSceneTransitionService.BeginCoverAsync", AudioCueContexts.None)]
        public const string RunTransition = "flow.run.transition";

        [AudioCue("shop.buy", "商店购买成功", "Shop", "ShopBoardPresenter.TryBuy", AudioCueContexts.ContentId)]
        public const string ShopBuy = "shop.buy";

        [AudioCue("shop.refresh", "商店刷新成功", "Shop", "ShopBoardPresenter.TryRefresh", AudioCueContexts.None)]
        public const string ShopRefresh = "shop.refresh";

        [AudioCue("shop.upgrade", "商店升级牌格", "Shop", "ShopBoardPresenter.TryBuy", AudioCueContexts.ContentId)]
        public const string ShopUpgrade = "shop.upgrade";

        [AudioCue("shop.insufficient_gold", "商店余额不足", "Shop", "ShopBoardPresenter.TryBuy", AudioCueContexts.None)]
        public const string ShopInsufficientGold = "shop.insufficient_gold";

        [AudioCue("tavern.buy", "牌店购买或服务成功", "Tavern", "TavernBoardPresenter.TrySelect", AudioCueContexts.ContentId)]
        public const string TavernBuy = "tavern.buy";

        [AudioCue("tavern.refresh", "牌店刷新成功", "Tavern", "TavernBoardPresenter.TryRefresh", AudioCueContexts.None)]
        public const string TavernRefresh = "tavern.refresh";

        [AudioCue("tavern.insufficient_gold", "牌店余额不足", "Tavern", "TavernBoardPresenter.TrySelect", AudioCueContexts.None)]
        public const string TavernInsufficientGold = "tavern.insufficient_gold";

        [AudioCue("reward.claim", "领取奖励", "Reward", "RewardBoardPresenter.HandleTake", AudioCueContexts.ContentId)]
        public const string RewardClaim = "reward.claim";

        [AudioCue("reward.abandon", "放弃奖励离开", "Reward", "RewardBoardPresenter.TryLeave", AudioCueContexts.None)]
        public const string RewardAbandon = "reward.abandon";

        [AudioCue("attribute.pick", "属性提升选择", "Attribute", "AttributeBoardPresenter.TrySelect", AudioCueContexts.ContentId)]
        public const string AttributePick = "attribute.pick";

        [AudioCue("economy.gold_gain", "获得金币", "Economy", "GoldGainPresentationBinder.OnGoldGainPresentationRequested", AudioCueContexts.None)]
        public const string GoldGain = "economy.gold_gain";

        [AudioCue("economy.gold_spend", "消耗金币", "Economy", "GoldGainPresentationBinder.OnGoldGainPresentationRequested", AudioCueContexts.None)]
        public const string GoldSpend = "economy.gold_spend";

        [AudioCue("flow.victory", "整局胜利提示", "Flow", "GameFlowOrchestrator.ShowBattleEndAndReturnAsync", AudioCueContexts.None)]
        public const string Victory = "flow.victory";

        [AudioCue("flow.defeat", "战斗失败提示", "Flow", "GameFlowOrchestrator.ShowBattleEndAndReturnAsync", AudioCueContexts.None)]
        public const string Defeat = "flow.defeat";

        [AudioCue("flow.return_main_menu", "返回主菜单", "Flow", "GameFlowOrchestrator.EnterMainMenuImmediate", AudioCueContexts.None)]
        public const string ReturnMainMenu = "flow.return_main_menu";

        public static void Pulse(string cueId, string diagnosticSource, string contentId = null)
        {
            InteractionAudioCues.Pulse(cueId, diagnosticSource, contentId);
        }

        public static void PulseRoom(string cueId, string diagnosticSource, string roomId)
        {
            TriggerPulseHub.PulseAudio(new NineGrid.Content.Audio.AudioCueRequest(
                cueId,
                diagnosticSource,
                string.Empty,
                string.Empty,
                roomId ?? string.Empty,
                string.Empty,
                string.Empty));
        }

        public static void PulseTransition(bool crossFloor, string diagnosticSource)
        {
            Pulse(crossFloor ? FloorCross : RunTransition, diagnosticSource);
        }

        public static void PulseGoldPresentation(bool isSpend, string diagnosticSource)
        {
            Pulse(isSpend ? GoldSpend : GoldGain, diagnosticSource);
        }

        public static bool IsInsufficientGoldReason(string reason)
        {
            return string.Equals(reason, "Not enough gold", StringComparison.Ordinal);
        }
    }
}

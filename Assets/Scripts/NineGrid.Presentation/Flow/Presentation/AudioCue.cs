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
}

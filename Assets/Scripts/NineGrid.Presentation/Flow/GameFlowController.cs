using NineGrid.Content.Audio;
using NineGrid.Core;
using NineGrid.Core.Localization;
using NineGrid.Flow.BoardBriefTip;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Commands;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;
using L10n = NineGrid.Core.Localization.L10n;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NineGrid.Flow
{
    /// <summary>
    /// 主流程场景 View：场景绑定 / 主菜单输入 / Notice·Panel 投影。
    /// 流程权威在 <see cref="IGameFlowShellSystem"/>；编排在内部 Orchestrator。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameFlowController : MonoBehaviour, IGameFlowView
    {
        [Header("Refs")]
        [Tooltip("面板路由；留空则运行时在同物体上 GetComponent / AddComponent。")]
        [SerializeField] private UiPanelRouter panelRouter;

        [Tooltip("主菜单「开始」按钮；留空则运行时查找 MainPanel/StartRun。")]
        [SerializeField] private Collider2D startRunHit;

        [Tooltip("主菜单「退出」按钮；留空则运行时查找 MainPanel/QuitGame。")]
        [SerializeField] private Collider2D quitGameHit;

        [Tooltip("主菜单「设置」按钮；留空则运行时查找 MainPanel/SettingsScreen。")]
        [SerializeField] private Collider2D settingsHit;

        [Tooltip("主菜单「教学」按钮；留空则运行时查找 MainPanel/TutorialRun。")]
        [SerializeField] private Collider2D tutorialHit;

        [Tooltip("主菜单「语言切换」按钮；留空则运行时查找 MainPanel/LanguageToggle。")]
        [SerializeField] private Collider2D languageHit;

        [Tooltip("点选相机；留空则运行时取 Camera.main。")]
        [SerializeField] private Camera worldCamera;

        [Header("Timing")]
        [Tooltip("房间事件即时结算 Notice 展示时长（秒）。")]
        [SerializeField] private float roomEventStubSeconds = 0.6f;

        [Tooltip("胜利 Notice 展示时长（秒）后回主菜单。")]
        [SerializeField] private float victoryNoticeSeconds = 1f;

        [Tooltip("失败 Notice 展示时长（秒）后回主菜单。")]
        [SerializeField] private float defeatNoticeSeconds = 1f;

        [Tooltip("胜利文案。")]
        [SerializeField] private string victoryMessage = "胜利";

        [Tooltip("失败文案。")]
        [SerializeField] private string defeatMessage = "失败";

        public const float QuickTestTimeScale = GameFlowShellSystem.QuickTestTimeScale;
        public const int QuickTestAvatarHp = GameFlowShellSystem.QuickTestAvatarHp;
        public const int QuickTestAvatarAttack = GameFlowShellSystem.QuickTestAvatarAttack;

        public float RoomEventStubSeconds => roomEventStubSeconds;
        public float VictoryNoticeSeconds => victoryNoticeSeconds;
        public float DefeatNoticeSeconds => defeatNoticeSeconds;

        // 序列化中文文案作为 Tr 默认值（ADR-0046）：zh 用序列化值，en 查 ui 表缺键回中文。
        public string VictoryMessage => L10n.Tr("notice.victory", victoryMessage);
        public string DefeatMessage => L10n.Tr("notice.defeat", defeatMessage);

        public GameFlowShellState State =>
            ResolveShell()?.State.Value ?? GameFlowShellState.MainMenu;

        public bool IsQuickTestMode => ResolveShell()?.IsQuickTestMode ?? false;
        public int NodeIndex => ResolveShell()?.NodeIndex ?? 0;
        public bool CanAcceptQuickTestEntry => ResolveShell()?.CanAcceptQuickTestEntry ?? false;

        private const float MenuHoverScale = 1.08f;

        private Collider2D hoveredMenuHit;
        private Vector3 hoveredMenuBaseScale = Vector3.one;

        private static readonly AudioCueRequest StartHoverRequest = CreateMenuRequest(
            InteractionAudioCues.MainMenuHover,
            "GameFlowController.MainMenu.StartHover",
            "main_menu.start");
        private static readonly AudioCueRequest QuitHoverRequest = CreateMenuRequest(
            InteractionAudioCues.MainMenuHover,
            "GameFlowController.MainMenu.QuitHover",
            "main_menu.quit");
        private static readonly AudioCueRequest StartPressRequest = CreateMenuRequest(
            InteractionAudioCues.MainMenuPress,
            "GameFlowController.MainMenu.StartPress",
            "main_menu.start");
        private static readonly AudioCueRequest QuitPressRequest = CreateMenuRequest(
            InteractionAudioCues.MainMenuPress,
            "GameFlowController.MainMenu.QuitPress",
            "main_menu.quit");
        private static readonly AudioCueRequest StartRejectRequest = CreateMenuRequest(
            InteractionAudioCues.MainMenuReject,
            "GameFlowController.MainMenu.StartReject",
            "main_menu.start");
        private static readonly AudioCueRequest QuitRejectRequest = CreateMenuRequest(
            InteractionAudioCues.MainMenuReject,
            "GameFlowController.MainMenu.QuitReject",
            "main_menu.quit");
        private static readonly AudioCueRequest QuitCancelRequest = CreateMenuRequest(
            InteractionAudioCues.MainMenuCancel,
            "GameFlowController.MainMenu.QuitCancel",
            "main_menu.quit");
        private static readonly AudioCueRequest SettingsHoverRequest = CreateMenuRequest(
            InteractionAudioCues.MainMenuHover,
            "GameFlowController.MainMenu.SettingsHover",
            "main_menu.settings");
        private static readonly AudioCueRequest SettingsPressRequest = CreateMenuRequest(
            InteractionAudioCues.MainMenuPress,
            "GameFlowController.MainMenu.SettingsPress",
            "main_menu.settings");
        private static readonly AudioCueRequest TutorialHoverRequest = CreateMenuRequest(
            InteractionAudioCues.MainMenuHover,
            "GameFlowController.MainMenu.TutorialHover",
            "main_menu.tutorial");
        private static readonly AudioCueRequest TutorialPressRequest = CreateMenuRequest(
            InteractionAudioCues.MainMenuPress,
            "GameFlowController.MainMenu.TutorialPress",
            "main_menu.tutorial");
        private static readonly AudioCueRequest TutorialRejectRequest = CreateMenuRequest(
            InteractionAudioCues.MainMenuReject,
            "GameFlowController.MainMenu.TutorialReject",
            "main_menu.tutorial");
        private static readonly AudioCueRequest LanguageHoverRequest = CreateMenuRequest(
            InteractionAudioCues.MainMenuHover,
            "GameFlowController.MainMenu.LanguageHover",
            "main_menu.language");
        private static readonly AudioCueRequest LanguagePressRequest = CreateMenuRequest(
            InteractionAudioCues.MainMenuPress,
            "GameFlowController.MainMenu.LanguagePress",
            "main_menu.language");

        private void Awake()
        {
            EnsureViewBindings();
            var shell = GameFlowShellSystem.EnsureRegistered();
            shell.Bind(this);
            // DisableDomainReload：上一局 Shell 相位/IsBusy 会残留，Update 会静默吞掉 StartRun。
            if (shell.State.Value != GameFlowShellState.MainMenu || shell.IsBusy)
            {
                shell.ReturnToMainMenu();
            }
            else
            {
                ShowMainMenuPanels();
                HideNotice();
            }
        }

        private void Start()
        {
            // 语言系统在 PresentationSceneRoot.OnBind(WireHosts) 注册，晚于本 Awake；
            // Start 再刷一次 label，保证持久化 en 偏好下按钮初始显示 English。
            RefreshLanguageToggleLabel();
        }

        private void OnDestroy()
        {
            var shell = ResolveShell() as GameFlowShellSystem;
            if (shell == null)
            {
                return;
            }

            // 先复位权威再 Unbind，避免无 Domain Reload 时 EditMode 残留 Busy/非 MainMenu。
            shell.ForceMainMenuAuthority();
            shell.UnbindIfView(this);
        }

        private void Update()
        {
            var shell = ResolveShell();
            if (shell == null
                || shell.State.Value != GameFlowShellState.MainMenu)
            {
                ClearMenuHover();
                return;
            }

            // 场景引用只在 Awake / 显式调用 EnsureViewBindings 时解析；Update 仅轮询缓存。
            // 局内功能菜单（音量等）打开时半黑屏盖住主菜单，勿再响应开始/退出。
            if (NineGrid.Presentation.Ui.PlayerAudioSettingsPanel.IsOpen
                || BattleUiDimmerOverlay.IsActive)
            {
                ClearMenuHover();
                return;
            }

            UpdateMenuHover();
            if (!WorldPointerUtility.WasPrimaryPressedThisFrame())
            {
                return;
            }

            if (WorldPointerUtility.TryOverlapColliderOnPlane(worldCamera, startRunHit))
            {
                if (shell.IsBusy)
                {
                    PulseMenuRequest(StartRejectRequest);
                    return;
                }

                PulseMenuRequest(StartPressRequest);
                // 正式开局前先过人物选择面板；场景缺预置时回退直接开局。
                if (!NineGrid.Presentation.Ui.CharacterSelectPanel.RequestOpen())
                {
                    BeginFormalRun();
                }

                return;
            }

            if (WorldPointerUtility.TryOverlapColliderOnPlane(worldCamera, settingsHit))
            {
                PulseMenuRequest(SettingsPressRequest);
                NineGrid.Presentation.Ui.PlayerAudioSettingsPanel.RequestOpen();
                return;
            }

            if (WorldPointerUtility.TryOverlapColliderOnPlane(worldCamera, tutorialHit))
            {
                if (shell.IsBusy)
                {
                    PulseMenuRequest(TutorialRejectRequest);
                    return;
                }

                PulseMenuRequest(TutorialPressRequest);
                BeginTutorialRun();
                return;
            }

            if (WorldPointerUtility.TryOverlapColliderOnPlane(worldCamera, languageHit))
            {
                PulseMenuRequest(LanguagePressRequest);
                ToggleLanguage();
                return;
            }

            if (WorldPointerUtility.TryOverlapColliderOnPlane(worldCamera, quitGameHit))
            {
                if (shell.IsBusy)
                {
                    PulseMenuRequest(QuitRejectRequest);
                    return;
                }

                PulseMenuRequest(QuitPressRequest);
                PulseMenuRequest(QuitCancelRequest);
                QuitGame();
            }
        }

        /// <summary>主菜单「开始游戏」：无作弊正式开局。真相在 BeginGameFlowRunCommand。</summary>
        [AudioCue(
            "ui.main_menu.start",
            "主菜单开始游戏确认",
            "MainMenu",
            "GameFlowController.BeginFormalRun",
            AudioCueContexts.None)]
        private const string MainMenuStartCueId = "ui.main_menu.start";

        public void BeginFormalRun()
        {
            TriggerPulseHub.PulseAudio(AudioCueRequest.Simple(
                MainMenuStartCueId,
                "GameFlowController.BeginFormalRun"));

            // 首次游玩（存档无教学完成标记）：先进教学关卡，通关后自动转正式开局。
            if (!NineGrid.Flow.Tutorial.TutorialProgressStore.IsCompleted())
            {
                Debug.Log("[GameFlow] 首次游玩：先进入教学关卡，通关后自动开始正式冒险。");
                SendBeginRun(GameFlowRunOptions.CreateTutorial(continueToFormalRun: true));
                return;
            }

            SendBeginRun(GameFlowRunOptions.CreateFormal());
        }

        /// <summary>主菜单「教学」独立入口：单场教学战斗，完成后返回主菜单。</summary>
        public void BeginTutorialRun()
        {
            SendBeginRun(GameFlowRunOptions.CreateTutorial(continueToFormalRun: false));
        }

        public void BeginQuickTestRun(QuickTestRunOptions options)
        {
            SendBeginRun(GameFlowRunOptions.CreateQuickTest(options));
        }

        public void ReturnToMainMenu()
        {
            var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            arch?.SendCommand(new ReturnToMainMenuCommand());
        }

        public void ShowQuickTestPickerNotice(string message)
        {
            ShowNotice(message);
        }

        public void HideQuickTestPickerNotice()
        {
            HideNotice();
        }

        public string BuildQuickTestPickerMenuText()
        {
            return GameFlowShellSystem.EnsureRegistered().BuildQuickTestPickerMenuText();
        }

        public bool TryBeginQuickTestFromPickerCode(int code)
        {
            return GameFlowShellSystem.EnsureRegistered().TryBeginQuickTestFromPickerCode(code);
        }

        public void EnsureViewBindings()
        {
            if (panelRouter == null)
            {
                panelRouter = GetComponent<UiPanelRouter>();
                if (panelRouter == null)
                {
                    panelRouter = gameObject.AddComponent<UiPanelRouter>();
                }
            }

            panelRouter.EnsureBindings();

            worldCamera = WorldPointerUtility.ResolveCamera(worldCamera);

            if (startRunHit == null)
            {
                var start = FindDeep("StartRun");
                if (start != null)
                {
                    startRunHit = start.GetComponent<Collider2D>();
                }
            }

            if (quitGameHit == null)
            {
                var quit = FindDeep("QuitGame");
                if (quit != null)
                {
                    quitGameHit = quit.GetComponent<Collider2D>();
                }
            }

            if (settingsHit == null)
            {
                var settings = FindDeep("SettingsScreen");
                if (settings != null)
                {
                    settingsHit = settings.GetComponent<Collider2D>();
                }
            }

            if (tutorialHit == null)
            {
                var tutorial = FindDeep("TutorialRun");
                if (tutorial != null)
                {
                    tutorialHit = tutorial.GetComponent<Collider2D>();
                }
            }

            if (languageHit == null)
            {
                var language = FindDeep("LanguageToggle");
                if (language != null)
                {
                    languageHit = language.GetComponent<Collider2D>();
                }
            }

            RefreshLanguageToggleLabel();
        }

        /// <summary>主菜单语言切换（ADR-0046）：zh ↔ en，写偏好 → 重载表 → 主菜单文本即刷。</summary>
        private void ToggleLanguage()
        {
            var language = LanguageSettingsSystem.EnsureRegistered();
            language.Toggle();
            RefreshLanguageToggleLabel();
        }

        /// <summary>按钮 label 显示当前语言（zh「中文」/ en「English」）。</summary>
        private void RefreshLanguageToggleLabel()
        {
            if (languageHit == null)
            {
                return;
            }

            var label = languageHit.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (label != null)
            {
                label.text = L10n.Tr("menu.language", "中文");
            }
        }

        public void ShowNotice(string message)
        {
            EnsureViewBindings();
            var tip = BoardBriefTipPresenter.EnsureExists();
            tip.ShowNotice(message ?? string.Empty);
        }

        public void HideNotice()
        {
            var tip = BoardBriefTipPresenter.InstanceOrNull();
            tip?.ClearNotice();
        }

        public void ShowMainMenuPanels()
        {
            EnsureViewBindings();
            panelRouter.ShowMainMenu();
        }

        public void ShowInRunShell(bool inBattle = true)
        {
            EnsureViewBindings();
            panelRouter.ShowInRunShell(inBattle);
        }

        public void ShowRewardOverlay()
        {
            EnsureViewBindings();
            panelRouter.ShowRewardOverlay();
        }

        public void ShowRoomEventOverlay()
        {
            EnsureViewBindings();
            panelRouter.ShowRoomEventOverlay();
        }

        public void HideAllOverlays()
        {
            EnsureViewBindings();
            panelRouter.HideAllOverlays();
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void UpdateMenuHover()
        {
            Collider2D next = null;
            if (WorldPointerUtility.TryOverlapColliderOnPlane(worldCamera, startRunHit))
            {
                next = startRunHit;
            }
            else if (WorldPointerUtility.TryOverlapColliderOnPlane(worldCamera, settingsHit))
            {
                next = settingsHit;
            }
            else if (WorldPointerUtility.TryOverlapColliderOnPlane(worldCamera, tutorialHit))
            {
                next = tutorialHit;
            }
            else if (WorldPointerUtility.TryOverlapColliderOnPlane(worldCamera, languageHit))
            {
                next = languageHit;
            }
            else if (WorldPointerUtility.TryOverlapColliderOnPlane(worldCamera, quitGameHit))
            {
                next = quitGameHit;
            }

            if (next == hoveredMenuHit)
            {
                return;
            }

            RestoreHoveredMenuScale();
            hoveredMenuHit = next;
            if (next != null)
            {
                hoveredMenuBaseScale = next.transform.localScale;
                next.transform.localScale = hoveredMenuBaseScale * MenuHoverScale;
                PulseMenuRequest(next == startRunHit
                    ? StartHoverRequest
                    : (next == settingsHit
                        ? SettingsHoverRequest
                        : (next == tutorialHit
                            ? TutorialHoverRequest
                            : (next == languageHit ? LanguageHoverRequest : QuitHoverRequest))));
            }
        }

        private void ClearMenuHover()
        {
            RestoreHoveredMenuScale();
            hoveredMenuHit = null;
        }

        private void RestoreHoveredMenuScale()
        {
            if (hoveredMenuHit != null)
            {
                hoveredMenuHit.transform.localScale = hoveredMenuBaseScale;
            }
        }

        private static void PulseMenuRequest(AudioCueRequest request)
        {
            TriggerPulseHub.PulseAudio(request);
        }

        private static AudioCueRequest CreateMenuRequest(string cueId, string source, string contentId)
        {
            return new AudioCueRequest(
                cueId,
                source,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                contentId);
        }

        private static void SendBeginRun(GameFlowRunOptions options)
        {
            var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            if (arch == null)
            {
                GameFlowShellSystem.EnsureRegistered().BeginRun(options);
                return;
            }

            arch.SendCommand(new BeginGameFlowRunCommand(options));
        }

        private static IGameFlowShellSystem ResolveShell()
        {
            return NineGridArchitecture.Interface?.GetSystem<IGameFlowShellSystem>()
                   ?? NineGridArchitecture.Current?.GetSystem<IGameFlowShellSystem>();
        }

        private static GameObject FindDeep(string objectName)
        {
            var all = Resources.FindObjectsOfTypeAll<Transform>();
            for (var i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null || t.name != objectName || !t.gameObject.scene.IsValid())
                {
                    continue;
                }

                return t.gameObject;
            }

            return null;
        }
    }
}

using NineGrid.Content.Audio;
using NineGrid.Core;
using NineGrid.Flow.BoardBriefTip;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Commands;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

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
        public string VictoryMessage => victoryMessage;
        public string DefeatMessage => defeatMessage;

        public GameFlowShellState State =>
            ResolveShell()?.State.Value ?? GameFlowShellState.MainMenu;

        public bool IsQuickTestMode => ResolveShell()?.IsQuickTestMode ?? false;
        public int NodeIndex => ResolveShell()?.NodeIndex ?? 0;
        public bool CanAcceptQuickTestEntry => ResolveShell()?.CanAcceptQuickTestEntry ?? false;

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
                || shell.State.Value != GameFlowShellState.MainMenu
                || shell.IsBusy)
            {
                return;
            }

            if (!WorldPointerUtility.WasPrimaryPressedThisFrame())
            {
                return;
            }

            EnsureViewBindings();
            if (WorldPointerUtility.TryOverlapColliderOnPlane(worldCamera, startRunHit))
            {
                BeginFormalRun();
                return;
            }

            if (WorldPointerUtility.TryOverlapColliderOnPlane(worldCamera, quitGameHit))
            {
                QuitGame();
            }
        }

        /// <summary>主菜单「开始游戏」：无作弊正式开局。真相在 BeginGameFlowRunCommand。</summary>
        [AudioCue(
            "ui.main_menu.start",
            "主菜单开始游戏点击",
            "MainMenu",
            "GameFlowController.BeginFormalRun",
            AudioCueContexts.None)]
        private const string MainMenuStartCueId = "ui.main_menu.start";

        public void BeginFormalRun()
        {
            TriggerPulseHub.PulseAudio(AudioCueRequest.Simple(
                MainMenuStartCueId,
                "GameFlowController.BeginFormalRun"));
            SendBeginRun(GameFlowRunOptions.CreateFormal());
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

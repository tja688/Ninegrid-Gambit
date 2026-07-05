using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using AMPInternal.Coroutines.SFX;
using NineGrid.GameFlow;
using NineGrid.Battle;
using NineGrid.Battle.Combat;
using NineGrid.UI;
using NineGrid.Presentation.Visuals;
using NinegridGambit.Grapple;

#pragma warning disable CS0618 // FindObjectOfType is deprecated in Unity 6 but functional

namespace NineGrid.Audio
{
    /// <summary>
    /// Thin audio service: subscribes to game events, looks up cues in GameAudioCueSO,
    /// and plays via Audio Manager Pro (SFXManager / MusicManager / SFXLoopManager).
    /// Designed to be non-invasive: existing logic code is not modified.
    /// For methods without existing C# events, minimal event Actions are added to the source class.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameAudioService : MonoBehaviour
    {
        [Header("Config")]
        [Tooltip("The master audio cue database SO.")]
        [SerializeField] GameAudioCueSO cueDatabase;

        [Header("Debug")]
        [SerializeField] bool logMissingCues = false;

        [Header("Winch Chain Click")]
        [Tooltip("绞盘点击锁链音效的最小间隔（秒），限制最高播放频率。")]
        [SerializeField] float winchChainMinInterval = 0.08f;

        // Active loop tracking
        readonly Dictionary<AudioKey, AMPAudioSource> _activeLoops = new();
        readonly Dictionary<int, AudioKey> _loopKeyBySourceId = new();
        AudioKey? _currentBgmKey;
        AnchorChainLauncher.Phase _lastChainPhase = AnchorChainLauncher.Phase.Idle;
        bool _forgeAmbientPlaying = false;
        float _lastWinchChainCueTime = float.NegativeInfinity;
        bool _forgeAudioBound;
        HydraulicSceneController _boundHydraulic;
        HydraulicMaterialBoard _boundBoard;
        HydraulicMaterialLane _boundLane;
        ForgeStationController _boundForgeStation;

        // Persistent / scene-bound subscription tracking (MainScene 重载后必须重绑)
        GameFlowController _boundFlow;
        bool _persistentSubscribed;
        Coroutine _rebindRoutine;

        BattleController _boundBattle;
        AnchorRammingController _boundAnchorRam;
        WinchCrankController _boundWinch;
        AnchorChainLauncher _boundChain;
        AnchorHpTracker _boundAnchorHp;
        BoreManager _boundBores;
        ScreenShakeEffect _boundShake;
        CameraJuice _boundJuice;
        ProloguePerformance _boundPrologue;
        DialogueSystem _boundDialogue;
        NoticeSystem _boundNotice;
        EnemyIntroducePanelController _boundEnemyPanel;
        IslandController _boundIsland;
        RouteController _boundRoute;
        MainMenuController _boundMenu;
        OreInventoryPanel _boundOreInv;
        HoverNoticePresenter _boundHover;
        readonly List<SelectableSceneElement> _boundSelectables = new();

        /// <summary>Static access for other scripts to trigger audio cues by key.</summary>
        public static GameAudioService Instance { get; private set; }

        public GameAudioCueSO Database => cueDatabase;

        /// <summary>GameAudioService 认为当前应播放的 BGM 键（MusicManager 路由）。</summary>
        public AudioKey? CurrentBgmKey => _currentBgmKey;

        /// <summary>当前由 GameAudioService 管理的循环音。</summary>
        public IReadOnlyDictionary<AudioKey, AMPAudioSource> ActiveLoops => _activeLoops;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (GetComponent<GameAudioDebugPanel>() == null)
                gameObject.AddComponent<GameAudioDebugPanel>();
            if (GetComponent<GameAudioVolumeController>() == null)
                gameObject.AddComponent<GameAudioVolumeController>();
            if (GetComponent<GameAudioVolumeUi>() == null)
                gameObject.AddComponent<GameAudioVolumeUi>();
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_rebindRoutine != null)
            {
                StopCoroutine(_rebindRoutine);
                _rebindRoutine = null;
            }
        }

        IEnumerator Start()
        {
            // Wait one frame so singletons (GameFlowController, BattleController, etc.) are initialized
            yield return null;
            SubscribePersistent();
            SubscribeScene();
            StartEnvironmentAudio();
        }

        void OnDestroy()
        {
            UnsubscribeScene();
            UnsubscribePersistent();
            UnbindForgeAudio();
            Instance = null;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_rebindRoutine != null)
            {
                StopCoroutine(_rebindRoutine);
            }

            _rebindRoutine = StartCoroutine(RebindSceneAfterLoad());
        }

        IEnumerator RebindSceneAfterLoad()
        {
            UnsubscribeScene();
            UnbindForgeAudio();
            _lastChainPhase = AnchorChainLauncher.Phase.Idle;
            StopLoop(AudioKey.LoopChainTaut);

            // 等一帧，确保新场景 Awake/单例就绪（与 BattleController 重绑时机对齐）
            yield return null;
            SubscribePersistentUi();
            SubscribeScene();
            _rebindRoutine = null;
        }

        // ===== Subscription =====

        void SubscribePersistent()
        {
            if (_persistentSubscribed)
            {
                return;
            }

            SubscribeGameFlow();
            SubscribePersistentUi();
            _persistentSubscribed = true;
        }

        void UnsubscribePersistent()
        {
            if (!_persistentSubscribed)
            {
                return;
            }

            if (_boundFlow != null)
            {
                _boundFlow.StateChanged -= OnGameFlowStateChanged;
                _boundFlow.RunStarted -= OnRunStarted;
                _boundFlow.PlayerDefeated -= OnPlayerDefeated;
                _boundFlow.VictorySettled -= OnVictorySettled;
                _boundFlow = null;
            }

            UnbindDialogue();
            UnbindNotice();
            _persistentSubscribed = false;
        }

        void SubscribeScene()
        {
            SubscribeBattle();
            SubscribeForge();
            SubscribePrologue();
            SubscribeSceneUi();
            SubscribeSelection();
        }

        void UnsubscribeScene()
        {
            UnbindBattle();
            UnbindCombatActors();
            UnbindPrologue();
            UnbindSceneUi();
            UnbindSelection();
        }

        void SubscribeGameFlow()
        {
            var flow = GameFlowController.Instance;
            if (flow == null || flow == _boundFlow)
            {
                return;
            }

            _boundFlow = flow;
            _boundFlow.StateChanged += OnGameFlowStateChanged;
            _boundFlow.RunStarted += OnRunStarted;
            _boundFlow.PlayerDefeated += OnPlayerDefeated;
            _boundFlow.VictorySettled += OnVictorySettled;
        }

        void OnGameFlowStateChanged(NineGrid.GameFlow.GameFlowState prev, NineGrid.GameFlow.GameFlowState next)
        {
            PlayCue(AudioKey.SceneStateChanged);
            HandleStateChangeForBgm(prev, next);
        }

        void OnRunStarted() => PlayCue(AudioKey.RunStarted);
        void OnPlayerDefeated() => PlayCue(AudioKey.PlayerDefeated);
        void OnVictorySettled() => PlayCue(AudioKey.VictorySettled);

        void SubscribePersistentUi()
        {
            var ui = UiSystem.Instance;
            var dialogue = ui != null ? ui.Dialogue : FindFirstObjectByType<DialogueSystem>(FindObjectsInactive.Include);
            if (dialogue != null && dialogue != _boundDialogue)
            {
                UnbindDialogue();
                _boundDialogue = dialogue;
                _boundDialogue.DialogueOpened += OnDialogueOpened;
                _boundDialogue.LineStarted += OnDialogueLineStarted;
                _boundDialogue.DialogueClosed += OnDialogueClosed;
                _boundDialogue.SequenceCompleted += OnDialogueSequenceCompleted;
            }

            var notice = ui != null ? ui.Notice : FindFirstObjectByType<NoticeSystem>(FindObjectsInactive.Include);
            if (notice != null && notice != _boundNotice)
            {
                UnbindNotice();
                _boundNotice = notice;
                _boundNotice.NoticeShown += OnNoticeShown;
                _boundNotice.NoticeHidden += OnNoticeHidden;
            }
        }

        void UnbindDialogue()
        {
            if (_boundDialogue == null)
            {
                return;
            }

            _boundDialogue.DialogueOpened -= OnDialogueOpened;
            _boundDialogue.LineStarted -= OnDialogueLineStarted;
            _boundDialogue.DialogueClosed -= OnDialogueClosed;
            _boundDialogue.SequenceCompleted -= OnDialogueSequenceCompleted;
            _boundDialogue = null;
        }

        void UnbindNotice()
        {
            if (_boundNotice == null)
            {
                return;
            }

            _boundNotice.NoticeShown -= OnNoticeShown;
            _boundNotice.NoticeHidden -= OnNoticeHidden;
            _boundNotice = null;
        }

        void OnDialogueOpened() => PlayCue(AudioKey.DialogueOpen);
        void OnDialogueLineStarted(DialogueLine _) => PlayCue(AudioKey.DialogueTextTyping);
        void OnDialogueClosed() => PlayCue(AudioKey.DialogueClose);
        void OnDialogueSequenceCompleted() => PlayCue(AudioKey.DialogueSequenceComplete);
        void OnNoticeShown(NoticeMessage _) => PlayCue(AudioKey.NoticeShown);
        void OnNoticeHidden() => PlayCue(AudioKey.NoticeHidden);

        void SubscribeBattle()
        {
            var battle = BattleController.Instance;
            if (battle == null)
            {
                return;
            }

            if (battle != _boundBattle)
            {
                UnbindBattle();
                _boundBattle = battle;
                _boundBattle.ExitCompleted += OnBattleExitCompleted;
                _boundBattle.BattleFinished += OnBattleFinished;
                _boundBattle.DamageDealt += OnDamageDealt;
                _boundBattle.ForgeModeEntered += OnEnterForgeMode;
                _boundBattle.AnchorModeEntered += OnEnterAnchorMode;
                _boundBattle.RamSucceeded += OnRamSucceeded;
                _boundBattle.ForgeModeEntered += TryBindForgeAudio;
            }

            // BattleController 常驻，但撞击/绞盘等战斗组件随 MainScene 重载换新，必须每次重绑。
            BindCombatActors();
        }

        void UnbindBattle()
        {
            if (_boundBattle == null)
            {
                return;
            }

            _boundBattle.ExitCompleted -= OnBattleExitCompleted;
            _boundBattle.BattleFinished -= OnBattleFinished;
            _boundBattle.DamageDealt -= OnDamageDealt;
            _boundBattle.ForgeModeEntered -= OnEnterForgeMode;
            _boundBattle.AnchorModeEntered -= OnEnterAnchorMode;
            _boundBattle.RamSucceeded -= OnRamSucceeded;
            _boundBattle.ForgeModeEntered -= TryBindForgeAudio;
            _boundBattle = null;
        }

        void OnBattleExitCompleted() => PlayCue(AudioKey.BattleExit);
        void OnBattleFinished(bool won) => PlayCue(AudioKey.BattleFinished);
        void OnDamageDealt(int dmg) => PlayCue(AudioKey.RamSucceeded);
        void OnEnterForgeMode() => PlayCue(AudioKey.EnterForgeMode);
        void OnEnterAnchorMode() => PlayCue(AudioKey.EnterAnchorMode);
        void OnRamSucceeded() => PlayCue(AudioKey.RamSucceeded);

        void BindCombatActors()
        {
            UnbindCombatActors();

            var anchorRam = FindFirstObjectByType<AnchorRammingController>(FindObjectsInactive.Include);
            if (anchorRam != null)
            {
                _boundAnchorRam = anchorRam;
                _boundAnchorRam.onRamBegin.AddListener(OnRamBegin);
                _boundAnchorRam.onCrankBegin.AddListener(OnCrankBegin);
                _boundAnchorRam.onPreImpact.AddListener(OnPreImpact);
                _boundAnchorRam.onImpact.AddListener(OnImpact);
                _boundAnchorRam.onBounce.AddListener(OnBounce);
                _boundAnchorRam.onRamEnd.AddListener(OnRamEnd);
            }

            var winch = FindFirstObjectByType<WinchCrankController>(FindObjectsInactive.Include);
            if (winch != null)
            {
                _boundWinch = winch;
                _boundWinch.Clicked += OnWinchClicked;
                _boundWinch.CrankBegun += OnWinchBeginCrank;
                _boundWinch.CrankEnded += OnWinchEndCrank;
            }

            var chain = FindFirstObjectByType<AnchorChainLauncher>(FindObjectsInactive.Include);
            if (chain != null)
            {
                _boundChain = chain;
                _boundChain.ChainFired += OnChainFire;
                _boundChain.ChainReset += OnChainReset;
            }

            var anchorHp = FindFirstObjectByType<AnchorHpTracker>(FindObjectsInactive.Include);
            if (anchorHp != null)
            {
                _boundAnchorHp = anchorHp;
                _boundAnchorHp.AnchorReset += OnAnchorHpResetFull;
                _boundAnchorHp.AnchorConsumed += OnAnchorHpConsume;
                _boundAnchorHp.AnchorDepleted += OnAnchorHpEmpty;
            }

            var bores = FindFirstObjectByType<BoreManager>(FindObjectsInactive.Include);
            if (bores != null)
            {
                _boundBores = bores;
                _boundBores.BoreShown += OnBoreShow;
                _boundBores.BoresHidden += OnBoreHideAll;
            }

            var shake = FindFirstObjectByType<ScreenShakeEffect>(FindObjectsInactive.Include);
            if (shake != null)
            {
                _boundShake = shake;
                _boundShake.ShakePlayed += OnScreenShakePlay;
            }

            var juice = CameraJuice.Resolve();
            if (juice != null)
            {
                _boundJuice = juice;
                _boundJuice.ZoomBegun += OnCameraZoomBegin;
            }
        }

        void UnbindCombatActors()
        {
            if (_boundAnchorRam != null)
            {
                _boundAnchorRam.onRamBegin.RemoveListener(OnRamBegin);
                _boundAnchorRam.onCrankBegin.RemoveListener(OnCrankBegin);
                _boundAnchorRam.onPreImpact.RemoveListener(OnPreImpact);
                _boundAnchorRam.onImpact.RemoveListener(OnImpact);
                _boundAnchorRam.onBounce.RemoveListener(OnBounce);
                _boundAnchorRam.onRamEnd.RemoveListener(OnRamEnd);
                _boundAnchorRam = null;
            }

            if (_boundWinch != null)
            {
                _boundWinch.Clicked -= OnWinchClicked;
                _boundWinch.CrankBegun -= OnWinchBeginCrank;
                _boundWinch.CrankEnded -= OnWinchEndCrank;
                _boundWinch = null;
            }

            if (_boundChain != null)
            {
                _boundChain.ChainFired -= OnChainFire;
                _boundChain.ChainReset -= OnChainReset;
                _boundChain = null;
            }

            if (_boundAnchorHp != null)
            {
                _boundAnchorHp.AnchorReset -= OnAnchorHpResetFull;
                _boundAnchorHp.AnchorConsumed -= OnAnchorHpConsume;
                _boundAnchorHp.AnchorDepleted -= OnAnchorHpEmpty;
                _boundAnchorHp = null;
            }

            if (_boundBores != null)
            {
                _boundBores.BoreShown -= OnBoreShow;
                _boundBores.BoresHidden -= OnBoreHideAll;
                _boundBores = null;
            }

            if (_boundShake != null)
            {
                _boundShake.ShakePlayed -= OnScreenShakePlay;
                _boundShake = null;
            }

            if (_boundJuice != null)
            {
                _boundJuice.ZoomBegun -= OnCameraZoomBegin;
                _boundJuice = null;
            }
        }

        void OnRamBegin() => PlayCue(AudioKey.RamBegin);
        void OnCrankBegin() => PlayCue(AudioKey.CrankBegin);
        void OnPreImpact() => PlayCue(AudioKey.PreImpact);
        void OnImpact() => PlayCue(AudioKey.Impact);
        void OnBounce() => PlayCue(AudioKey.Bounce);
        void OnRamEnd() => PlayCue(AudioKey.RamEnd);

        void OnWinchClicked()
        {
            if (_boundWinch == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now - _lastWinchChainCueTime < winchChainMinInterval)
            {
                return;
            }

            _lastWinchChainCueTime = now;
            float intensity = _boundWinch.Intensity01;
            float vol = Mathf.Lerp(0.35f, 1f, intensity);
            float pitch = Mathf.Lerp(0.85f, 1.35f, intensity);
            PlayCue(AudioKey.WinchClicked, vol, pitch);
        }

        void OnWinchBeginCrank() => PlayCue(AudioKey.WinchBeginCrank);
        void OnWinchEndCrank() => PlayCue(AudioKey.WinchEndCrank);
        void OnChainFire() => PlayCue(AudioKey.ChainFire);
        void OnChainReset() => PlayCue(AudioKey.ChainReset);
        void OnAnchorHpResetFull() => PlayCue(AudioKey.AnchorHpResetFull);
        void OnAnchorHpConsume() => PlayCue(AudioKey.AnchorHpConsume);
        void OnAnchorHpEmpty() => PlayCue(AudioKey.AnchorHpEmpty);
        void OnBoreShow(int slot) => PlayCue(AudioKey.BoreShow);
        void OnBoreHideAll() => PlayCue(AudioKey.BoreHideAll);
        void OnScreenShakePlay() => PlayCue(AudioKey.ScreenShakePlay);
        void OnCameraZoomBegin() => PlayCue(AudioKey.CameraZoomBegin);

        void SubscribeForge()
        {
            TryBindForgeAudio();
        }

        void TryBindForgeAudio()
        {
            var hydraulic = HydraulicSceneController.Instance
                ?? FindFirstObjectByType<HydraulicSceneController>(FindObjectsInactive.Include);
            if (hydraulic == null)
            {
                return;
            }

            if (_forgeAudioBound && _boundHydraulic == hydraulic)
            {
                return;
            }

            UnbindForgeAudio();
            _boundHydraulic = hydraulic;

            hydraulic.EnterStarted += OnHydraulicEnterStarted;
            hydraulic.EnterCompleted += OnHydraulicEnterCompleted;
            hydraulic.ExitCompleted += OnHydraulicExitCompleted;
            hydraulic.ForgeCommitted += OnHydraulicForgeCommitted;
            hydraulic.HydraulicCompleted += OnHydraulicCompleted;
            hydraulic.HammerFallStarted += OnHydraulicHammerFallStarted;
            hydraulic.HammerImpacted += OnHydraulicHammerImpacted;
            hydraulic.HammerHoldStarted += OnHydraulicHammerHoldStarted;
            hydraulic.HammerRiseStarted += OnHydraulicHammerRiseStarted;

            var lane = hydraulic.MaterialLane
                ?? FindFirstObjectByType<HydraulicMaterialLane>(FindObjectsInactive.Include);
            if (lane != null)
            {
                _boundLane = lane;
                lane.Delivered += OnLaneDelivered;
                lane.DeliverLanded += OnLaneDeliverLanded;
                lane.LaneReset += OnLaneReset;
            }

            var board = hydraulic.MaterialBoard
                ?? FindFirstObjectByType<HydraulicMaterialBoard>(FindObjectsInactive.Include);
            if (board != null)
            {
                _boundBoard = board;
                board.DragBegun += OnBoardDragBegun;
                board.PlacedOnAnvil += OnBoardPlacedOnAnvil;
                board.AnvilRelayouted += OnBoardAnvilRelayouted;
                board.PlacedOnTable += OnBoardPlacedOnTable;
                board.DragCancelled += OnBoardDragCancelled;
                board.AnvilFullRejected += OnBoardAnvilFullRejected;
            }

            var forge = FindFirstObjectByType<ForgeStationController>(FindObjectsInactive.Include);
            if (forge != null)
            {
                _boundForgeStation = forge;
                forge.ForgeRequested += OnForgeRequested;
                forge.ForgeExited += OnForgeExited;
                forge.EmptyWarningShown += OnForgeEmptyWarningShown;
            }

            _forgeAudioBound = true;
        }

        void UnbindForgeAudio()
        {
            if (_boundHydraulic != null)
            {
                _boundHydraulic.EnterStarted -= OnHydraulicEnterStarted;
                _boundHydraulic.EnterCompleted -= OnHydraulicEnterCompleted;
                _boundHydraulic.ExitCompleted -= OnHydraulicExitCompleted;
                _boundHydraulic.ForgeCommitted -= OnHydraulicForgeCommitted;
                _boundHydraulic.HydraulicCompleted -= OnHydraulicCompleted;
                _boundHydraulic.HammerFallStarted -= OnHydraulicHammerFallStarted;
                _boundHydraulic.HammerImpacted -= OnHydraulicHammerImpacted;
                _boundHydraulic.HammerHoldStarted -= OnHydraulicHammerHoldStarted;
                _boundHydraulic.HammerRiseStarted -= OnHydraulicHammerRiseStarted;
            }

            if (_boundLane != null)
            {
                _boundLane.Delivered -= OnLaneDelivered;
                _boundLane.DeliverLanded -= OnLaneDeliverLanded;
                _boundLane.LaneReset -= OnLaneReset;
            }

            if (_boundBoard != null)
            {
                _boundBoard.DragBegun -= OnBoardDragBegun;
                _boundBoard.PlacedOnAnvil -= OnBoardPlacedOnAnvil;
                _boundBoard.AnvilRelayouted -= OnBoardAnvilRelayouted;
                _boundBoard.PlacedOnTable -= OnBoardPlacedOnTable;
                _boundBoard.DragCancelled -= OnBoardDragCancelled;
                _boundBoard.AnvilFullRejected -= OnBoardAnvilFullRejected;
            }

            if (_boundForgeStation != null)
            {
                _boundForgeStation.ForgeRequested -= OnForgeRequested;
                _boundForgeStation.ForgeExited -= OnForgeExited;
                _boundForgeStation.EmptyWarningShown -= OnForgeEmptyWarningShown;
            }

            _boundHydraulic = null;
            _boundLane = null;
            _boundBoard = null;
            _boundForgeStation = null;
            _forgeAudioBound = false;
        }

        void OnHydraulicEnterStarted()
        {
            PlayCue(AudioKey.HydraulicEnterStart);
            StartForgeAmbient();
        }

        void OnHydraulicEnterCompleted() => PlayCue(AudioKey.HydraulicEnterComplete);

        void OnHydraulicExitCompleted()
        {
            PlayCue(AudioKey.HydraulicExit);
            StopForgeAmbient();
        }

        void OnHydraulicForgeCommitted(bool[] _) => PlayCue(AudioKey.HydraulicPreForge);

        void OnHydraulicCompleted() => PlayCue(AudioKey.HydraulicCompleted);

        void OnHydraulicHammerFallStarted() => PlayCue(AudioKey.HydraulicHammerFall);

        void OnHydraulicHammerImpacted() => PlayCue(AudioKey.HydraulicHammerImpact, 1f, 0.92f);

        void OnHydraulicHammerHoldStarted() => PlayCue(AudioKey.HydraulicHammerHold);

        void OnHydraulicHammerRiseStarted() => PlayCue(AudioKey.HydraulicHammerRise);

        void OnLaneDelivered() => PlayCue(AudioKey.LaneDeliver);

        void OnLaneDeliverLanded() => PlayForgeMetalCue(AudioKey.LaneDeliverLand, 0.72f, 1.02f);

        void OnLaneReset() => PlayCue(AudioKey.LaneReset);

        void OnBoardDragBegun() => PlayForgeMetalCue(AudioKey.BoardBeginDrag, 0.58f, 1.08f);

        void OnBoardPlacedOnAnvil(CardInstance _, int __) =>
            PlayForgeMetalCue(AudioKey.BoardPlaceOnAnvil, 0.88f, 0.96f);

        void OnBoardAnvilRelayouted() => PlayForgeMetalCue(AudioKey.BoardRelayoutAnvil, 0.5f, 1.12f);

        void OnBoardPlacedOnTable() => PlayForgeMetalCue(AudioKey.BoardPlaceOnTable, 0.76f, 1f);

        void OnBoardDragCancelled() => PlayForgeMetalCue(AudioKey.BoardCancelDrag, 0.45f, 0.9f);

        void OnBoardAnvilFullRejected() => PlayForgeMetalCue(AudioKey.BoardAnvilFull, 0.95f, 0.82f);

        void OnForgeRequested() => PlayCue(AudioKey.ForgeTryForge);

        void OnForgeExited() => PlayCue(AudioKey.ForgeExit);

        void OnForgeEmptyWarningShown() => PlayCue(AudioKey.ForgeEmptyWarning);

        void PlayForgeMetalCue(AudioKey key, float volume, float pitch)
        {
            float jitter = UnityEngine.Random.Range(-0.05f, 0.05f);
            PlayCue(key, volume, pitch + jitter);
        }

        void SubscribePrologue()
        {
            var prologue = FindFirstObjectByType<ProloguePerformance>(FindObjectsInactive.Include);
            if (prologue == null || prologue == _boundPrologue)
            {
                return;
            }

            UnbindPrologue();
            _boundPrologue = prologue;
            _boundPrologue.SeaHoldStarted += OnPrologueSeaHold;
            _boundPrologue.PlayerSailedIn += OnProloguePlayerSailIn;
            _boundPrologue.EnemySailedIn += OnPrologueEnemySailIn;
            _boundPrologue.DialogueStarted += OnPrologueDialogueStart;
        }

        void UnbindPrologue()
        {
            if (_boundPrologue == null)
            {
                return;
            }

            _boundPrologue.SeaHoldStarted -= OnPrologueSeaHold;
            _boundPrologue.PlayerSailedIn -= OnProloguePlayerSailIn;
            _boundPrologue.EnemySailedIn -= OnPrologueEnemySailIn;
            _boundPrologue.DialogueStarted -= OnPrologueDialogueStart;
            _boundPrologue = null;
        }

        void OnPrologueSeaHold() => PlayCue(AudioKey.PrologueSeaHold);
        void OnProloguePlayerSailIn() => PlayCue(AudioKey.ProloguePlayerSailIn);
        void OnPrologueEnemySailIn() => PlayCue(AudioKey.PrologueEnemySailIn);
        void OnPrologueDialogueStart() => PlayCue(AudioKey.PrologueDialogueStart);

        void SubscribeSceneUi()
        {
            var ui = UiSystem.Instance;
            var enemyPanel = ui != null ? ui.EnemyInfo : FindFirstObjectByType<EnemyIntroducePanelController>(FindObjectsInactive.Include);
            if (enemyPanel != null && enemyPanel != _boundEnemyPanel)
            {
                UnbindEnemyPanel();
                _boundEnemyPanel = enemyPanel;
                _boundEnemyPanel.EnterCompleted += OnEnemyPanelShow;
                _boundEnemyPanel.ExitCompleted += OnEnemyPanelHide;
            }

            var island = FindFirstObjectByType<IslandController>(FindObjectsInactive.Include);
            if (island != null && island != _boundIsland)
            {
                UnbindIsland();
                _boundIsland = island;
                _boundIsland.RefineryOpened += OnIslandOpenRefinery;
                _boundIsland.ShipyardOpened += OnIslandOpenShipyard;
                _boundIsland.PanelSwitched += OnIslandPanelSwitch;
                _boundIsland.Left += OnIslandLeave;
            }

            var route = FindFirstObjectByType<RouteController>(FindObjectsInactive.Include);
            if (route != null && route != _boundRoute)
            {
                UnbindRoute();
                _boundRoute = route;
                _boundRoute.EventSelected += OnRouteSelect;
            }

            var menu = FindFirstObjectByType<MainMenuController>(FindObjectsInactive.Include);
            if (menu != null && menu != _boundMenu)
            {
                UnbindMenu();
                _boundMenu = menu;
                _boundMenu.GameStarted += OnMainMenuStart;
            }

            var inv = FindFirstObjectByType<OreInventoryPanel>(FindObjectsInactive.Include);
            if (inv != null && inv != _boundOreInv)
            {
                UnbindOreInventory();
                _boundOreInv = inv;
                _boundOreInv.Opened += OnOreInventoryOpen;
                _boundOreInv.Closed += OnOreInventoryClose;
            }

            var hoverNotice = FindFirstObjectByType<HoverNoticePresenter>(FindObjectsInactive.Include);
            if (hoverNotice != null && hoverNotice != _boundHover)
            {
                UnbindHoverNotice();
                _boundHover = hoverNotice;
                _boundHover.HoverNoticeShown += OnHoverNoticeShow;
            }
        }

        void UnbindSceneUi()
        {
            UnbindEnemyPanel();
            UnbindIsland();
            UnbindRoute();
            UnbindMenu();
            UnbindOreInventory();
            UnbindHoverNotice();
        }

        void UnbindEnemyPanel()
        {
            if (_boundEnemyPanel == null)
            {
                return;
            }

            _boundEnemyPanel.EnterCompleted -= OnEnemyPanelShow;
            _boundEnemyPanel.ExitCompleted -= OnEnemyPanelHide;
            _boundEnemyPanel = null;
        }

        void UnbindIsland()
        {
            if (_boundIsland == null)
            {
                return;
            }

            _boundIsland.RefineryOpened -= OnIslandOpenRefinery;
            _boundIsland.ShipyardOpened -= OnIslandOpenShipyard;
            _boundIsland.PanelSwitched -= OnIslandPanelSwitch;
            _boundIsland.Left -= OnIslandLeave;
            _boundIsland = null;
        }

        void UnbindRoute()
        {
            if (_boundRoute == null)
            {
                return;
            }

            _boundRoute.EventSelected -= OnRouteSelect;
            _boundRoute = null;
        }

        void UnbindMenu()
        {
            if (_boundMenu == null)
            {
                return;
            }

            _boundMenu.GameStarted -= OnMainMenuStart;
            _boundMenu = null;
        }

        void UnbindOreInventory()
        {
            if (_boundOreInv == null)
            {
                return;
            }

            _boundOreInv.Opened -= OnOreInventoryOpen;
            _boundOreInv.Closed -= OnOreInventoryClose;
            _boundOreInv = null;
        }

        void UnbindHoverNotice()
        {
            if (_boundHover == null)
            {
                return;
            }

            _boundHover.HoverNoticeShown -= OnHoverNoticeShow;
            _boundHover = null;
        }

        void OnEnemyPanelShow() => PlayCue(AudioKey.EnemyPanelShow);
        void OnEnemyPanelHide() => PlayCue(AudioKey.EnemyPanelHide);
        void OnIslandOpenRefinery() => PlayCue(AudioKey.IslandOpenRefinery);
        void OnIslandOpenShipyard() => PlayCue(AudioKey.IslandOpenShipyard);
        void OnIslandPanelSwitch() => PlayCue(AudioKey.IslandPanelSwitch);
        void OnIslandLeave() => PlayCue(AudioKey.IslandLeave);
        void OnRouteSelect() => PlayCue(AudioKey.RouteSelect);
        void OnMainMenuStart() => PlayCue(AudioKey.MainMenuStart);
        void OnOreInventoryOpen() => PlayCue(AudioKey.OreInventoryOpen);
        void OnOreInventoryClose() => PlayCue(AudioKey.OreInventoryClose);
        void OnHoverNoticeShow() => PlayCue(AudioKey.HoverNoticeShow);

        void SubscribeSelection()
        {
            var selectables = FindObjectsByType<SelectableSceneElement>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < selectables.Length; i++)
            {
                var sel = selectables[i];
                if (sel == null || _boundSelectables.Contains(sel))
                {
                    continue;
                }

                sel.HoverEntered += OnSelectableHoverEnter;
                sel.HoverExited += OnSelectableHoverExit;
                _boundSelectables.Add(sel);
            }
        }

        void UnbindSelection()
        {
            for (int i = 0; i < _boundSelectables.Count; i++)
            {
                var sel = _boundSelectables[i];
                if (sel == null)
                {
                    continue;
                }

                sel.HoverEntered -= OnSelectableHoverEnter;
                sel.HoverExited -= OnSelectableHoverExit;
            }

            _boundSelectables.Clear();
        }

        void OnSelectableHoverEnter() => PlayCue(AudioKey.SelectableHoverEnter);
        void OnSelectableHoverExit() => PlayCue(AudioKey.SelectableHoverExit);

        // ===== Environment Audio =====

        void StartEnvironmentAudio()
        {
            var flow = GameFlowController.Instance;
            if (flow != null)
                HandleStateChangeForBgm(flow.PreviousState, flow.CurrentState);
        }

        void HandleStateChangeForBgm(NineGrid.GameFlow.GameFlowState prev, NineGrid.GameFlow.GameFlowState next)
        {
            StopSceneMusicAndLoops();

            if (next == NineGrid.GameFlow.GameFlowState.BossBattle)
            {
                PlayCue(AudioKey.BgmBoss);
                return;
            }

            if (NineGrid.GameFlow.GameFlowScenes.IsBattleState(next))
            {
                PlayCue(AudioKey.BgmBattle);
                return;
            }

            if (NineGrid.GameFlow.GameFlowScenes.IsIslandState(next))
            {
                StartLoop(AudioKey.BgmIsland);
                return;
            }

            if (NineGrid.GameFlow.GameFlowScenes.IsTransitionalState(next))
            {
                PlayCue(AudioKey.BgmRoute);
                return;
            }

            if (next == NineGrid.GameFlow.GameFlowState.MainMenu)
                PlayCue(AudioKey.BgmMainMenu);
        }

        void StopSceneMusicAndLoops()
        {
            if (MusicManager.Main != null && MusicManager.Main.IsPlaying())
                MusicManager.Main.Stop(0.5f);

            _currentBgmKey = null;
            StopLoop(AudioKey.AmbientBattle);
            StopLoop(AudioKey.AmbientOcean);
            StopLoop(AudioKey.BgmIsland);
        }

        void StartForgeAmbient()
        {
            if (_forgeAmbientPlaying) return;
            _forgeAmbientPlaying = true;
            StartLoop(AudioKey.AmbientForge);
        }

        void StopForgeAmbient()
        {
            if (!_forgeAmbientPlaying) return;
            _forgeAmbientPlaying = false;
            StopLoop(AudioKey.AmbientForge);
        }

        // ===== Chain phase polling (for chain flying/tightening/attached/taut sounds) =====

        void Update()
        {
            UpdateChainPhase();
        }

        void UpdateChainPhase()
        {
            var chain = _boundChain != null
                ? _boundChain
                : FindFirstObjectByType<AnchorChainLauncher>(FindObjectsInactive.Include);
            if (chain == null)
            {
                return;
            }

            var phase = chain.CurrentPhase;
            if (phase == _lastChainPhase) return;

            switch (phase)
            {
                case AnchorChainLauncher.Phase.Flying:
                    PlayCue(AudioKey.ChainFlying);
                    break;
                case AnchorChainLauncher.Phase.Settling:
                    PlayCue(AudioKey.ChainSettling);
                    break;
                case AnchorChainLauncher.Phase.Tightening:
                    PlayCue(AudioKey.ChainTightening);
                    StartLoop(AudioKey.LoopChainTaut);
                    break;
                case AnchorChainLauncher.Phase.Attached:
                    PlayCue(AudioKey.ChainAttached);
                    break;
                case AnchorChainLauncher.Phase.Idle:
                    StopLoop(AudioKey.LoopChainTaut);
                    break;
            }
            _lastChainPhase = phase;
        }

        // ===== Play API =====

        /// <summary>Play a cue by key with default volume/pitch from the SO.</summary>
        public void PlayCue(AudioKey key)
        {
            PlayCue(key, null, null);
        }

        /// <summary>Play a cue by key with optional dynamic volume/pitch override.</summary>
        public void PlayCue(AudioKey key, float? dynamicVolume, float? dynamicPitch)
        {
            if (cueDatabase == null)
            {
                if (logMissingCues) Debug.LogWarning($"[Audio] No cue database assigned on {gameObject.name}");
                return;
            }

            if (!cueDatabase.TryGetCue(key, out var entry))
            {
                if (logMissingCues) Debug.LogWarning($"[Audio] No cue entry found for key {key}");
                return;
            }

            if (!entry.enabled) return;

            // Route to the appropriate player
            if (entry.musicTrack != null)
            {
                PlayMusic(key, entry);
            }
            else if (entry.loopMode != LoopMode.None || entry.loopClip != null)
            {
                StartLoop(key, dynamicVolume);
            }
            else if (entry.sfxObject != null)
            {
                PlaySFX(entry, dynamicVolume, dynamicPitch);
            }
            else
            {
                // No audio reference assigned yet - this is expected during pre-production
                if (logMissingCues) Debug.Log($"[Audio] Cue {key} ({entry.displayName}) has no audio reference assigned yet.");
            }
        }

        void PlaySFX(AudioCueEntry entry, float? dynVol, float? dynPitch)
        {
            if (SFXManager.Main == null) return;

            float vol = entry.overrideVolume ? entry.volume : 1f;
            float pitch = entry.overridePitch ? entry.pitch : 1f;

            if (dynVol.HasValue) vol = dynVol.Value;
            if (dynPitch.HasValue) pitch = dynPitch.Value;

            SFXManager.Main.Play(entry.sfxObject, 0f, vol, pitch);
        }

        void PlayMusic(AudioKey key, AudioCueEntry entry)
        {
            if (MusicManager.Main == null) return;
            _currentBgmKey = key;
            MusicManager.Main.Play(entry.musicTrack, 0f, 0.5f);
        }

        // ===== Loop Management =====

        void StartLoop(AudioKey key, float? dynVol = null)
        {
            if (cueDatabase == null || !cueDatabase.TryGetCue(key, out var entry)) return;
            if (!entry.enabled) return;

            // Stop existing loop if any
            StopLoop(key);

            AudioClip clip = entry.loopClip;
            if (clip == null)
            {
                // Try to extract clip from SFXObject's first layer
                if (entry.sfxObject != null && entry.sfxObject.SFXLayers != null && entry.sfxObject.SFXLayers.Length > 0)
                {
                    clip = entry.sfxObject.SFXLayers[0].SFX;
                }
            }
            if (clip == null) return;

            if (SFXLoopManager.Main == null) return;

            float vol = entry.overrideVolume ? entry.volume : 1f;
            if (dynVol.HasValue) vol = dynVol.Value;

            var source = SFXLoopManager.Main.Play(clip, vol);
            if (source != null)
            {
                _activeLoops[key] = source;
                _loopKeyBySourceId[source.GetInstanceID()] = key;

                // Auto-stop after duration if configured
                if (entry.loopMode == LoopMode.LoopWithDuration && entry.loopDuration > 0f)
                {
                    StartCoroutine(StopLoopAfterDelay(key, entry.loopDuration));
                }
            }
        }

        void StopLoop(AudioKey key)
        {
            if (_activeLoops.TryGetValue(key, out var source))
            {
                if (source != null)
                {
                    _loopKeyBySourceId.Remove(source.GetInstanceID());
                    if (SFXLoopManager.Main != null)
                    {
                        SFXLoopManager.Main.Stop(source, 0.3f);
                    }
                }
                _activeLoops.Remove(key);
            }
        }

        IEnumerator StopLoopAfterDelay(AudioKey key, float delay)
        {
            yield return new WaitForSeconds(delay);
            StopLoop(key);
        }

        // ===== Public utility for external callers =====

        /// <summary>Stop all currently playing loops.</summary>
        public void StopAllLoops()
        {
            var keys = new List<AudioKey>(_activeLoops.Keys);
            foreach (var key in keys)
            {
                StopLoop(key);
            }
        }

        /// <summary>调试面板：显示名。</summary>
        public string GetCueDisplayName(AudioKey key)
        {
            if (cueDatabase != null && cueDatabase.TryGetCue(key, out var entry) && entry != null)
                return entry.displayName;
            return key.ToString();
        }

        /// <summary>调试面板：尝试把正在播放的 AudioSource 映射回 AudioKey。</summary>
        public bool TryResolvePlayingSource(
            AudioSource source,
            GameAudioMonitor.AudioChannel channel,
            out AudioKey? key,
            out string displayName)
        {
            key = null;
            displayName = null;
            if (source == null || cueDatabase == null) return false;

            if (channel == GameAudioMonitor.AudioChannel.Loop
                && _loopKeyBySourceId.TryGetValue(source.GetInstanceID(), out var loopKey))
            {
                key = loopKey;
                displayName = GetCueDisplayName(loopKey);
                return true;
            }

            if (channel == GameAudioMonitor.AudioChannel.Music && _currentBgmKey.HasValue)
            {
                key = _currentBgmKey;
                displayName = GetCueDisplayName(_currentBgmKey.Value);
                return true;
            }

            if (source.clip != null && TryResolveCueByClip(source.clip, out var clipKey, out var clipName))
            {
                key = clipKey;
                displayName = clipName;
                return true;
            }

            return false;
        }

        bool TryResolveCueByClip(AudioClip clip, out AudioKey key, out string displayName)
        {
            key = default;
            displayName = null;
            if (clip == null || cueDatabase?.cues == null) return false;

            for (int i = 0; i < cueDatabase.cues.Length; i++)
            {
                var entry = cueDatabase.cues[i];
                if (entry == null || !entry.enabled) continue;

                if (entry.loopClip == clip)
                {
                    key = entry.key;
                    displayName = entry.displayName;
                    return true;
                }

                if (entry.musicTrack != null && TrackUsesClip(entry.musicTrack, clip))
                {
                    key = entry.key;
                    displayName = entry.displayName;
                    return true;
                }

                if (entry.sfxObject != null && entry.sfxObject.SFXLayers != null)
                {
                    for (int j = 0; j < entry.sfxObject.SFXLayers.Length; j++)
                    {
                        if (entry.sfxObject.SFXLayers[j].SFX == clip)
                        {
                            key = entry.key;
                            displayName = entry.displayName;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        static bool TrackUsesClip(Track track, AudioClip clip)
        {
            if (track == null || clip == null) return false;
            if (track.Intro == clip) return true;
            if (track.Editions == null) return false;
            for (int i = 0; i < track.Editions.Length; i++)
            {
                var edition = track.Editions[i];
                if (edition.Soundtrack == clip || edition.TransitionSound == clip)
                    return true;
            }
            return false;
        }
    }
}

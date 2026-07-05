using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AMPInternal.Coroutines.SFX;
using NineGrid.GameFlow;
using NineGrid.Battle;
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

        // Active loop tracking
        readonly Dictionary<AudioKey, AMPAudioSource> _activeLoops = new();
        AnchorChainLauncher.Phase _lastChainPhase = AnchorChainLauncher.Phase.Idle;
        bool _forgeAmbientPlaying = false;

        /// <summary>Static access for other scripts to trigger audio cues by key.</summary>
        public static GameAudioService Instance { get; private set; }

        public GameAudioCueSO Database => cueDatabase;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        IEnumerator Start()
        {
            // Wait one frame so singletons (GameFlowController, BattleController, etc.) are initialized
            yield return null;
            SubscribeAll();
            StartEnvironmentAudio();
        }

        void OnDestroy()
        {
            Instance = null;
        }

        // ===== Subscription =====

        void SubscribeAll()
        {
            SubscribeGameFlow();
            SubscribeBattle();
            SubscribeForge();
            SubscribePrologue();
            SubscribeUI();
            SubscribeSelection();
        }

        void SubscribeGameFlow()
        {
            var flow = GameFlowController.Instance;
            if (flow != null)
            {
                flow.StateChanged += (prev, next) =>
                {
                    PlayCue(AudioKey.SceneStateChanged);
                    HandleStateChangeForBgm(prev, next);
                };

                // StartNewRun, NotifyPlayerDefeated, NotifyVictorySettled
                // These will use the minimal event Actions added to GameFlowController
                flow.RunStarted += () => PlayCue(AudioKey.RunStarted);
                flow.PlayerDefeated += () => PlayCue(AudioKey.PlayerDefeated);
                flow.VictorySettled += () => PlayCue(AudioKey.VictorySettled);
            }

            // SceneFlowDirector transitions - subscribe via GameFlowController.StateChanged (already done above)
            // Transition fade out/in are tied to state changes
        }

        void SubscribeBattle()
        {
            var battle = BattleController.Instance;
            if (battle != null)
            {
                battle.EnterCompleted += () => PlayCue(AudioKey.BattleEnter);
                battle.ExitCompleted += () => PlayCue(AudioKey.BattleExit);
                battle.BattleFinished += won => PlayCue(AudioKey.BattleFinished);
                battle.DamageDealt += dmg => PlayCue(AudioKey.RamSucceeded);

                // Events added to BattleController
                battle.ForgeModeEntered += () => PlayCue(AudioKey.EnterForgeMode);
                battle.AnchorModeEntered += () => PlayCue(AudioKey.EnterAnchorMode);
                battle.RamSucceeded += () => PlayCue(AudioKey.RamSucceeded);
            }

            // AnchorRammingController - 6 UnityEvents + Completed
            var anchorRam = FindObjectOfType<AnchorRammingController>();
            if (anchorRam != null)
            {
                anchorRam.onRamBegin.AddListener(() => PlayCue(AudioKey.RamBegin));
                anchorRam.onCrankBegin.AddListener(() => PlayCue(AudioKey.CrankBegin));
                anchorRam.onPreImpact.AddListener(() => PlayCue(AudioKey.PreImpact));
                anchorRam.onImpact.AddListener(() => PlayCue(AudioKey.Impact));
                anchorRam.onBounce.AddListener(() => PlayCue(AudioKey.Bounce));
                anchorRam.onRamEnd.AddListener(() => PlayCue(AudioKey.RamEnd));
            }

            // WinchCrankController - dynamic pitch/volume based on Intensity01
            var winch = FindObjectOfType<WinchCrankController>();
            if (winch != null)
            {
                winch.Clicked += () =>
                {
                    float intensity = winch.Intensity01;
                    float vol = Mathf.Lerp(0.3f, 1f, intensity);
                    float pitch = Mathf.Lerp(0.8f, 1.5f, intensity);
                    PlayCue(AudioKey.WinchClicked, vol, pitch);
                };

                // Events added to WinchCrankController
                winch.CrankBegun += () => PlayCue(AudioKey.WinchBeginCrank);
                winch.CrankEnded += () => PlayCue(AudioKey.WinchEndCrank);
            }

            // AnchorChainLauncher - phase polling for chain sounds
            // Fire() event added to AnchorChainLauncher
            var chain = FindObjectOfType<AnchorChainLauncher>();
            if (chain != null)
            {
                chain.ChainFired += () => PlayCue(AudioKey.ChainFire);
                chain.ChainReset += () => PlayCue(AudioKey.ChainReset);
            }

            // AnchorHpTracker
            var anchorHp = FindObjectOfType<AnchorHpTracker>();
            if (anchorHp != null)
            {
                anchorHp.AnchorReset += () => PlayCue(AudioKey.AnchorHpResetFull);
                anchorHp.AnchorConsumed += () => PlayCue(AudioKey.AnchorHpConsume);
                anchorHp.AnchorDepleted += () => PlayCue(AudioKey.AnchorHpEmpty);
            }

            // BoreManager
            var bores = FindObjectOfType<BoreManager>();
            if (bores != null)
            {
                bores.BoreShown += slot => PlayCue(AudioKey.BoreShow);
                bores.BoresHidden += () => PlayCue(AudioKey.BoreHideAll);
            }

            // ScreenShakeEffect
            var shake = FindObjectOfType<ScreenShakeEffect>();
            if (shake != null)
            {
                shake.ShakePlayed += () => PlayCue(AudioKey.ScreenShakePlay);
            }

            // CameraJuice
            var juice = CameraJuice.Resolve();
            if (juice != null)
            {
                juice.ZoomBegun += () => PlayCue(AudioKey.CameraZoomBegin);
            }
        }

        void SubscribeForge()
        {
            var hydraulic = HydraulicSceneController.Instance;
            if (hydraulic != null)
            {
                hydraulic.EnterStarted += () =>
                {
                    PlayCue(AudioKey.HydraulicEnterStart);
                    StartForgeAmbient();
                };
                hydraulic.EnterCompleted += () => PlayCue(AudioKey.HydraulicEnterComplete);
                hydraulic.ExitCompleted += () =>
                {
                    PlayCue(AudioKey.HydraulicExit);
                    StopForgeAmbient();
                };
                hydraulic.ForgeCommitted += _ => PlayCue(AudioKey.HydraulicPreForge);
                hydraulic.HydraulicCompleted += () => PlayCue(AudioKey.HydraulicCompleted);
                hydraulic.HammerFallStarted += () => PlayCue(AudioKey.HydraulicHammerFall);
                hydraulic.HammerImpacted += () => PlayCue(AudioKey.HydraulicHammerImpact);
                hydraulic.HammerHoldStarted += () => PlayCue(AudioKey.HydraulicHammerHold);
                hydraulic.HammerRiseStarted += () => PlayCue(AudioKey.HydraulicHammerRise);
            }

            // HydraulicMaterialLane
            var lane = FindObjectOfType<HydraulicMaterialLane>();
            if (lane != null)
            {
                lane.Delivered += () => PlayCue(AudioKey.LaneDeliver);
                lane.DeliverLanded += () => PlayCue(AudioKey.LaneDeliverLand);
                lane.LaneReset += () => PlayCue(AudioKey.LaneReset);
            }

            // HydraulicMaterialBoard
            var board = FindObjectOfType<HydraulicMaterialBoard>();
            if (board != null)
            {
                board.DragBegun += () => PlayCue(AudioKey.BoardBeginDrag);
                board.PlacedOnAnvil += () => PlayCue(AudioKey.BoardPlaceOnAnvil);
                board.AnvilRelayouted += () => PlayCue(AudioKey.BoardRelayoutAnvil);
                board.PlacedOnTable += () => PlayCue(AudioKey.BoardPlaceOnTable);
                board.DragCancelled += () => PlayCue(AudioKey.BoardCancelDrag);
                board.AnvilFullRejected += () => PlayCue(AudioKey.BoardAnvilFull);
            }

            // ForgeStationController
            var forge = FindObjectOfType<ForgeStationController>();
            if (forge != null)
            {
                forge.ForgeRequested += () => PlayCue(AudioKey.ForgeTryForge);
                forge.ForgeExited += () => PlayCue(AudioKey.ForgeExit);
                forge.EmptyWarningShown += () => PlayCue(AudioKey.ForgeEmptyWarning);
            }
        }

        void SubscribePrologue()
        {
            var prologue = FindObjectOfType<ProloguePerformance>();
            if (prologue != null)
            {
                prologue.SeaHoldStarted += () => PlayCue(AudioKey.PrologueSeaHold);
                prologue.PlayerSailedIn += () => PlayCue(AudioKey.ProloguePlayerSailIn);
                prologue.EnemySailedIn += () => PlayCue(AudioKey.PrologueEnemySailIn);
                prologue.DialogueStarted += () => PlayCue(AudioKey.PrologueDialogueStart);
            }

            // DialogueSystem
            var dialogue = FindObjectOfType<DialogueSystem>();
            if (dialogue != null)
            {
                dialogue.DialogueOpened += () => PlayCue(AudioKey.DialogueOpen);
                dialogue.LineStarted += _ => PlayCue(AudioKey.DialogueTextTyping);
                dialogue.DialogueClosed += () => PlayCue(AudioKey.DialogueClose);
                dialogue.SequenceCompleted += () => PlayCue(AudioKey.DialogueSequenceComplete);
            }

            // NoticeSystem
            var notice = FindObjectOfType<NoticeSystem>();
            if (notice != null)
            {
                notice.NoticeShown += _ => PlayCue(AudioKey.NoticeShown);
                notice.NoticeHidden += () => PlayCue(AudioKey.NoticeHidden);
            }
        }

        void SubscribeUI()
        {
            // EnemyIntroducePanelController
            var enemyPanel = FindObjectOfType<EnemyIntroducePanelController>();
            if (enemyPanel != null)
            {
                enemyPanel.EnterCompleted += () => PlayCue(AudioKey.EnemyPanelShow);
                enemyPanel.ExitCompleted += () => PlayCue(AudioKey.EnemyPanelHide);
            }

            // IslandController
            var island = FindObjectOfType<IslandController>();
            if (island != null)
            {
                island.RefineryOpened += () => PlayCue(AudioKey.IslandOpenRefinery);
                island.ShipyardOpened += () => PlayCue(AudioKey.IslandOpenShipyard);
                island.PanelSwitched += () => PlayCue(AudioKey.IslandPanelSwitch);
                island.Left += () => PlayCue(AudioKey.IslandLeave);
            }

            // RouteController
            var route = FindObjectOfType<RouteController>();
            if (route != null)
            {
                route.EventSelected += () => PlayCue(AudioKey.RouteSelect);
            }

            // MainMenuController
            var menu = FindObjectOfType<MainMenuController>();
            if (menu != null)
            {
                menu.GameStarted += () => PlayCue(AudioKey.MainMenuStart);
            }

            // OreInventoryPanel
            var inv = FindObjectOfType<OreInventoryPanel>();
            if (inv != null)
            {
                inv.Opened += () => PlayCue(AudioKey.OreInventoryOpen);
                inv.Closed += () => PlayCue(AudioKey.OreInventoryClose);
            }

            // HoverNoticePresenter - search by type name since it may not be in a known namespace
            var hoverNotice = FindObjectOfType<HoverNoticePresenter>();
            if (hoverNotice != null)
            {
                hoverNotice.HoverNoticeShown += () => PlayCue(AudioKey.HoverNoticeShow);
            }
        }

        void SubscribeSelection()
        {
            // SelectableSceneElement - multiple instances exist, subscribe to each
            var selectables = FindObjectsOfType<SelectableSceneElement>();
            foreach (var sel in selectables)
            {
                sel.HoverEntered += () => PlayCue(AudioKey.SelectableHoverEnter);
                sel.HoverExited += () => PlayCue(AudioKey.SelectableHoverExit);
            }
        }

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
            var chain = FindObjectOfType<AnchorChainLauncher>();
            if (chain == null) return;

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
                PlayMusic(entry);
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

        void PlayMusic(AudioCueEntry entry)
        {
            if (MusicManager.Main == null) return;
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
                if (source != null && SFXLoopManager.Main != null)
                {
                    SFXLoopManager.Main.Stop(source, 0.3f);
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
    }
}

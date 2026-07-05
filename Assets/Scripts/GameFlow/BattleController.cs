using System;
using System.Collections;
using DG.Tweening;
using NineGrid.Battle;
using NineGrid.Battle.Combat;
using NineGrid.Data;
using NineGrid.Presentation.Visuals;
using NineGrid.UI;
using NinegridGambit.Grapple;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NineGrid.GameFlow
{
    public enum BattlePhaseState
    {
        Idle = 0,
        Entering = 1,
        Active = 2,
        Exiting = 3,
    }

    /// <summary>
    /// 战斗环节独立状态机：由 <see cref="GameFlowController"/> 在序章/战斗节点就绪后拉起。
    /// 负责权限解锁、敌人信息面板、锻造 / 抛锚入口；胜负结算留位。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleController : MonoBehaviour
    {
        public static BattleController Instance { get; private set; }

        [Header("Ships")]
        [SerializeField] SelectableSceneElement playerSelectable;
        [SerializeField] SelectableSceneElement enemySelectable;
        [SerializeField] Transform player;
        [SerializeField] Transform playerExit;

        [Header("Systems")]
        [SerializeField] SceneElementPointerSelector pointerSelector;
        [SerializeField] HydraulicSceneController hydraulicScene;
        [FormerlySerializedAs("enemyIntroducePanel")]
        [SerializeField] EnemyIntroducePanelController enemyInfoPanel;
        [SerializeField] AnchorChainLauncher anchorChain;
        [SerializeField] AnchorRammingController anchorRam;
        [SerializeField] BoreManager bores;
        [SerializeField] AnchorHpTracker anchorHp;
        [SerializeField] HullModSlotsController hullModSlots;

        [Header("Flow (占位数值)")]
        [Tooltip("船锚余量组件缺失时的兜底撞击次数：撞满该次数即算打完本场。")]
        [SerializeField] int fallbackRamsToWin = 3;
        [Tooltip("未铸造钻头就点敌人时的提示文案。")]
        [TextArea(1, 3)]
        [SerializeField] string forgeFirstNotice = "先点玩家船体铸造钻头，再抛锚撞击。";

        [Header("Combat (真实战斗)")]
        [Tooltip("矿石目录 SO（留空时编辑器自动从 Assets/ScriptableObjects/Data/OreCatalog.asset 加载）。")]
        [SerializeField] OreCatalog oreCatalog;
        [Tooltip("敌舰目录 SO（留空时编辑器自动加载）。")]
        [SerializeField] EnemyShipCatalog enemyCatalog;
        [Tooltip("船体改造目录 SO（留空时编辑器自动加载）。")]
        [SerializeField] HullModCatalog hullModCatalog;
        [Tooltip("本场敌舰数据 SO（留空时按 GameFlowState 对应节点自动选择）。")]
        [SerializeField] EnemyShipDataSO currentEnemy;

        [Header("Exit Motion")]
        [SerializeField] float playerExitDuration = 1f;
        [SerializeField] Ease playerExitEase = Ease.InCubic;

        [Header("Debug Input")]
        [SerializeField] bool enableDebugHotkeys = true;
        [SerializeField] KeyCode endBattleKey = KeyCode.Keypad4;
        [SerializeField] KeyCode anchorRamKey = KeyCode.Keypad3;

        Coroutine _routine;
        Coroutine _anchorRoutine;
        Tween _playerExitTween;
        BattlePhaseState _state = BattlePhaseState.Idle;
        bool _hydraulicEventsBound;
        HydraulicSceneController _boundHydraulicScene;
        bool _ramming;
        bool _hasForgedBore;
        bool _anchorWarned;
        int _ramsCompleted;
        bool _won;
        CombatModel _combat;
        bool _enemyDescVisible;
        readonly bool[] _pendingBoreOccupancy = new bool[BoreManager.BoreCount];

        public BattlePhaseState State => _state;
        public bool IsBusy =>
            _state == BattlePhaseState.Entering
            || _state == BattlePhaseState.Exiting;
        public bool IsActive => _state == BattlePhaseState.Active;

        /// <summary>战斗中可点击玩家进入锻造。</summary>
        public bool CanEnterForgeMode { get; private set; }

        /// <summary>战斗中可点击敌人触发抛锚准备（子状态机尚未落地）。</summary>
        public bool CanEnterAnchorMode { get; private set; }

        /// <summary>战斗模型（牌库/伤害/血量）。战斗开始后非空。</summary>
        public CombatModel Combat => _combat;

        /// <summary>敌舰血量变化（current, max）。供未来血条 UI 订阅。</summary>
        public event Action<int, int> EnemyHpChanged;

        /// <summary>造成伤害时触发（damage）。</summary>
        public event Action<int> DamageDealt;

        /// <summary>战斗入场完成（权限已开，敌人信息面板入场结束，进入 Active）。</summary>
        public event Action EnterCompleted;

        /// <summary>战斗退场完成。暂不推进主流程下一节点。</summary>
        public event Action ExitCompleted;

        /// <summary>战斗结束（含胜负标记）。SceneFlowDirector 据此推进主流程下一节点。</summary>
        public event Action<bool> BattleFinished;
        public event Action ForgeModeEntered;
        public event Action AnchorModeEntered;
        public event Action RamSucceeded;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Battle] 场景中存在多个实例，保留先创建的。");
                return;
            }

            Instance = this;
            ResolveRefs();
            BindHydraulicEvents(forceRebind: true);
            LockPermissions();
            anchorHp?.SetVisible(false);
            hullModSlots?.SetVisible(false);
            enemyInfoPanel?.HideImmediate();
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnDestroy()
        {
            UnbindHydraulicEvents();

            if (Instance == this)
            {
                Instance = null;
            }

            KillMotion();
        }

        /// <summary>
        /// GameFlow 常驻、MainScene 重载时液压场景是新实例；旧订阅会失效，需在此重绑。
        /// </summary>
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!string.Equals(scene.name, GameFlowScenes.Main, StringComparison.Ordinal))
            {
                return;
            }

            ResolveRefs();
            BindHydraulicEvents(forceRebind: true);
        }

        void Update()
        {
            // 小键盘挂在始终激活的 GameFlow 上。
            // 液压宿主默认可失活，不能依赖它自己的 Update。
            if (enableDebugHotkeys)
            {
                HandleDebugHotkeys();
            }

            if (_state == BattlePhaseState.Active)
            {
                UpdateActiveCombat();
            }
        }

        void HandleDebugHotkeys()
        {
            // Keypad1：液压入场 / 常规退场（宿主失活时由这里拉起）
            if (DebugHotkeyInput.WasPressedThisFrame(KeyCode.Keypad1))
            {
                ResolveRefs();
                if (hydraulicScene == null)
                {
                    Debug.LogWarning("[Battle] 找不到液压场景，无法响应小键盘1。");
                }
                else if (hydraulicScene.State == HydraulicSceneState.Hidden)
                {
                    hydraulicScene.Enter();
                }
                else if (hydraulicScene.State == HydraulicSceneState.Active)
                {
                    hydraulicScene.Exit();
                }
            }

            // Keypad2：液压完成退场
            if (DebugHotkeyInput.WasPressedThisFrame(KeyCode.Keypad2))
            {
                ResolveRefs();
                if (hydraulicScene == null)
                {
                    Debug.LogWarning("[Battle] 找不到液压场景，无法响应小键盘2。");
                }
                else
                {
                    hydraulicScene.PlayHydraulic();
                }
            }

            // Keypad5：沿管道倾倒一份材料到桌面
            if (DebugHotkeyInput.WasPressedThisFrame(KeyCode.Keypad5))
            {
                ResolveRefs();
                if (hydraulicScene == null)
                {
                    Debug.LogWarning("[Battle] 找不到液压场景，无法响应小键盘5。");
                }
                else
                {
                    hydraulicScene.TryDeliverMaterial();
                }
            }

            // Keypad3：抛锚互撞（仅 Active，等价于点击敌人）
            if (_state == BattlePhaseState.Active && !_ramming
                && DebugHotkeyInput.WasPressedThisFrame(anchorRamKey))
            {
                TryEnterAnchorMode();
            }

            // Keypad4：结束战斗（仅 Active）
            if (_state == BattlePhaseState.Active && DebugHotkeyInput.WasPressedThisFrame(endBattleKey))
            {
                ExitBattle();
            }
        }

        /// <summary>主流程钩子：进入战斗环节。</summary>
        public bool EnterBattle()
        {
            if (_state != BattlePhaseState.Idle)
            {
                Debug.LogWarning($"[Battle] EnterBattle 被忽略，当前状态={_state}");
                return false;
            }

            ResolveRefs();
            BindHydraulicEvents(forceRebind: true);
            KillMotion();
            ResetBattleState();
            _state = BattlePhaseState.Entering;
            _routine = StartCoroutine(EnterRoutine());
            return true;
        }

        /// <summary>重置本场战斗的流程状态：钻头、船锚余量、锻造材料、撞击计数、战斗模型。</summary>
        void ResetBattleState()
        {
            _hasForgedBore = false;
            _anchorWarned = false;
            _ramsCompleted = 0;
            _won = false;

            // 确保 RunData 存在（首次战斗或新 run 时初始化）
            var run = RunData.Ensure();
            if (run.Deck.Count == 0)
            {
                RunData.StartNewRun();
                run = RunData.Ensure();
            }

            // 初始化战斗模型（牌库 / 敌我血量 / 伤害公式）
            ResolveOreCatalog();
            ResolveEnemyCatalog();
            ResolveHullModCatalog();
            ResolveCurrentEnemy();

            _combat = new CombatModel();
            var playerHp = anchorHp != null && anchorHp.Capacity > 0
                ? anchorHp.Capacity
                : Mathf.Max(1, fallbackRamsToWin);
            // 应用临时心值加成
            if (run.NextBattleHeartBonus > 0)
            {
                playerHp += run.NextBattleHeartBonus;
                run.NextBattleHeartBonus = 0;
            }
            var enemyArmor = currentEnemy != null ? currentEnemy.ArmorValue : 200;
            // 应用顺风波及事件惩罚
            if (run.NextMonsterHpPenalty > 0)
            {
                enemyArmor = Mathf.Max(1, enemyArmor - run.NextMonsterHpPenalty);
                run.NextMonsterHpPenalty = 0;
            }

            // 从 RunData 构建持久牌库
            var persistentDeck = run.BuildCombatDeck(oreCatalog);
            // 获取遗物定义列表
            var relics = run.GetActiveRelics();
            // 获取铸台倍率（含 slotUpgrades + 冲击龙骨遗物）
            var slotMultipliers = run.GetEffectiveSlotMultipliers();
            // 获取敌舰技能
            var enemySkills = ResolveEnemySkills();

            _combat.InitBattle(oreCatalog, enemyArmor, playerHp,
                persistentDeck, relics, slotMultipliers, enemySkills);
            _combat.GoldChanged += OnCombatGoldChanged;
            ApplyCurrentEnemyPresentation();
            _combat.EnemyHpChanged += (cur, max) => EnemyHpChanged?.Invoke(cur, max);
            _combat.DamageDealt += dmg => DamageDealt?.Invoke(dmg);

            bores?.HideAll();
            anchorHp?.ResetFull();
            anchorHp?.SetVisible(false);
            hullModSlots?.SetVisible(false);
            hydraulicScene?.ResetForgeState();
        }

        void OnCombatGoldChanged(int delta)
        {
            var run = RunData.Ensure();
            run.Gold += delta;
            RunData.Save();
            if (delta != 0)
                Debug.Log($"[Battle] 金币变化：{delta:+#;-#}（当前{run.Gold}）");
        }

        /// <summary>结束战斗（调试热键 / 后续胜负条件）。</summary>
        public bool ExitBattle()
        {
            if (_state != BattlePhaseState.Active)
            {
                Debug.LogWarning($"[Battle] ExitBattle 被忽略，当前状态={_state}");
                return false;
            }

            KillMotion();
            _state = BattlePhaseState.Exiting;
            _routine = StartCoroutine(ExitRoutine());
            return true;
        }

        /// <summary>敌人/玩家死亡时结束战斗（外部调用入口）。</summary>
        public void NotifyCombatantDefeated(bool playerDied)
        {
            _won = !playerDied;
            Debug.Log($"[Battle] NotifyCombatantDefeated playerDied={playerDied} won={_won}");
            if (_state == BattlePhaseState.Active)
            {
                ExitBattle();
            }
        }

        /// <summary>进入锻造子状态机（液压场景，可默认失活，由 Enter 拉起）。</summary>
        public bool TryEnterForgeMode()
        {
            if (!CanEnterForgeMode || _state != BattlePhaseState.Active)
            {
                return false;
            }

            ResolveRefs();
            if (hydraulicScene == null)
            {
                Debug.LogWarning("[Battle] 找不到 HydraulicSceneController，无法进入锻造。");
                return false;
            }

            if (hydraulicScene.State != HydraulicSceneState.Hidden)
            {
                return false;
            }

            ForgeModeEntered?.Invoke();
            return hydraulicScene.Enter();
        }

        /// <summary>
        /// 点击敌人：抛锚 → 命中绷直后进入「抛锚互撞」子流程（绞盘狂点拉近 → 对撞 → 弹开回位）。
        /// </summary>
        public bool TryEnterAnchorMode()
        {
            if (!CanEnterAnchorMode || _state != BattlePhaseState.Active || _ramming)
            {
                return false;
            }

            ResolveRefs();
            if (anchorChain == null)
            {
                Debug.LogWarning("[Battle] 找不到 AnchorChainSystem，无法抛锚。");
                return false;
            }

            if (anchorChain.IsBusy)
            {
                return false;
            }

            // 扔出船锚。命中(Attached)后由互撞子流程接管。
            if (!anchorChain.Fire())
            {
                Debug.LogWarning(
                    $"[Battle] Anchor Fire 失败 phase={anchorChain.CurrentPhase} canFire={anchorChain.CanFire}");
                return false;
            }

            if (_anchorRoutine != null)
            {
                StopCoroutine(_anchorRoutine);
            }

            _anchorRoutine = StartCoroutine(AnchorRamRoutine());
            AnchorModeEntered?.Invoke();
            return true;
        }

        /// <summary>
        /// 抛锚互撞子流程：等命中 → 交给 <see cref="AnchorRammingController"/> 跑完整套演出 → 回到 Active。
        /// 找不到互撞控制器时回退为「命中后复位」，避免卡死。
        /// </summary>
        IEnumerator AnchorRamRoutine()
        {
            ResolveRefs();

            if (anchorRam == null)
            {
                Debug.LogWarning("[Battle] 未找到 AnchorRammingController，回退为命中后复位。");
                var timeoutAt = Time.unscaledTime + 5f;
                yield return new WaitUntil(() =>
                    anchorChain == null
                    || anchorChain.CurrentPhase == AnchorChainLauncher.Phase.Attached
                    || Time.unscaledTime >= timeoutAt);
                if (anchorChain != null)
                {
                    anchorChain.ResetToIdle();
                }
                _anchorRoutine = null;
                yield break;
            }

            _ramming = true;

            var done = false;
            void OnRamCompleted()
            {
                anchorRam.Completed -= OnRamCompleted;
                done = true;
            }

            anchorRam.Completed += OnRamCompleted;

            if (!anchorRam.Begin())
            {
                anchorRam.Completed -= OnRamCompleted;
                _ramming = false;
                if (anchorChain != null)
                {
                    anchorChain.ResetToIdle();
                }
                _anchorRoutine = null;
                yield break;
            }

            // 互撞跑完或战斗被退出时收尾。
            yield return new WaitUntil(() => done || _state != BattlePhaseState.Active);

            anchorRam.Completed -= OnRamCompleted;
            _ramming = false;
            _anchorRoutine = null;

            if (done && _state == BattlePhaseState.Active)
            {
                OnRamSucceeded();
            }
        }

        /// <summary>一次撞击成功：应用真实伤害到敌舰 → 判胜负 → 玩家挨打 → 下回合。</summary>
        void OnRamSucceeded()
        {
            _ramsCompleted++;
            RamSucceeded?.Invoke();

            if (_combat == null)
            {
                // 兜底：无战斗模型时走旧的次数判定
                bool depleted;
                if (anchorHp != null && anchorHp.Capacity > 0)
                {
                    depleted = anchorHp.ConsumeOne();
                }
                else
                {
                    depleted = _ramsCompleted >= Mathf.Max(1, fallbackRamsToWin);
                }

                if (depleted)
                {
                    _won = true;
                    ExitBattle();
                }

                return;
            }

            // 应用撞击伤害（敌舰扣血 → 判死 → 玩家挨 1 点 → 判死 → 回合清理）
            var result = _combat.ApplyRam();
            Debug.Log($"[Battle] 撞击结算：伤害 {result.Damage}，敌舰剩余 {_combat.State.EnemyHp}/{_combat.State.EnemyMaxHp}" +
                      $"，玩家剩余 {_combat.State.PlayerHp}/{_combat.State.PlayerMaxHp}");

            // 玩家挨打时同步船锚余量视觉（敌舰存活 → 玩家 -1）
            if (!result.EnemyDead)
            {
                anchorHp?.ConsumeOne();
            }

            if (result.EnemyDead || result.PlayerDead)
            {
                _won = result.EnemyDead;
                ExitBattle();
            }
            else
            {
                // 战斗继续：钻头已用完，下回合重新锻造
                bores?.HideAll();
                _hasForgedBore = false;
                for (var i = 0; i < _pendingBoreOccupancy.Length; i++)
                {
                    _pendingBoreOccupancy[i] = false;
                }
            }
        }

        IEnumerator EnterRoutine()
        {
            UnlockPermissions();
            ShowBattleHud();

            // 战斗开始：敌人信息面板入场；介绍文字改由 hover 敌人时瞬间显示。
            if (enemyInfoPanel != null)
            {
                var enterDone = false;
                void OnEnterCompleted()
                {
                    enemyInfoPanel.EnterCompleted -= OnEnterCompleted;
                    enterDone = true;
                }

                enemyInfoPanel.EnterCompleted += OnEnterCompleted;
                try
                {
                    enemyInfoPanel.RequestShow(currentEnemy != null ? currentEnemy.BuildIntroText() : null);
                }
                catch (Exception ex)
                {
                    enemyInfoPanel.EnterCompleted -= OnEnterCompleted;
                    Debug.LogException(ex);
                    enterDone = true;
                }

                if (!enterDone && enemyInfoPanel.IsShown)
                {
                    enemyInfoPanel.EnterCompleted -= OnEnterCompleted;
                    enterDone = true;
                }

                if (!enterDone)
                {
                    var timeout = Time.unscaledTime + 2f;
                    yield return new WaitUntil(() =>
                        enterDone
                        || enemyInfoPanel.State == EnemyIntroducePanelState.Shown
                        || enemyInfoPanel.State == EnemyIntroducePanelState.Hidden
                        || Time.unscaledTime >= timeout);

                    enemyInfoPanel.EnterCompleted -= OnEnterCompleted;
                }
            }

            _routine = null;
            _state = BattlePhaseState.Active;
            EnterCompleted?.Invoke();
            Debug.Log("[Battle] Active");
        }

        IEnumerator ExitRoutine()
        {
            LockPermissions();
            HideBattleHud();
            HideEnemyDescription();

            // 若锻造子场景开着，先常规退场。
            if (hydraulicScene != null
                && (hydraulicScene.IsActive || hydraulicScene.IsBusy))
            {
                // 宿主可能仍激活且处于 Active；Hidden 则无需处理。
                if (hydraulicScene.State == HydraulicSceneState.Active)
                {
                    var hydraulicDone = false;
                    void OnHydraulicExit()
                    {
                        hydraulicScene.ExitCompleted -= OnHydraulicExit;
                        hydraulicDone = true;
                    }

                    hydraulicScene.ExitCompleted += OnHydraulicExit;
                    if (!hydraulicScene.Exit())
                    {
                        hydraulicScene.ExitCompleted -= OnHydraulicExit;
                        hydraulicDone = true;
                    }

                    yield return new WaitUntil(() =>
                        hydraulicDone || hydraulicScene.State == HydraulicSceneState.Hidden);
                }
            }

            // 战斗结束：收起敌人信息面板与文字。
            if (enemyInfoPanel != null
                && enemyInfoPanel.State != EnemyIntroducePanelState.Hidden)
            {
                var panelDone = false;
                void OnPanelExit()
                {
                    enemyInfoPanel.ExitCompleted -= OnPanelExit;
                    panelDone = true;
                }

                enemyInfoPanel.ExitCompleted += OnPanelExit;
                enemyInfoPanel.RequestHide();

                if (enemyInfoPanel.State == EnemyIntroducePanelState.Hidden)
                {
                    enemyInfoPanel.ExitCompleted -= OnPanelExit;
                    panelDone = true;
                }

                if (!panelDone)
                {
                    var timeout = Time.unscaledTime + 2f;
                    yield return new WaitUntil(() =>
                        panelDone
                        || enemyInfoPanel.State == EnemyIntroducePanelState.Hidden
                        || Time.unscaledTime >= timeout);
                    enemyInfoPanel.ExitCompleted -= OnPanelExit;
                }
            }
            else
            {
                enemyInfoPanel?.HideImmediate();
            }

            // 玩家缓动走到 player exit。
            yield return SailPlayerToExit();

            _routine = null;
            _state = BattlePhaseState.Idle;
            ExitCompleted?.Invoke();

            // 战后结算：保存牌库、发放金币奖励
            if (_won && _combat != null)
            {
                var run = RunData.Ensure();
                var allCards = _combat.GetAllCards();
                run.SyncDeckFromCombat(allCards);
                var flowState = GameFlowController.Instance?.CurrentState ?? GameFlowState.Battle0;
                run.ResolveBattleWin(flowState, _combat.State.Turn, firstTurnKill: _combat.State.Turn == 1);
                Debug.Log($"[Battle] 战后结算完成：金币{run.Gold}，矿舱{run.Deck.Count}块");
            }
            else if (!_won)
            {
                RunData.ResolveDefeat();
                Debug.Log("[Battle] 战败，存档已清除");
            }

            BattleFinished?.Invoke(_won);
            Debug.Log($"[Battle] Idle（won={_won}）→ 交由 SceneFlowDirector 推进主流程");
        }

        void UpdateActiveCombat()
        {
            UpdateEnemyDescriptionHover();

            if (hydraulicScene != null && (hydraulicScene.IsActive || hydraulicScene.IsBusy))
            {
                return;
            }

            // 互撞演出进行时，绞盘由 WinchCrankController 独占鼠标；此处不再处理选船点击。
            if (_ramming)
            {
                return;
            }

            if (!WasPrimaryClickPressedThisFrame())
            {
                return;
            }

            var hovered = pointerSelector != null ? pointerSelector.Hovered : null;
            if (hovered == null)
            {
                return;
            }

            if (hovered == playerSelectable)
            {
                TryEnterForgeMode();
                return;
            }

            if (hovered == enemySelectable)
            {
                // 未铸造钻头就点敌人：先拦截一次并弹提示；玩家坚持再点则放任其空抛锚。
                if (!_hasForgedBore && !_anchorWarned)
                {
                    _anchorWarned = true;
                    ShowNotice(forgeFirstNotice);
                    return;
                }

                TryEnterAnchorMode();
            }
        }

        void UpdateEnemyDescriptionHover()
        {
            if (enemyInfoPanel == null || enemySelectable == null)
            {
                return;
            }

            // 锻造子场景开启时不显示敌人描述。
            if (hydraulicScene != null && (hydraulicScene.IsActive || hydraulicScene.IsBusy))
            {
                HideEnemyDescription();
                return;
            }

            var hovered = pointerSelector != null ? pointerSelector.Hovered : null;
            var onEnemy = hovered == enemySelectable;
            if (onEnemy)
            {
                ShowEnemyDescription();
            }
            else
            {
                HideEnemyDescription();
            }
        }

        void ShowEnemyDescription()
        {
            if (_enemyDescVisible || enemyInfoPanel == null || !enemyInfoPanel.IsShown)
            {
                return;
            }

            enemyInfoPanel.ShowDescriptionImmediate();
            _enemyDescVisible = true;
        }

        void HideEnemyDescription()
        {
            if (!_enemyDescVisible)
            {
                return;
            }

            enemyInfoPanel?.HideDescriptionImmediate();
            _enemyDescVisible = false;
        }

        void ShowBattleHud()
        {
            anchorHp?.SetVisible(true);
            hullModSlots?.SetVisible(true);
        }

        void HideBattleHud()
        {
            anchorHp?.SetVisible(false);
            hullModSlots?.SetVisible(false);
        }

        void ShowNotice(string text)
        {
            var notice = UiSystem.Instance != null ? UiSystem.Instance.Notice : null;
            notice?.Show(NoticeChannel.Notice, text, 2.2f);
        }

        void BindHydraulicEvents(bool forceRebind = false)
        {
            ResolveHydraulicSceneRef();

            if (hydraulicScene == null)
            {
                UnbindHydraulicEvents();
                return;
            }

            if (!forceRebind
                && _hydraulicEventsBound
                && _boundHydraulicScene == hydraulicScene)
            {
                return;
            }

            UnbindHydraulicEvents();

            hydraulicScene.EnterStarted += OnForgeEnterStarted;
            hydraulicScene.EnterCompleted += OnForgeEnterCompleted;
            hydraulicScene.ExitCompleted += OnForgeExitCompleted;
            hydraulicScene.HydraulicCompleted += OnForgeHydraulicCompleted;
            hydraulicScene.ForgeCommitted += OnForgeCommitted;
            _hydraulicEventsBound = true;
            _boundHydraulicScene = hydraulicScene;
        }

        void UnbindHydraulicEvents()
        {
            var target = _boundHydraulicScene != null ? _boundHydraulicScene : hydraulicScene;
            if (target != null)
            {
                target.EnterStarted -= OnForgeEnterStarted;
                target.EnterCompleted -= OnForgeEnterCompleted;
                target.ExitCompleted -= OnForgeExitCompleted;
                target.HydraulicCompleted -= OnForgeHydraulicCompleted;
                target.ForgeCommitted -= OnForgeCommitted;
            }

            _hydraulicEventsBound = false;
            _boundHydraulicScene = null;
        }

        void ResolveHydraulicSceneRef()
        {
            var current = HydraulicSceneController.Instance
                ?? FindFirstObjectByType<HydraulicSceneController>(FindObjectsInactive.Include);

            if (current != null)
            {
                hydraulicScene = current;
            }
        }

        /// <summary>铸造提交：记录钻头占用 + 用真实矿石数据计算伤害并存为待应用撞击伤害。</summary>
        void OnForgeCommitted(bool[] occupancy)
        {
            // 记录钻头占用（视觉用）
            if (occupancy != null)
            {
                var any = false;
                for (var i = 0; i < _pendingBoreOccupancy.Length; i++)
                {
                    var has = i < occupancy.Length && occupancy[i];
                    _pendingBoreOccupancy[i] = has;
                    any |= has;
                }

                if (any)
                {
                    _hasForgedBore = true;
                }
            }

            // 真实伤害计算：从砧台读矿石数据 → CombatModel.CommitForge
            var board = hydraulicScene?.MaterialBoard;
            if (board != null && _combat != null)
            {
                var anvilStacks = board.GetAnvilStacks();
                var tableCards = board.GetTableCards();
                var damage = _combat.CommitForge(anvilStacks, tableCards);
                Debug.Log($"[Battle] 锻造提交：预计撞击伤害 {damage}（敌舰剩余 {_combat.State.EnemyHp}/{_combat.State.EnemyMaxHp}）");
            }
        }

        /// <summary>铸造完成（锤头砸下、场景退场）：按提交时的占用亮出对应钻头，并恢复敌人信息。</summary>
        void OnForgeHydraulicCompleted()
        {
            bores?.ShowBores(_pendingBoreOccupancy);
            OnForgeExitCompleted();
        }

        /// <summary>锻造开启：瞬间藏起敌人信息面板与文字。</summary>
        void OnForgeEnterStarted()
        {
            if (_state != BattlePhaseState.Active && _state != BattlePhaseState.Entering)
            {
                return;
            }

            HideEnemyDescription();
            enemyInfoPanel?.SuspendImmediate();
        }

        /// <summary>锻造入场完成：自动出货 5 块矿石（对应 web drawCards(DRAW_COUNT=5)）。</summary>
        void OnForgeEnterCompleted()
        {
            if (_state != BattlePhaseState.Active && _state != BattlePhaseState.Entering)
            {
                return;
            }

            var lane = hydraulicScene?.MaterialLane;
            if (lane == null)
            {
                return;
            }

            // 桌面有遗留矿石时不重复抽卡（玩家退出锻造看对面后重入时保留）。
            if (lane.SettledOnTableCount > 0)
            {
                return;
            }

            var drawCount = _combat != null ? _combat.GetDrawCount() : CombatCalculator.DrawCount;
            lane.AutoDeliver(drawCount, 0.18f);
        }

        /// <summary>锻造退出：瞬间恢复敌人信息（战斗已结束则不恢复）。</summary>
        void OnForgeExitCompleted()
        {
            if (_state != BattlePhaseState.Active)
            {
                return;
            }

            enemyInfoPanel?.ResumeImmediate();
        }

        void UnlockPermissions()
        {
            SetSelectableEnabled(playerSelectable, true);
            SetSelectableEnabled(enemySelectable, true);
            CanEnterForgeMode = true;
            CanEnterAnchorMode = true;
        }

        void LockPermissions()
        {
            CanEnterForgeMode = false;
            CanEnterAnchorMode = false;
            HideEnemyDescription();
            SetSelectableEnabled(playerSelectable, false);
            SetSelectableEnabled(enemySelectable, false);

            if (playerSelectable != null)
            {
                playerSelectable.SetHovered(false);
            }

            if (enemySelectable != null)
            {
                enemySelectable.SetHovered(false);
            }
        }

        static void SetSelectableEnabled(SelectableSceneElement selectable, bool enabled)
        {
            if (selectable != null)
            {
                selectable.enabled = enabled;
            }
        }

        IEnumerator SailPlayerToExit()
        {
            ResolveRefs();
            if (player == null || playerExit == null)
            {
                Debug.LogWarning("[Battle] player / player exit 缺失，跳过出场缓动。");
                yield break;
            }

            if (playerExitDuration <= 0f)
            {
                player.position = playerExit.position;
                yield break;
            }

            var completed = false;
            _playerExitTween = player
                .DOMove(playerExit.position, playerExitDuration)
                .SetEase(playerExitEase)
                .SetUpdate(true)
                .OnComplete(() => completed = true);

            yield return new WaitUntil(() => completed);
            _playerExitTween = null;
            player.position = playerExit.position;
        }

        void KillMotion()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            if (_anchorRoutine != null)
            {
                StopCoroutine(_anchorRoutine);
                _anchorRoutine = null;
            }

            if (anchorRam != null && anchorRam.IsRunning)
            {
                anchorRam.ForceStop();
            }

            _ramming = false;

            if (anchorChain != null && anchorChain.IsBusy)
            {
                anchorChain.ResetToIdle();
            }

            if (_playerExitTween != null && _playerExitTween.IsActive())
            {
                _playerExitTween.Kill();
            }

            _playerExitTween = null;

            if (player != null)
            {
                player.DOKill();
            }
        }

        void ResolveRefs()
        {
            if (player == null)
            {
                player = FindByName("player");
            }

            if (playerExit == null)
            {
                playerExit = FindByName("player exit");
            }

            if (playerSelectable == null && player != null)
            {
                playerSelectable = player.GetComponent<SelectableSceneElement>();
            }

            if (enemySelectable == null)
            {
                var enemy = FindByName("enemy");
                if (enemy != null)
                {
                    enemySelectable = enemy.GetComponent<SelectableSceneElement>();
                }
            }

            if (pointerSelector == null)
            {
                pointerSelector = FindFirstObjectByType<SceneElementPointerSelector>();
            }

            ResolveHydraulicSceneRef();
            BindHydraulicEvents();

            if (enemyInfoPanel == null)
            {
                enemyInfoPanel = UiSystem.Instance != null
                    ? UiSystem.Instance.EnemyInfo
                    : FindFirstObjectByType<EnemyIntroducePanelController>();
            }

            if (anchorChain == null)
            {
                anchorChain = FindFirstObjectByType<AnchorChainLauncher>(FindObjectsInactive.Include);
            }

            if (anchorRam == null)
            {
                anchorRam = FindFirstObjectByType<AnchorRammingController>(FindObjectsInactive.Include);
            }

            if (bores == null)
            {
                bores = FindFirstObjectByType<BoreManager>(FindObjectsInactive.Include);
            }

            if (anchorHp == null)
            {
                anchorHp = FindFirstObjectByType<AnchorHpTracker>(FindObjectsInactive.Include);
            }

            if (hullModSlots == null)
            {
                hullModSlots = FindFirstObjectByType<HullModSlotsController>(FindObjectsInactive.Include);
            }
        }

        /// <summary>编辑器下自动加载 OreCatalog（未在 Inspector 指定时）。</summary>
        void ResolveOreCatalog()
        {
            if (oreCatalog != null) return;
#if UNITY_EDITOR
            oreCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<OreCatalog>(
                "Assets/ScriptableObjects/Data/OreCatalog.asset");
#endif
            if (oreCatalog == null)
            {
                Debug.LogWarning("[Battle] OreCatalog 未指定且自动加载失败。" +
                                  "请在 Inspector 指定或确认 Assets/ScriptableObjects/Data/OreCatalog.asset 存在。");
            }
        }

        void ResolveCurrentEnemy()
        {
            if (currentEnemy != null)
            {
                return;
            }

            // 按 GameFlowState 对应的节点选敌舰
            var flowState = GameFlowController.Instance != null
                ? GameFlowController.Instance.CurrentState
                : GameFlowState.Battle0;
            var stageKey = RunData.GetStageKey(flowState);
            var stageConfig = WebGameData.GetStage(stageKey);
            string targetDisplayName = null;
            int targetHp = 200;

            if (stageConfig != null && stageConfig.MonsterPool.Length > 0)
            {
                var monsterId = WebGameData.PickRandomMonster(stageConfig.MonsterPool);
                var enemyDef = WebGameData.GetEnemy(monsterId);
                if (enemyDef != null)
                {
                    targetDisplayName = enemyDef.DisplayName;
                    targetHp = enemyDef.Hp;
                }
            }

            // 从 EnemyShips 文件夹加载所有敌舰 SO，按名字匹配
            if (!string.IsNullOrEmpty(targetDisplayName))
            {
                currentEnemy = FindEnemySOByName(targetDisplayName);
            }

#if UNITY_EDITOR
            if (currentEnemy == null)
            {
                currentEnemy = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyShipDataSO>(
                    "Assets/ScriptableObjects/Data/EnemyShips/enemy_tengmanhao.asset");
            }
#endif

            if (currentEnemy == null)
            {
                currentEnemy = ScriptableObject.CreateInstance<EnemyShipDataSO>();
            }

            Debug.Log($"[Battle] 选定敌舰：{currentEnemy?.DisplayName ?? "?"}（目标：{targetDisplayName ?? "?"}，HP={targetHp}）");
        }

        /// <summary>按名字从 EnemyShips 文件夹查找敌舰 SO。</summary>
        EnemyShipDataSO FindEnemySOByName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return null;
#if UNITY_EDITOR
            var guids = UnityEditor.AssetDatabase.FindAssets("t:EnemyShipDataSO",
                new[] { "Assets/ScriptableObjects/Data/EnemyShips" });
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var so = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyShipDataSO>(path);
                if (so != null && so.DisplayName == displayName)
                    return so;
            }
#endif
            return null;
        }

        /// <summary>获取本场敌舰的技能关键词列表。</summary>
        string[] ResolveEnemySkills()
        {
            var flowState = GameFlowController.Instance != null
                ? GameFlowController.Instance.CurrentState
                : GameFlowState.Battle0;
            var stageKey = RunData.GetStageKey(flowState);
            var stageConfig = WebGameData.GetStage(stageKey);
            if (stageConfig == null || stageConfig.MonsterPool.Length == 0) return null;
            var monsterId = stageConfig.MonsterPool[0]; // 简化：取池中第一个
            var enemyDef = WebGameData.GetEnemy(monsterId);
            return enemyDef?.Keywords;
        }

        void ResolveEnemyCatalog()
        {
            if (enemyCatalog != null) return;
#if UNITY_EDITOR
            enemyCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyShipCatalog>(
                "Assets/ScriptableObjects/Data/EnemyShipCatalog.asset");
#endif
        }

        void ResolveHullModCatalog()
        {
            if (hullModCatalog != null) return;
#if UNITY_EDITOR
            hullModCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<HullModCatalog>(
                "Assets/ScriptableObjects/Data/HullModCatalog.asset");
#endif
        }

        void ApplyCurrentEnemyPresentation()
        {
            if (currentEnemy == null)
            {
                return;
            }

            var enemy = FindByName("enemy");
            if (enemy == null)
            {
                return;
            }

            var visual = enemy.GetComponent<StripSpriteCharacterVisual>();
            currentEnemy.ApplyVisual(visual);
        }

        static Transform FindByName(string objectName)
        {
            var go = GameObject.Find(objectName);
            return go != null ? go.transform : null;
        }

        static bool WasPrimaryClickPressedThisFrame()
        {
            // 鼠标点击：旧 Input 优先，新 Input System 兜底。
            if (Input.GetMouseButtonDown(0))
            {
                return true;
            }

#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                return true;
            }
#endif
            return false;
        }
    }
}

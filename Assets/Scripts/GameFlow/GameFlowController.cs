using System;
using NineGrid.UI;
using QFramework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NineGrid.GameFlow
{
    /// <summary>
    /// 跨场景全局唯一的主流程状态机。
    /// 挂在场景中配置；不自动切场景，场景跳转由外部在需要时申请。
    /// </summary>
    public sealed class GameFlowController : PersistentMonoSingleton<GameFlowController>
    {
        /// <summary>
        /// 战役线性序列：序章 / 战斗0 之后的完整主线。
        /// Prologue 与 Battle0 互斥入口，不在此表内互相跳转。
        /// </summary>
        static readonly GameFlowState[] CampaignSequence =
        {
            GameFlowState.Prologue,
            GameFlowState.Battle0,
            GameFlowState.Island1,
            GameFlowState.Route1,
            GameFlowState.Event1,
            GameFlowState.Battle1,
            GameFlowState.Island2,
            GameFlowState.Route2,
            GameFlowState.Event2,
            GameFlowState.Battle2,
            GameFlowState.Island3,
            GameFlowState.Route3,
            GameFlowState.Event3,
            GameFlowState.Battle3Elite,
            GameFlowState.IslandEliteReward,
            GameFlowState.Battle4,
            GameFlowState.Island4,
            GameFlowState.Route4,
            GameFlowState.Event4,
            GameFlowState.Battle5,
            GameFlowState.Island5,
            GameFlowState.Route5,
            GameFlowState.Event5,
            GameFlowState.Battle6,
            GameFlowState.Island6,
            GameFlowState.Route6,
            GameFlowState.Event6,
            GameFlowState.BossBattle,
            GameFlowState.VictorySettlement,
        };

        [Header("开局")]
        [Tooltip("勾选：从 MainScene 启动时直接进入序章。MainPanelScene 启动时始终进主菜单。")]
        [SerializeField] bool enterPrologueOnStart = true;

        readonly FSM<GameFlowState> _fsm = new();

        bool _booted;

        /// <summary>
        /// 由 <see cref="SceneFlowDirector"/> 置真：节点进入动作（序章演出 / 开打）改为在场景加载完成后由 Director 调
        /// <see cref="RunNodeEntry(GameFlowState)"/>，避免场景未就位就在旧场景残留对象上触发战斗。
        /// </summary>
        public static bool DeferNodeEntry { get; set; }

        /// <summary>是否已 Boot（FSM 已进入初始状态）。未 Boot 时 <see cref="CurrentState"/> 只是占位默认值。</summary>
        public bool IsBooted => _booted;

        public GameFlowState CurrentState =>
            _fsm.CurrentState != null ? _fsm.CurrentStateId : GameFlowState.MainMenu;

        public GameFlowState PreviousState =>
            _fsm.CurrentState != null ? _fsm.PreviousStateId : GameFlowState.MainMenu;

        public bool EnterPrologueOnStart => enterPrologueOnStart;
        public bool HasCompletedPrologue => GameFlowProgress.HasCompletedPrologue;
        public bool IsInBattle => GameFlowScenes.IsBattleState(CurrentState);
        public bool IsInIsland => GameFlowScenes.IsIslandState(CurrentState);
        public bool IsPrologueRun => CurrentState == GameFlowState.Prologue;

        /// <summary>状态切换后回调（previous, next）。不自动加载场景。</summary>
        public event Action<GameFlowState, GameFlowState> StateChanged;
        public event Action RunStarted;
        public event Action PlayerDefeated;
        public event Action VictorySettled;

        protected override void Awake()
        {
            base.Awake();
            // Instance getter 可能在 Awake 前写入 mInstance，导致 base 未置 mEnabled。
            if (mInstance == this)
            {
                mEnabled = true;
            }

            if (!mEnabled)
            {
                return;
            }

            gameObject.name = "[GameFlow]";
            BuildFsm();
        }

        void Start()
        {
            if (!mEnabled || _booted)
            {
                return;
            }

            Boot();
        }

        void Update()
        {
            if (_booted)
            {
                _fsm.Update();
            }
        }

        void OnDestroy()
        {
            if (mInstance == this)
            {
                _fsm.Clear();
            }
        }

        /// <summary>从主菜单开始一局：始终从序章进入。</summary>
        public void StartNewRun()
        {
            RunData.StartNewRun();
            EnterState(GameFlowState.Prologue);
            RunStarted?.Invoke();
        }

        /// <summary>推进到战役下一节点。胜利结算后回到主菜单。不自动切场景。</summary>
        public void Advance()
        {
            if (!_booted)
            {
                Boot();
            }

            var current = CurrentState;
            if (current == GameFlowState.MainMenu)
            {
                StartNewRun();
                return;
            }

            if (current == GameFlowState.VictorySettlement)
            {
                GoToMainMenu();
                return;
            }

            if (current == GameFlowState.Prologue)
            {
                MarkPrologueCompleted();
                EnterState(GameFlowState.Island1);
                return;
            }

            if (current == GameFlowState.Battle0)
            {
                EnterState(GameFlowState.Island1);
                return;
            }

            var index = IndexOf(current);
            if (index < 0)
            {
                Debug.LogWarning($"[GameFlow] Advance: 未知状态 {current}，回主菜单。");
                GoToMainMenu();
                return;
            }

            if (index >= CampaignSequence.Length - 1)
            {
                GoToMainMenu();
                return;
            }

            EnterState(CampaignSequence[index + 1]);
        }

        /// <summary>
        /// Route* 节点已在 <see cref="RouteController"/> 完成事件选择后调用。
        /// 跳过线性序列中的 Event* 跳板，直接进入对应 Battle*。
        /// </summary>
        public void AdvanceFromRouteAfterEvent()
        {
            if (!_booted)
            {
                Boot();
            }

            var current = CurrentState;
            if (!GameFlowScenes.IsRouteState(current))
            {
                Debug.LogWarning(
                    $"[GameFlow] AdvanceFromRouteAfterEvent: 当前 {current} 不是 Route*，忽略。");
                return;
            }

            var index = IndexOf(current);
            if (index < 0 || index + 2 >= CampaignSequence.Length)
            {
                Debug.LogWarning($"[GameFlow] AdvanceFromRouteAfterEvent: 无法从 {current} 跳到战斗，回退 Advance。");
                Advance();
                return;
            }

            EnterState(CampaignSequence[index + 2]);
        }

        /// <summary>中途暴毙：回主菜单状态（不自动切场景）。</summary>
        public void NotifyPlayerDefeated()
        {
            GoToMainMenu();
            PlayerDefeated?.Invoke();
        }

        /// <summary>胜利结算完成：回主菜单状态（不自动切场景）。</summary>
        public void NotifyVictorySettled()
        {
            GoToMainMenu();
            VictorySettled?.Invoke();
        }

        public void GoToMainMenu()
        {
            RunData.ClearSave();
            EnterState(GameFlowState.MainMenu);
        }

        /// <summary>调试 / 特殊跳转。正常流程请用 Advance / StartNewRun。</summary>
        public void ForceState(GameFlowState state)
        {
            EnterState(state);
        }

        void Boot()
        {
            GameFlowState initial;
            var activeScene = SceneManager.GetActiveScene().name;
            if (string.Equals(activeScene, GameFlowScenes.MainPanel, StringComparison.Ordinal))
            {
                initial = GameFlowState.MainMenu;
            }
            else if (enterPrologueOnStart)
            {
                initial = GameFlowState.Prologue;
            }
            else
            {
                MarkPrologueCompleted();
                initial = GameFlowState.Battle0;
            }

            _booted = true;
            _fsm.StartState(initial);

            // FSM.StartState 不触发 OnStateChanged；开局也补发一次，让 SceneFlowDirector 能接管首节点。
            if (DeferNodeEntry)
            {
                StateChanged?.Invoke(initial, initial);
            }

            Debug.Log($"[GameFlow] Boot → {initial} (enterPrologueOnStart={enterPrologueOnStart})");
        }

        void BuildFsm()
        {
            foreach (GameFlowState state in Enum.GetValues(typeof(GameFlowState)))
            {
                var captured = state;
                _fsm.State(captured)
                    .OnEnter(() => OnEnterState(captured));
            }

            _fsm.OnStateChanged((previous, next) =>
            {
                StateChanged?.Invoke(previous, next);
                Debug.Log($"[GameFlow] {previous} → {next}");
                SceneFlowDirector.HandleStateChanged(next);
            });
        }

        void EnterState(GameFlowState state)
        {
            if (!_booted)
            {
                _booted = true;
                _fsm.StartState(state);
                return;
            }

            if (state == CurrentState)
            {
                return;
            }

            _fsm.ChangeState(state);
        }

        void OnEnterState(GameFlowState state)
        {
            // 有 SceneFlowDirector 时，节点进入动作交给它在场景加载完成后驱动。
            if (DeferNodeEntry)
            {
                return;
            }

            RunNodeEntry(state);
        }

        /// <summary>按当前状态执行节点进入动作。</summary>
        public void RunNodeEntry() => RunNodeEntry(CurrentState);

        /// <summary>
        /// 执行某节点的进入动作（序章演出 / 战斗入场）。岛屿 / 路线 / 主菜单 / 胜利由各自场景控制器接管。
        /// SceneFlowDirector 在目标场景加载完成后调用本方法。
        /// </summary>
        public void RunNodeEntry(GameFlowState state)
        {
            if (state == GameFlowState.Prologue)
            {
                // 序章：含教学对话的完整入场。
                StartBattleEntrance(includeDialogue: true);
            }
            else if (GameFlowScenes.IsBattleState(state))
            {
                // 其余战斗：玩家进位、敌人跟来，无对话。
                StartBattleEntrance(includeDialogue: false);
            }
            // 岛屿 / 路线 / 主菜单 / 胜利：由场景内控制器在加载后自初始化。
        }

        /// <summary>入场演出（可选对话）跑完后进入战斗环节；找不到演出组件则直接开打。</summary>
        void StartBattleEntrance(bool includeDialogue)
        {
            var performance = ProloguePerformance.Instance;
            if (performance == null)
            {
                performance = FindFirstObjectByType<ProloguePerformance>();
            }

            if (performance == null)
            {
                Debug.LogWarning("[GameFlow] 场景中没有 ProloguePerformance，直接进入战斗环节。");
                BeginBattlePhase();
                return;
            }

            if (includeDialogue)
            {
                PlayerRunHudController.Instance?.Suppress(PlayerRunHudSuppressReason.PrologueOpening);
            }
            else
            {
                PlayerRunHudController.Instance?.Release(PlayerRunHudSuppressReason.PrologueOpening);
            }

            var entranceState = CurrentState;

            void OnPerformanceCompleted()
            {
                performance.Completed -= OnPerformanceCompleted;
                if (CurrentState == entranceState)
                {
                    BeginBattlePhase();
                }
            }

            performance.Completed += OnPerformanceCompleted;
            performance.PlayEntrance(includeDialogue);
        }

        /// <summary>战斗环节入口。序章演出结束后 / 战斗0 直进时调用，转交 <see cref="BattleController"/>。</summary>
        void BeginBattlePhase()
        {
            PlayerRunHudController.Instance?.Release(PlayerRunHudSuppressReason.PrologueOpening);

            var battle = BattleController.Instance;
            if (battle == null)
            {
                battle = FindFirstObjectByType<BattleController>();
            }

            if (battle == null)
            {
                Debug.LogWarning($"[GameFlow] 场景中没有 BattleController，战斗环节未启动。state={CurrentState}");
                return;
            }

            if (!battle.EnterBattle())
            {
                Debug.LogWarning($"[GameFlow] BattleController.EnterBattle 失败，state={CurrentState} battle={battle.State}");
                return;
            }

            Debug.Log($"[GameFlow] 进入战斗环节 state={CurrentState}");
        }

        void MarkPrologueCompleted()
        {
            if (GameFlowProgress.HasCompletedPrologue)
            {
                return;
            }

            GameFlowProgress.HasCompletedPrologue = true;
            Debug.Log("[GameFlow] 序章教学已完成。");
        }

        static int IndexOf(GameFlowState state)
        {
            for (var i = 0; i < CampaignSequence.Length; i++)
            {
                if (CampaignSequence[i] == state)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}

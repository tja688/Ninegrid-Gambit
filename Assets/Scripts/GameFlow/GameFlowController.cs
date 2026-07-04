using System;
using QFramework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NineGrid.GameFlow
{
    /// <summary>
    /// 跨场景全局唯一的主流程状态机。
    /// 负责战役节点推进、场景切换占位，以及序章仅首次进入的分流。
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

        readonly FSM<GameFlowState> _fsm = new();

        bool _booted;
        bool _isTransitioning;

        public GameFlowState CurrentState =>
            _fsm.CurrentState != null ? _fsm.CurrentStateId : GameFlowState.MainMenu;

        public GameFlowState PreviousState =>
            _fsm.CurrentState != null ? _fsm.PreviousStateId : GameFlowState.MainMenu;
        public bool HasCompletedPrologue => GameFlowProgress.HasCompletedPrologue;
        public bool IsInBattle => GameFlowScenes.IsBattleState(CurrentState);
        public bool IsInIsland => GameFlowScenes.IsIslandState(CurrentState);
        public bool IsPrologueRun => CurrentState == GameFlowState.Prologue;

        /// <summary>状态切换后回调（previous, next）。</summary>
        public event Action<GameFlowState, GameFlowState> StateChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            _ = Instance;
        }

        protected override void Awake()
        {
            base.Awake();
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

        /// <summary>从主菜单开始一局。首次进序章，之后进战斗0。</summary>
        public void StartNewRun()
        {
            EnterState(GameFlowProgress.HasCompletedPrologue
                ? GameFlowState.Battle0
                : GameFlowState.Prologue);
        }

        /// <summary>推进到战役下一节点。胜利结算后回到主菜单。</summary>
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

        /// <summary>中途暴毙：回主菜单。</summary>
        public void NotifyPlayerDefeated()
        {
            GoToMainMenu();
        }

        /// <summary>胜利结算完成：回主菜单。</summary>
        public void NotifyVictorySettled()
        {
            GoToMainMenu();
        }

        public void GoToMainMenu()
        {
            EnterState(GameFlowState.MainMenu);
        }

        /// <summary>调试 / 特殊跳转。正常流程请用 Advance / StartNewRun。</summary>
        public void ForceState(GameFlowState state)
        {
            EnterState(state);
        }

        void Boot()
        {
            var initial = GameFlowProgress.HasCompletedPrologue
                ? GameFlowState.MainMenu
                : GameFlowState.Prologue;

            _booted = true;
            _fsm.StartState(initial);
            Debug.Log($"[GameFlow] Boot → {initial} (prologueDone={GameFlowProgress.HasCompletedPrologue})");
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
            ApplyStateEnter(state, reloadScene: true);
        }

        void ApplyStateEnter(GameFlowState state, bool reloadScene)
        {
            // 占位：后续在此挂战斗初始化、岛屿 UI、事件表等。
            if (state == GameFlowState.Prologue)
            {
                // 序章演出：播放对话池中的 prologue_intro，结束后推进主流程。
                var ui = NineGrid.UI.UiSystem.Instance;
                if (ui != null && ui.Dialogue != null)
                {
                    void OnPrologueDialogueDone()
                    {
                        ui.Dialogue.SequenceCompleted -= OnPrologueDialogueDone;
                        if (CurrentState == GameFlowState.Prologue)
                        {
                            Advance();
                        }
                    }

                    ui.Dialogue.SequenceCompleted += OnPrologueDialogueDone;
                    ui.Dialogue.Play("prologue_intro");
                }
            }
            else if (GameFlowScenes.IsBattleState(state))
            {
                // 占位：按 state 配置遭遇战
            }
            else if (GameFlowScenes.IsIslandState(state))
            {
                // 占位：岛屿休整 / 精英奖励
            }
            else if (state == GameFlowState.MainMenu)
            {
                // 占位：主菜单
            }
            else if (state == GameFlowState.VictorySettlement)
            {
                // 占位：胜利结算
            }

            if (reloadScene)
            {
                LoadSceneForState(state);
            }
        }

        void LoadSceneForState(GameFlowState state)
        {
            var targetScene = GameFlowScenes.GetSceneName(state);
            var active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.name == targetScene)
            {
                return;
            }

            if (_isTransitioning)
            {
                return;
            }

            _isTransitioning = true;
            var op = SceneManager.LoadSceneAsync(targetScene);
            if (op == null)
            {
                _isTransitioning = false;
                Debug.LogError($"[GameFlow] 无法加载场景: {targetScene}（是否已加入 Build Settings？）");
                return;
            }

            op.completed += _ => _isTransitioning = false;
        }

        void MarkPrologueCompleted()
        {
            if (GameFlowProgress.HasCompletedPrologue)
            {
                return;
            }

            GameFlowProgress.HasCompletedPrologue = true;
            Debug.Log("[GameFlow] 序章教学已完成，后续开局将跳过序章。");
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

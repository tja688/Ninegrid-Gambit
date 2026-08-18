using System.Collections.Generic;
using NineGrid.Content.Audio;
using NineGrid.Core;
using NineGrid.Core.Systems;
using NineGrid.Flow;
using NineGrid.Flow.Diagnostics;
using NineGrid.Flow.Presentation;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// 流程唯一权威：Shell 相位 / node / run mode / generation / CTS（经 Orchestrator）。
    /// 公开 <see cref="IGameFlowShellSystem"/> 只读；写入经 Command。
    /// </summary>
    public sealed class GameFlowShellSystem : AbstractSystem, IGameFlowShellSystem
    {
        public const float QuickTestTimeScale = 1f;
        public const int QuickTestAvatarHp = 99;
        public const int QuickTestPlayerCoins = 999;
        public const int QuickTestAvatarAttack = QuickTestRunPlanner.AvatarAttack;

        private readonly BindableProperty<GameFlowShellState> mState =
            new BindableProperty<GameFlowShellState>(GameFlowShellState.MainMenu);

        private readonly GameFlowOrchestrator mOrchestrator;

        private IGameFlowView mView;
        private bool mIsBusy;
        private bool mQuickTestMode;
        private bool mTutorialMode;
        private int mNodeIndex;
        private int mGeneration;
        private List<int> mQuickTestContentNodeQueue;
        private int mQuickTestContentNodeCursor;
        private QuickTestNodeOrderMode mQuickTestNodeOrderMode = QuickTestNodeOrderMode.Shuffled;
        private string mPinnedFirstBattleDeckId;
        private IReadOnlyList<string> mQuickTestSkillIds = System.Array.Empty<string>();
        private IReadOnlyList<string> mQuickTestTrapContentIds = System.Array.Empty<string>();

        public GameFlowShellSystem()
        {
            mOrchestrator = new GameFlowOrchestrator(this);
        }

        public IReadonlyBindableProperty<GameFlowShellState> State
        {
            get { return mState; }
        }

        public int NodeIndex => mNodeIndex;

        public bool IsBusy => mIsBusy;

        public bool IsQuickTestMode => mQuickTestMode;

        /// <summary>教学关卡模式（单场受控教学战斗，不进节点循环）。</summary>
        public bool IsTutorialMode => mTutorialMode;

        public int Generation => mGeneration;

        public bool CanAcceptQuickTestEntry =>
            mState.Value == GameFlowShellState.MainMenu && !mIsBusy;

        public bool IsBound => mView != null;

        internal IGameFlowView View => mView;

        internal QuickTestNodeOrderMode QuickTestNodeOrderMode => mQuickTestNodeOrderMode;

        internal string PinnedFirstBattleDeckId => mPinnedFirstBattleDeckId;

        internal IReadOnlyList<string> QuickTestSkillIds => mQuickTestSkillIds;

        internal IReadOnlyList<string> QuickTestTrapContentIds => mQuickTestTrapContentIds;

        internal IReadOnlyList<int> QuickTestContentNodeQueue => mQuickTestContentNodeQueue;

        protected override void OnInit()
        {
        }

        public void Bind(IGameFlowView view)
        {
            mView = view;
            SubmitMusicForShellState(GameFlowShellState.MainMenu, "GameFlowShellSystem.Bind");
        }

        public void Unbind()
        {
            mView = null;
        }

        public void UnbindIfView(IGameFlowView view)
        {
            if (mView == view)
            {
                mView = null;
            }
        }

        /// <summary>仅供 <see cref="Commands.SetGameFlowShellStateCommand"/> 写入相位。</summary>
        public void ApplyState(GameFlowShellState next)
        {
            mState.Value = next;
        }
        internal MusicRequestResult SubmitMusicForState(
            DesiredMusicState state,
            string stableSource)
        {
            var architecture = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            if (architecture == null)
            {
                return null;
            }

            var music = architecture.GetSystem<IMusicSystem>();
            if (music == null)
            {
                return null;
            }

            return music.RequestState(new MusicStateRequest(state, stableSource));
        }

        internal MusicRequestResult SubmitMusicForShellState(
            GameFlowShellState state,
            string stableSource)
        {
            var desired = DesiredMusicState.RunExploration;
            switch (state)
            {
                case GameFlowShellState.MainMenu:
                    desired = DesiredMusicState.MainMenu;
                    break;
                case GameFlowShellState.BattleStub:
                    var flowArchitecture = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
                    var run = flowArchitecture?.GetModel<RunModel>();
                    var room = run != null && run.Room != null ? run.Room.Value : RoomKind.None;
                    var nodeIndex = run != null && run.NodeIndex != null ? run.NodeIndex.Value : 0;
                    var isBossNode = room == RoomKind.Boss
                        || (nodeIndex > 0 && nodeIndex % RunModel.NodesPerFloor == 0);
                    desired = isBossNode ? DesiredMusicState.BossBattle : DesiredMusicState.Battle;
                    break;
                case GameFlowShellState.VictoryNotice:
                    desired = DesiredMusicState.Victory;
                    break;
                case GameFlowShellState.DefeatNotice:
                    desired = DesiredMusicState.Defeat;
                    break;
                case GameFlowShellState.RewardChoice:
                case GameFlowShellState.RoomChoice:
                case GameFlowShellState.RoomEvent:
                    desired = DesiredMusicState.RunExploration;
                    break;
            }

            return SubmitMusicForState(desired, stableSource);
        }

        public void BeginRun(GameFlowRunOptions options)
        {
            mOrchestrator.Start(options ?? GameFlowRunOptions.CreateFormal());
        }

        public void ReturnToMainMenu()
        {
            mOrchestrator.Stop();
        }

        /// <summary>
        /// DisableDomainReload 下 Play 退出后 Architecture 仍存活时，强制把 Shell 权威拉回主菜单。
        /// 不碰场景 View/表现清场（销毁期可能已失效）；进 Play 的 Awake 再走完整 ReturnToMainMenu。
        /// </summary>
        public void ForceMainMenuAuthority()
        {
            mOrchestrator.Dispose();
            mState.Value = GameFlowShellState.MainMenu;
            ClearRunSession();
        }

        public void Signal(GameFlowSignal signal)
        {
            mOrchestrator.Signal(signal);
        }

        public bool TryBeginQuickTestFromPickerCode(int code)
        {
            if (!CanAcceptQuickTestEntry)
            {
                return false;
            }

            if (!QuickTestDeckCatalog.TryResolvePickerCode(code, out var preset))
            {
                return false;
            }

            mView?.HideNotice();
            BeginRun(GameFlowRunOptions.CreateQuickTest(new QuickTestRunOptions
            {
                NodeOrder = preset.NodeOrder,
                PinnedFirstBattleDeckId = preset.PinnedFirstBattleDeckId,
                SkillIds = preset.SkillIds,
                TrapContentIds = preset.TrapContentIds,
            }));
            return true;
        }

        public string BuildQuickTestPickerMenuText()
        {
            CoreCardPresentationMapper.EnsureContentCatalogLoaded();
            var catalog = NineGridArchitecture.Current.GetSystem<IContentSystem>()?.Catalog;
            return QuickTestDeckCatalog.BuildPickerMenuText(catalog);
        }

        internal void ApplyRunMode(bool quickTestMode)
        {
            mQuickTestMode = quickTestMode;
        }

        internal void ApplyTutorialMode(bool tutorialMode)
        {
            mTutorialMode = tutorialMode;
        }

        internal void ResetNodeProgress()
        {
            mNodeIndex = 0;
            mQuickTestContentNodeQueue = null;
            mQuickTestContentNodeCursor = 0;
            mPinnedFirstBattleDeckId = null;
            mQuickTestSkillIds = System.Array.Empty<string>();
            mQuickTestTrapContentIds = System.Array.Empty<string>();
            mQuickTestNodeOrderMode = QuickTestNodeOrderMode.Shuffled;
        }

        internal void BumpGeneration()
        {
            mGeneration++;
        }

        internal void SetBusy(bool busy)
        {
            mIsBusy = busy;
        }

        internal void IncrementNodeIndex()
        {
            mNodeIndex++;
        }

        /// <summary>
        /// 作弊跨层：把壳层全局节点序号设为「目标节点的前一格」，
        /// 与 <see cref="SetNodeProgressBeforeRestoredNode"/> 同口径，使节点循环
        /// 首次 <see cref="IncrementNodeIndex"/> 后落在
        /// <c>(floor-1)*NodesPerFloor + ToDisplayNode(nodeIndex)</c>。
        /// </summary>
        internal void SyncShellNodeIndexForCheat(int floor, int nodeIndex)
        {
            var targetGlobal = (floor - 1) * RunModel.NodesPerFloor + MapNodeProgression.ToDisplayNode(nodeIndex);
            mNodeIndex = targetGlobal > 0 ? targetGlobal - 1 : 0;
        }

        /// <summary>
        /// 正式 Run 发牌内容节点：跟 Core <see cref="RunModel"/> 楼层/层内节点，
        /// 不跟壳层 <see cref="NodeIndex"/>（作弊跨层等会令二者漂移）。
        /// </summary>
        internal static int ResolveFormalBattleContentNodeIndex(RunModel run)
        {
            if (run == null)
            {
                return 1;
            }

            var floor = run.Floor != null ? run.Floor.Value : 1;
            var nodeIndex = run.NodeIndex != null ? run.NodeIndex.Value : 0;
            return (floor - 1) * RunModel.NodesPerFloor + MapNodeProgression.ToDisplayNode(nodeIndex);
        }

        /// <summary>
        /// 读档恢复：把壳层全局节点序号设为「目标节点的前一格」，
        /// 使节点循环首次 <see cref="IncrementNodeIndex"/> 后正好落在快照捕获时的全局序号
        /// （BuildNodeDeckOptions 的内容节点序号由此对齐，保证发牌复现）。
        /// </summary>
        internal void SetNodeProgressBeforeRestoredNode(int shellGlobalNodeIndex)
        {
            mNodeIndex = shellGlobalNodeIndex > 0 ? shellGlobalNodeIndex - 1 : 0;
        }

        internal void PrepareQuickTest(QuickTestRunOptions options)
        {
            var qt = options ?? new QuickTestRunOptions();
            mQuickTestNodeOrderMode = qt.NodeOrder;
            mPinnedFirstBattleDeckId = string.IsNullOrWhiteSpace(qt.PinnedFirstBattleDeckId)
                ? null
                : qt.PinnedFirstBattleDeckId.Trim();
            mQuickTestSkillIds = qt.SkillIds != null && qt.SkillIds.Count > 0
                ? qt.SkillIds
                : System.Array.Empty<string>();
            mQuickTestTrapContentIds = qt.TrapContentIds != null && qt.TrapContentIds.Count > 0
                ? qt.TrapContentIds
                : System.Array.Empty<string>();
            PrepareQuickTestContentNodeQueue(mQuickTestNodeOrderMode);
        }

        internal void ClearQuickTest()
        {
            mQuickTestNodeOrderMode = QuickTestNodeOrderMode.Shuffled;
            mPinnedFirstBattleDeckId = null;
            mQuickTestSkillIds = System.Array.Empty<string>();
            mQuickTestTrapContentIds = System.Array.Empty<string>();
            mQuickTestContentNodeQueue = null;
            mQuickTestContentNodeCursor = 0;
        }

        internal void ClearRunSession()
        {
            mQuickTestMode = false;
            mTutorialMode = false;
            ClearQuickTest();
            DiagTraceShared.ClearRunTag();
            mNodeIndex = 0;
            mIsBusy = false;
            mGeneration++;
        }

        internal bool TryConsumePinnedFirstBattle(out string deckId)
        {
            deckId = null;
            if (!mQuickTestMode || mNodeIndex != 1 || string.IsNullOrEmpty(mPinnedFirstBattleDeckId))
            {
                return false;
            }

            deckId = mPinnedFirstBattleDeckId;
            mPinnedFirstBattleDeckId = null;
            return true;
        }

        internal int ResolveBattleContentNodeIndex()
        {
            if (!mQuickTestMode
                || mQuickTestContentNodeQueue == null
                || mQuickTestContentNodeQueue.Count == 0)
            {
                return mNodeIndex;
            }

            if (mQuickTestContentNodeCursor >= mQuickTestContentNodeQueue.Count)
            {
                Debug.LogWarning(
                    $"[GameFlow] 快速测试节点队列已耗尽，回退顺序节点 {mNodeIndex}。");
                return mNodeIndex;
            }

            return mQuickTestContentNodeQueue[mQuickTestContentNodeCursor++];
        }

        private void PrepareQuickTestContentNodeQueue(QuickTestNodeOrderMode orderMode)
        {
            CoreCardPresentationMapper.EnsureContentCatalogLoaded();
            var arch = NineGridArchitecture.Current;
            var catalog = arch?.GetSystem<IContentSystem>()?.Catalog;
            var ruleIndices = QuickTestRunPlanner.CollectRuleNodeIndices(catalog);
            mQuickTestContentNodeQueue = orderMode == QuickTestNodeOrderMode.Sequential
                ? QuickTestRunPlanner.BuildSequentialContentNodeQueue(ruleIndices)
                : QuickTestRunPlanner.BuildShuffledContentNodeQueue(ruleIndices);
            mQuickTestContentNodeCursor = 0;
        }

        public static GameFlowShellSystem EnsureRegistered(IArchitecture architecture = null)
        {
            architecture = architecture
                ?? NineGridArchitecture.Interface
                ?? NineGridArchitecture.Current;
            if (architecture == null)
            {
                var created = new GameFlowShellSystem();
                return created;
            }

            var existing = architecture.GetSystem<IGameFlowShellSystem>() as GameFlowShellSystem;
            if (existing != null)
            {
                return existing;
            }

            var system = new GameFlowShellSystem();
            architecture.RegisterSystem<IGameFlowShellSystem>(system);
            return system;
        }
    }
}

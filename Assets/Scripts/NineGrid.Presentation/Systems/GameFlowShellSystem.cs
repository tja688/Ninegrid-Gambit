using System.Collections.Generic;
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
        public const int QuickTestAvatarAttack = QuickTestRunPlanner.AvatarAttack;

        private readonly BindableProperty<GameFlowShellState> mState =
            new BindableProperty<GameFlowShellState>(GameFlowShellState.MainMenu);

        private readonly GameFlowOrchestrator mOrchestrator;

        private IGameFlowView mView;
        private bool mIsBusy;
        private bool mQuickTestMode;
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

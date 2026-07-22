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
        private bool mTestMode;
        private bool mQuickTestMode;
        private int mNodeIndex;
        private int mGeneration;
        private List<int> mQuickTestContentNodeQueue;
        private int mQuickTestContentNodeCursor;
        private QuickTestNodeOrderMode mQuickTestNodeOrderMode = QuickTestNodeOrderMode.Shuffled;
        private string mPinnedFirstBattleDeckId;

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

        public bool IsTestMode => mTestMode;

        public bool IsQuickTestMode => mQuickTestMode;

        public int Generation => mGeneration;

        public bool CanAcceptQuickTestEntry =>
            mState.Value == GameFlowShellState.MainMenu && !mIsBusy;

        public bool CanAcceptInBattleDebugQuickMode =>
            mState.Value != GameFlowShellState.MainMenu
            && mState.Value != GameFlowShellState.VictoryNotice
            && mState.Value != GameFlowShellState.DefeatNotice;

        public bool IsBound => mView != null;

        internal IGameFlowView View => mView;

        internal QuickTestNodeOrderMode QuickTestNodeOrderMode => mQuickTestNodeOrderMode;

        internal string PinnedFirstBattleDeckId => mPinnedFirstBattleDeckId;

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
            mOrchestrator.Start(options ?? new GameFlowRunOptions());
        }

        public void ReturnToMainMenu()
        {
            mOrchestrator.Stop();
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

            CoreCardPresentationMapper.EnsureContentCatalogLoaded();
            var catalog = NineGridArchitecture.Current.GetSystem<IContentSystem>()?.Catalog;
            if (!QuickTestDeckCatalog.TryResolvePickerCode(code, catalog, out var deckId, out _))
            {
                return false;
            }

            mView?.HideNotice();
            BeginRun(new GameFlowRunOptions
            {
                TestMode = true,
                QuickTestMode = true,
                QuickTest = new QuickTestRunOptions
                {
                    NodeOrder = code == QuickTestDeckCatalog.FormalOrderPickerCode
                        ? QuickTestNodeOrderMode.Sequential
                        : QuickTestNodeOrderMode.Shuffled,
                    PinnedFirstBattleDeckId = deckId,
                },
            });
            return true;
        }

        public string BuildQuickTestPickerMenuText()
        {
            CoreCardPresentationMapper.EnsureContentCatalogLoaded();
            var catalog = NineGridArchitecture.Current.GetSystem<IContentSystem>()?.Catalog;
            return QuickTestDeckCatalog.BuildPickerMenuText(catalog);
        }

        public string BuildInBattleDebugQuickModeMenuText()
        {
            return "局内快速模式\n\n"
                + "当前全局速度：x" + FormatTimeScale(Time.timeScale) + "\n\n"
                + "\\1  全局速度 x1\n"
                + "\\2  全局速度 x2（再按在此基础上 x2）\n\n"
                + "释放 \\ 键关闭";
        }

        public static void ApplyInBattleDebugQuickModeTimeScaleX1()
        {
            Time.timeScale = 1f;
            Debug.Log("[GameFlow] 局内 Debug 快速模式：全局速度 x1");
        }

        public static void ApplyInBattleDebugQuickModeTimeScaleX2()
        {
            if (Time.timeScale <= 1.01f)
            {
                Time.timeScale = 2f;
            }
            else
            {
                Time.timeScale *= 2f;
            }

            Debug.Log("[GameFlow] 局内 Debug 快速模式：全局速度 x" + FormatTimeScale(Time.timeScale));
        }

        internal void ApplyRunMode(bool testMode, bool quickTestMode)
        {
            mTestMode = testMode;
            mQuickTestMode = quickTestMode;
        }

        internal void ResetNodeProgress()
        {
            mNodeIndex = 0;
            mQuickTestContentNodeQueue = null;
            mQuickTestContentNodeCursor = 0;
            mPinnedFirstBattleDeckId = null;
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
            PrepareQuickTestContentNodeQueue(mQuickTestNodeOrderMode);
        }

        internal void ClearQuickTest()
        {
            mQuickTestNodeOrderMode = QuickTestNodeOrderMode.Shuffled;
            mPinnedFirstBattleDeckId = null;
            mQuickTestContentNodeQueue = null;
            mQuickTestContentNodeCursor = 0;
        }

        internal void ClearRunSession()
        {
            mTestMode = false;
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

        private static string FormatTimeScale(float scale)
        {
            return Mathf.Approximately(scale, Mathf.Round(scale))
                ? Mathf.RoundToInt(scale).ToString()
                : scale.ToString("0.##");
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

using System.Collections;
using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NineGrid.Presentation.Adaptors;
using NineGrid.Presentation.Diagnostics;
using NineGrid.Presentation.Visuals;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.FSM
{
    /// <summary>
    /// P1 节点流程：按战役规则构造 <see cref="NodeDeckOptions"/>，自动/手动发 <see cref="StartNodeCommand"/> 并驱动批次播放。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TableNineNodeFlowCoordinator : MonoBehaviour, IController
    {
        [Header("Flow")]
        [SerializeField] private bool autoStartNodes = true;

        [Header("References")]
        [SerializeField] private InGameFlowShellFsm flowShell;
        [SerializeField] private PresentationBatchPlayer batchPlayer;

        private CoreCommandDispatcher commandDispatcher;
        private Coroutine autoStartCoroutine;
        private FloorDeckSelection deckSelection;
        private float mNextAutoWaitLogTime;

        public bool IsAutoStartActive => autoStartCoroutine != null;

        public IArchitecture GetArchitecture()
        {
            return NineGridArchitecture.Interface;
        }

        private void Awake()
        {
            commandDispatcher = new CoreCommandDispatcher(GetArchitecture());
            ResolveReferences();
        }

        public void OnRunSessionEntered()
        {
            if (autoStartNodes)
            {
                ScheduleAutoStart();
            }
        }

        public void OnPhaseChanged(GamePhase phase)
        {
            if (!autoStartNodes || !ShouldScheduleAutoStart(phase))
            {
                return;
            }

            ScheduleAutoStart();
        }

        /// <summary>
        /// 手动或自动启动下一节点；<paramref name="ignoreAutoFlag"/> 为 true 时供调试快捷键使用。
        /// </summary>
        public bool TryStartNextNode(bool ignoreAutoFlag = false)
        {
            if (!ignoreAutoFlag && !autoStartNodes)
            {
                return false;
            }

            ResolveReferences();

            var phaseSystem = this.GetSystem<IPhaseSystem>();
            if (!phaseSystem.CanExecute(GameCommandKind.StartNode))
            {
                PresentationTrace.Log(
                    PresentationTraceChannel.Flow,
                    PresentationTraceLevel.Warn,
                    "NODE_START_BLOCKED",
                    ("reason", "phaseIllegal"),
                    ("phase", phaseSystem.CurrentPhase));
                return false;
            }

            if (this.GetSystem<IPresentationSyncSystem>().IsInputLocked)
            {
                PresentationTrace.Log(
                    PresentationTraceChannel.Flow,
                    PresentationTraceLevel.Warn,
                    "NODE_START_BLOCKED",
                    ("reason", "inputLocked"),
                    ("batch", this.GetSystem<IPresentationSyncSystem>().ActiveBatchId));
                return false;
            }

            var options = BuildNextNodeDeckOptions();
            var result = commandDispatcher.Send(new StartNodeCommand(options));
            if (!result.Accepted)
            {
                PresentationTrace.Log(
                    PresentationTraceChannel.Flow,
                    PresentationTraceLevel.Warn,
                    "NODE_START_BLOCKED",
                    ("reason", "commandRejected"));
                return false;
            }

            flowShell?.NotifyNodeSessionStarted();
            batchPlayer?.PlayDispatchResult(result);

            var run = this.GetModel<RunModel>();
            PresentationTrace.Log(
                PresentationTraceChannel.Flow,
                PresentationTraceLevel.Info,
                "NODE_START_OK",
                ("floor", run.Floor.Value),
                ("node", run.NodeIndex.Value + 1),
                ("batch", result.Batch?.BatchId ?? 0));
            return true;
        }

        private void ScheduleAutoStart()
        {
            if (autoStartCoroutine != null)
            {
                StopCoroutine(autoStartCoroutine);
            }

            autoStartCoroutine = StartCoroutine(AutoStartWhenReady());
        }

        private IEnumerator AutoStartWhenReady()
        {
            yield return null;
            mNextAutoWaitLogTime = Time.realtimeSinceStartup;

            while (ShouldAttemptAutoStart())
            {
                if (this.GetSystem<IPresentationSyncSystem>().IsInputLocked)
                {
                    if (Time.realtimeSinceStartup >= mNextAutoWaitLogTime)
                    {
                        mNextAutoWaitLogTime = Time.realtimeSinceStartup
                            + PresentationTrace.Config.NodeAutoWaitLogIntervalSeconds;
                        PresentationTrace.Log(
                            PresentationTraceChannel.Flow,
                            PresentationTraceLevel.Trace,
                            "NODE_AUTO_WAIT",
                            ("waitingLock", true),
                            ("phase", this.GetModel<RunModel>().Phase.Value),
                            ("canStart", this.GetSystem<IPhaseSystem>().CanExecute(GameCommandKind.StartNode)));
                    }

                    yield return null;
                    continue;
                }

                PresentationTrace.Log(
                    PresentationTraceChannel.Flow,
                    PresentationTraceLevel.Info,
                    "NODE_START_ATTEMPT",
                    ("phase", this.GetModel<RunModel>().Phase.Value));

                if (TryStartNextNode())
                {
                    break;
                }

                yield return null;
            }

            autoStartCoroutine = null;
        }

        private bool ShouldAttemptAutoStart()
        {
            var phase = this.GetModel<RunModel>().Phase.Value;
            return ShouldScheduleAutoStart(phase)
                && this.GetSystem<IPhaseSystem>().CanExecute(GameCommandKind.StartNode);
        }

        private static bool ShouldScheduleAutoStart(GamePhase phase)
        {
            return phase == GamePhase.None
                || phase == GamePhase.BuildEnemyPool
                || phase == GamePhase.NodeCompleted;
        }

        private NodeDeckOptions BuildNextNodeDeckOptions()
        {
            ContentCatalogRuntimeBootstrap.EnsureLoaded(GetArchitecture());

            var run = this.GetModel<RunModel>();
            var nodeIndex = run.NodeIndex.Value + 1;
            var floor = run.Floor.Value;
            deckSelection = EnsureDeckSelection(floor);

            var content = this.GetSystem<IContentSystem>();
            content.TryReloadFromConfig();
            var catalog = content.HasCatalog ? content.Catalog : null;

            var deckKind = FindNodeDeckKind(catalog, nodeIndex);
            var deckId = deckSelection == null ? string.Empty : deckSelection.GetDeckId(deckKind);
            return this.GetSystem<IRewardSystem>().BuildNodeDeckOptions(nodeIndex, deckId);
        }

        private FloorDeckSelection EnsureDeckSelection(int floor)
        {
            if (deckSelection != null && deckSelection.Floor == floor)
            {
                return deckSelection;
            }

            var content = this.GetSystem<IContentSystem>();
            content.TryReloadFromConfig();
            var catalog = content.HasCatalog ? content.Catalog : null;
            var rng = this.GetUtility<IRngUtility>();

            deckSelection = new FloorDeckSelection(
                floor,
                PickDeck(catalog, MonsterDeckKind.WeakElite, rng),
                PickDeck(catalog, MonsterDeckKind.StrongElite, rng),
                PickDeck(catalog, MonsterDeckKind.Boss, rng));
            return deckSelection;
        }

        private static MonsterDeckKind FindNodeDeckKind(GameContentCatalog catalog, int nodeIndex)
        {
            if (catalog == null)
            {
                return MonsterDeckKind.Unknown;
            }

            for (var i = 0; i < catalog.Rewards.NodeDeckRules.Count; i++)
            {
                var rule = catalog.Rewards.NodeDeckRules[i];
                if (rule.NodeIndex == nodeIndex)
                {
                    return rule.DeckKind;
                }
            }

            return MonsterDeckKind.Unknown;
        }

        private static string PickDeck(GameContentCatalog catalog, MonsterDeckKind kind, IRngUtility rng)
        {
            if (catalog == null || rng == null)
            {
                return string.Empty;
            }

            var candidates = new List<string>();
            foreach (var pair in catalog.MonsterDecks)
            {
                if (pair.Value.Kind == kind)
                {
                    candidates.Add(pair.Key);
                }
            }

            if (candidates.Count == 0)
            {
                return string.Empty;
            }

            return candidates[rng.Range(0, candidates.Count)];
        }

        private void ResolveReferences()
        {
            if (flowShell == null)
            {
                flowShell = GetComponent<InGameFlowShellFsm>();
                if (flowShell == null)
                {
                    flowShell = FindFirstObjectByType<InGameFlowShellFsm>();
                }
            }

            if (batchPlayer == null)
            {
                batchPlayer = GetComponent<PresentationBatchPlayer>();
                if (batchPlayer == null)
                {
                    batchPlayer = FindFirstObjectByType<PresentationBatchPlayer>();
                }
            }
        }

        private sealed class FloorDeckSelection
        {
            public FloorDeckSelection(int floor, string weakEliteDeckId, string strongEliteDeckId, string bossDeckId)
            {
                Floor = floor;
                WeakEliteDeckId = weakEliteDeckId ?? string.Empty;
                StrongEliteDeckId = strongEliteDeckId ?? string.Empty;
                BossDeckId = bossDeckId ?? string.Empty;
            }

            public int Floor { get; private set; }
            public string WeakEliteDeckId { get; private set; }
            public string StrongEliteDeckId { get; private set; }
            public string BossDeckId { get; private set; }

            public string GetDeckId(MonsterDeckKind kind)
            {
                switch (kind)
                {
                    case MonsterDeckKind.WeakElite:
                        return WeakEliteDeckId;
                    case MonsterDeckKind.StrongElite:
                        return StrongEliteDeckId;
                    case MonsterDeckKind.Boss:
                        return BossDeckId;
                    default:
                        return string.Empty;
                }
            }
        }
    }
}

using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Systems;
using NineGrid.Presentation.Adaptors;
using NineGrid.Presentation.FSM;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Tools
{
    /// <summary>
    /// 开发入口：PlayMode 快捷键发 <see cref="StartNodeCommand"/>，驱动流程壳进入 NodePlaying 并播放开局批次。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DevStartNodeTool : MonoBehaviour, IController
    {
        [Header("Hotkey")]
        [SerializeField] private KeyCode startNodeKey = KeyCode.F5;

        [Header("References")]
        [SerializeField] private InGameFlowShellFsm flowShell;
        [SerializeField] private PresentationBatchPlayer batchPlayer;

        [Header("Node Deck")]
        [SerializeField] private bool useDefaultBattleDeck = true;

        private CoreCommandDispatcher commandDispatcher;

        public IArchitecture GetArchitecture()
        {
            return NineGridArchitecture.Interface;
        }

        private void Awake()
        {
            commandDispatcher = new CoreCommandDispatcher(GetArchitecture());
        }

        private void Update()
        {
            if (!Application.isPlaying || !Input.GetKeyDown(startNodeKey))
            {
                return;
            }

            TryStartNode();
        }

        public void TryStartNode()
        {
            if (flowShell == null)
            {
                flowShell = FindFirstObjectByType<InGameFlowShellFsm>();
            }

            if (batchPlayer == null)
            {
                batchPlayer = FindFirstObjectByType<PresentationBatchPlayer>();
            }

            if (!this.GetSystem<IPhaseSystem>().CanExecute(GameCommandKind.StartNode))
            {
                Debug.LogWarning("[DevStartNode] StartNode not legal in phase " + this.GetSystem<IPhaseSystem>().CurrentPhase);
                return;
            }

            NodeDeckOptions options = useDefaultBattleDeck
                ? NodeDeckOptions.CreateDefaultBattle()
                : new NodeDeckOptions();

            var result = commandDispatcher.Send(new StartNodeCommand(options));
            if (!result.Accepted)
            {
                Debug.LogWarning("[DevStartNode] StartNode rejected.");
                return;
            }

            flowShell?.NotifyNodeSessionStarted();
            batchPlayer?.PlayDispatchResult(result);
            Debug.Log("[DevStartNode] StartNode accepted; batch id=" + (result.Batch?.BatchId ?? 0));
        }
    }
}

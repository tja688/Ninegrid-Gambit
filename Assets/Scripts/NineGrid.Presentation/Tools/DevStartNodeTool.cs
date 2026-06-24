using NineGrid.Core;
using NineGrid.Core.Systems;
using NineGrid.Presentation.FSM;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Tools
{
    /// <summary>
    /// 开发入口：PlayMode 快捷键手动发 StartNode（正式流程由 <see cref="TableNineNodeFlowCoordinator"/> 自动推进）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DevStartNodeTool : MonoBehaviour, IController
    {
        [Header("Hotkey")]
        [SerializeField] private KeyCode startNodeKey = KeyCode.F5;

        [Header("References")]
        [SerializeField] private TableNineNodeFlowCoordinator nodeFlowCoordinator;

        public IArchitecture GetArchitecture()
        {
            return NineGridArchitecture.Interface;
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
            if (nodeFlowCoordinator == null)
            {
                nodeFlowCoordinator = FindFirstObjectByType<TableNineNodeFlowCoordinator>();
            }

            if (nodeFlowCoordinator == null)
            {
                Debug.LogWarning("[DevStartNode] TableNineNodeFlowCoordinator not found.");
                return;
            }

            if (!this.GetSystem<IPhaseSystem>().CanExecute(GameCommandKind.StartNode))
            {
                Debug.LogWarning(
                    "[DevStartNode] StartNode not legal in phase "
                    + this.GetSystem<IPhaseSystem>().CurrentPhase);
                return;
            }

            nodeFlowCoordinator.TryStartNextNode(ignoreAutoFlag: true);
        }
    }
}

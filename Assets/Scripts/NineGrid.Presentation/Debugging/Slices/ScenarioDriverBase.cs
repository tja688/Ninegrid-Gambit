using System.Collections;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Presentation.Bridge;
using NineGrid.Presentation.Orchestration;
using QFramework;
using Sirenix.OdinInspector;
using UnityEngine;

namespace NineGrid.Presentation.Debugging.Slices
{
    /// <summary>
    /// 垂直切片薄骨架：从 Bootstrap 取 Gateway，子类填 <see cref="BuildDeck"/> 与场景步骤。
    /// </summary>
    public abstract class ScenarioDriverBase : MonoBehaviour
    {
        [SerializeField, LabelText("随机种子")]
        protected ulong seed = 1UL;

        [SerializeField, LabelText("启用 Flow 时序追踪")]
        protected bool enableFlowTrace;

        private Coroutine mRunningScenario;
        private FlowTraceRecorder mFlowTraceRecorder;

        protected NineGridSceneBootstrap Bootstrap => NineGridSceneBootstrap.Current;
        protected CommandGateway Gateway => Bootstrap?.Gateway;
        protected IArchitecture Architecture => Bootstrap?.Architecture;

        protected abstract NodeDeckOptions BuildDeck();

        protected virtual IEnumerator RunScenario()
        {
            yield return SendAndWait(new StartNodeCommand(BuildDeck()));
        }

        [Button("Seed 摆盘"), ShowIf("@UnityEngine.Application.isPlaying")]
        public void Seed()
        {
            StartScenario(RunScenario());
        }

        [Button("Reset"), ShowIf("@UnityEngine.Application.isPlaying")]
        public void Reset()
        {
            StopScenario();
            ResetScenarioState();
        }

        protected void StartScenario(IEnumerator steps)
        {
            if (!Application.isPlaying || steps == null)
            {
                return;
            }

            StopScenario();
            if (enableFlowTrace)
            {
                InstallFlowTrace();
            }

            mRunningScenario = StartCoroutine(RunScenarioWrapper(steps));
        }

        protected IEnumerator SendAndWait(ICommand<CoreCommandResult> command)
        {
            CommandGateway gateway = Gateway;
            if (gateway == null)
            {
                Debug.LogWarning("[ScenarioDriver] CommandGateway unavailable.");
                yield break;
            }

            CoreCommandDispatchResult result = gateway.Send(command);
            if (result.Batch != null)
            {
                PlaybackTrace.Dump(result.Batch, logParallelWarnings: true);
            }

            if (!result.Accepted)
            {
                Debug.LogWarning("[ScenarioDriver] Command rejected.");
                yield break;
            }

            yield return WaitUntilInputUnlocked(gateway);
        }

        protected void StopScenario()
        {
            if (mRunningScenario != null)
            {
                StopCoroutine(mRunningScenario);
                mRunningScenario = null;
            }
        }

        protected void ResetScenarioState()
        {
            NineGridSceneBootstrap bootstrap = Bootstrap;
            if (bootstrap == null || Architecture == null)
            {
                return;
            }

            StopAllFlowBindings();
            bootstrap.ActorFactory?.DestroyAll();
            bootstrap.ViewRegistry?.ClearActors();
            InitialGameFactory.Create(Architecture, new InitialGameOptions { Seed = seed });
            bootstrap.BuildInitialActors();
            mFlowTraceRecorder = null;
        }

        private IEnumerator RunScenarioWrapper(IEnumerator steps)
        {
            yield return steps;
            mRunningScenario = null;

            if (mFlowTraceRecorder != null)
            {
                mFlowTraceRecorder.FlushToLog();
            }
        }

        private IEnumerator WaitUntilInputUnlocked(CommandGateway gateway)
        {
            const float timeoutSeconds = 60f;
            var elapsed = 0f;
            while (gateway.IsInputLocked)
            {
                elapsed += Time.unscaledDeltaTime;
                if (elapsed >= timeoutSeconds)
                {
                    Debug.LogError("[ScenarioDriver] Timed out waiting for input unlock.");
                    yield break;
                }

                yield return null;
            }
        }

        private void StopAllFlowBindings()
        {
            FlowRegistry registry = Bootstrap?.FlowRegistry;
            if (registry == null)
            {
                return;
            }

            foreach (FlowId flowId in System.Enum.GetValues(typeof(FlowId)))
            {
                if (flowId == FlowId.None)
                {
                    continue;
                }

                if (registry.TryGet(flowId, out IFlowBinding binding))
                {
                    binding.Stop();
                }
            }
        }

        private void InstallFlowTrace()
        {
            NineGridSceneBootstrap bootstrap = Bootstrap;
            if (bootstrap?.FlowRegistry == null)
            {
                return;
            }

            mFlowTraceRecorder = new FlowTraceRecorder();
            FlowRegistry registry = bootstrap.FlowRegistry;
            foreach (FlowId flowId in System.Enum.GetValues(typeof(FlowId)))
            {
                if (flowId == FlowId.None || !registry.TryGet(flowId, out IFlowBinding inner))
                {
                    continue;
                }

                if (inner is TracingFlowBinding)
                {
                    continue;
                }

                registry.Register(new TracingFlowBinding(inner, mFlowTraceRecorder));
            }
        }
    }
}

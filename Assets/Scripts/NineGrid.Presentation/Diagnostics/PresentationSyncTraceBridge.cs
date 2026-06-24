using System.Text;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Systems;
using NineGrid.Presentation.FSM;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Diagnostics
{
    /// <summary>
    /// 订阅内核 PresentationSync 事件并写入 <see cref="PresentationTrace"/>。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-200)]
    public sealed class PresentationSyncTraceBridge : MonoBehaviour, IController
    {
        [SerializeField] private PresentationTraceConfig config;

        private float mLockSince = -1f;
        private int mLockedBatchId;

        public IArchitecture GetArchitecture()
        {
            return NineGridArchitecture.Interface;
        }

        private void Awake()
        {
            if (config != null)
            {
                PresentationTrace.Initialize(config);
            }
            else
            {
                PresentationTrace.Initialize(null);
            }
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            this.RegisterEvent<Evt_PresentationBatchOpened>(HandleBatchOpened);
            this.RegisterEvent<Evt_PresentationBatchCleared>(HandleBatchCleared);
            this.RegisterEvent<Evt_PresentationBatchFinishRejected>(HandleFinishRejected);
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            this.UnRegisterEvent<Evt_PresentationBatchOpened>(HandleBatchOpened);
            this.UnRegisterEvent<Evt_PresentationBatchCleared>(HandleBatchCleared);
            this.UnRegisterEvent<Evt_PresentationBatchFinishRejected>(HandleFinishRejected);
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            var sync = this.GetSystem<IPresentationSyncSystem>();
            var phase = this.GetModel<RunModel>().Phase.Value;
            PresentationTrace.SetContext(sync.ActiveBatchId, phase.ToString(), string.Empty);

            if (sync.IsInputLocked)
            {
                if (mLockSince < 0f)
                {
                    mLockSince = Time.realtimeSinceStartup;
                    mLockedBatchId = sync.ActiveBatchId;
                }
            }
            else if (mLockSince >= 0f)
            {
                mLockSince = -1f;
                mLockedBatchId = 0;
            }
        }

        public float GetLockHeldSeconds()
        {
            if (mLockSince < 0f)
            {
                return 0f;
            }

            return Time.realtimeSinceStartup - mLockSince;
        }

        public int LockedBatchId => mLockedBatchId;

        private void HandleBatchOpened(Evt_PresentationBatchOpened evt)
        {
            var batch = evt.Batch;
            var phase = this.GetModel<RunModel>().Phase.Value;
            PresentationTrace.SetContext(evt.BatchId, phase.ToString(), string.Empty);
            PresentationTrace.Log(
                PresentationTraceChannel.Lock,
                PresentationTraceLevel.Info,
                "LOCK_ON",
                ("batch", evt.BatchId),
                ("instr", batch != null ? batch.Instructions.Count : 0),
                ("requiresAck", batch != null && batch.RequiresAcknowledgement),
                ("phase", phase));
        }

        private void HandleBatchCleared(Evt_PresentationBatchCleared evt)
        {
            var phase = this.GetModel<RunModel>().Phase.Value;
            PresentationTrace.SetContext(0, phase.ToString(), string.Empty);
            PresentationTrace.Log(
                PresentationTraceChannel.Lock,
                PresentationTraceLevel.Info,
                "LOCK_OFF",
                ("batch", evt.BatchId),
                ("ack", evt.WasAcknowledged),
                ("phase", phase));
        }

        private void HandleFinishRejected(Evt_PresentationBatchFinishRejected evt)
        {
            PresentationTrace.Log(
                PresentationTraceChannel.Lock,
                PresentationTraceLevel.Error,
                "LOCK_REJECT_MISMATCH",
                ("expected", evt.ExpectedBatchId),
                ("got", evt.ReceivedBatchId),
                ("reason", evt.Reason));
        }
    }
}

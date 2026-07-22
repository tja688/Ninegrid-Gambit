using System;
using NineGrid.Cards;
using NineGrid.Flow.Diagnostics;
using UnityEngine;

namespace NineGrid.Presentation.Controllers
{
    /// <summary>
    /// 诊断输出 Controller：持有 Recorder 接线生命周期，卸载后旁路可整体拔除。
    /// </summary>
    public sealed class DiagnosticOutputController : PresentationController
    {
        private bool mRecordersAttached;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterInstallHook()
        {
            DiagnosticOutputHook.AttachRecorders = () => EnsureInstalled().AttachProductionRecorders();
            DiagnosticOutputHook.DetachRecorders = () =>
            {
                var existing = UnityEngine.Object.FindObjectOfType<DiagnosticOutputController>();
                if (existing != null)
                {
                    existing.DetachRecorders();
                }
            };
        }

        public static DiagnosticOutputController EnsureInstalled()
        {
            var existing = UnityEngine.Object.FindObjectOfType<DiagnosticOutputController>();
            if (existing != null)
            {
                return existing;
            }

            var host = new GameObject(nameof(DiagnosticOutputController));
            return host.AddComponent<DiagnosticOutputController>();
        }

        protected override void OnBind()
        {
            // 生产 Attach 由 InBattle 经 DiagnosticOutputHook 显式触发；此处不自动挂 Recorder。
        }

        protected override void OnUnbind()
        {
            DetachRecorders();
        }

        /// <summary>生产路径：挂接 Flow Recorders。</summary>
        public void AttachProductionRecorders()
        {
            if (mRecordersAttached)
            {
                return;
            }

            FieldTraceHelper.RegisterSinkHandlers();
            PerfTraceRecorder.RegisterSinkHandlers();
            RegistryTraceRecorder.RegisterSinkHandlers();
            mRecordersAttached = true;
        }

        /// <summary>EditMode：注入可观测 handler，验证可拔除。</summary>
        public void AttachRecordersForTests(
            Action<string, int, string, string[]> record,
            Action<int, int, int, string> occupancyConflict,
            Func<string, string[], int> beginChoreo,
            Action<string> notifyUserInteraction)
        {
            DetachRecorders();
            PerfTraceSink.Record = record;
            FlowFieldTraceSink.OccupancyConflict = occupancyConflict;
            ChoreoTraceSink.BeginChoreo = beginChoreo;
            RegistryTraceSink.NotifyUserInteraction = notifyUserInteraction;
            mRecordersAttached = true;
        }

        public void DetachRecorders()
        {
            if (!mRecordersAttached
                && PerfTraceSink.Record == null
                && FlowFieldTraceSink.OccupancyConflict == null
                && ChoreoTraceSink.BeginChoreo == null
                && RegistryTraceSink.NotifyUserInteraction == null)
            {
                return;
            }

            FieldTraceHelper.UnregisterSinkHandlers();
            PerfTraceRecorder.UnregisterSinkHandlers();
            RegistryTraceRecorder.UnregisterSinkHandlers();
            PerfTraceSink.ClearHandlers();
            FlowFieldTraceSink.ClearHandlers();
            ChoreoTraceSink.ClearHandlers();
            RegistryTraceSink.ClearHandlers();
            mRecordersAttached = false;
        }
    }
}

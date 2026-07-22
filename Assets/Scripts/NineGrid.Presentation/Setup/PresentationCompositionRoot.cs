using System;
using NineGrid.Core;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using QFramework;

namespace NineGrid.Presentation.Setup
{
    public sealed class PresentationCompositionRoot
    {
        private IPresentationRuntimeSystem mRuntime;

        public IPresentationRuntimeSystem Install(
            IIntentScriptFactory scriptFactory,
            IUiPickPreviewSink uiPickPreview = null,
            ITimelineDiagnosticSink timelineDiagnostics = null)
        {
            if (mRuntime != null)
            {
                throw new InvalidOperationException("Presentation composition root is already installed.");
            }

            var architecture = NineGridArchitecture.Interface;
            var existing = architecture.GetSystem<IPresentationRuntimeSystem>();
            if (existing != null && existing.IsStarted)
            {
                // 冲突时不接管既有运行时：保持 mRuntime 为 null，避免丢弃一个
                // 并非本组合根创建、仍在运行的 System 引用。
                throw new InvalidOperationException("Presentation runtime is already registered and started.");
            }

            if (existing == null)
            {
                existing = new PresentationRuntimeSystem();
                architecture.RegisterSystem(existing);
            }

            existing.Start(scriptFactory, uiPickPreview, timelineDiagnostics);
            mRuntime = existing;
            return mRuntime;
        }

        public void Shutdown(IntentClearReason reason)
        {
            if (mRuntime == null) return;
            mRuntime.Stop(reason);
            mRuntime = null;
        }
    }
}

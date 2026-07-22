using System;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Setup;
using NineGrid.Presentation.Systems;
using QFramework;

namespace NineGrid.Presentation.Tests.Fixtures
{
    /// <summary>
    /// 迁移票复用：经 CompositionRoot 安装 Runtime，提供 Tick / TickUntilIdle。
    /// </summary>
    public sealed class PresentationRuntimeFixture : IDisposable
    {
        private readonly PresentationCompositionRoot mRoot;

        public IPresentationRuntimeSystem Runtime { get; private set; }
        public RecordingScriptFactory ScriptFactory { get; private set; }
        public IReadonlyBindableProperty<bool> MainlineBusy
        {
            get { return Runtime.MainlineBusy; }
        }

        private PresentationRuntimeFixture(
            PresentationCompositionRoot root,
            IPresentationRuntimeSystem runtime,
            RecordingScriptFactory scriptFactory)
        {
            mRoot = root;
            Runtime = runtime;
            ScriptFactory = scriptFactory;
        }

        public static PresentationRuntimeFixture Install(
            PresentationArchitectureFixture architecture,
            IIntentScriptFactory scriptFactory,
            IUiPickPreviewSink uiPickPreview = null)
        {
            if (architecture == null)
            {
                throw new ArgumentNullException("architecture");
            }

            var root = new PresentationCompositionRoot();
            var runtime = root.Install(scriptFactory, uiPickPreview);
            return new PresentationRuntimeFixture(
                root,
                runtime,
                scriptFactory as RecordingScriptFactory);
        }

        public static PresentationRuntimeFixture Install(
            PresentationArchitectureFixture architecture,
            RecordingScriptFactory scriptFactory,
            out RecordingUiPickSink uiPick)
        {
            uiPick = new RecordingUiPickSink();
            return Install(architecture, scriptFactory, uiPick);
        }

        public bool TrySubmitIntent(InputIntent intent, out bool uiPickPreview)
        {
            return Runtime.TrySubmitIntent(intent, out uiPickPreview);
        }

        public void Tick(float deltaTime = 0.016f)
        {
            Runtime.Tick(deltaTime);
        }

        public void TickUntilIdle(int maxTicks = 64, float deltaTime = 0.016f)
        {
            for (var i = 0; i < maxTicks && Runtime.MainlineBusy.Value; i++)
            {
                Runtime.Tick(deltaTime);
            }

            if (Runtime.MainlineBusy.Value)
            {
                throw new TimeoutException(
                    "Presentation runtime remained busy after " + maxTicks + " ticks.");
            }
        }

        public void Dispose()
        {
            if (mRoot != null)
            {
                mRoot.Shutdown(IntentClearReason.LayerChange);
            }

            Runtime = null;
            ScriptFactory = null;
        }
    }
}

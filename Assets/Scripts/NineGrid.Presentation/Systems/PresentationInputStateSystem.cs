using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Flow.Diagnostics;
using NineGrid.Flow.Presentation;
using QFramework;

namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// 输入所有权轴只读投影 + Opening/Overlay/BoardSelect 暂持源（写入仅经 Command）。
    /// </summary>
    public sealed class PresentationInputStateSystem : AbstractSystem, IPresentationInputStateSystem
    {
        private readonly BindableProperty<bool> mOpening = new BindableProperty<bool>(false);
        private readonly BindableProperty<bool> mChoiceOverlay = new BindableProperty<bool>(false);
        private readonly BindableProperty<bool> mBoardSelect = new BindableProperty<bool>(false);

        public InputOwner CurrentOwner
        {
            get
            {
                if (mChoiceOverlay.Value)
                {
                    return InputOwner.ChoiceOverlay;
                }

                if (mOpening.Value)
                {
                    return InputOwner.Opening;
                }

                if (mBoardSelect.Value || BoardCardSelectModeController.IsActive)
                {
                    return InputOwner.BoardSelect;
                }

                return InputOwner.ProtectedField;
            }
        }

        public IReadonlyBindableProperty<bool> OpeningPresentationActive => mOpening;
        public IReadonlyBindableProperty<bool> ChoiceOverlayActive => mChoiceOverlay;
        public IReadonlyBindableProperty<bool> BoardSelectModeActive => mBoardSelect;

        public bool MainlineBusy
        {
            get
            {
                var runtime = ResolveRuntime();
                return runtime != null && runtime.IsStarted && runtime.MainlineBusy.Value;
            }
        }

        public bool HasExternalHold
        {
            get
            {
                var runtime = ResolveRuntime();
                return runtime != null && runtime.IsStarted && runtime.HasExternalHold;
            }
        }

        private IPresentationRuntimeSystem ResolveRuntime()
        {
            var runtime = this.GetSystem<IPresentationRuntimeSystem>();
            if (runtime != null)
            {
                return runtime;
            }

            return NineGridArchitecture.Interface?.GetSystem<IPresentationRuntimeSystem>();
        }

        public void SetOpeningPresentationActive(bool active)
        {
            mOpening.Value = active;
        }

        public void SetChoiceOverlayActive(bool active)
        {
            mChoiceOverlay.Value = active;
        }

        public void SetBoardSelectModeActive(bool active)
        {
            mBoardSelect.Value = active;
        }

        public void ResetGates(string reason = null)
        {
            mOpening.Value = false;
            mChoiceOverlay.Value = false;
            mBoardSelect.Value = false;
            ChoreoTraceContext.ClearOccupancyDesyncLatch(reason ?? "ResetInputGates");

            var runtime = ResolveRuntime();
            if (runtime != null && runtime.IsStarted)
            {
                runtime.ForceEndExternalHold(reason ?? "ResetInputGates");
            }
        }

        protected override void OnInit()
        {
        }

        protected override void OnDeinit()
        {
            ResetGates("OnDeinit");
        }

        /// <summary>测试 / 宿主懒注册。</summary>
        public static IPresentationInputStateSystem EnsureRegistered(IArchitecture architecture = null)
        {
            var arch = architecture ?? NineGridArchitecture.Interface;
            if (arch == null)
            {
                return null;
            }

            var existing = arch.GetSystem<IPresentationInputStateSystem>();
            if (existing != null)
            {
                return existing;
            }

            IPresentationInputStateSystem created = new PresentationInputStateSystem();
            arch.RegisterSystem(created);
            return created;
        }
    }
}

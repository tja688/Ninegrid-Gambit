using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Flow.Diagnostics;
using NineGrid.Flow.Presentation;
using QFramework;

namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// 分型只读输入门禁：派生投影 + Opening/Overlay 暂持源（写入仅经 Command）。
    /// </summary>
    public sealed class PresentationInputStateSystem : AbstractSystem, IPresentationInputStateSystem
    {
        private readonly BindableProperty<bool> mOpening = new BindableProperty<bool>(false);
        private readonly BindableProperty<bool> mChoiceOverlay = new BindableProperty<bool>(false);
        private readonly BindableProperty<bool> mBoardSelect = new BindableProperty<bool>(false);

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

        public PresentationInputGateResult EvaluateExplore()
        {
            if (ChoreoTraceContext.OccupancyDesyncLatched)
            {
                return PresentationInputGateResult.Reject("occupancyDesync");
            }

            if (mChoiceOverlay.Value)
            {
                return PresentationInputGateResult.Reject("choiceOverlay");
            }

            if (mOpening.Value)
            {
                return PresentationInputGateResult.Reject("openingDeal");
            }

            if (mBoardSelect.Value || BoardCardSelectModeController.IsActive)
            {
                return PresentationInputGateResult.Reject("boardSelect");
            }

            if (MainlineBusy)
            {
                return PresentationInputGateResult.BufferToDirector("mainlineBusy");
            }

            return PresentationInputGateResult.Allow();
        }

        public PresentationInputGateResult EvaluateAttack()
        {
            if (ChoreoTraceContext.OccupancyDesyncLatched)
            {
                return PresentationInputGateResult.Reject("occupancyDesync");
            }

            if (mChoiceOverlay.Value)
            {
                return PresentationInputGateResult.Reject("choiceOverlay");
            }

            if (mBoardSelect.Value || BoardCardSelectModeController.IsActive)
            {
                return PresentationInputGateResult.Reject("boardSelect");
            }

            if (MainlineBusy)
            {
                return PresentationInputGateResult.Reject("mainlineBusy");
            }

            var battle = this.GetSystem<IFieldBattlePresentationSystem>()
                ?? NineGridArchitecture.Interface?.GetSystem<IFieldBattlePresentationSystem>();
            if (battle != null && battle.IsBusy)
            {
                return PresentationInputGateResult.Reject("battleBusy");
            }

            var ground = this.GetSystem<IGroundFieldGeometrySystem>()
                ?? NineGridArchitecture.Interface?.GetSystem<IGroundFieldGeometrySystem>();
            if (ground != null && ground.IsFieldBusy)
            {
                return PresentationInputGateResult.Reject("fieldBusy");
            }

            return PresentationInputGateResult.Allow();
        }

        public PresentationInputGateResult EvaluatePickup()
        {
            if (ChoreoTraceContext.OccupancyDesyncLatched)
            {
                return PresentationInputGateResult.Reject("occupancyDesync");
            }

            if (mOpening.Value)
            {
                return PresentationInputGateResult.Reject("openingDeal");
            }

            if (mChoiceOverlay.Value)
            {
                return PresentationInputGateResult.Reject("choiceOverlay");
            }

            if (mBoardSelect.Value || BoardCardSelectModeController.IsActive)
            {
                return PresentationInputGateResult.Reject("boardSelect");
            }

            if (HasExternalHold || MainlineBusy)
            {
                return PresentationInputGateResult.Reject("mainlineBusy");
            }

            return PresentationInputGateResult.Allow();
        }

        public PresentationInputGateResult EvaluateUseItem()
        {
            if (ChoreoTraceContext.OccupancyDesyncLatched)
            {
                return PresentationInputGateResult.Reject("occupancyDesync");
            }

            if (mChoiceOverlay.Value)
            {
                return PresentationInputGateResult.Reject("choiceOverlay");
            }

            if (mBoardSelect.Value || BoardCardSelectModeController.IsActive)
            {
                return PresentationInputGateResult.RouteToBoardSelect();
            }

            if (MainlineBusy)
            {
                return PresentationInputGateResult.BufferToDirector("mainlineBusy");
            }

            return PresentationInputGateResult.Allow();
        }

        public PresentationInputGateResult EvaluateBoardSelectionBegin()
        {
            if (ChoreoTraceContext.OccupancyDesyncLatched)
            {
                return PresentationInputGateResult.Reject("occupancyDesync");
            }

            if (mChoiceOverlay.Value)
            {
                return PresentationInputGateResult.Reject("choiceOverlay");
            }

            if (MainlineBusy)
            {
                return PresentationInputGateResult.Reject("mainlineBusy");
            }

            if (mBoardSelect.Value || BoardCardSelectModeController.IsActive)
            {
                return PresentationInputGateResult.Reject("boardSelectActive");
            }

            return PresentationInputGateResult.Allow();
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

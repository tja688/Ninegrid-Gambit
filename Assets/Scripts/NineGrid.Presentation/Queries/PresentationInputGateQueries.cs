using NineGrid.Presentation.Systems;
using QFramework;

namespace NineGrid.Presentation.Queries
{
    public sealed class EvaluateExploreInputGateQuery : AbstractQuery<PresentationInputGateResult>
    {
        protected override PresentationInputGateResult OnDo()
        {
            var input = this.GetSystem<IPresentationInputStateSystem>()
                ?? PresentationInputStateSystem.EnsureRegistered();
            return input != null
                ? input.EvaluateExplore()
                : PresentationInputGateResult.Reject("noInputState");
        }
    }

    public sealed class EvaluateAttackInputGateQuery : AbstractQuery<PresentationInputGateResult>
    {
        protected override PresentationInputGateResult OnDo()
        {
            var input = this.GetSystem<IPresentationInputStateSystem>()
                ?? PresentationInputStateSystem.EnsureRegistered();
            return input != null
                ? input.EvaluateAttack()
                : PresentationInputGateResult.Reject("noInputState");
        }
    }

    public sealed class EvaluatePickupInputGateQuery : AbstractQuery<PresentationInputGateResult>
    {
        protected override PresentationInputGateResult OnDo()
        {
            var input = this.GetSystem<IPresentationInputStateSystem>()
                ?? PresentationInputStateSystem.EnsureRegistered();
            return input != null
                ? input.EvaluatePickup()
                : PresentationInputGateResult.Reject("noInputState");
        }
    }

    public sealed class EvaluateUseItemInputGateQuery : AbstractQuery<PresentationInputGateResult>
    {
        protected override PresentationInputGateResult OnDo()
        {
            var input = this.GetSystem<IPresentationInputStateSystem>()
                ?? PresentationInputStateSystem.EnsureRegistered();
            return input != null
                ? input.EvaluateUseItem()
                : PresentationInputGateResult.Reject("noInputState");
        }
    }

    public sealed class EvaluateBoardSelectionBeginGateQuery : AbstractQuery<PresentationInputGateResult>
    {
        protected override PresentationInputGateResult OnDo()
        {
            var input = this.GetSystem<IPresentationInputStateSystem>()
                ?? PresentationInputStateSystem.EnsureRegistered();
            return input != null
                ? input.EvaluateBoardSelectionBegin()
                : PresentationInputGateResult.Reject("noInputState");
        }
    }
}

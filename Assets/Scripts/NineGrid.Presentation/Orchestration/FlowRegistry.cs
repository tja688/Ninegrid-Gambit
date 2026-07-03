using System.Collections.Generic;

namespace NineGrid.Presentation.Orchestration
{
    public sealed class FlowRegistry
    {
        private readonly Dictionary<FlowId, IFlowBinding> mBindings = new Dictionary<FlowId, IFlowBinding>();

        public void Register(IFlowBinding binding)
        {
            if (binding == null)
            {
                return;
            }

            mBindings[binding.Id] = binding;
        }

        public bool TryGet(FlowId flowId, out IFlowBinding binding)
        {
            return mBindings.TryGetValue(flowId, out binding);
        }

        public IFlowBinding Get(FlowId flowId)
        {
            IFlowBinding binding;
            if (!TryGet(flowId, out binding))
            {
                throw new KeyNotFoundException("Flow binding not registered: " + flowId);
            }

            return binding;
        }

        public void WrapAll(System.Func<IFlowBinding, IFlowBinding> wrapper)
        {
            if (wrapper == null)
            {
                return;
            }

            var keys = new List<FlowId>(mBindings.Keys);
            for (var i = 0; i < keys.Count; i++)
            {
                FlowId flowId = keys[i];
                if (mBindings.TryGetValue(flowId, out IFlowBinding binding))
                {
                    mBindings[flowId] = wrapper(binding);
                }
            }
        }
    }
}

using System.Collections.Generic;

namespace NineGrid.Presentation.Orchestration
{
    public sealed class ReactionRegistry
    {
        private readonly Dictionary<ReactionId, IReactionBinding> mBindings = new Dictionary<ReactionId, IReactionBinding>();

        public void Register(IReactionBinding binding)
        {
            if (binding == null)
            {
                return;
            }

            mBindings[binding.Id] = binding;
        }

        public bool TryGet(ReactionId reactionId, out IReactionBinding binding)
        {
            return mBindings.TryGetValue(reactionId, out binding);
        }

        public IReactionBinding Get(ReactionId reactionId)
        {
            IReactionBinding binding;
            if (!TryGet(reactionId, out binding))
            {
                throw new KeyNotFoundException("Reaction binding not registered: " + reactionId);
            }

            return binding;
        }
    }
}

using System.Collections.Generic;

namespace NineGrid.Content
{
    public sealed class VisualAssetCatalog
    {
        private readonly Dictionary<string, VisualAssetDefinition> mEntries =
            new Dictionary<string, VisualAssetDefinition>();

        public IReadOnlyDictionary<string, VisualAssetDefinition> Entries => mEntries;

        public bool TryGet(string visualId, out VisualAssetDefinition definition)
        {
            return mEntries.TryGetValue(visualId ?? string.Empty, out definition);
        }

        public VisualAssetCatalog Add(VisualAssetDefinition definition)
        {
            if (definition == null)
            {
                return this;
            }

            mEntries[definition.VisualId] = definition;
            return this;
        }
    }
}

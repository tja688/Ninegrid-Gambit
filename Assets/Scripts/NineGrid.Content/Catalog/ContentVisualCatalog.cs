using System.Collections.Generic;

namespace NineGrid.Content
{
    public sealed class ContentVisualCatalog
    {
        private readonly Dictionary<string, ContentVisualDefinition> mEntries =
            new Dictionary<string, ContentVisualDefinition>();

        public IReadOnlyDictionary<string, ContentVisualDefinition> Entries
        {
            get { return mEntries; }
        }

        public bool TryGet(string contentId, out ContentVisualDefinition definition)
        {
            return mEntries.TryGetValue(contentId ?? string.Empty, out definition);
        }

        public ContentVisualCatalog Add(ContentVisualDefinition definition)
        {
            if (definition == null)
            {
                return this;
            }

            mEntries[definition.ContentId] = definition;
            return this;
        }
    }
}

using System.Collections.Generic;

namespace NineGrid.Content
{
    public sealed class CardFrameStyleCatalog
    {
        public const string StyleWhite = "frame.white";
        public const string StyleBlue = "frame.blue";
        public const string StyleGold = "frame.gold";
        public const string StyleRed = "frame.red";
        public const string StyleNormal = "frame.normal";
        public const string StyleElite = "frame.elite";
        public const string StyleBoss = "frame.boss";

        public static readonly string[] RequiredStyleIds =
        {
            StyleWhite,
            StyleBlue,
            StyleGold,
            StyleRed,
            StyleNormal,
            StyleElite,
            StyleBoss
        };

        private readonly Dictionary<string, CardFrameStyleDefinition> mEntries =
            new Dictionary<string, CardFrameStyleDefinition>();

        public IReadOnlyDictionary<string, CardFrameStyleDefinition> Entries => mEntries;

        public bool TryGet(string styleId, out CardFrameStyleDefinition definition)
        {
            return mEntries.TryGetValue(styleId ?? string.Empty, out definition);
        }

        public CardFrameStyleCatalog Add(CardFrameStyleDefinition definition)
        {
            if (definition == null)
            {
                return this;
            }

            mEntries[definition.StyleId] = definition;
            return this;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Presentation.Visuals
{
    [CreateAssetMenu(
        fileName = "StripSpriteVisualCatalog",
        menuName = "NineGrid/Strip Sprite Visual Catalog")]
    public sealed class StripSpriteVisualCatalog : ScriptableObject
    {
        [SerializeField] private List<Entry> entries = new();

        public IReadOnlyList<Entry> Entries => entries;

        public bool TryGetEntry(string visualId, out Entry entry)
        {
            if (string.IsNullOrEmpty(visualId))
            {
                entry = null;
                return false;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].Id == visualId)
                {
                    entry = entries[i];
                    return true;
                }
            }

            entry = null;
            return false;
        }

        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string id;
            [SerializeField] private string displayName;
            [SerializeField] private RuntimeAnimatorController animatorController;
            [SerializeField] private Sprite previewSprite;

            public string Id => id;
            public string DisplayName => string.IsNullOrEmpty(displayName) ? id : displayName;
            public RuntimeAnimatorController AnimatorController => animatorController;
            public Sprite PreviewSprite => previewSprite;
        }
    }
}

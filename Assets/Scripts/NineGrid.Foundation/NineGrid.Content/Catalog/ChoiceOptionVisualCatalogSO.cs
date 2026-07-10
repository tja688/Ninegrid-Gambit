using UnityEngine;

namespace NineGrid.Content
{
    [CreateAssetMenu(
        fileName = "ChoiceOptionVisualCatalog",
        menuName = "NineGrid/Content/Choice Option Visual Catalog")]
    public sealed class ChoiceOptionVisualCatalogSO : ContentVisualSpriteCatalogSO
    {
        public override ContentVisualKind Kind
        {
            get { return ContentVisualKind.ChoiceOption; }
        }
    }
}

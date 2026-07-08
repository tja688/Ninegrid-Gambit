using UnityEngine;

namespace NineGrid.Content
{
    [CreateAssetMenu(
        fileName = "SkillVisualCatalog",
        menuName = "NineGrid/Content/Skill Visual Catalog")]
    public sealed class SkillVisualCatalogSO : ContentVisualSpriteCatalogSO
    {
        public override ContentVisualKind Kind
        {
            get { return ContentVisualKind.Skill; }
        }
    }
}

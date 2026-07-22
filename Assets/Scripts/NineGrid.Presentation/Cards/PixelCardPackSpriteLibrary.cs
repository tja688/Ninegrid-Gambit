using UnityEngine;

namespace NineGrid.Cards
{
    [CreateAssetMenu(fileName = "PixelCardPackSpriteLibrary", menuName = "NineGrid/Cards/Pixel Card Pack Sprites")]
    public sealed class PixelCardPackSpriteLibrary : ScriptableObject
    {
        public const string PackPath = "Assets/Arts/Images/Multiple/pixel_art_card_pack.png";

        [SerializeField] Sprite[] digits = new Sprite[10];
        [SerializeField] Sprite armorBlockSprite;

        public Sprite GetDigit(int value)
        {
            if (digits == null || digits.Length == 0)
            {
                return null;
            }

            var index = Mathf.Clamp(value, 0, digits.Length - 1);
            return digits[index];
        }

        public Sprite ArmorBlockSprite => armorBlockSprite;

#if UNITY_EDITOR
        public void ReloadFromPack()
        {
            var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(PackPath);
            var loadedDigits = new Sprite[10];
            Sprite armor = null;

            foreach (var asset in assets)
            {
                if (asset is not Sprite sprite)
                {
                    continue;
                }

                if (TryParseDigitIndex(sprite.name, out var digitIndex))
                {
                    loadedDigits[digitIndex] = sprite;
                    continue;
                }

                if (sprite.name == "pixel_art_card_pack_155")
                {
                    armor = sprite;
                }
            }

            digits = loadedDigits;
            armorBlockSprite = armor;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        private static bool TryParseDigitIndex(string spriteName, out int index)
        {
            const string prefix = "pixel_art_card_pack_";
            if (!spriteName.StartsWith(prefix))
            {
                index = -1;
                return false;
            }

            var suffix = spriteName.Substring(prefix.Length);
            if (!int.TryParse(suffix, out var spriteIndex) || spriteIndex < 144 || spriteIndex > 153)
            {
                index = -1;
                return false;
            }

            index = spriteIndex - 144;
            return true;
        }

        private void OnValidate()
        {
            if (digits == null || digits.Length != 10)
            {
                digits = new Sprite[10];
            }
        }
#endif
    }
}

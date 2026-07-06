#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace NineGrid.Presentation.Cards.Editor
{
    public static class PixelCardPackSpriteLibraryMenu
    {
        private const string AssetPath = "Assets/Scripts/NineGrid.Presentation/Cards/PixelCardPackSpriteLibrary.asset";

        [MenuItem("NineGrid/Cards/Create Pixel Card Pack Sprite Library")]
        public static void CreateOrReloadLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<PixelCardPackSpriteLibrary>(AssetPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<PixelCardPackSpriteLibrary>();
                AssetDatabase.CreateAsset(library, AssetPath);
            }

            library.ReloadFromPack();
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = library;
        }
    }
}
#endif

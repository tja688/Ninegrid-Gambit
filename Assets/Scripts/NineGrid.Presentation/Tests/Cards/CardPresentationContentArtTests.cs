using System.Collections.Generic;
using NineGrid.Content.CardPresentation;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests.Cards
{
    public sealed class CardPresentationContentArtTests
    {
        [Test]
        public void RewriteLegacyArtsImagesPath_MovesUnderContentArtRoot_KeepsSubSprite()
        {
            var rewritten = CardPresentationContentArt.RewriteLegacyArtsImagesPath(
                "Assets/Arts/Images/Multiple/icons_full_32.png#icons_full_32_210");
            Assert.AreEqual(
                "Assets/Resources/ContentArt/Multiple/icons_full_32.png#icons_full_32_210",
                rewritten);
            Assert.IsTrue(CardPresentationContentArt.IsUnderContentArtRoot(rewritten));
        }

        [Test]
        public void RewriteLegacyArtsImagesPath_AlreadyUnderRoot_Unchanged()
        {
            const string path = "Assets/Resources/ContentArt/Png/Icons/white_node/pawn.png";
            Assert.AreEqual(path, CardPresentationContentArt.RewriteLegacyArtsImagesPath(path));
        }

        [Test]
        public void TryGetResourcesRelativeKey_StripsExtensionAndSubSprite()
        {
            Assert.IsTrue(CardPresentationContentArt.TryGetResourcesRelativeKey(
                "Assets/Resources/ContentArt/Multiple/icons_full_32.png#icons_full_32_42",
                out var key));
            Assert.AreEqual("ContentArt/Multiple/icons_full_32", key);
        }

        [Test]
        public void CollectAssetPathEntries_SkipsNoneAnimAndEmptySprites()
        {
            var dto = CardPresentationJsonIO.CreateDefault("help.demo", "HelpCard");
            dto.sprites.mainIcon = "Assets/Resources/ContentArt/a.png";
            dto.animations.slots[0].sourceType = "folder";
            dto.animations.slots[0].path = "Assets/Resources/ContentArt/idle";
            dto.animations.slots[1].sourceType = "none";
            dto.animations.slots[1].path = "Assets/Resources/ContentArt/should_skip";

            var entries = new List<CardPresentationAssetPathEntry>();
            CardPresentationContentArt.CollectAssetPathEntries(dto, entries);

            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual("sprites.mainIcon", entries[0].Field);
            Assert.AreEqual("sprite", entries[0].Kind);
            Assert.AreEqual("folder", entries[1].Kind);
            Assert.AreEqual("Assets/Resources/ContentArt/idle", entries[1].Path);
        }
    }
}

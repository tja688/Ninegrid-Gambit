using System.Collections.Generic;
using NineGrid.Cards.Anim;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Content.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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

    public sealed class VisualEffectCatalogTests
    {
        [Test]
        public void RewriteLegacyEffectsPath_MovesUnderContentArtEffectsRoot()
        {
            var rewritten = VisualEffectContentArt.RewriteLegacyEffectsPath(
                "Assets/Arts/Images/Multiple/Effects/Explosions/epic_explosion_001/epic_explosion_001_large_orange/spritesheet.png");
            Assert.AreEqual(
                "Assets/Resources/ContentArt/Multiple/Effects/Explosions/epic_explosion_001/epic_explosion_001_large_orange/spritesheet.png",
                rewritten);
            Assert.IsTrue(VisualEffectContentArt.IsUnderEffectsRoot(rewritten));
        }

        [Test]
        public void TryParseLeaf_ExtractsSizeAndColor()
        {
            Assert.IsTrue(VisualEffectCatalogEditorIO.TryParseLeaf(
                "epic_explosion_001",
                "epic_explosion_001_large_orange",
                out var size,
                out var color));
            Assert.AreEqual("large", size);
            Assert.AreEqual("orange", color);
        }

        [Test]
        public void BuildId_UsesLowerCategoryAndLeaf()
        {
            var id = VisualEffectCatalogEditorIO.BuildId("Magic Bursts", "round_sparkle_burst_001", "small", "blue");
            Assert.AreEqual("vfx.magic_bursts.round_sparkle_burst_001.small_blue", id);
        }

        [Test]
        public void ScanDiskEntries_FindsExplosions()
        {
            var entries = VisualEffectCatalogEditorIO.ScanDiskEntries();
            Assert.Greater(entries.Count, 0, "Effects tree should yield spritesheet entries");
            var hasExplosions = false;
            for (var i = 0; i < entries.Count; i++)
            {
                if (string.Equals(entries[i].category, "Explosions", System.StringComparison.OrdinalIgnoreCase))
                {
                    hasExplosions = true;
                    break;
                }
            }

            Assert.IsTrue(hasExplosions);
        }

        [Test]
        public void ScanAndMerge_PreservesStickyOverrides()
        {
            var scanned = VisualEffectCatalogEditorIO.ScanDiskEntries();
            Assert.Greater(scanned.Count, 0);

            var first = scanned[0];
            var sticky = new VisualEffectCatalogEditorIO.EditorRow
            {
                Dto = new VisualEffectEntryDto
                {
                    id = first.id,
                    category = first.category,
                    variantId = first.variantId,
                    size = first.size,
                    color = first.color,
                    sheetPath = "Assets/Resources/ContentArt/Multiple/Effects/placeholder.png",
                    defaultFps = 24f,
                    defaultScale = 2.5f,
                    displayName = "custom-name",
                    timingBindings = new[] { "on_impact" },
                },
            };
            sticky.MarkSaved();

            var merged = VisualEffectCatalogEditorIO.ScanAndMerge(new[] { sticky }, out _);
            VisualEffectEntryDto found = null;
            for (var i = 0; i < merged.Count; i++)
            {
                if (merged[i].Id == first.id)
                {
                    found = merged[i].Dto;
                    break;
                }
            }

            Assert.IsNotNull(found);
            Assert.AreEqual(24f, found.defaultFps, 0.001f);
            Assert.AreEqual(2.5f, found.defaultScale, 0.001f);
            Assert.AreEqual("custom-name", found.displayName);
            Assert.AreEqual(1, found.timingBindings.Length);
            Assert.AreEqual("on_impact", found.timingBindings[0]);
            Assert.AreEqual(first.sheetPath, found.sheetPath);
        }

        [Test]
        public void LoadFrames_Atlas_FromScannedSheet_Succeeds()
        {
            var entries = VisualEffectCatalogEditorIO.ScanDiskEntries();
            Assert.Greater(entries.Count, 0);

            string sheetPath = null;
            for (var i = 0; i < entries.Count; i++)
            {
                if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(entries[i].sheetPath)))
                {
                    sheetPath = entries[i].sheetPath;
                    break;
                }
            }

            Assert.IsFalse(string.IsNullOrEmpty(sheetPath));
            var frames = CardAnimFrameSource.LoadFrames("atlas", sheetPath);
            Assert.Greater(frames.Length, 0, sheetPath);
        }

        [Test]
        public void JsonRoundTrip_FingerprintStable()
        {
            var dto = new VisualEffectEntryDto
            {
                id = "vfx.explosions.demo.large_orange",
                category = "Explosions",
                variantId = "demo",
                size = "large",
                color = "orange",
                sheetPath = "Assets/Resources/ContentArt/Multiple/Effects/demo/spritesheet.png",
                defaultFps = 12f,
                defaultScale = 1f,
                displayName = "demo",
                timingBindings = System.Array.Empty<string>(),
            };
            var json = JsonUtility.ToJson(dto);
            var back = JsonUtility.FromJson<VisualEffectEntryDto>(json);
            VisualEffectCatalog.Normalize(back);
            Assert.AreEqual(dto.id, back.id);
            Assert.AreEqual(dto.sheetPath, back.sheetPath);
            Assert.AreEqual(dto.defaultFps, back.defaultFps, 0.001f);
        }

        [Test]
        public void CategoryLabel_MapsKnownFolders()
        {
            Assert.AreEqual("爆炸", VisualEffectCatalogEditorIO.GetCategoryLabel("Explosions"));
            Assert.AreEqual("冲击", VisualEffectCatalogEditorIO.GetCategoryLabel("Impacts"));
        }
    }
}

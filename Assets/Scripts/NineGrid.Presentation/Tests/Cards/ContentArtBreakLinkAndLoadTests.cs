using System.IO;
using NineGrid.Cards.Anim;
using NineGrid.Content.CardPresentation;
using NineGrid.Content.Editor;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.Cards
{
    public sealed class ContentArtBreakLinkAndLoadTests
    {
        [Test]
        public void TryResolve_DeliberateBadSpritePath_Fails()
        {
            var entry = new CardPresentationAssetPathEntry
            {
                ContentId = "test.bad",
                Field = "sprites.mainIcon",
                Kind = "sprite",
                Path = "Assets/Resources/ContentArt/__missing_sprite_for_breaklink__.png"
            };

            Assert.IsFalse(ContentArtBreakLinkValidator.TryResolve(entry, out var reason));
            Assert.IsFalse(string.IsNullOrEmpty(reason));
        }

        [Test]
        public void TryResolve_DeliberateBadFolderPath_Fails()
        {
            var entry = new CardPresentationAssetPathEntry
            {
                ContentId = "test.bad",
                Field = "animations.slots[0].idle",
                Kind = "folder",
                Path = "Assets/Resources/ContentArt/__missing_folder_for_breaklink__"
            };

            Assert.IsFalse(ContentArtBreakLinkValidator.TryResolve(entry, out var reason));
            Assert.IsFalse(string.IsNullOrEmpty(reason));
        }

        [Test]
        public void ValidateAuthoringCards_AfterMigration_AllGreen()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var contentArtAbs = Path.GetFullPath(
                Path.Combine(
                    projectRoot,
                    CardPresentationContentArt.RootAssetFolder.Replace('/', Path.DirectorySeparatorChar)));
            Assume.That(Directory.Exists(contentArtAbs), "ContentArt root must exist after migration.");

            var findings = ContentArtBreakLinkValidator.ValidateAuthoringCards();
            if (findings.Count > 0)
            {
                var sample = findings[0];
                Assert.Fail(
                    "Break-link failures=" + findings.Count
                    + " sample=" + sample.ContentId + " " + sample.Field + " → " + sample.Path
                    + " | " + sample.Reason);
            }
        }

        [Test]
        public void LoadSprite_KnownContentArtMainIcon_NonNull()
        {
            const string path =
                "Assets/Resources/ContentArt/Multiple/icons_full_32.png#icons_full_32_210";
            Assume.That(
                CardPresentationContentArt.IsUnderContentArtRoot(path),
                "path convention");

            var sprite = CardPresentationSpritePath.LoadSprite(path);
            Assume.That(sprite != null || AssetExistsForAssume(path),
                "asset must be migrated before this assertion");
            Assert.IsNotNull(sprite);
            Assert.AreEqual("icons_full_32_210", sprite.name);
        }

        [Test]
        public void LoadFramesFromResources_KnownFolder_NonNullDeterministicOrder()
        {
            // 挑一张有 idle folder 的怪物；迁移后路径落在 ContentArt。
            var authoring = CardPresentationJsonIO.GetAuthoringAbsolutePath("monster.beggar");
            if (!File.Exists(authoring))
            {
                Assert.Ignore("monster.beggar presentation JSON missing");
            }

            Assert.IsTrue(CardPresentationJsonIO.TryLoad(authoring, out var dto, out var error), error);
            string folderPath = null;
            for (var i = 0; i < dto.animations.slots.Length; i++)
            {
                var slot = dto.animations.slots[i];
                if (slot != null
                    && CardPresentationAnimResolve.NormalizeSourceType(slot.sourceType) == "folder"
                    && !string.IsNullOrWhiteSpace(slot.path))
                {
                    folderPath = slot.path;
                    break;
                }
            }

            Assume.That(!string.IsNullOrEmpty(folderPath), "beggar should have a folder anim");
            Assume.That(
                CardPresentationContentArt.IsUnderContentArtRoot(folderPath),
                "anim folder must be under ContentArt after migration: " + folderPath);

            var viaPublic = CardAnimFrameSource.LoadFrames("folder", folderPath);
            var viaResources = CardAnimFrameSource.LoadFolderFramesFromResources(folderPath);

            Assert.IsNotNull(viaPublic);
            Assert.Greater(viaPublic.Length, 0, "folder frames should load");
            Assert.IsNotNull(viaResources);
            Assert.Greater(viaResources.Length, 0, "Resources.LoadAll path should load");

            // 帧序确定：按 ExtractFrameIndex / 名排序单调非降。
            for (var i = 1; i < viaResources.Length; i++)
            {
                Assert.LessOrEqual(
                    CardAnimFrameSource.CompareFramePaths(viaResources[i - 1].name, viaResources[i].name),
                    0);
            }
        }

        private static bool AssetExistsForAssume(string pathOrKey)
        {
            CardPresentationSpritePath.SplitPath(pathOrKey, out var path, out _);
#if UNITY_EDITOR
            return !string.IsNullOrEmpty(UnityEditor.AssetDatabase.AssetPathToGUID(path));
#else
            return false;
#endif
        }
    }
}

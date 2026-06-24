using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NineGrid.Content;
using NineGrid.Content.Editor;
using NineGrid.Core.Content;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Core.Tests
{
    public sealed class P6ContentVisualTests
    {
        private static string DataDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.CurrentDirectory,
                    TableNineLubanCatalogFactory.DefaultDataRelativePath);
            }
        }

        [Test]
        public void GeneratedLubanVisualTableLoadsFromStreamingAssets()
        {
            var catalog = TableNineVisualCatalogFactory.CreateFromDirectory(DataDirectory);
            Assert.AreEqual(186, catalog.Entries.Count);
            Assert.IsTrue(catalog.TryGet("help.bomb", out var bomb));
            Assert.AreEqual(ContentVisualKind.HelpCard, bomb.Kind);
            Assert.IsFalse(string.IsNullOrEmpty(bomb.Description));
        }

        [Test]
        public void GeneratedVisualAssetAndFrameStyleTablesLoad()
        {
            var assetCatalog = TableNineVisualAssetCatalogFactory.CreateFromDirectory(DataDirectory);
            var frameCatalog = TableNineCardFrameStyleCatalogFactory.CreateFromDirectory(DataDirectory);
            Assert.IsTrue(assetCatalog.TryGet(VisualIdNaming.MissingSpriteId, out _));
            Assert.IsTrue(frameCatalog.TryGet(CardFrameStyleCatalog.StyleGold, out var gold));
            Assert.Greater(gold.Color.G, 0.9f);
        }

        [Test]
        public void ProductionContentIdsAreCoveredByVisualTable()
        {
            var core = TableNineLubanCatalogFactory.CreateFromDirectory(DataDirectory);
            var visual = TableNineVisualCatalogFactory.CreateFromDirectory(DataDirectory);
            var missing = new List<string>();

            foreach (var id in core.Cards.Keys.OrderBy(value => value))
            {
                if (!visual.TryGet(id, out _))
                {
                    missing.Add("card:" + id);
                }
            }

            foreach (var id in core.Relics.Keys.OrderBy(value => value))
            {
                if (!visual.TryGet(id, out _))
                {
                    missing.Add("relic:" + id);
                }
            }

            foreach (var id in core.Skills.Keys.OrderBy(value => value))
            {
                if (!visual.TryGet(id, out _))
                {
                    missing.Add("skill:" + id);
                }
            }

            foreach (var kind in core.Rewards.Rooms.Keys.OrderBy(value => value.ToString()))
            {
                if (!visual.TryGet(kind.ToString(), out _))
                {
                    missing.Add("room:" + kind);
                }
            }

            foreach (var id in core.MonsterDecks.Keys.OrderBy(value => value))
            {
                if (!visual.TryGet(id, out _))
                {
                    missing.Add("deck:" + id);
                }
            }

            if (!visual.TryGet("avatar.default", out _))
            {
                missing.Add("avatar.default");
            }

            Assert.IsEmpty(missing, string.Join(", ", missing));
        }

        [Test]
        public void ResolverUsesCoreDisplayNameAndVisualDescription()
        {
            var core = TableNineLubanCatalogFactory.CreateFromDirectory(DataDirectory);
            var visual = TableNineVisualCatalogFactory.CreateFromDirectory(DataDirectory);
            var frame = TableNineCardFrameStyleCatalogFactory.CreateFromDirectory(DataDirectory);

            ContentVisualResolvedView view;
            Assert.IsTrue(ContentVisualResolver.TryResolve("help.bomb", core, visual, frame, out view));
            Assert.AreEqual(core.Cards["help.bomb"].DisplayName, view.DisplayName);
            Assert.IsFalse(string.IsNullOrEmpty(view.Description));
            Assert.AreEqual(ContentVisualKind.HelpCard, view.Kind);
        }

        [Test]
        public void ResolverFallsBackToIconConventionWhenIconKeyEmpty()
        {
            var core = TableNineLubanCatalogFactory.CreateFromDirectory(DataDirectory);
            var visual = TableNineVisualCatalogFactory.CreateFromDirectory(DataDirectory);
            var frame = TableNineCardFrameStyleCatalogFactory.CreateFromDirectory(DataDirectory);

            ContentVisualResolvedView view;
            Assert.IsTrue(ContentVisualResolver.TryResolve("relic.craving", core, visual, frame, out view));
            Assert.AreEqual(VisualIdNaming.ForIcon("relic.craving"), view.IconVisualId);
            Assert.AreEqual(
                VisualAssetKeyNaming.FromConvention(ContentVisualKind.Relic, VisualAssetSlot.Icon, "relic.craving"),
                view.IconAssetKey);
        }

        [Test]
        public void ResolverFallsBackMonsterFaceToDeckConvention()
        {
            var core = TableNineLubanCatalogFactory.CreateFromDirectory(DataDirectory);
            var visual = TableNineVisualCatalogFactory.CreateFromDirectory(DataDirectory);
            var frame = TableNineCardFrameStyleCatalogFactory.CreateFromDirectory(DataDirectory);

            ContentVisualResolvedView view;
            Assert.IsTrue(ContentVisualResolver.TryResolve("monster.beggar", core, visual, frame, out view));
            Assert.AreEqual(VisualIdNaming.ForFace("monster.beggar"), view.FaceVisualId);
            Assert.AreEqual(
                VisualAssetKeyNaming.FromConventionFaceDeck(core.Cards["monster.beggar"].DeckId),
                view.FaceAssetKey);
        }

        [Test]
        public void ResolverDerivesFrameStyleFromRarityAndEliteFlags()
        {
            var core = TableNineLubanCatalogFactory.CreateFromDirectory(DataDirectory);
            var visual = TableNineVisualCatalogFactory.CreateFromDirectory(DataDirectory);
            var frame = TableNineCardFrameStyleCatalogFactory.CreateFromDirectory(DataDirectory);

            ContentVisualResolvedView helpView;
            Assert.IsTrue(ContentVisualResolver.TryResolve("help.bomb", core, visual, frame, out helpView));
            Assert.AreEqual(CardFrameStyleCatalog.StyleWhite, helpView.FrameStyleId);

            ContentVisualResolvedView relicView;
            Assert.IsTrue(ContentVisualResolver.TryResolve("relic.craving", core, visual, frame, out relicView));
            Assert.AreEqual(CardFrameStyleCatalog.StyleGold, relicView.FrameStyleId);

            ContentVisualResolvedView bossView;
            Assert.IsTrue(ContentVisualResolver.TryResolve("monster.fire_dragon", core, visual, frame, out bossView));
            Assert.AreEqual(CardFrameStyleCatalog.StyleBoss, bossView.FrameStyleId);
            Assert.AreEqual(frame.Entries[CardFrameStyleCatalog.StyleBoss].Color.R, bossView.FrameColor.R, 0.01f);
        }

        [Test]
        public void VisualAssetValidatorPassesForGeneratedTables()
        {
            var tables = TableNineVisualCatalogFactory.LoadTables(DataDirectory);
            var issues = VisualAssetValidator.Validate(tables);
            var errors = issues.Where(issue => issue.Severity == "error").ToList();
            Assert.IsEmpty(errors, string.Join("; ", errors.Select(issue => issue.Message)));
        }

        [Test]
        public void ContentVisualBootstrapLoadsAllVisualTables()
        {
            var directory = ContentVisualBootstrap.ResolveLubanDataDirectory();
            ContentVisualCatalog visualCatalog;
            VisualAssetCatalog assetCatalog;
            CardFrameStyleCatalog frameCatalog;
            Assert.IsTrue(ContentVisualBootstrap.TryLoadAll(directory, out visualCatalog, out assetCatalog, out frameCatalog));
            Assert.Greater(visualCatalog.Entries.Count, 0);
            Assert.Greater(assetCatalog.Entries.Count, 0);
            Assert.Greater(frameCatalog.Entries.Count, 0);
        }

        [Test]
        public void SpriteKeyCodec_EncodesVisualId()
        {
            var guids = UnityEditor.AssetDatabase.FindAssets("t:Sprite");
            Assert.IsNotEmpty(guids);

            Sprite sprite = null;
            for (var i = 0; i < guids.Length && sprite == null; i++)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
                for (var j = 0; j < assets.Length; j++)
                {
                    var candidate = assets[j] as Sprite;
                    if (candidate != null)
                    {
                        sprite = candidate;
                        break;
                    }
                }
            }

            Assert.IsNotNull(sprite);

            var encoded = ContentVisualSpriteKeyCodec.Encode(
                ContentVisualKeySlot.Icon,
                "help.bomb",
                ContentVisualKind.HelpCard,
                sprite);
            Assert.AreEqual(VisualIdNaming.ForIcon("help.bomb"), encoded);

            var upsert = ContentVisualSpriteKeyCodec.BuildAssetUpsert(
                ContentVisualKeySlot.Icon,
                "help.bomb",
                ContentVisualKind.HelpCard,
                sprite);
            Assert.IsNotNull(upsert);
            Assert.AreEqual(encoded, upsert.VisualId);
            Assert.IsFalse(string.IsNullOrEmpty(upsert.AssetKey));
        }

        [Test]
        public void XlsxPatch_UpdatesOnlyIconFaceColumns()
        {
            var sourcePath = ContentVisualXlsxIO.ResolveAbsolutePath();
            Assert.IsTrue(File.Exists(sourcePath));

            var tempPath = Path.Combine(Path.GetTempPath(), "content_visual_patch_test.xlsx");
            File.Copy(sourcePath, tempPath, true);

            var rows = ContentVisualXlsxIO.ReadAll(tempPath);
            var target = rows.First(row => row.ContentId == "help.bomb");
            var originalDescription = target.Description;
            var patchIcon = VisualIdNaming.ForIcon("help.bomb");

            ContentVisualXlsxIO.PatchVisualKeys(tempPath, new[]
            {
                new ContentVisualXlsxRow
                {
                    ContentId = target.ContentId,
                    SheetRowIndex = target.SheetRowIndex,
                    IconKey = patchIcon,
                    FaceKey = target.FaceKey
                }
            });

            var reloaded = ContentVisualXlsxIO.ReadAll(tempPath);
            var updated = reloaded.First(row => row.ContentId == "help.bomb");
            Assert.AreEqual(patchIcon, updated.IconKey);
            Assert.AreEqual(originalDescription, updated.Description);

            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }

        [Test]
        public void XlsxPatch_PreservesLubanHeaderRows()
        {
            var sourcePath = ContentVisualXlsxIO.ResolveAbsolutePath();
            var tempPath = Path.Combine(Path.GetTempPath(), "content_visual_header_test.xlsx");
            File.Copy(sourcePath, tempPath, true);

            var before = ContentVisualXlsxIO.ReadHeaderRows(tempPath);
            var rows = ContentVisualXlsxIO.ReadAll(tempPath);
            var target = rows[0];
            ContentVisualXlsxIO.PatchVisualKeys(tempPath, new[]
            {
                new ContentVisualXlsxRow
                {
                    ContentId = target.ContentId,
                    SheetRowIndex = target.SheetRowIndex,
                    IconKey = VisualIdNaming.ForIcon(target.ContentId),
                    FaceKey = target.FaceKey
                }
            });

            var after = ContentVisualXlsxIO.ReadHeaderRows(tempPath);
            Assert.AreEqual(before.varRow, after.varRow);
            Assert.IsTrue(after.varRow.StartsWith("##var|"));
            Assert.IsTrue(after.typeRow.StartsWith("##type|"));

            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}

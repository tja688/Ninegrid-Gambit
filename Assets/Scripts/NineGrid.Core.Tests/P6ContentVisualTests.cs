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

            ContentVisualResolvedView view;
            Assert.IsTrue(ContentVisualResolver.TryResolve("help.bomb", core, visual, out view));
            Assert.AreEqual(core.Cards["help.bomb"].DisplayName, view.DisplayName);
            Assert.IsFalse(string.IsNullOrEmpty(view.Description));
            Assert.AreEqual(ContentVisualKind.HelpCard, view.Kind);
        }

        [Test]
        public void ResolverFallsBackToIconConventionWhenIconKeyEmpty()
        {
            var core = TableNineLubanCatalogFactory.CreateFromDirectory(DataDirectory);
            var visual = TableNineVisualCatalogFactory.CreateFromDirectory(DataDirectory);

            ContentVisualResolvedView view;
            Assert.IsTrue(ContentVisualResolver.TryResolve("relic.craving", core, visual, out view));
            Assert.AreEqual(
                ContentVisualResolver.BuildIconConventionPath(ContentVisualKind.Relic, "relic.craving"),
                view.IconKey);
            Assert.AreEqual(view.IconKey, view.IconResourcePath);
        }

        [Test]
        public void ResolverFallsBackMonsterFaceToDeckId()
        {
            var core = TableNineLubanCatalogFactory.CreateFromDirectory(DataDirectory);
            var visual = TableNineVisualCatalogFactory.CreateFromDirectory(DataDirectory);

            ContentVisualResolvedView view;
            Assert.IsTrue(ContentVisualResolver.TryResolve("monster.beggar", core, visual, out view));
            Assert.AreEqual(core.Cards["monster.beggar"].DeckId, view.FaceKey);
        }

        [Test]
        public void ResolverDerivesFrameFromRarityAndEliteFlags()
        {
            var core = TableNineLubanCatalogFactory.CreateFromDirectory(DataDirectory);
            var visual = TableNineVisualCatalogFactory.CreateFromDirectory(DataDirectory);

            ContentVisualResolvedView helpView;
            Assert.IsTrue(ContentVisualResolver.TryResolve("help.bomb", core, visual, out helpView));
            Assert.AreEqual("white_frame", helpView.FrameKey);

            ContentVisualResolvedView relicView;
            Assert.IsTrue(ContentVisualResolver.TryResolve("relic.craving", core, visual, out relicView));
            Assert.AreEqual("gold_frame", relicView.FrameKey);

            ContentVisualResolvedView bossView;
            Assert.IsTrue(ContentVisualResolver.TryResolve("monster.fire_dragon", core, visual, out bossView));
            Assert.AreEqual("boss_frame", bossView.FrameKey);
        }

        [Test]
        public void ContentVisualBootstrapLoadsFromSameDirectoryAsCoreCatalog()
        {
            var directory = ContentVisualBootstrap.ResolveLubanDataDirectory();
            ContentVisualCatalog catalog;
            Assert.IsTrue(ContentVisualBootstrap.TryLoad(directory, out catalog));
            Assert.Greater(catalog.Entries.Count, 0);
        }

        [Test]
        public void SpriteKeyCodec_RoundTripsAssetPathSubsprite()
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

            var encoded = ContentVisualSpriteKeyCodec.Encode(sprite);
            StringAssert.StartsWith("Assets/", encoded);
            StringAssert.Contains("#", encoded);
            StringAssert.EndsWith(sprite.name, encoded);

            Sprite decoded;
            Assert.IsTrue(ContentVisualSpriteKeyCodec.TryDecode(encoded, out decoded));
            Assert.AreEqual(sprite.name, decoded.name);
            Assert.AreEqual(
                UnityEditor.AssetDatabase.GetAssetPath(sprite),
                UnityEditor.AssetDatabase.GetAssetPath(decoded));
        }

        [Test]
        public void XlsxPatch_UpdatesOnlyVisualColumns()
        {
            var sourcePath = ContentVisualXlsxIO.ResolveAbsolutePath();
            Assert.IsTrue(File.Exists(sourcePath));

            var tempPath = Path.Combine(Path.GetTempPath(), "content_visual_patch_test.xlsx");
            File.Copy(sourcePath, tempPath, true);

            var rows = ContentVisualXlsxIO.ReadAll(tempPath);
            var target = rows.First(row => row.ContentId == "help.bomb");
            var originalDescription = target.Description;
            var patchIcon = "Assets/Test/icon.png#test_icon";

            ContentVisualXlsxIO.PatchVisualKeys(tempPath, new[]
            {
                new ContentVisualXlsxRow
                {
                    ContentId = target.ContentId,
                    SheetRowIndex = target.SheetRowIndex,
                    IconKey = patchIcon,
                    FaceKey = target.FaceKey,
                    FrameKey = target.FrameKey
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
                    IconKey = "Assets/Test/patch.png#patch",
                    FaceKey = target.FaceKey,
                    FrameKey = target.FrameKey
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

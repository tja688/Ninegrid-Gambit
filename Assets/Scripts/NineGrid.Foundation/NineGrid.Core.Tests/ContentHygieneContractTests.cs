using System.Collections.Generic;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// #140：内容卫生终验 — 索引条目 ↔ 磁盘生产内容双向一致、Authoring/Streaming 双写一致、
    /// 正式内容无悬空 skillId / 模板 / grant ID、无空壳技能、归档内容不进入正式 grant 路径。
    /// 数据源：Arts/StreamingAssets 磁盘 + 生产 Catalog Bootstrap。
    /// </summary>
    public sealed class ContentHygieneContractTests
    {
        private IArchitecture mArch;
        private GameContentCatalog mCatalog;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            CardPresentationConfigCatalog.Invalidate();
            mCatalog = ContentCatalogBootstrap.Load();
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, mCatalog);
        }

        [TearDown]
        public void TearDown()
        {
            CardPresentationConfigCatalog.Invalidate();
            NineGridArchitecture.ResetForTests();
            mArch = null;
            mCatalog = null;
        }

        [Test]
        public void ProductionIndex_EntriesMatchDiskContentExactly()
        {
            var findings = ContentHygieneValidator.ValidateIndexVsDisk();
            Assert.AreEqual(0, findings.Count, Format(findings));
        }

        [Test]
        public void ProductionAuthoringStreaming_AreIdenticalMirrors()
        {
            var findings = ContentHygieneValidator.ValidateMirror();
            Assert.AreEqual(0, findings.Count, Format(findings));
        }

        [Test]
        public void ProductionSkillIds_AllResolveToSkillJson()
        {
            var findings = ContentHygieneValidator.ValidateSkillLinks();
            Assert.AreEqual(0, findings.Count, Format(findings));
        }

        [Test]
        public void ProductionAssemblies_AllResolveToTemplates_AndTemplateDefIdsExist()
        {
            var findings = ContentHygieneValidator.ValidateAssemblyTemplateLinks();
            Assert.AreEqual(0, findings.Count, Format(findings));

            var bodyFindings = ContentHygieneValidator.ValidateTemplateBodyDefIds();
            Assert.AreEqual(0, bodyFindings.Count, Format(bodyFindings));
        }

        [Test]
        public void ProductionSkills_NoEmptyShells()
        {
            var findings = ContentHygieneValidator.ValidateEmptyShellSkills();
            Assert.AreEqual(0, findings.Count, Format(findings));
        }

        [Test]
        public void ProductionArchiveContent_NotReachableViaGrantPaths()
        {
            var findings = ContentHygieneValidator.ValidateArchiveReachability();
            Assert.AreEqual(0, findings.Count, Format(findings));
        }

        [Test]
        public void ProductionRewardPools_NoDanglingGrantIds_NoArchivedMembers()
        {
            var archived = CollectArchivedIds();
            var catalog = mCatalog;

            foreach (var pair in catalog.Rewards.Pools)
            {
                var pool = pair.Value;
                Assert.IsNotNull(pool, "pool null: " + pair.Key);
                for (var i = 0; i < pool.Entries.Count; i++)
                {
                    var entry = pool.Entries[i];
                    Assert.IsTrue(
                        catalog.Cards.ContainsKey(entry.DefId) || catalog.Relics.ContainsKey(entry.DefId),
                        "pool " + pair.Key + " entry " + entry.DefId + " is dangling (no card/relic in catalog)");
                    Assert.IsFalse(
                        archived.Contains(entry.DefId),
                        "pool " + pair.Key + " entry " + entry.DefId + " is archived content");
                }
            }

            // 房间开局注入的 grant ID 必须存在。
            var roomKinds = new[]
            {
                RoomKind.Elite, RoomKind.Boss, RoomKind.Attribute,
                RoomKind.Fountain, RoomKind.Gold, RoomKind.Shop, RoomKind.Tavern,
                RoomKind.Treasure, RoomKind.TreasureReward, RoomKind.ItemReward,
            };
            for (var r = 0; r < roomKinds.Length; r++)
            {
                if (!catalog.Rewards.TryGetRoom(roomKinds[r], out var room))
                {
                    continue;
                }

                for (var i = 0; i < room.OpeningInjects.Count; i++)
                {
                    var inject = room.OpeningInjects[i];
                    if (!string.IsNullOrWhiteSpace(inject.CardDefId))
                    {
                        Assert.IsTrue(
                            catalog.Cards.ContainsKey(inject.CardDefId) || catalog.Relics.ContainsKey(inject.CardDefId),
                            "room " + roomKinds[r] + " inject " + inject.CardDefId + " is dangling");
                        Assert.IsFalse(archived.Contains(inject.CardDefId), "room " + roomKinds[r]
                            + " inject references archived " + inject.CardDefId);
                    }

                    for (var p = 0; p < inject.Pool.Count; p++)
                    {
                        var option = inject.Pool[p];
                        Assert.IsTrue(
                            catalog.Cards.ContainsKey(option.CardDefId) || catalog.Relics.ContainsKey(option.CardDefId),
                            "room " + roomKinds[r] + " inject pool option " + option.CardDefId + " is dangling");
                        Assert.IsFalse(archived.Contains(option.CardDefId), "room " + roomKinds[r]
                            + " inject pool option references archived " + option.CardDefId);
                    }
                }
            }
        }

        private static HashSet<string> CollectArchivedIds()
        {
            var archived = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var contentId in CardPresentationConfigCatalog.AllContentIds)
            {
                if (!CardPresentationConfigCatalog.TryGet(contentId, out var dto) || dto == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(dto.deckId))
                {
                    continue;
                }

                var deck = dto.deckId.Trim();
                if (string.Equals(deck, "deck.relic_archive", System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(deck, "deck.help_archive", System.StringComparison.OrdinalIgnoreCase))
                {
                    archived.Add(contentId);
                }
            }

            return archived;
        }

        [Test]
        public void IndexValidator_DetectsDuplicateContentIdsOnDisk()
        {
            using (var fixture = new TempCardsFixture())
            {
                fixture.WriteCard("alpha.json", "test.duplicate");
                fixture.WriteCard("beta.json", "test.duplicate");

                var duplicates = CardPresentationIndexIO.FindDuplicateContentIds(fixture.Authoring);
                Assert.AreEqual(1, duplicates.Length);
                Assert.AreEqual("test.duplicate", duplicates[0]);

                var issues = CardPresentationIndexIO.ValidateIndexVsDisk(fixture.Authoring, fixture.Streaming);
                Assert.IsTrue(issues.Exists(i => i.Contains("duplicate contentId on disk test.duplicate")), string.Join("; ", issues));
            }
        }

        [Test]
        public void IndexValidator_DetectsIndexVsDiskDrift()
        {
            using (var fixture = new TempCardsFixture())
            {
                fixture.WriteCard("test_a.json", "test.a");
                fixture.WriteCard("test_b.json", "test.b");
                fixture.WriteIndex("test.a");

                var issues = CardPresentationIndexIO.ValidateIndexVsDisk(fixture.Authoring, fixture.Streaming);
                Assert.IsTrue(issues.Exists(i => i.Contains("missing entry for test.b")), string.Join("; ", issues));
            }
        }

        [Test]
        public void IndexValidator_DetectsAuthoringStreamingMirrorDrift()
        {
            using (var fixture = new TempCardsFixture())
            {
                fixture.WriteCard("test_a.json", "test.a");
                fixture.WriteIndex("test.a");

                var issues = CardPresentationIndexIO.ValidateMirror(fixture.Authoring, fixture.Streaming);
                Assert.IsTrue(issues.Exists(i => i.Contains("streaming missing test_a.json")), string.Join("; ", issues));
            }
        }

        /// <summary>临时卡牌目录夹具：Authoring 有卡、Streaming 空目录；TearDown 自动清理。</summary>
        private sealed class TempCardsFixture : System.IDisposable
        {
            public string Authoring { get; }
            public string Streaming { get; }

            public TempCardsFixture()
            {
                var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ng-hygiene-" + System.Guid.NewGuid().ToString("N"));
                Authoring = System.IO.Path.Combine(root, "cards");
                Streaming = System.IO.Path.Combine(root, "stream");
                System.IO.Directory.CreateDirectory(Authoring);
                System.IO.Directory.CreateDirectory(Streaming);
            }

            public void WriteCard(string fileName, string contentId)
            {
                var dto = CardPresentationJsonIO.CreateDefault(contentId, "Monster");
                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(Authoring, fileName),
                    CardPresentationJsonIO.ToJson(dto),
                    new System.Text.UTF8Encoding(false));
            }

            public void WriteIndex(params string[] ids)
            {
                CardPresentationIndexIO.WriteIndex(Authoring, ids, out _);
                CardPresentationIndexIO.WriteIndex(Streaming, ids, out _);
            }

            public void Dispose()
            {
                try
                {
                    System.IO.Directory.Delete(System.IO.Path.GetDirectoryName(Authoring), true);
                }
                catch (System.IO.IOException)
                {
                }
                catch (System.UnauthorizedAccessException)
                {
                }
            }
        }

        private static string Format(List<ContentHygieneValidator.Finding> findings)
        {
            if (findings.Count == 0)
            {
                return string.Empty;
            }

            return string.Join("; ", findings);
        }
    }
}

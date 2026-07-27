#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Core.Content;
using NineGrid.Presentation.Editor;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    public enum CardPresentationSidebarCategory
    {
        Avatar = 0,
        Monster = 1,
        Item = 2,
        Relic = 3,
        Other = 4,
    }

    public sealed class CardPresentationEditorEntry
    {
        public CardPresentationConfigDto Dto { get; set; }
        public string SavedJson { get; set; } = string.Empty;

        public string ContentId => Dto?.contentId ?? string.Empty;
        public string Kind => Dto?.kind ?? string.Empty;
        public string DeckId => Dto?.deckId ?? string.Empty;
        public string DisplayName => Dto?.displayName ?? string.Empty;

        public bool IsDirty
        {
            get
            {
                if (Dto == null)
                {
                    return false;
                }

                return !string.Equals(
                    CardPresentationJsonIO.ToJson(Dto),
                    SavedJson ?? string.Empty,
                    StringComparison.Ordinal);
            }
        }

        public void MarkSaved()
        {
            SavedJson = Dto != null ? CardPresentationJsonIO.ToJson(Dto) : string.Empty;
        }
    }

    public sealed class CardPresentationSidebarDeckGroup
    {
        public string DeckId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public List<CardPresentationEditorEntry> Entries { get; } = new List<CardPresentationEditorEntry>();
    }

    public sealed class CardPresentationSidebarGroup
    {
        public CardPresentationSidebarCategory Category { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool ExpandByDefault { get; set; } = true;
        public List<CardPresentationEditorEntry> Entries { get; } = new List<CardPresentationEditorEntry>();
        public List<CardPresentationSidebarDeckGroup> DeckGroups { get; } = new List<CardPresentationSidebarDeckGroup>();
    }

    public sealed class CardPresentationInsertableIcon
    {
        public string Code { get; set; } = string.Empty;
        public string DisplayNameZh { get; set; } = string.Empty;
        public string Token => "[" + Code + "]";
    }

    [Serializable]
    public sealed class CardPresentationIndexDto
    {
        public int schemaVersion = 1;
        public string[] contentIds = Array.Empty<string>();
    }

    public sealed class CardPresentationEditorSession
    {
        private readonly List<CardPresentationEditorEntry> entries = new List<CardPresentationEditorEntry>();
        private readonly Dictionary<string, CardPresentationEditorEntry> byId =
            new Dictionary<string, CardPresentationEditorEntry>(StringComparer.Ordinal);
        private string focusedContentId = string.Empty;

        public IReadOnlyList<CardPresentationEditorEntry> Entries => entries;
        public GameContentCatalog CoreCatalog { get; private set; }
        public ContentVisualCatalog VisualCatalog { get; private set; }
        public ContentVisualSpriteCatalogSet SpriteCatalogs { get; private set; }
        public string XlsxPath { get; private set; }
        public string FocusedContentId
        {
            get => focusedContentId;
            set => focusedContentId = value ?? string.Empty;
        }

        public int DirtyCount => entries.Count(e => e.IsDirty);

        public void Reload()
        {
            XlsxPath = ContentVisualXlsxIO.ResolveAbsolutePath();
            SpriteCatalogs = ContentVisualEditorSession.LoadOrCreateCatalogSet();
            ReloadCatalogs();

            entries.Clear();
            byId.Clear();

            var xlsxRows = ContentVisualXlsxIO.ReadAll(XlsxPath);
            for (var i = 0; i < xlsxRows.Count; i++)
            {
                var row = xlsxRows[i];
                if (row == null || string.IsNullOrWhiteSpace(row.ContentId))
                {
                    continue;
                }

                if (!CardPresentationMigration.IsPresentationEditorEntry(row.ContentKind, row.ContentId))
                {
                    continue;
                }

                if (byId.ContainsKey(row.ContentId))
                {
                    continue;
                }

                var entry = LoadOrCreateEntry(row);
                entries.Add(entry);
                byId[entry.ContentId] = entry;
            }

            // 补入仅有 JSON、xlsx 未列的已有卡（罕见）。
            TryAppendOrphanJsonEntries();

            entries.Sort((a, b) => string.CompareOrdinal(a.ContentId, b.ContentId));

            if (!string.IsNullOrEmpty(focusedContentId) && !byId.ContainsKey(focusedContentId))
            {
                focusedContentId = string.Empty;
            }
        }

        public void ReloadCatalogs()
        {
            var dataDirectory = ContentVisualBootstrap.ResolveLubanDataDirectory();
            CoreCatalog = TableNineLubanCatalogFactory.CreateFromDirectory(dataDirectory);
            VisualCatalog = TableNineVisualCatalogFactory.CreateFromDirectory(dataDirectory);
        }

        public CardPresentationEditorEntry GetFocused()
        {
            if (string.IsNullOrEmpty(focusedContentId))
            {
                return null;
            }

            byId.TryGetValue(focusedContentId, out var entry);
            return entry;
        }

        public void MarkDirty(string contentId)
        {
            // Dirty 由 SavedJson 对比驱动；此方法留给调用方语义占位（可触发布局刷新）。
            _ = contentId;
        }

        public bool TrySaveAll(out string error)
        {
            error = null;
            var dirty = entries.Where(e => e.IsDirty).ToList();
            if (dirty.Count == 0)
            {
                return true;
            }

            try
            {
                CardPresentationJsonIO.EnsureDirectoriesExist();
                for (var i = 0; i < dirty.Count; i++)
                {
                    CardPresentationJsonIO.SaveAuthoring(dirty[i].Dto);
                    dirty[i].MarkSaved();
                }

                CardPresentationConfigCatalog.Invalidate();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public bool TrySaveFocused(out string error)
        {
            error = null;
            var focused = GetFocused();
            if (focused == null || focused.Dto == null)
            {
                error = "无选中条目。";
                return false;
            }

            if (!focused.IsDirty)
            {
                return true;
            }

            try
            {
                CardPresentationJsonIO.EnsureDirectoriesExist();
                CardPresentationJsonIO.SaveAuthoring(focused.Dto);
                focused.MarkSaved();
                CardPresentationConfigCatalog.Invalidate();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public int MigrateFromLegacyCatalogs(out string message)
        {
            var created = 0;
            var skipped = 0;
            try
            {
                CardPresentationJsonIO.EnsureDirectoriesExist();
                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    if (entry?.Dto == null)
                    {
                        continue;
                    }

                    if (CardPresentationMigration.AuthoringFileExists(entry.ContentId))
                    {
                        skipped++;
                        continue;
                    }

                    var kind = ParseKind(entry.Kind);
                    ContentVisualDirectSlotSprites slots = default;
                    var catalog = SpriteCatalogs != null ? SpriteCatalogs.ResolveCatalog(kind) : null;
                    if (catalog != null)
                    {
                        catalog.TryGetDirectSlots(entry.ContentId, out slots);
                    }

                    var description = entry.Dto.description;
                    entry.Dto = CardPresentationMigration.CreateFilledDefault(
                        entry.ContentId,
                        entry.Kind,
                        description,
                        slots,
                        CoreCatalog,
                        GetDisplayName);
                    entry.SavedJson = string.Empty; // force dirty until save
                    CardPresentationJsonIO.SaveAuthoring(entry.Dto);
                    entry.MarkSaved();
                    created++;
                }

                CardPresentationConfigCatalog.Invalidate();
                message = "迁移完成：新建 " + created + "，已有 JSON 跳过 " + skipped + "。";
                return created;
            }
            catch (Exception ex)
            {
                message = "迁移失败：" + ex.Message;
                return created;
            }
        }

        public bool TryExportIndex(out string message)
        {
            try
            {
                CardPresentationJsonIO.EnsureDirectoriesExist();
                var ids = entries
                    .Select(e => e.ContentId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray();

                var index = new CardPresentationIndexDto
                {
                    schemaVersion = 1,
                    contentIds = ids,
                };
                var json = JsonUtility.ToJson(index, true);
                var authoringAbs = Path.Combine(
                    Application.dataPath,
                    "Arts",
                    "ContentVisual",
                    "cards",
                    CardPresentationJsonIO.IndexFileName);
                var streamingAbs = Path.Combine(
                    Application.streamingAssetsPath,
                    "ContentVisual",
                    "cards",
                    CardPresentationJsonIO.IndexFileName);

                File.WriteAllText(authoringAbs, json, new UTF8Encoding(false));
                Directory.CreateDirectory(Path.GetDirectoryName(streamingAbs) ?? streamingAbs);
                File.WriteAllText(streamingAbs, json, new UTF8Encoding(false));

                AssetDatabase.ImportAsset(
                    CardPresentationJsonIO.AuthoringFolder + "/" + CardPresentationJsonIO.IndexFileName);
                AssetDatabase.ImportAsset(
                    CardPresentationJsonIO.StreamingFolder + "/" + CardPresentationJsonIO.IndexFileName);

                message = "已导出索引：" + ids.Length + " 条 → " + CardPresentationJsonIO.AuthoringFolder
                          + "/" + CardPresentationJsonIO.IndexFileName;
                return true;
            }
            catch (Exception ex)
            {
                message = "导出索引失败：" + ex.Message;
                return false;
            }
        }

        public List<CardPresentationSidebarGroup> GetSidebarGroups()
        {
            var avatar = new CardPresentationSidebarGroup
            {
                Category = CardPresentationSidebarCategory.Avatar,
                Title = "玩家卡",
                ExpandByDefault = true,
            };
            var monster = new CardPresentationSidebarGroup
            {
                Category = CardPresentationSidebarCategory.Monster,
                Title = "怪物卡",
                ExpandByDefault = true,
            };
            var item = new CardPresentationSidebarGroup
            {
                Category = CardPresentationSidebarCategory.Item,
                Title = "道具卡",
                ExpandByDefault = true,
            };
            var relic = new CardPresentationSidebarGroup
            {
                Category = CardPresentationSidebarCategory.Relic,
                Title = "遗物卡",
                ExpandByDefault = true,
            };
            var other = new CardPresentationSidebarGroup
            {
                Category = CardPresentationSidebarCategory.Other,
                Title = "描述富文本",
                ExpandByDefault = false,
            };

            var monsterByDeck = new Dictionary<string, CardPresentationSidebarDeckGroup>(StringComparer.Ordinal);

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var category = MapSidebarCategory(entry.Kind);
                switch (category)
                {
                    case CardPresentationSidebarCategory.Avatar:
                        avatar.Entries.Add(entry);
                        break;
                    case CardPresentationSidebarCategory.Monster:
                        {
                            var deckId = string.IsNullOrWhiteSpace(entry.DeckId) ? "(未分组)" : entry.DeckId.Trim();
                            if (!monsterByDeck.TryGetValue(deckId, out var deckGroup))
                            {
                                deckGroup = new CardPresentationSidebarDeckGroup
                                {
                                    DeckId = deckId,
                                    Title = ResolveDeckTitle(deckId),
                                };
                                monsterByDeck[deckId] = deckGroup;
                            }

                            deckGroup.Entries.Add(entry);
                            break;
                        }
                    case CardPresentationSidebarCategory.Item:
                        item.Entries.Add(entry);
                        break;
                    case CardPresentationSidebarCategory.Relic:
                        relic.Entries.Add(entry);
                        break;
                }
            }

            monster.DeckGroups.AddRange(
                monsterByDeck.Values.OrderBy(g => g.Title, StringComparer.OrdinalIgnoreCase));

            return new List<CardPresentationSidebarGroup> { avatar, monster, item, relic, other };
        }

        public List<CardPresentationInsertableIcon> GetInsertableIcons()
        {
            var list = new List<CardPresentationInsertableIcon>();
            var slots = CardFacePreviewBuilder.ListInsertableSlots();
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null || string.IsNullOrEmpty(slot.Code))
                {
                    continue;
                }

                list.Add(new CardPresentationInsertableIcon
                {
                    Code = slot.Code,
                    DisplayNameZh = slot.DisplayNameZh ?? slot.Code,
                });
            }

            return list;
        }

        public string GetDisplayName(string contentId, string contentKind)
        {
            if (CoreCatalog != null)
            {
                if (CoreCatalog.Cards != null
                    && CoreCatalog.Cards.TryGetValue(contentId ?? string.Empty, out var card)
                    && card != null
                    && !string.IsNullOrEmpty(card.DisplayName))
                {
                    return card.DisplayName;
                }

                if (CoreCatalog.Relics != null
                    && CoreCatalog.Relics.TryGetValue(contentId ?? string.Empty, out var relic)
                    && relic != null
                    && !string.IsNullOrEmpty(relic.DisplayName))
                {
                    return relic.DisplayName;
                }

                if (CoreCatalog.Skills != null
                    && CoreCatalog.Skills.TryGetValue(contentId ?? string.Empty, out var skill)
                    && skill != null
                    && !string.IsNullOrEmpty(skill.DisplayName))
                {
                    return skill.DisplayName;
                }
            }

            if (string.Equals(contentKind, "Avatar", StringComparison.OrdinalIgnoreCase))
            {
                return "玩家";
            }

            return contentId ?? string.Empty;
        }

        public static CardPresentationSidebarCategory MapSidebarCategory(string kind)
        {
            if (string.IsNullOrWhiteSpace(kind))
            {
                return CardPresentationSidebarCategory.Other;
            }

            if (Enum.TryParse(kind, true, out ContentVisualKind cvk))
            {
                switch (cvk)
                {
                    case ContentVisualKind.Avatar:
                        return CardPresentationSidebarCategory.Avatar;
                    case ContentVisualKind.Monster:
                        return CardPresentationSidebarCategory.Monster;
                    case ContentVisualKind.HelpCard:
                        return CardPresentationSidebarCategory.Item;
                    case ContentVisualKind.Skill:
                        // 技能不进配置器；若泄漏也不进道具卡侧栏。
                        return CardPresentationSidebarCategory.Other;
                    case ContentVisualKind.Relic:
                        return CardPresentationSidebarCategory.Relic;
                }
            }

            var k = kind.Trim();
            if (string.Equals(k, "PlayerCard", StringComparison.OrdinalIgnoreCase)
                || string.Equals(k, "Item", StringComparison.OrdinalIgnoreCase)
                || string.Equals(k, "HelpCard", StringComparison.OrdinalIgnoreCase))
            {
                return CardPresentationSidebarCategory.Item;
            }

            if (string.Equals(k, "Avatar", StringComparison.OrdinalIgnoreCase))
            {
                return CardPresentationSidebarCategory.Avatar;
            }

            if (string.Equals(k, "Monster", StringComparison.OrdinalIgnoreCase))
            {
                return CardPresentationSidebarCategory.Monster;
            }

            if (string.Equals(k, "Relic", StringComparison.OrdinalIgnoreCase))
            {
                return CardPresentationSidebarCategory.Relic;
            }

            return CardPresentationSidebarCategory.Other;
        }

        private CardPresentationEditorEntry LoadOrCreateEntry(ContentVisualXlsxRow row)
        {
            var contentId = row.ContentId;
            var kind = row.ContentKind ?? string.Empty;
            var description = row.Description ?? string.Empty;

            CardPresentationConfigDto dto = null;
            var authoringPath = CardPresentationJsonIO.GetAuthoringPath(contentId);
            if (CardPresentationJsonIO.TryLoad(authoringPath, out var loaded, out _))
            {
                dto = loaded;
            }
            else
            {
                var streamingPath = CardPresentationJsonIO.GetStreamingPath(contentId);
                if (CardPresentationJsonIO.TryLoad(streamingPath, out loaded, out _))
                {
                    dto = loaded;
                }
            }

            var cvKind = ParseKind(kind);
            ContentVisualDirectSlotSprites slots = default;
            var catalog = SpriteCatalogs != null ? SpriteCatalogs.ResolveCatalog(cvKind) : null;
            if (catalog != null)
            {
                catalog.TryGetDirectSlots(contentId, out slots);
            }

            if (dto == null)
            {
                dto = CardPresentationMigration.CreateFilledDefault(
                    contentId,
                    kind,
                    description,
                    slots,
                    CoreCatalog,
                    GetDisplayName);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(dto.kind))
                {
                    dto.kind = kind;
                }

                if (string.IsNullOrWhiteSpace(dto.description) && !string.IsNullOrWhiteSpace(description))
                {
                    dto.description = description;
                }

                CardPresentationMigration.FillEmptyFromCore(dto, CoreCatalog, GetDisplayName);

                // JSON 缺图槽时用 SO 补缺（不覆盖已有路径）。
                FillMissingSpritePaths(dto, slots);
            }

            var entry = new CardPresentationEditorEntry { Dto = dto };
            entry.MarkSaved();
            return entry;
        }

        private void TryAppendOrphanJsonEntries()
        {
            var absFolder = Path.Combine(Application.dataPath, "Arts", "ContentVisual", "cards");
            if (!Directory.Exists(absFolder))
            {
                return;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(absFolder, "*.json", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                return;
            }

            for (var i = 0; i < files.Length; i++)
            {
                var name = Path.GetFileName(files[i]);
                if (string.Equals(name, CardPresentationJsonIO.IndexFileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!CardPresentationJsonIO.TryLoad(files[i], out var dto, out _) || dto == null)
                {
                    continue;
                }

                var id = dto.contentId;
                if (string.IsNullOrWhiteSpace(id) || byId.ContainsKey(id))
                {
                    continue;
                }

                if (!CardPresentationMigration.IsPresentationEditorEntry(dto.kind, id))
                {
                    continue;
                }

                CardPresentationMigration.FillEmptyFromCore(dto, CoreCatalog, GetDisplayName);
                var entry = new CardPresentationEditorEntry { Dto = dto };
                entry.MarkSaved();
                entries.Add(entry);
                byId[id] = entry;
            }
        }

        private static void FillMissingSpritePaths(
            CardPresentationConfigDto dto,
            ContentVisualDirectSlotSprites slots)
        {
            if (dto.sprites == null)
            {
                dto.sprites = new CardPresentationSpritesDto();
            }

            if (string.IsNullOrWhiteSpace(dto.sprites.mainIcon) && slots.MainIcon != null)
            {
                dto.sprites.mainIcon = CardPresentationMigration.AssetPathOrEmpty(slots.MainIcon);
            }

            if (string.IsNullOrWhiteSpace(dto.sprites.faceBackground) && slots.FaceBackground != null)
            {
                dto.sprites.faceBackground = CardPresentationMigration.AssetPathOrEmpty(slots.FaceBackground);
            }

            if (string.IsNullOrWhiteSpace(dto.sprites.backBorder) && slots.BackBorder != null)
            {
                dto.sprites.backBorder = CardPresentationMigration.AssetPathOrEmpty(slots.BackBorder);
            }

            if (string.IsNullOrWhiteSpace(dto.sprites.backShirt) && slots.BackShirt != null)
            {
                dto.sprites.backShirt = CardPresentationMigration.AssetPathOrEmpty(slots.BackShirt);
            }

            if (string.IsNullOrWhiteSpace(dto.sprites.backLogo) && slots.BackLogo != null)
            {
                dto.sprites.backLogo = CardPresentationMigration.AssetPathOrEmpty(slots.BackLogo);
            }
        }

        private string ResolveDeckTitle(string deckId)
        {
            if (string.IsNullOrWhiteSpace(deckId) || deckId == "(未分组)")
            {
                return "(未分组)";
            }

            if (CoreCatalog?.MonsterDecks != null
                && CoreCatalog.MonsterDecks.TryGetValue(deckId, out var deck)
                && deck != null
                && !string.IsNullOrEmpty(deck.DisplayName))
            {
                return deck.DisplayName + " (" + deckId + ")";
            }

            return deckId;
        }

        private static ContentVisualKind ParseKind(string kind)
        {
            if (Enum.TryParse(kind, true, out ContentVisualKind parsed))
            {
                return parsed;
            }

            return ContentVisualKind.Unknown;
        }
    }
}
#endif

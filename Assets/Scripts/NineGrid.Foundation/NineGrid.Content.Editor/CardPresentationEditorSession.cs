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
    /// <summary>侧栏三大类（解耦装配 IA）。</summary>
    public enum CardPresentationSidebarSection
    {
        Faces = 0,
        EffectPool = 1,
        Decks = 2,
    }

    /// <summary>内容区焦点种类。</summary>
    public enum CardPresentationEditorFocusKind
    {
        None = 0,
        Face = 1,
        EffectTemplate = 2,
        Deck = 3,
        DescriptionGlossary = 4,
    }

    /// <summary>
    /// 旧侧栏玩法角色分桶（仅兼容测试 / 预览 Kind 兜底；UI 不再按此分栏）。
    /// </summary>
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

    public sealed class CardPresentationInsertableIcon
    {
        public string Code { get; set; } = string.Empty;
        public string DisplayNameZh { get; set; } = string.Empty;
        public string Token => "[" + Code + "]";
    }

    /// <summary>效果装配下拉：中文 design_text 作显示名，templateId 作持久化值。</summary>
    public sealed class EffectTemplateChoice
    {
        public string TemplateId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    [Serializable]
    public sealed class CardPresentationIndexDto
    {
        public int schemaVersion = 1;
        public string[] contentIds = Array.Empty<string>();
    }

    public sealed class CardPresentationEditorSession
    {
        public const string UngroupedDeckId = "(未分组)";
        public const string PlayerDeckId = "deck.player";

        private readonly List<CardPresentationEditorEntry> faceEntries = new List<CardPresentationEditorEntry>();
        private readonly List<CardPresentationEditorEntry> deckEntries = new List<CardPresentationEditorEntry>();
        private readonly Dictionary<string, CardPresentationEditorEntry> faceById =
            new Dictionary<string, CardPresentationEditorEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, CardPresentationEditorEntry> deckById =
            new Dictionary<string, CardPresentationEditorEntry>(StringComparer.Ordinal);
        private readonly List<EffectTemplateEditorIO.TemplateRow> effectTemplates =
            new List<EffectTemplateEditorIO.TemplateRow>();
        private readonly List<string> knownSkillIds = new List<string>();

        private CardPresentationEditorFocusKind focusKind = CardPresentationEditorFocusKind.None;
        private string focusedContentId = string.Empty;
        private string focusedTemplateId = string.Empty;

        public IReadOnlyList<CardPresentationEditorEntry> FaceEntries => faceEntries;
        public IReadOnlyList<CardPresentationEditorEntry> DeckEntries => deckEntries;
        public IReadOnlyList<EffectTemplateEditorIO.TemplateRow> EffectTemplates => effectTemplates;
        public IReadOnlyList<string> KnownSkillIds => knownSkillIds;
        public GameContentCatalog CoreCatalog { get; private set; }
        public ContentVisualCatalog VisualCatalog { get; private set; }
        public ContentVisualSpriteCatalogSet SpriteCatalogs { get; private set; }
        public string XlsxPath { get; private set; }

        public CardPresentationEditorFocusKind FocusKind
        {
            get => focusKind;
            set => focusKind = value;
        }

        public string FocusedContentId
        {
            get => focusedContentId;
            set => focusedContentId = value ?? string.Empty;
        }

        public string FocusedTemplateId
        {
            get => focusedTemplateId;
            set => focusedTemplateId = value ?? string.Empty;
        }

        /// <summary>兼容旧调用：卡面条目列表。</summary>
        public IReadOnlyList<CardPresentationEditorEntry> Entries => faceEntries;

        public int DirtyCount
        {
            get
            {
                var n = faceEntries.Count(e => e.IsDirty) + deckEntries.Count(e => e.IsDirty);
                for (var i = 0; i < effectTemplates.Count; i++)
                {
                    if (effectTemplates[i] != null && effectTemplates[i].IsDirty)
                    {
                        n++;
                    }
                }

                return n;
            }
        }

        public void Reload()
        {
            XlsxPath = ContentVisualXlsxIO.ResolveAbsolutePath();
            SpriteCatalogs = ContentVisualEditorSession.LoadOrCreateCatalogSet();
            ReloadCatalogs();

            faceEntries.Clear();
            faceById.Clear();
            deckEntries.Clear();
            deckById.Clear();
            effectTemplates.Clear();
            knownSkillIds.Clear();

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

                if (faceById.ContainsKey(row.ContentId))
                {
                    continue;
                }

                var entry = LoadOrCreateEntry(row);
                faceEntries.Add(entry);
                faceById[entry.ContentId] = entry;
            }

            TryAppendOrphanJsonEntries();
            LoadDeckEntriesFromDisk();
            EnsurePlayerDeckSeed();
            LoadKnownSkillIdsFromDisk();

            faceEntries.Sort((a, b) => string.CompareOrdinal(a.ContentId, b.ContentId));
            deckEntries.Sort((a, b) => string.CompareOrdinal(a.ContentId, b.ContentId));

            var templates = EffectTemplateEditorIO.LoadAll(out _);
            effectTemplates.AddRange(templates);

            if (!string.IsNullOrEmpty(focusedContentId)
                && !faceById.ContainsKey(focusedContentId)
                && !deckById.ContainsKey(focusedContentId))
            {
                focusedContentId = string.Empty;
            }

            if (!string.IsNullOrEmpty(focusedTemplateId)
                && effectTemplates.All(t => t == null
                    || !string.Equals(t.id, focusedTemplateId, StringComparison.Ordinal)))
            {
                focusedTemplateId = string.Empty;
            }
        }

        public void ReloadCatalogs()
        {
            CoreCatalog = ContentCatalogBootstrap.Load();
            VisualCatalog = new ContentVisualCatalog();
        }

        public CardPresentationEditorEntry GetFocusedFace()
        {
            if (focusKind != CardPresentationEditorFocusKind.Face
                || string.IsNullOrEmpty(focusedContentId))
            {
                return null;
            }

            faceById.TryGetValue(focusedContentId, out var entry);
            return entry;
        }

        public CardPresentationEditorEntry GetFocusedDeck()
        {
            if (focusKind != CardPresentationEditorFocusKind.Deck
                || string.IsNullOrEmpty(focusedContentId))
            {
                return null;
            }

            deckById.TryGetValue(focusedContentId, out var entry);
            return entry;
        }

        public EffectTemplateEditorIO.TemplateRow GetFocusedTemplate()
        {
            if (focusKind != CardPresentationEditorFocusKind.EffectTemplate
                || string.IsNullOrEmpty(focusedTemplateId))
            {
                return null;
            }

            for (var i = 0; i < effectTemplates.Count; i++)
            {
                var row = effectTemplates[i];
                if (row != null && string.Equals(row.id, focusedTemplateId, StringComparison.Ordinal))
                {
                    return row;
                }
            }

            return null;
        }

        /// <summary>兼容旧窗口：仅返回卡面焦点。</summary>
        public CardPresentationEditorEntry GetFocused()
        {
            return GetFocusedFace();
        }

        public void MarkDirty(string contentId)
        {
            _ = contentId;
        }

        public void FocusFace(string contentId)
        {
            focusKind = CardPresentationEditorFocusKind.Face;
            focusedContentId = contentId ?? string.Empty;
            focusedTemplateId = string.Empty;
        }

        public void FocusDeck(string contentId)
        {
            focusKind = CardPresentationEditorFocusKind.Deck;
            focusedContentId = contentId ?? string.Empty;
            focusedTemplateId = string.Empty;
        }

        public void FocusEffectTemplate(string templateId)
        {
            focusKind = CardPresentationEditorFocusKind.EffectTemplate;
            focusedTemplateId = templateId ?? string.Empty;
            focusedContentId = string.Empty;
        }

        public void FocusDescriptionGlossary()
        {
            focusKind = CardPresentationEditorFocusKind.DescriptionGlossary;
            focusedContentId = string.Empty;
            focusedTemplateId = string.Empty;
        }

        public List<CardPresentationSidebarDeckGroup> GetFaceDeckGroups()
        {
            var byDeck = new Dictionary<string, CardPresentationSidebarDeckGroup>(StringComparer.Ordinal);
            for (var i = 0; i < faceEntries.Count; i++)
            {
                var entry = faceEntries[i];
                var deckId = string.IsNullOrWhiteSpace(entry.DeckId)
                    ? UngroupedDeckId
                    : entry.DeckId.Trim();
                if (!byDeck.TryGetValue(deckId, out var group))
                {
                    group = new CardPresentationSidebarDeckGroup
                    {
                        DeckId = deckId,
                        Title = ResolveDeckTitle(deckId),
                    };
                    byDeck[deckId] = group;
                }

                group.Entries.Add(entry);
            }

            return byDeck.Values
                .OrderBy(g => g.DeckId == UngroupedDeckId ? 1 : 0)
                .ThenBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public List<string> GetDeckIdChoices()
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < deckEntries.Count; i++)
            {
                var id = deckEntries[i]?.ContentId;
                if (!string.IsNullOrWhiteSpace(id))
                {
                    set.Add(id.Trim());
                }
            }

            for (var i = 0; i < faceEntries.Count; i++)
            {
                var id = faceEntries[i]?.DeckId;
                if (!string.IsNullOrWhiteSpace(id))
                {
                    set.Add(id.Trim());
                }
            }

            return set.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public List<string> GetEffectTemplateIdChoices()
        {
            return effectTemplates
                .Where(t => t != null && !string.IsNullOrWhiteSpace(t.id))
                .Select(t => t.id)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// 装配下拉用：Label = design_text + 过渡来源后缀（原帮助卡/怪物技能/遗物），Value 仍为 templateId。
        /// </summary>
        public List<EffectTemplateChoice> GetEffectTemplateChoices()
        {
            var labelCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var choices = new List<EffectTemplateChoice>();
            for (var i = 0; i < effectTemplates.Count; i++)
            {
                var row = effectTemplates[i];
                if (row == null || string.IsNullOrWhiteSpace(row.id))
                {
                    continue;
                }

                var rawLabel = FormatEffectTemplateChoiceLabel(row.id, row.design_text);
                if (!labelCounts.TryGetValue(rawLabel, out var count))
                {
                    count = 0;
                }

                labelCounts[rawLabel] = count + 1;
                choices.Add(new EffectTemplateChoice
                {
                    TemplateId = row.id,
                    Label = rawLabel,
                });
            }

            // 同文案多模板时附加 id，避免下拉无法区分。
            for (var i = 0; i < choices.Count; i++)
            {
                var choice = choices[i];
                if (labelCounts.TryGetValue(choice.Label, out var count) && count > 1)
                {
                    choice.Label = choice.Label + " · " + choice.TemplateId;
                }
            }

            choices.Sort((a, b) => string.CompareOrdinal(a.Label, b.Label));
            return choices;
        }

        /// <summary>
        /// 统一效果池显示名：中文 design_text + 过渡性来源标注（收束前便于辨认旧容器）。
        /// </summary>
        public static string FormatEffectTemplateChoiceLabel(string templateId, string designText)
        {
            var body = string.IsNullOrWhiteSpace(designText)
                ? (string.IsNullOrWhiteSpace(templateId) ? "（无模板）" : templateId.Trim())
                : designText.Trim();
            var origin = ResolveEffectOriginSuffix(templateId);
            return string.IsNullOrEmpty(origin) ? body : body + origin;
        }

        /// <summary>tpl.help.* →（原帮助卡效果）；tpl.skill.* →（原怪物技能效果）；tpl.relic.* →（原遗物效果）。</summary>
        public static string ResolveEffectOriginSuffix(string templateId)
        {
            if (string.IsNullOrWhiteSpace(templateId))
            {
                return string.Empty;
            }

            var id = templateId.Trim();
            if (id.StartsWith("tpl.help.", StringComparison.OrdinalIgnoreCase))
            {
                return "（原帮助卡效果）";
            }

            if (id.StartsWith("tpl.skill.", StringComparison.OrdinalIgnoreCase))
            {
                return "（原怪物技能效果）";
            }

            if (id.StartsWith("tpl.relic.", StringComparison.OrdinalIgnoreCase))
            {
                return "（原遗物效果）";
            }

            return "（原效果）";
        }

        /// <summary>
        /// 把怪物 <c>skillIds</c> 展开进 <c>effectAssemblies</c> 并清空 skillIds，编辑器内只保留单一效果装配源。
        /// </summary>
        /// <returns>是否发生了展开（调用方可标脏）。</returns>
        public bool TryExpandSkillIdsIntoAssemblies(CardPresentationConfigDto dto)
        {
            if (dto == null || dto.skillIds == null || dto.skillIds.Length == 0)
            {
                return false;
            }

            var existing = dto.effectAssemblies != null
                ? new List<EffectAssemblyDto>(dto.effectAssemblies)
                : new List<EffectAssemblyDto>();
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < existing.Count; i++)
            {
                var row = existing[i];
                if (row != null && !string.IsNullOrWhiteSpace(row.id))
                {
                    seenIds.Add(row.id.Trim());
                }
            }

            var loadedAnySkill = false;
            for (var s = 0; s < dto.skillIds.Length; s++)
            {
                var skillId = dto.skillIds[s];
                if (string.IsNullOrWhiteSpace(skillId))
                {
                    continue;
                }

                if (!TryLoadSkillDto(skillId.Trim(), out var skillDto) || skillDto == null)
                {
                    continue;
                }

                loadedAnySkill = true;
                if (skillDto.effectAssemblies == null)
                {
                    continue;
                }

                for (var a = 0; a < skillDto.effectAssemblies.Length; a++)
                {
                    var src = skillDto.effectAssemblies[a];
                    if (src == null || string.IsNullOrWhiteSpace(src.templateId))
                    {
                        continue;
                    }

                    var mountId = string.IsNullOrWhiteSpace(src.id)
                        ? skillId.Trim() + ".fx" + a
                        : src.id.Trim();
                    if (!seenIds.Add(mountId))
                    {
                        continue;
                    }

                    existing.Add(new EffectAssemblyDto
                    {
                        id = mountId,
                        templateId = src.templateId.Trim(),
                        containerType = string.IsNullOrWhiteSpace(src.containerType)
                            ? "MonsterSkill"
                            : src.containerType.Trim(),
                        argsJson = string.IsNullOrWhiteSpace(src.argsJson) ? "{}" : src.argsJson,
                    });
                }
            }

            // 技能 JSON 全找不到时保留 skillIds，避免静默丢挂载。
            if (!loadedAnySkill)
            {
                return false;
            }

            dto.effectAssemblies = existing.ToArray();
            dto.skillIds = Array.Empty<string>();
            return true;
        }

        private static bool TryLoadSkillDto(string skillContentId, out CardPresentationConfigDto dto)
        {
            dto = null;
            if (string.IsNullOrWhiteSpace(skillContentId))
            {
                return false;
            }

            var authoringPath = CardPresentationJsonIO.GetAuthoringPath(skillContentId);
            if (CardPresentationJsonIO.TryLoad(authoringPath, out dto, out _) && dto != null)
            {
                return true;
            }

            var streamingPath = CardPresentationJsonIO.GetStreamingPath(skillContentId);
            return CardPresentationJsonIO.TryLoad(streamingPath, out dto, out _) && dto != null;
        }

        public bool TryGetEffectTemplateDesignText(string templateId, out string designText)
        {
            designText = string.Empty;
            if (string.IsNullOrWhiteSpace(templateId))
            {
                return false;
            }

            for (var i = 0; i < effectTemplates.Count; i++)
            {
                var row = effectTemplates[i];
                if (row == null || !string.Equals(row.id, templateId, StringComparison.Ordinal))
                {
                    continue;
                }

                designText = row.design_text ?? string.Empty;
                return true;
            }

            return false;
        }

        public bool TryGetDeckDto(string deckId, out CardPresentationConfigDto dto)
        {
            dto = null;
            if (string.IsNullOrWhiteSpace(deckId) || deckId == UngroupedDeckId)
            {
                return false;
            }

            if (deckById.TryGetValue(deckId.Trim(), out var entry) && entry?.Dto != null)
            {
                dto = entry.Dto;
                return true;
            }

            return false;
        }

        public static void NormalizeLogicMounts(CardPresentationConfigDto dto)
        {
            if (dto == null)
            {
                return;
            }

            if (dto.effectAssemblies == null)
            {
                dto.effectAssemblies = Array.Empty<EffectAssemblyDto>();
            }

            if (dto.effectAssemblies.Length == 0)
            {
                dto.effectIds = Array.Empty<string>();
            }

            if (dto.skillIds == null)
            {
                dto.skillIds = Array.Empty<string>();
            }
        }

        public bool TrySaveAll(out string error)
        {
            error = null;
            try
            {
                CardPresentationJsonIO.EnsureDirectoriesExist();
                for (var i = 0; i < faceEntries.Count; i++)
                {
                    var entry = faceEntries[i];
                    if (entry == null || !entry.IsDirty || entry.Dto == null)
                    {
                        continue;
                    }

                    TryExpandSkillIdsIntoAssemblies(entry.Dto);
                    NormalizeLogicMounts(entry.Dto);
                    CardPresentationJsonIO.SaveAuthoring(entry.Dto);
                    entry.MarkSaved();
                }

                for (var i = 0; i < deckEntries.Count; i++)
                {
                    var entry = deckEntries[i];
                    if (entry == null || !entry.IsDirty || entry.Dto == null)
                    {
                        continue;
                    }

                    CardPresentationJsonIO.SaveAuthoring(entry.Dto);
                    entry.MarkSaved();
                }

                var dirtyTemplates = effectTemplates.Where(t => t != null && t.IsDirty).ToList();
                if (dirtyTemplates.Count > 0)
                {
                    if (!EffectTemplateEditorIO.TrySaveAll(effectTemplates, out error))
                    {
                        return false;
                    }
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
            try
            {
                if (focusKind == CardPresentationEditorFocusKind.EffectTemplate)
                {
                    var row = GetFocusedTemplate();
                    if (row == null)
                    {
                        error = "无选中效果模板。";
                        return false;
                    }

                    if (!row.IsDirty)
                    {
                        return true;
                    }

                    return EffectTemplateEditorIO.TrySaveAll(effectTemplates, out error);
                }

                CardPresentationEditorEntry entry = null;
                if (focusKind == CardPresentationEditorFocusKind.Face)
                {
                    entry = GetFocusedFace();
                }
                else if (focusKind == CardPresentationEditorFocusKind.Deck)
                {
                    entry = GetFocusedDeck();
                }

                if (entry == null || entry.Dto == null)
                {
                    error = "无选中条目。";
                    return false;
                }

                if (!entry.IsDirty)
                {
                    return true;
                }

                CardPresentationJsonIO.EnsureDirectoriesExist();
                if (focusKind == CardPresentationEditorFocusKind.Face)
                {
                    TryExpandSkillIdsIntoAssemblies(entry.Dto);
                    NormalizeLogicMounts(entry.Dto);
                }

                CardPresentationJsonIO.SaveAuthoring(entry.Dto);
                entry.MarkSaved();
                CardPresentationConfigCatalog.Invalidate();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public bool TryCreateDeck(string contentId, string displayName, string deckKind, out string error)
        {
            error = null;
            contentId = (contentId ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(contentId))
            {
                error = "contentId 不能为空。";
                return false;
            }

            if (!contentId.StartsWith("deck.", StringComparison.OrdinalIgnoreCase))
            {
                contentId = "deck." + contentId;
            }

            if (deckById.ContainsKey(contentId) || faceById.ContainsKey(contentId))
            {
                error = "已存在：" + contentId;
                return false;
            }

            try
            {
                var dto = CardPresentationJsonIO.CreateDefault(contentId, "Deck");
                dto.schemaVersion = 2;
                dto.displayName = string.IsNullOrWhiteSpace(displayName) ? contentId : displayName.Trim();
                dto.deckKind = string.IsNullOrWhiteSpace(deckKind) ? "Presentation" : deckKind.Trim();
                dto.effectAssemblies = Array.Empty<EffectAssemblyDto>();
                dto.effectIds = Array.Empty<string>();
                dto.skillIds = Array.Empty<string>();
                CardPresentationJsonIO.EnsureDirectoriesExist();
                CardPresentationJsonIO.SaveAuthoring(dto);
                var entry = new CardPresentationEditorEntry { Dto = dto };
                entry.MarkSaved();
                deckEntries.Add(entry);
                deckById[contentId] = entry;
                deckEntries.Sort((a, b) => string.CompareOrdinal(a.ContentId, b.ContentId));
                CardPresentationConfigCatalog.Invalidate();
                FocusDeck(contentId);
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
                for (var i = 0; i < faceEntries.Count; i++)
                {
                    var entry = faceEntries[i];
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
                    entry.SavedJson = string.Empty;
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
                var ids = faceEntries
                    .Select(e => e.ContentId)
                    .Concat(deckEntries.Select(e => e.ContentId))
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

                if (CoreCatalog.MonsterDecks != null
                    && CoreCatalog.MonsterDecks.TryGetValue(contentId ?? string.Empty, out var deck)
                    && deck != null
                    && !string.IsNullOrEmpty(deck.DisplayName))
                {
                    return deck.DisplayName;
                }
            }

            if (string.Equals(contentKind, "Avatar", StringComparison.OrdinalIgnoreCase))
            {
                return "玩家";
            }

            return contentId ?? string.Empty;
        }

        public static bool IsItemLikeKind(string kind)
        {
            if (string.IsNullOrWhiteSpace(kind))
            {
                return false;
            }

            if (Enum.TryParse(kind, true, out ContentVisualKind cvk))
            {
                return cvk == ContentVisualKind.HelpCard;
            }

            var k = kind.Trim();
            return string.Equals(k, "PlayerCard", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(k, "Item", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(k, "HelpCard", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsCombatStatsKind(string kind)
        {
            if (string.IsNullOrWhiteSpace(kind))
            {
                return false;
            }

            if (Enum.TryParse(kind, true, out ContentVisualKind cvk))
            {
                return cvk == ContentVisualKind.Avatar || cvk == ContentVisualKind.Monster;
            }

            var k = kind.Trim();
            return string.Equals(k, "Avatar", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(k, "Monster", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsMonsterKind(string kind)
        {
            if (string.IsNullOrWhiteSpace(kind))
            {
                return false;
            }

            if (Enum.TryParse(kind, true, out ContentVisualKind cvk))
            {
                return cvk == ContentVisualKind.Monster;
            }

            return string.Equals(kind.Trim(), "Monster", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>兼容旧测试：玩法角色粗分桶（UI 已弃用）。</summary>
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
                        return CardPresentationSidebarCategory.Other;
                    case ContentVisualKind.Relic:
                        return CardPresentationSidebarCategory.Relic;
                }
            }

            if (IsItemLikeKind(kind))
            {
                return CardPresentationSidebarCategory.Item;
            }

            if (string.Equals(kind.Trim(), "Avatar", StringComparison.OrdinalIgnoreCase))
            {
                return CardPresentationSidebarCategory.Avatar;
            }

            if (string.Equals(kind.Trim(), "Monster", StringComparison.OrdinalIgnoreCase))
            {
                return CardPresentationSidebarCategory.Monster;
            }

            if (string.Equals(kind.Trim(), "Relic", StringComparison.OrdinalIgnoreCase))
            {
                return CardPresentationSidebarCategory.Relic;
            }

            return CardPresentationSidebarCategory.Other;
        }

        private void EnsurePlayerDeckSeed()
        {
            if (!deckById.ContainsKey(PlayerDeckId))
            {
                var dto = CardPresentationJsonIO.CreateDefault(PlayerDeckId, "Deck");
                dto.schemaVersion = 2;
                dto.displayName = "玩家卡组";
                dto.deckKind = "Presentation";
                dto.effectAssemblies = Array.Empty<EffectAssemblyDto>();
                dto.effectIds = Array.Empty<string>();
                dto.skillIds = Array.Empty<string>();
                try
                {
                    CardPresentationJsonIO.EnsureDirectoriesExist();
                    CardPresentationJsonIO.SaveAuthoring(dto);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[CardPresentation] seed deck.player 失败：" + ex.Message);
                }

                var entry = new CardPresentationEditorEntry { Dto = dto };
                entry.MarkSaved();
                deckEntries.Add(entry);
                deckById[PlayerDeckId] = entry;
            }

            if (faceById.TryGetValue("avatar.default", out var avatar)
                && avatar?.Dto != null
                && string.IsNullOrWhiteSpace(avatar.Dto.deckId))
            {
                avatar.Dto.deckId = PlayerDeckId;
                try
                {
                    CardPresentationJsonIO.SaveAuthoring(avatar.Dto);
                    avatar.MarkSaved();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[CardPresentation] 写入 avatar.default.deckId 失败：" + ex.Message);
                }
            }
        }

        private void LoadDeckEntriesFromDisk()
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

                if (!string.Equals(dto.kind, "Deck", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var id = dto.contentId;
                if (string.IsNullOrWhiteSpace(id) || deckById.ContainsKey(id))
                {
                    continue;
                }

                var entry = new CardPresentationEditorEntry { Dto = dto };
                entry.MarkSaved();
                deckEntries.Add(entry);
                deckById[id] = entry;
            }
        }

        private void LoadKnownSkillIdsFromDisk()
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

            var set = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < files.Length; i++)
            {
                if (!CardPresentationJsonIO.TryLoad(files[i], out var dto, out _) || dto == null)
                {
                    continue;
                }

                if (!string.Equals(dto.kind, "Skill", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(dto.contentId))
                {
                    continue;
                }

                set.Add(dto.contentId.Trim());
            }

            knownSkillIds.AddRange(set.OrderBy(id => id, StringComparer.Ordinal));
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
                FillMissingSpritePaths(dto, slots);
            }

            if (dto.effectAssemblies == null)
            {
                dto.effectAssemblies = Array.Empty<EffectAssemblyDto>();
            }

            if (dto.skillIds == null)
            {
                dto.skillIds = Array.Empty<string>();
            }

            if (dto.effectIds == null)
            {
                dto.effectIds = Array.Empty<string>();
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
                if (string.IsNullOrWhiteSpace(id) || faceById.ContainsKey(id))
                {
                    continue;
                }

                if (!CardPresentationMigration.IsPresentationEditorEntry(dto.kind, id))
                {
                    continue;
                }

                CardPresentationMigration.FillEmptyFromCore(dto, CoreCatalog, GetDisplayName);
                if (dto.effectAssemblies == null)
                {
                    dto.effectAssemblies = Array.Empty<EffectAssemblyDto>();
                }

                if (dto.skillIds == null)
                {
                    dto.skillIds = Array.Empty<string>();
                }

                var entry = new CardPresentationEditorEntry { Dto = dto };
                entry.MarkSaved();
                faceEntries.Add(entry);
                faceById[id] = entry;
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
            if (string.IsNullOrWhiteSpace(deckId) || deckId == UngroupedDeckId)
            {
                return UngroupedDeckId;
            }

            if (deckById.TryGetValue(deckId, out var deckEntry)
                && deckEntry?.Dto != null
                && !string.IsNullOrWhiteSpace(deckEntry.Dto.displayName))
            {
                return deckEntry.Dto.displayName.Trim() + " (" + deckId + ")";
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

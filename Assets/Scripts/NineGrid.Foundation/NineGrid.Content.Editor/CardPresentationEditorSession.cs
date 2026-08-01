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
    /// <summary>侧栏分区（解耦装配 IA）。</summary>
    public enum CardPresentationSidebarSection
    {
        Faces = 0,
        EffectPool = 1,
        Decks = 2,
        VfxLibrary = 3,
    }

    /// <summary>内容区焦点种类。</summary>
    public enum CardPresentationEditorFocusKind
    {
        None = 0,
        Face = 1,
        EffectTemplate = 2,
        Deck = 3,
        DescriptionGlossary = 4,
        VisualEffect = 5,
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

        /// <summary>
        /// 编辑器会话态：卡面描述是否已手改为自定义（不落盘）。
        /// 清空描述框后放权，重新跟随效果装配自动增删。
        /// </summary>
        public bool DescriptionCustomLocked { get; set; }

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

    public sealed class VisualEffectSidebarCategoryGroup
    {
        public string Category { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public Dictionary<string, VisualEffectSidebarVariantGroup> Variants { get; } =
            new Dictionary<string, VisualEffectSidebarVariantGroup>(StringComparer.Ordinal);
    }

    public sealed class VisualEffectSidebarVariantGroup
    {
        public string VariantId { get; set; } = string.Empty;
        public List<VisualEffectCatalogEditorIO.EditorRow> Entries { get; } =
            new List<VisualEffectCatalogEditorIO.EditorRow>();
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

    /// <summary>卡组下拉：显示名为主、deckId 作次要提示，持久化值仍为 deckId。</summary>
    public sealed class DeckChoice
    {
        public string DeckId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    /// <summary>
    /// 编辑器效果池分类（道具 / 遗物 / 怪物技能 / 机关技能）。JSON 模板仍同构；本枚举只约束默认可见集与同类多载。
    /// </summary>
    public enum EffectTemplateOriginCategory
    {
        Other = 0,
        Item = 1,
        Relic = 2,
        MonsterSkill = 3,
        TrapSkill = 4,
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
        private readonly List<VisualEffectCatalogEditorIO.EditorRow> visualEffects =
            new List<VisualEffectCatalogEditorIO.EditorRow>();
        private readonly List<string> knownSkillIds = new List<string>();

        private CardPresentationEditorFocusKind focusKind = CardPresentationEditorFocusKind.None;
        private string focusedContentId = string.Empty;
        private string focusedTemplateId = string.Empty;
        private string focusedVisualEffectId = string.Empty;

        public IReadOnlyList<CardPresentationEditorEntry> FaceEntries => faceEntries;
        public IReadOnlyList<CardPresentationEditorEntry> DeckEntries => deckEntries;
        public IReadOnlyList<EffectTemplateEditorIO.TemplateRow> EffectTemplates => effectTemplates;
        public IReadOnlyList<VisualEffectCatalogEditorIO.EditorRow> VisualEffects => visualEffects;
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

        public string FocusedVisualEffectId
        {
            get => focusedVisualEffectId;
            set => focusedVisualEffectId = value ?? string.Empty;
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

                for (var i = 0; i < visualEffects.Count; i++)
                {
                    if (visualEffects[i] != null && visualEffects[i].IsDirty)
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
            visualEffects.Clear();
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

            var vfxRows = VisualEffectCatalogEditorIO.LoadAll(out _);
            if (vfxRows.Count == 0)
            {
                vfxRows = VisualEffectCatalogEditorIO.ScanAndMerge(null, out _);
                if (vfxRows.Count > 0)
                {
                    VisualEffectCatalogEditorIO.TrySaveAll(vfxRows, out _);
                }
            }

            visualEffects.AddRange(vfxRows);

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

            if (!string.IsNullOrEmpty(focusedVisualEffectId)
                && visualEffects.All(v => v == null
                    || !string.Equals(v.Id, focusedVisualEffectId, StringComparison.Ordinal)))
            {
                focusedVisualEffectId = string.Empty;
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
            focusedVisualEffectId = string.Empty;
        }

        public void FocusDeck(string contentId)
        {
            focusKind = CardPresentationEditorFocusKind.Deck;
            focusedContentId = contentId ?? string.Empty;
            focusedTemplateId = string.Empty;
            focusedVisualEffectId = string.Empty;
        }

        public void FocusEffectTemplate(string templateId)
        {
            focusKind = CardPresentationEditorFocusKind.EffectTemplate;
            focusedTemplateId = templateId ?? string.Empty;
            focusedContentId = string.Empty;
            focusedVisualEffectId = string.Empty;
        }

        public void FocusDescriptionGlossary()
        {
            focusKind = CardPresentationEditorFocusKind.DescriptionGlossary;
            focusedContentId = string.Empty;
            focusedTemplateId = string.Empty;
            focusedVisualEffectId = string.Empty;
        }

        public void FocusVisualEffect(string visualEffectId)
        {
            focusKind = CardPresentationEditorFocusKind.VisualEffect;
            focusedVisualEffectId = visualEffectId ?? string.Empty;
            focusedContentId = string.Empty;
            focusedTemplateId = string.Empty;
        }

        public VisualEffectCatalogEditorIO.EditorRow GetFocusedVisualEffect()
        {
            if (focusKind != CardPresentationEditorFocusKind.VisualEffect
                || string.IsNullOrEmpty(focusedVisualEffectId))
            {
                return null;
            }

            for (var i = 0; i < visualEffects.Count; i++)
            {
                var row = visualEffects[i];
                if (row != null && string.Equals(row.Id, focusedVisualEffectId, StringComparison.Ordinal))
                {
                    return row;
                }
            }

            return null;
        }

        /// <summary>
        /// 侧栏：按一级分类 → 变体分组。每个变体挂其下 size×color 叶子条目。
        /// </summary>
        public List<VisualEffectSidebarCategoryGroup> GetVisualEffectSidebarGroups()
        {
            var byCategory = new Dictionary<string, VisualEffectSidebarCategoryGroup>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < visualEffects.Count; i++)
            {
                var row = visualEffects[i];
                var dto = row?.Dto;
                if (dto == null || string.IsNullOrWhiteSpace(dto.category) || string.IsNullOrWhiteSpace(dto.variantId))
                {
                    continue;
                }

                if (!byCategory.TryGetValue(dto.category, out var cat))
                {
                    cat = new VisualEffectSidebarCategoryGroup
                    {
                        Category = dto.category,
                        Title = VisualEffectCatalogEditorIO.GetCategoryLabel(dto.category),
                    };
                    byCategory[dto.category] = cat;
                }

                if (!cat.Variants.TryGetValue(dto.variantId, out var variant))
                {
                    variant = new VisualEffectSidebarVariantGroup
                    {
                        VariantId = dto.variantId,
                    };
                    cat.Variants[dto.variantId] = variant;
                }

                variant.Entries.Add(row);
            }

            var ordered = new List<VisualEffectSidebarCategoryGroup>();
            for (var i = 0; i < VisualEffectCatalogEditorIO.CategoryLabels.Length; i++)
            {
                var folder = VisualEffectCatalogEditorIO.CategoryLabels[i].Folder;
                if (byCategory.TryGetValue(folder, out var known))
                {
                    SortVariantEntries(known);
                    ordered.Add(known);
                    byCategory.Remove(folder);
                }
            }

            foreach (var orphan in byCategory.Values.OrderBy(c => c.Category, StringComparer.OrdinalIgnoreCase))
            {
                SortVariantEntries(orphan);
                ordered.Add(orphan);
            }

            return ordered;
        }

        private static void SortVariantEntries(VisualEffectSidebarCategoryGroup cat)
        {
            foreach (var variant in cat.Variants.Values)
            {
                variant.Entries.Sort((a, b) =>
                {
                    var cmp = string.Compare(a.Dto?.size, b.Dto?.size, StringComparison.OrdinalIgnoreCase);
                    if (cmp != 0)
                    {
                        return cmp;
                    }

                    return string.Compare(a.Dto?.color, b.Dto?.color, StringComparison.OrdinalIgnoreCase);
                });
            }
        }

        /// <summary>选中某变体时默认叶子：优先 large，其次字典序第一。</summary>
        public VisualEffectCatalogEditorIO.EditorRow PreferDefaultLeafForVariant(string category, string variantId)
        {
            VisualEffectCatalogEditorIO.EditorRow best = null;
            for (var i = 0; i < visualEffects.Count; i++)
            {
                var row = visualEffects[i];
                var dto = row?.Dto;
                if (dto == null)
                {
                    continue;
                }

                if (!string.Equals(dto.category, category, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(dto.variantId, variantId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (best == null)
                {
                    best = row;
                    continue;
                }

                var bestLarge = string.Equals(best.Dto.size, "large", StringComparison.OrdinalIgnoreCase);
                var curLarge = string.Equals(dto.size, "large", StringComparison.OrdinalIgnoreCase);
                if (curLarge && !bestLarge)
                {
                    best = row;
                    continue;
                }

                if (curLarge == bestLarge
                    && string.CompareOrdinal(dto.id, best.Dto.id) < 0)
                {
                    best = row;
                }
            }

            return best;
        }

        public IReadOnlyList<VisualEffectCatalogEditorIO.EditorRow> GetLeavesForVariant(string category, string variantId)
        {
            var list = new List<VisualEffectCatalogEditorIO.EditorRow>();
            for (var i = 0; i < visualEffects.Count; i++)
            {
                var row = visualEffects[i];
                var dto = row?.Dto;
                if (dto == null)
                {
                    continue;
                }

                if (string.Equals(dto.category, category, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(dto.variantId, variantId, StringComparison.Ordinal))
                {
                    list.Add(row);
                }
            }

            list.Sort((a, b) =>
            {
                var cmp = string.Compare(a.Dto?.size, b.Dto?.size, StringComparison.OrdinalIgnoreCase);
                if (cmp != 0)
                {
                    return cmp;
                }

                return string.Compare(a.Dto?.color, b.Dto?.color, StringComparison.OrdinalIgnoreCase);
            });
            return list;
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

        /// <summary>
        /// 卡面「所属卡组」下拉：Label 显示名优先（编码括号提示），Value 仍为 deckId。
        /// </summary>
        public List<DeckChoice> GetDeckChoices()
        {
            var ids = GetDeckIdChoices();
            var list = new List<DeckChoice>(ids.Count);
            for (var i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                list.Add(new DeckChoice
                {
                    DeckId = id,
                    Label = FormatDeckChoiceLabel(id),
                });
            }

            return list
                .OrderBy(c => c.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>侧栏分组标题与卡组下拉共用：有显示名则「显示名 (deckId)」，否则裸 id。</summary>
        public string FormatDeckChoiceLabel(string deckId)
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

        /// <summary>所属卡组是否至少配置了一个卡背槽（供编辑器提示）。</summary>
        public bool DeckHasAnyBackSprite(string deckId)
        {
            if (!TryGetDeckDto(deckId, out var deckDto) || deckDto?.sprites == null)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(deckDto.sprites.backBorder)
                   || !string.IsNullOrWhiteSpace(deckDto.sprites.backShirt)
                   || !string.IsNullOrWhiteSpace(deckDto.sprites.backLogo);
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
        /// 装配下拉用：Label = 参数化 design_text（<c>{amount}</c> 而非 peer 预设数字）；
        /// Value 仍为 templateId。未指定容器时返回全库（侧栏/诊断）；指定后只返回同类。
        /// </summary>
        public List<EffectTemplateChoice> GetEffectTemplateChoices()
        {
            return GetEffectTemplateChoices(containerTypeFilter: null, includeCategorySuffix: true);
        }

        /// <param name="containerTypeFilter">
        /// <c>HelpCard</c> / <c>Relic</c> / <c>MonsterSkill</c>；空则不过滤。
        /// </param>
        /// <param name="includeCategorySuffix">同类池内默认 false（分类已由卡种决定）；跨类 orphan 显示可开。</param>
        public List<EffectTemplateChoice> GetEffectTemplateChoices(
            string containerTypeFilter,
            bool includeCategorySuffix = false)
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

                if (!string.IsNullOrWhiteSpace(containerTypeFilter)
                    && !TemplateMatchesContainerType(row.id, containerTypeFilter))
                {
                    continue;
                }

                var parameterized = EffectDesignTextParameterizer.Parameterize(
                    row.design_text,
                    row.body,
                    (IReadOnlyDictionary<string, object>)null);
                var rawLabel = FormatEffectTemplateChoiceLabel(
                    row.id,
                    parameterized,
                    includeCategorySuffix);
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
        /// 效果池显示名：中文 design_text；可选附加「（道具/遗物/怪物技能效果）」分类标注。
        /// </summary>
        public static string FormatEffectTemplateChoiceLabel(string templateId, string designText)
        {
            return FormatEffectTemplateChoiceLabel(templateId, designText, includeCategorySuffix: true);
        }

        public static string FormatEffectTemplateChoiceLabel(
            string templateId,
            string designText,
            bool includeCategorySuffix)
        {
            var body = string.IsNullOrWhiteSpace(designText)
                ? (string.IsNullOrWhiteSpace(templateId) ? "（无模板）" : templateId.Trim())
                : designText.Trim();
            if (!includeCategorySuffix)
            {
                return body;
            }

            var origin = ResolveEffectOriginSuffix(templateId);
            return string.IsNullOrEmpty(origin) ? body : body + origin;
        }

        /// <summary>
        /// 模板是否属于某装配容器类（编辑器同类多载门禁；运行时 JSON 仍同构可跨类引用）。
        /// </summary>
        public static bool TemplateMatchesContainerType(string templateId, string containerType)
        {
            if (string.IsNullOrWhiteSpace(containerType))
            {
                return true;
            }

            var category = ResolveEffectTemplateCategory(templateId);
            var ct = containerType.Trim();
            if (string.Equals(ct, "HelpCard", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ct, "Item", StringComparison.OrdinalIgnoreCase))
            {
                return category == EffectTemplateOriginCategory.Item;
            }

            if (string.Equals(ct, "Relic", StringComparison.OrdinalIgnoreCase))
            {
                return category == EffectTemplateOriginCategory.Relic;
            }

            if (string.Equals(ct, "MonsterSkill", StringComparison.OrdinalIgnoreCase))
            {
                return category == EffectTemplateOriginCategory.MonsterSkill;
            }

            if (string.Equals(ct, "Trap", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ct, "TrapSkill", StringComparison.OrdinalIgnoreCase))
            {
                return category == EffectTemplateOriginCategory.TrapSkill;
            }

            return false;
        }

        /// <summary>侧栏分组标题。</summary>
        public static string ResolveEffectCategoryTitle(EffectTemplateOriginCategory category)
        {
            switch (category)
            {
                case EffectTemplateOriginCategory.Item:
                    return "道具效果";
                case EffectTemplateOriginCategory.Relic:
                    return "遗物效果";
                case EffectTemplateOriginCategory.MonsterSkill:
                    return "怪物技能效果";
                case EffectTemplateOriginCategory.TrapSkill:
                    return "机关技能效果";
                default:
                    return "其他效果";
            }
        }

        /// <summary>
        /// 模板简要描述参数化：用实参字面量把 design_text 里的预设数换成 <c>{param}</c>。
        /// </summary>
        public string GetParameterizedDesignText(string templateId, string argsJson)
        {
            if (!TryGetEffectTemplateRow(templateId, out var designText, out var bodyJson))
            {
                return string.IsNullOrWhiteSpace(templateId) ? string.Empty : templateId.Trim();
            }

            var sourceArgs = string.IsNullOrWhiteSpace(argsJson)
                ? SuggestArgsJsonForTemplate(templateId)
                : argsJson;
            return EffectDesignTextParameterizer.Parameterize(designText, bodyJson, sourceArgs);
        }

        /// <summary>
        /// 按当前装配列表拼自动卡面描述（多条用中文分号连接；空装配 → 空串）。
        /// </summary>
        public string BuildAutoCardDescription(IReadOnlyList<EffectAssemblyDto> assemblies)
        {
            if (assemblies == null || assemblies.Count == 0)
            {
                return string.Empty;
            }

            var briefs = new List<string>(assemblies.Count);
            for (var i = 0; i < assemblies.Count; i++)
            {
                var assembly = assemblies[i];
                if (assembly == null || string.IsNullOrWhiteSpace(assembly.templateId))
                {
                    continue;
                }

                var brief = GetParameterizedDesignText(assembly.templateId, assembly.argsJson);
                if (!string.IsNullOrWhiteSpace(brief))
                {
                    briefs.Add(brief);
                }
            }

            return EffectDesignTextParameterizer.JoinBriefs(briefs);
        }

        /// <summary>
        /// 根据当前描述推断是否应锁定自定义：空 / 与自动文案全等 → 不锁；否则锁定。
        /// </summary>
        public bool InferDescriptionCustomLocked(CardPresentationConfigDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.description))
            {
                return false;
            }

            var auto = BuildAutoCardDescription(dto.effectAssemblies);
            return !string.Equals(dto.description.Trim(), auto.Trim(), StringComparison.Ordinal);
        }

        /// <summary>未锁定时把卡面描述写成装配简要拼接。</summary>
        public bool TrySyncAutoDescription(CardPresentationConfigDto dto, bool customLocked)
        {
            if (dto == null || customLocked)
            {
                return false;
            }

            var auto = BuildAutoCardDescription(dto.effectAssemblies);
            if (string.Equals(dto.description ?? string.Empty, auto, StringComparison.Ordinal))
            {
                return false;
            }

            dto.description = auto;
            return true;
        }

        private bool TryGetEffectTemplateRow(string templateId, out string designText, out string bodyJson)
        {
            designText = string.Empty;
            bodyJson = string.Empty;
            if (string.IsNullOrWhiteSpace(templateId))
            {
                return false;
            }

            var id = templateId.Trim();
            for (var i = 0; i < effectTemplates.Count; i++)
            {
                var row = effectTemplates[i];
                if (row == null || !string.Equals(row.id, id, StringComparison.Ordinal))
                {
                    continue;
                }

                designText = row.design_text ?? string.Empty;
                bodyJson = row.body ?? string.Empty;
                return true;
            }

            if (EffectTemplateCatalog.TryGet(id, out var template) && template != null)
            {
                designText = template.DesignText ?? string.Empty;
                bodyJson = template.BodyJson ?? string.Empty;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 按模板 id 推断编辑器分类：标准前缀 <c>tpl.help/relic/skill/trap.*</c>；
        /// 共享/遗留 id 再按名称中的 help/relic/skill/trap 段启发式归类。
        /// </summary>
        public static EffectTemplateOriginCategory ResolveEffectTemplateCategory(string templateId)
        {
            if (string.IsNullOrWhiteSpace(templateId))
            {
                return EffectTemplateOriginCategory.Other;
            }

            var id = templateId.Trim();
            if (id.StartsWith("tpl.help.", StringComparison.OrdinalIgnoreCase))
            {
                return EffectTemplateOriginCategory.Item;
            }

            if (id.StartsWith("tpl.relic.", StringComparison.OrdinalIgnoreCase))
            {
                return EffectTemplateOriginCategory.Relic;
            }

            if (id.StartsWith("tpl.skill.", StringComparison.OrdinalIgnoreCase))
            {
                return EffectTemplateOriginCategory.MonsterSkill;
            }

            if (id.StartsWith("tpl.trap.", StringComparison.OrdinalIgnoreCase))
            {
                return EffectTemplateOriginCategory.TrapSkill;
            }

            // shared / 遗留无前缀：如 tpl.shared.3.help_…、tpl.gain_armor_on_use_help_card
            if (id.IndexOf("help", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return EffectTemplateOriginCategory.Item;
            }

            if (id.IndexOf("relic", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return EffectTemplateOriginCategory.Relic;
            }

            if (id.IndexOf("skill", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return EffectTemplateOriginCategory.MonsterSkill;
            }

            if (id.IndexOf("trap", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return EffectTemplateOriginCategory.TrapSkill;
            }

            return EffectTemplateOriginCategory.Other;
        }

        /// <summary>tpl.help.* →（道具效果）；tpl.skill.* →（怪物技能效果）；tpl.relic.* →（遗物效果）；tpl.trap.* →（机关技能效果）。</summary>
        public static string ResolveEffectOriginSuffix(string templateId)
        {
            switch (ResolveEffectTemplateCategory(templateId))
            {
                case EffectTemplateOriginCategory.Item:
                    return "（道具效果）";
                case EffectTemplateOriginCategory.MonsterSkill:
                    return "（怪物技能效果）";
                case EffectTemplateOriginCategory.Relic:
                    return "（遗物效果）";
                case EffectTemplateOriginCategory.TrapSkill:
                    return "（机关技能效果）";
                default:
                    return string.IsNullOrWhiteSpace(templateId) ? string.Empty : "（其他效果）";
            }
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

        /// <summary>
        /// 选中效果模板时填充 argsJson：优先复用同模板已有装配实参（如 skill.stray_cub），
        /// 否则按 body 占位符生成最小 JSON。避免 UI 默认 <c>{}</c> 把 <c>{{value}}</c> 落成加 0。
        /// </summary>
        public string SuggestArgsJsonForTemplate(string templateId)
        {
            if (string.IsNullOrWhiteSpace(templateId))
            {
                return "{}";
            }

            var id = templateId.Trim();
            string bodyJson = null;
            for (var i = 0; i < effectTemplates.Count; i++)
            {
                var row = effectTemplates[i];
                if (row != null && string.Equals(row.id, id, StringComparison.Ordinal))
                {
                    bodyJson = row.body;
                    break;
                }
            }

            if (bodyJson == null
                && EffectTemplateCatalog.TryGet(id, out var template)
                && template != null)
            {
                bodyJson = template.BodyJson;
            }

            var peerArgs = FindPeerArgsJsonForTemplate(id);
            return EffectAssemblyResolver.SuggestArgsJson(bodyJson, peerArgs);
        }

        private static string FindPeerArgsJsonForTemplate(string templateId)
        {
            foreach (var contentId in CardPresentationConfigCatalog.AllContentIds)
            {
                if (!CardPresentationConfigCatalog.TryGet(contentId, out var dto)
                    || dto?.effectAssemblies == null)
                {
                    continue;
                }

                for (var i = 0; i < dto.effectAssemblies.Length; i++)
                {
                    var assembly = dto.effectAssemblies[i];
                    if (assembly == null
                        || !string.Equals(assembly.templateId, templateId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var args = assembly.argsJson;
                    if (!string.IsNullOrWhiteSpace(args) && args.Trim() != "{}")
                    {
                        return args.Trim();
                    }
                }
            }

            return null;
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

                var dirtyVfx = visualEffects.Where(v => v != null && v.IsDirty).ToList();
                if (dirtyVfx.Count > 0)
                {
                    if (!VisualEffectCatalogEditorIO.TrySaveAll(visualEffects, out error))
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

                if (focusKind == CardPresentationEditorFocusKind.VisualEffect)
                {
                    var row = GetFocusedVisualEffect();
                    if (row == null)
                    {
                        error = "无选中特效。";
                        return false;
                    }

                    if (!row.IsDirty)
                    {
                        return true;
                    }

                    return VisualEffectCatalogEditorIO.TrySaveAll(visualEffects, out error);
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

        public bool TryCreateDeck(string contentId, string displayName, out string error)
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
                return cvk == ContentVisualKind.Avatar
                    || cvk == ContentVisualKind.Monster
                    || cvk == ContentVisualKind.Trap;
            }

            var k = kind.Trim();
            return string.Equals(k, "Avatar", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(k, "Monster", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(k, "Trap", StringComparison.OrdinalIgnoreCase);
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

        private string ResolveDeckTitle(string deckId) => FormatDeckChoiceLabel(deckId);

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

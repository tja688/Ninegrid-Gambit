using System;
using System.Collections.Generic;
using NineGrid.Core.Effects;

namespace NineGrid.Core.Localization
{
    /// <summary>
    /// 轻量本地化目录（静态门面；性质是内容目录/装配缝，不是业务规则状态 Sink）。
    /// 从 <c>Resources/Localization/{lang}/</c> 加载三张翻译表并提供查询：
    /// cards（键 = contentId / deckId）、glossary（键 = 现行中文词条名）、ui（扁平键）。
    /// 加载纪律：缺文件 = 空表 = 全回退中文（静默，不报错刷屏）；坏 JSON 不替换旧表。
    /// 语言切换由 <c>ILanguageSettingsSystem</c> 驱动：写 PlayerPrefs → 本目录重载表 → 发 Changed。
    /// </summary>
    public static class LocalizationCatalog
    {
        /// <summary>cards 表单条：字段全部可选，null 表示缺字段（回退中文）。</summary>
        public sealed class CardTextEntry
        {
            public string DisplayName;
            public string Description;
            public string FaceIntro;
        }

        /// <summary>glossary 表单条：displayName 为目标语言词条名，intro 为解释正文。</summary>
        public sealed class GlossaryEntry
        {
            public string DisplayName;
            public string Intro;
        }

        private const string ResourcesRoot = "Localization/";

        private static Func<string, string> sTableTextLoader;
        private static Action<string> sWarningLogger;

        private static readonly Dictionary<string, CardTextEntry> Cards =
            new Dictionary<string, CardTextEntry>(StringComparer.Ordinal);

        private static readonly Dictionary<string, GlossaryEntry> Glossary =
            new Dictionary<string, GlossaryEntry>(StringComparer.Ordinal);

        private static readonly Dictionary<string, string> Ui =
            new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>当前语言码（默认 zh；由语言设置系统在装配时写入）。</summary>
        public static string CurrentLanguage { get; private set; } = LanguageId.Zh;

        /// <summary>是否为源语言（zh 不查表，全部走内联中文）。</summary>
        public static bool IsSourceLanguage => LanguageId.IsSource(CurrentLanguage);

        /// <summary>
        /// 表代数：每次语言切换 / 表重载 +1。缓存了表内容的消费者
        /// （如词条双键 lookup、卡面 DTO 覆盖缓存）据此失效。
        /// </summary>
        public static int TablesVersion { get; private set; } = 1;

        /// <summary>由 Unity 装配层注入 Resources 文本读取与告警输出，保持 Core 无引擎引用。</summary>
        public static void ConfigureRuntime(
            Func<string, string> tableTextLoader,
            Action<string> warningLogger = null)
        {
            sTableTextLoader = tableTextLoader;
            sWarningLogger = warningLogger;
        }

        /// <summary>切语言：规整语言码 → 重载表 → 代数 +1。zh 清空三表（全走默认值）。</summary>
        public static void SetLanguage(string code)
        {
            var normalized = LanguageId.Normalize(code);
            CurrentLanguage = normalized;
            ReloadTables();
        }

        /// <summary>按当前语言重载三表（Editor 改表后也可显式调用）。</summary>
        public static void ReloadTables()
        {
            if (IsSourceLanguage)
            {
                Cards.Clear();
                Glossary.Clear();
                Ui.Clear();
            }
            else
            {
                LoadCardsTable(CurrentLanguage);
                LoadGlossaryTable(CurrentLanguage);
                LoadUiTable(CurrentLanguage);
            }

            TablesVersion++;
        }

        /// <summary>ui 表查询：缺键 / 源语言直接回中文默认值，永不空串。</summary>
        public static string TranslateUi(string key, string zhDefault)
        {
            if (IsSourceLanguage
                || string.IsNullOrEmpty(key)
                || !Ui.TryGetValue(key, out var value)
                || string.IsNullOrEmpty(value))
            {
                return zhDefault;
            }

            return value;
        }

        /// <summary>cards 表查询（键 = contentId / deckId）；源语言恒 false。</summary>
        public static bool TryGetCardText(string contentId, out CardTextEntry entry)
        {
            entry = null;
            if (IsSourceLanguage || string.IsNullOrWhiteSpace(contentId))
            {
                return false;
            }

            return Cards.TryGetValue(contentId.Trim(), out entry) && entry != null;
        }

        /// <summary>glossary 表查询（键 = 中文词条名，Trim 后）；源语言恒 false。</summary>
        public static bool TryGetGlossary(string zhName, out GlossaryEntry entry)
        {
            entry = null;
            if (IsSourceLanguage || string.IsNullOrWhiteSpace(zhName))
            {
                return false;
            }

            return Glossary.TryGetValue(zhName.Trim(), out entry) && entry != null;
        }

        /// <summary>词条显示名按当前语言解析：缺翻译回中文名。</summary>
        public static string ResolveGlossaryDisplayName(string zhName)
        {
            if (TryGetGlossary(zhName, out var entry) && !string.IsNullOrWhiteSpace(entry.DisplayName))
            {
                return entry.DisplayName.Trim();
            }

            return zhName;
        }

        /// <summary>词条解释按当前语言解析：缺翻译回中文正文。</summary>
        public static string ResolveGlossaryIntro(string zhName, string zhExplanation)
        {
            if (TryGetGlossary(zhName, out var entry) && !string.IsNullOrWhiteSpace(entry.Intro))
            {
                return entry.Intro;
            }

            return zhExplanation;
        }

        private static void LoadCardsTable(string language)
        {
            if (!TryParseEntries(language, "cards", out var entries))
            {
                return; // 坏 JSON：保留旧表。缺文件：TryParseEntries 已清空。
            }

            Cards.Clear();
            foreach (var pair in entries)
            {
                var node = new EffectDslNode(pair.Value);
                if (!node.IsObject)
                {
                    continue;
                }

                Cards[pair.Key] = new CardTextEntry
                {
                    DisplayName = ReadOptionalString(node, "displayName"),
                    Description = ReadOptionalString(node, "description"),
                    FaceIntro = ReadOptionalString(node, "faceIntro"),
                };
            }
        }

        private static void LoadGlossaryTable(string language)
        {
            if (!TryParseEntries(language, "glossary", out var entries))
            {
                return;
            }

            Glossary.Clear();
            foreach (var pair in entries)
            {
                var node = new EffectDslNode(pair.Value);
                if (!node.IsObject)
                {
                    continue;
                }

                var key = pair.Key?.Trim();
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                Glossary[key] = new GlossaryEntry
                {
                    DisplayName = ReadOptionalString(node, "displayName"),
                    Intro = ReadOptionalString(node, "intro"),
                };
            }
        }

        private static void LoadUiTable(string language)
        {
            if (!TryParseEntries(language, "ui", out var entries))
            {
                return;
            }

            Ui.Clear();
            foreach (var pair in entries)
            {
                if (pair.Value is string text && !string.IsNullOrEmpty(text))
                {
                    Ui[pair.Key] = text;
                }
            }
        }

        /// <summary>
        /// 读取并解析一张表的 entries 对象。
        /// 返回 false 且不动旧表 = 坏 JSON；返回 true + 空集合 = 缺文件/空表（调用方清空旧表）。
        /// </summary>
        private static bool TryParseEntries(
            string language,
            string tableName,
            out Dictionary<string, object> entries)
        {
            entries = new Dictionary<string, object>(StringComparer.Ordinal);
            var text = sTableTextLoader?.Invoke(ResourcesRoot + language + "/" + tableName);
            if (string.IsNullOrWhiteSpace(text))
            {
                // 缺文件 = 空表 = 全回退中文；并行翻译表可后到，不许报错刷屏。
                return true;
            }

            try
            {
                var root = EffectJson.Parse(text);
                var entriesNode = root.Get("entries");
                if (!(entriesNode.RawValue is Dictionary<string, object> raw))
                {
                    sWarningLogger?.Invoke(
                        "[LocalizationCatalog] " + language + "/" + tableName
                        + ".json 缺 entries 对象，保留旧表。");
                    return false;
                }

                foreach (var pair in raw)
                {
                    entries[pair.Key] = pair.Value;
                }

                return true;
            }
            catch (Exception ex)
            {
                sWarningLogger?.Invoke(
                    "[LocalizationCatalog] 解析 " + language + "/" + tableName
                    + ".json 失败，保留旧表：" + ex.Message);
                return false;
            }
        }

        private static string ReadOptionalString(EffectDslNode node, string key)
        {
            var value = node.Get(key);
            if (value.IsNull)
            {
                return null;
            }

            var text = value.AsString(null);
            return string.IsNullOrEmpty(text) ? null : text;
        }
    }
}

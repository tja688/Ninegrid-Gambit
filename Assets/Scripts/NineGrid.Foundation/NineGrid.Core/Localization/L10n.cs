using System;

namespace NineGrid.Core.Localization
{
    /// <summary>
    /// 语言码（字符串码持久化，可扩展）。中文是唯一真源语言：
    /// 任何语言缺键一律回退中文默认值，永不空串（ADR-0046）。
    /// </summary>
    public static class LanguageId
    {
        public const string Zh = "zh";
        public const string En = "en";

        /// <summary>规整任意输入为受支持语言码；未知输入回退 zh。</summary>
        public static string Normalize(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return Zh;
            }

            code = code.Trim().ToLowerInvariant();
            return string.Equals(code, En, StringComparison.Ordinal) ? En : Zh;
        }

        /// <summary>是否为源语言（zh：直接使用代码/场景/JSON 内联中文，不查表）。</summary>
        public static bool IsSource(string code)
        {
            return !string.Equals(Normalize(code), En, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// 代码串翻译门面：<c>L10n.Tr("notice.gold_insufficient", "金币不足")</c>。
    /// 中文默认值内联在调用点（zh 即走默认值；en 查 ui 表，缺键回中文）。
    /// 含格式化的用 <see cref="Tr"/> 取 <c>{0}</c> 模板再 string.Format。
    /// </summary>
    public static class L10n
    {
        public static string Tr(string key, string zhDefault)
        {
            return LocalizationCatalog.TranslateUi(key, zhDefault);
        }
    }
}

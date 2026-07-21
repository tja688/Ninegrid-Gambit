namespace NineGrid.LivingUI
{
    /// <summary>
    /// 内容命名约定：「（原初元素）」注解不参与同名匹配；其它括号（如限字）仍属身份的一部分。
    /// </summary>
    public static class LivingUiContentNames
    {
        public const string PrimordialSuffix = "（原初元素）";
        public const string PrimordialSuffixAscii = "(原初元素)";

        /// <summary>
        /// 去掉原初注解后的基础名。例：血量图标（原初元素）→ 血量图标；
        /// Notice Text（限 62 ）保持不变。
        /// </summary>
        public static string BaseName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;

            var result = name;
            // 可重复剥离，避免误叠后缀
            while (true)
            {
                var next = result
                    .Replace(PrimordialSuffix, string.Empty)
                    .Replace(PrimordialSuffixAscii, string.Empty);
                if (next == result) break;
                result = next;
            }

            return result.Trim();
        }

        public static bool BaseNameEquals(string a, string b)
        {
            return BaseName(a) == BaseName(b);
        }

        public static string EnsurePrimordialName(string name)
        {
            var baseName = BaseName(name);
            if (string.IsNullOrEmpty(baseName)) return PrimordialSuffix;
            return baseName + PrimordialSuffix;
        }
    }
}

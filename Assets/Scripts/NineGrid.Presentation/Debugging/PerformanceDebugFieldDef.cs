namespace NineGrid.Presentation.Debugging
{
    public sealed class PerformanceDebugFieldDef
    {
        public PerformanceDebugFieldDef(
            string key,
            string label,
            PerformanceDebugParamKind kind,
            string defaultValue = "",
            string[] enumOptions = null)
        {
            Key = key;
            Label = label;
            Kind = kind;
            DefaultValue = defaultValue ?? string.Empty;
            EnumOptions = enumOptions ?? System.Array.Empty<string>();
        }

        public string Key { get; }
        public string Label { get; }
        public PerformanceDebugParamKind Kind { get; }
        public string DefaultValue { get; }
        public string[] EnumOptions { get; }
    }
}

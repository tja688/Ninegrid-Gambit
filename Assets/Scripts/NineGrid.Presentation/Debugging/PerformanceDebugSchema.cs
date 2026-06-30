using System.Collections.Generic;

namespace NineGrid.Presentation.Debugging
{
    public sealed class PerformanceDebugSchema
    {
        private readonly List<PerformanceDebugFieldDef> fields = new();

        public IReadOnlyList<PerformanceDebugFieldDef> Fields => fields;

        public PerformanceDebugSchema Add(
            string key,
            string label,
            PerformanceDebugParamKind kind,
            string defaultValue = "",
            params string[] enumOptions)
        {
            fields.Add(new PerformanceDebugFieldDef(key, label, kind, defaultValue, enumOptions));
            return this;
        }

        public PerformanceDebugPayload CreateDefaultPayload()
        {
            var payload = new PerformanceDebugPayload();
            for (var i = 0; i < fields.Count; i++)
            {
                PerformanceDebugFieldDef field = fields[i];
                payload.Set(field.Key, field.DefaultValue);
            }

            return payload;
        }
    }
}

using NineGrid.Presentation.Shared;

namespace NineGrid.Presentation.Debugging
{
    internal static class PerformanceDebugSerializationUtil
    {
        public static void SetField(UnityEngine.Component component, string fieldName, object value)
        {
            PresentationSerializationUtil.SetField(component, fieldName, value);
        }

        public static void SetField(object target, string fieldName, object value)
        {
            PresentationSerializationUtil.SetField(target, fieldName, value);
        }
    }
}

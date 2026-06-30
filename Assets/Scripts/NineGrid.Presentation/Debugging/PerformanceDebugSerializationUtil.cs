using System.Reflection;
using UnityEngine;

namespace NineGrid.Presentation.Debugging
{
    internal static class PerformanceDebugSerializationUtil
    {
        public static void SetField(Component component, string fieldName, object value)
        {
            SetField((object)component, fieldName, value);
        }

        public static void SetField(object target, string fieldName, object value)
        {
            if (target == null || string.IsNullOrEmpty(fieldName))
            {
                return;
            }

            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            field?.SetValue(target, value);
        }
    }
}

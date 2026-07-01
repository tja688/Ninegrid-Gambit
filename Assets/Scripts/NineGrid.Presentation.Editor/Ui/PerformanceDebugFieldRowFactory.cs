#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NineGrid.Presentation.Debugging;
using NineGrid.Presentation.Editor.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace NineGrid.Presentation.Editor.Ui
{
    public static class PerformanceDebugFieldRowFactory
    {
        public delegate void FieldChangedHandler(string key, string value);

        public static VisualElement CreateParamRow(
            PerformanceDebugFieldDef field,
            string currentValue,
            PerformanceDebugPayload workingPayload,
            FieldChangedHandler onChanged,
            Action refreshDerivedDirection)
        {
            string description = DescribeField(field);

            switch (field.Kind)
            {
                case PerformanceDebugParamKind.Bool:
                {
                    bool isOn = currentValue is "1" or "true" or "True" or "yes" or "Yes";
                    var toggle = new Toggle { value = isOn };
                    toggle.RegisterValueChangedCallback(evt =>
                    {
                        onChanged?.Invoke(field.Key, evt.newValue ? "true" : "false");
                        refreshDerivedDirection?.Invoke();
                    });
                    return PerformanceDebugWarmConsoleUi.WrapControl(field.Label, description, toggle);
                }
                case PerformanceDebugParamKind.Derived:
                {
                    var derivedLabel = PerformanceDebugWarmConsoleUi.CreateDescriptionLabel(
                        PerformanceDebugLayoutApplier.FormatDerivedDirection(workingPayload));
                    derivedLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                    derivedLabel.name = "derived-direction-label";
                    return PerformanceDebugWarmConsoleUi.WrapControl(
                        field.Label, "由 Player/Target Slot 推导（只读）", derivedLabel);
                }
                case PerformanceDebugParamKind.Enum:
                case PerformanceDebugParamKind.ContextPreset:
                {
                    var options = new List<string>(field.EnumOptions);
                    if (options.Count == 0)
                    {
                        options.Add(currentValue);
                    }

                    int index = Mathf.Max(0, options.IndexOf(currentValue));
                    var popup = new PopupField<string>(options, index);
                    popup.RegisterValueChangedCallback(evt =>
                    {
                        onChanged?.Invoke(field.Key, evt.newValue);
                        refreshDerivedDirection?.Invoke();
                    });
                    return PerformanceDebugWarmConsoleUi.WrapControl(field.Label, description, popup);
                }
                default:
                {
                    var textField = new TextField { value = currentValue };
                    textField.RegisterValueChangedCallback(evt =>
                    {
                        onChanged?.Invoke(field.Key, evt.newValue ?? string.Empty);
                        refreshDerivedDirection?.Invoke();
                    });
                    return PerformanceDebugWarmConsoleUi.WrapControl(field.Label, description, textField);
                }
            }
        }

        public static void RefreshDerivedLabels(VisualElement container, PerformanceDebugPayload payload)
        {
            if (container == null || payload == null)
            {
                return;
            }

            container.Query<Label>(name: "derived-direction-label").ForEach(label =>
            {
                label.text = PerformanceDebugLayoutApplier.FormatDerivedDirection(payload);
            });
        }

        public static string DescribeField(PerformanceDebugFieldDef field)
        {
            return field.Kind switch
            {
                PerformanceDebugParamKind.Int => "整数",
                PerformanceDebugParamKind.Float => "浮点数",
                PerformanceDebugParamKind.Bool => "true / false",
                PerformanceDebugParamKind.ActorId => "演员 ID，如 player / enemy",
                PerformanceDebugParamKind.AnchorId => "锚点 ID：grid.slot3 / deck.slot5 / hand.handcard1",
                PerformanceDebugParamKind.ContextPreset => "重建场景演员布局",
                PerformanceDebugParamKind.Enum => "枚举选项",
                PerformanceDebugParamKind.Derived => "由槽位几何推导（只读）",
                _ => field.Key,
            };
        }
    }
}
#endif

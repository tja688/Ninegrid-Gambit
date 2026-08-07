#if UNITY_EDITOR
using System;
using System.Reflection;
using NineGrid.Flow.Presentation;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    public static class AudioCueDeclarationEditor
    {
        [MenuItem("NineGrid/Tools/Scan Audio Cue Declarations")]
        public static void ScanMenu()
        {
            var result = AudioCueDeclarationScanner.Scan(Assembly.Load("NineGrid.Presentation"));
            if (result.Findings.Count == 0)
            {
                Debug.Log("[AudioCueDeclarationScanner] declarations=" + result.Declarations.Count + " findings=0");
                return;
            }

            var lines = new string[result.Findings.Count];
            for (var i = 0; i < result.Findings.Count; i++)
            {
                var finding = result.Findings[i];
                lines[i] = finding.CueId + " " + finding.FieldName + " " + finding.Message;
            }

            Debug.LogError(
                "[AudioCueDeclarationScanner] declarations=" + result.Declarations.Count
                + " findings=" + result.Findings.Count + "\n"
                + string.Join("\n", lines));
        }
    }
}
#endif

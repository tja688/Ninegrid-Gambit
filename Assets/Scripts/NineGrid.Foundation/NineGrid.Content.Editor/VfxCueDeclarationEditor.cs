#if UNITY_EDITOR
using System.Reflection;
using NineGrid.Content.Vfx;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
  public static class VfxCueDeclarationEditor
  {
    [MenuItem("NineGrid/Tools/Scan VFX Declarations")]
    public static void ScanMenu()
    {
      var result = VfxDeclarationScanner.Scan(
          VfxBindingCatalog.LoadFromResources(),
          Assembly.Load("NineGrid.Presentation"));
      if (result.Findings.Count == 0)
      {
        Debug.Log(
            "[VfxDeclarationScanner] cues=" + result.CueDeclarations.Count
            + " states=" + result.StateDeclarations.Count
            + " findings=0");
        return;
      }

      var lines = new string[result.Findings.Count];
      for (var i = 0; i < result.Findings.Count; i++)
      {
        var finding = result.Findings[i];
        lines[i] = finding.Kind + " " + finding.IdentityId + " " + finding.FieldName + " " + finding.Message;
      }

      Debug.LogError(
          "[VfxDeclarationScanner] cues=" + result.CueDeclarations.Count
          + " states=" + result.StateDeclarations.Count
          + " findings=" + result.Findings.Count + "\n"
          + string.Join("\n", lines));
    }
  }
}
#endif

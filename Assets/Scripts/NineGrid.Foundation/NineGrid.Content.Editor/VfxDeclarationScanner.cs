using System;
using System.Collections.Generic;
using System.Reflection;
using NineGrid.Content.Vfx;
using NineGrid.Flow.Presentation;

namespace NineGrid.Content.Editor
{
  public sealed class VfxCueDeclaration
  {
    public VfxCueDeclaration(FieldInfo field, VfxCueAttribute attribute)
    {
      Field = field;
      Attribute = attribute;
    }

    public FieldInfo Field { get; }
    public VfxCueAttribute Attribute { get; }
  }

  public sealed class PersistentVfxStateDeclaration
  {
    public PersistentVfxStateDeclaration(FieldInfo field, PersistentVfxStateAttribute attribute)
    {
      Field = field;
      Attribute = attribute;
    }

    public FieldInfo Field { get; }
    public PersistentVfxStateAttribute Attribute { get; }
  }

  public sealed class VfxDeclarationFinding
  {
    public string IdentityId { get; set; }
    public string Kind { get; set; }
    public string FieldName { get; set; }
    public string Message { get; set; }
  }

  public sealed class VfxDeclarationScanResult
  {
    public IReadOnlyList<VfxCueDeclaration> CueDeclarations { get; set; }
    public IReadOnlyList<PersistentVfxStateDeclaration> StateDeclarations { get; set; }
    public IReadOnlyList<VfxDeclarationFinding> Findings { get; set; }
  }

  public static class VfxDeclarationScanner
  {
    public static VfxDeclarationScanResult Scan(params Assembly[] assemblies)
    {
      return Scan(null, assemblies);
    }

    public static VfxDeclarationScanResult Scan(
        VfxBindingCatalog catalog,
        params Assembly[] assemblies)
    {
      var cueDeclarations = new List<VfxCueDeclaration>();
      var stateDeclarations = new List<PersistentVfxStateDeclaration>();
      var findings = new List<VfxDeclarationFinding>();
      var cueById = new Dictionary<string, VfxCueDeclaration>(StringComparer.Ordinal);
      var stateById = new Dictionary<string, PersistentVfxStateDeclaration>(StringComparer.Ordinal);
      var scanAssemblies = assemblies ?? Array.Empty<Assembly>();

      for (var assemblyIndex = 0; assemblyIndex < scanAssemblies.Length; assemblyIndex++)
      {
        var types = GetTypesSafely(scanAssemblies[assemblyIndex]);
        for (var typeIndex = 0; typeIndex < types.Length; typeIndex++)
        {
          var fields = types[typeIndex].GetFields(
              BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
          for (var fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
          {
            ScanCueField(fields[fieldIndex], cueDeclarations, findings, cueById);
            ScanStateField(fields[fieldIndex], stateDeclarations, findings, stateById);
          }
        }
      }

      if (catalog != null)
      {
        ScanUnboundCues(catalog, cueDeclarations, findings);
        ScanUnboundStates(catalog, stateDeclarations, findings);
      }

      return new VfxDeclarationScanResult
      {
        CueDeclarations = cueDeclarations,
        StateDeclarations = stateDeclarations,
        Findings = findings,
      };
    }

    private static void ScanCueField(
        FieldInfo field,
        List<VfxCueDeclaration> declarations,
        List<VfxDeclarationFinding> findings,
        Dictionary<string, VfxCueDeclaration> byCueId)
    {
      var attributes = field.GetCustomAttributes(typeof(VfxCueAttribute), false);
      var attribute = attributes.Length > 0 ? attributes[0] as VfxCueAttribute : null;
      if (attribute == null)
      {
        return;
      }

      var declaration = new VfxCueDeclaration(field, attribute);
      declarations.Add(declaration);
      var fieldName = field.DeclaringType.FullName + "." + field.Name;
      if (string.IsNullOrWhiteSpace(attribute.CueId))
      {
        findings.Add(new VfxDeclarationFinding
        {
          Kind = "cue",
          IdentityId = string.Empty,
          FieldName = fieldName,
          Message = "视觉特效 cue ID 不能为空。",
        });
      }

      if (string.IsNullOrWhiteSpace(attribute.Note))
      {
        findings.Add(new VfxDeclarationFinding
        {
          Kind = "cue",
          IdentityId = attribute.CueId,
          FieldName = fieldName,
          Message = "视觉特效中文说明不能为空。",
        });
      }

      if (!string.IsNullOrWhiteSpace(attribute.CueId)
          && byCueId.TryGetValue(attribute.CueId, out var previous))
      {
        findings.Add(new VfxDeclarationFinding
        {
          Kind = "cue",
          IdentityId = attribute.CueId,
          FieldName = fieldName,
          Message = "视觉特效 cue ID 重复：" + previous.Field.DeclaringType.FullName + "." + previous.Field.Name,
        });
      }
      else if (!string.IsNullOrWhiteSpace(attribute.CueId))
      {
        byCueId[attribute.CueId] = declaration;
      }
    }

    private static void ScanStateField(
        FieldInfo field,
        List<PersistentVfxStateDeclaration> declarations,
        List<VfxDeclarationFinding> findings,
        Dictionary<string, PersistentVfxStateDeclaration> byStateId)
    {
      var attributes = field.GetCustomAttributes(typeof(PersistentVfxStateAttribute), false);
      var attribute = attributes.Length > 0 ? attributes[0] as PersistentVfxStateAttribute : null;
      if (attribute == null)
      {
        return;
      }

      var declaration = new PersistentVfxStateDeclaration(field, attribute);
      declarations.Add(declaration);
      var fieldName = field.DeclaringType.FullName + "." + field.Name;
      if (string.IsNullOrWhiteSpace(attribute.StateId))
      {
        findings.Add(new VfxDeclarationFinding
        {
          Kind = "state",
          IdentityId = string.Empty,
          FieldName = fieldName,
          Message = "持续视觉状态 state ID 不能为空。",
        });
      }

      if (string.IsNullOrWhiteSpace(attribute.Note))
      {
        findings.Add(new VfxDeclarationFinding
        {
          Kind = "state",
          IdentityId = attribute.StateId,
          FieldName = fieldName,
          Message = "持续视觉状态中文说明不能为空。",
        });
      }

      if (!string.IsNullOrWhiteSpace(attribute.StateId)
          && byStateId.TryGetValue(attribute.StateId, out var previous))
      {
        findings.Add(new VfxDeclarationFinding
        {
          Kind = "state",
          IdentityId = attribute.StateId,
          FieldName = fieldName,
          Message = "持续视觉状态 ID 重复：" + previous.Field.DeclaringType.FullName + "." + previous.Field.Name,
        });
      }
      else if (!string.IsNullOrWhiteSpace(attribute.StateId))
      {
        byStateId[attribute.StateId] = declaration;
      }
    }

    private static void ScanUnboundCues(
        VfxBindingCatalog catalog,
        List<VfxCueDeclaration> declarations,
        List<VfxDeclarationFinding> findings)
    {
      var boundCueIds = new HashSet<string>(StringComparer.Ordinal);
      var bindings = catalog.CueBindings;
      for (var i = 0; i < bindings.Count; i++)
      {
        var binding = bindings[i];
        if (binding != null && !string.IsNullOrWhiteSpace(binding.CueId))
        {
          boundCueIds.Add(binding.CueId);
        }
      }

      for (var i = 0; i < declarations.Count; i++)
      {
        var declaration = declarations[i];
        var cueId = declaration.Attribute.CueId;
        if (string.IsNullOrWhiteSpace(cueId) || boundCueIds.Contains(cueId))
        {
          continue;
        }

        findings.Add(new VfxDeclarationFinding
        {
          Kind = "cue",
          IdentityId = cueId,
          FieldName = declaration.Field.DeclaringType.FullName + "." + declaration.Field.Name,
          Message = "视觉特效 cue 未绑定。",
        });
      }
    }

    private static void ScanUnboundStates(
        VfxBindingCatalog catalog,
        List<PersistentVfxStateDeclaration> declarations,
        List<VfxDeclarationFinding> findings)
    {
      var boundStateIds = new HashSet<string>(StringComparer.Ordinal);
      var bindings = catalog.StateBindings;
      for (var i = 0; i < bindings.Count; i++)
      {
        var binding = bindings[i];
        if (binding != null && !string.IsNullOrWhiteSpace(binding.StateId))
        {
          boundStateIds.Add(binding.StateId);
        }
      }

      for (var i = 0; i < declarations.Count; i++)
      {
        var declaration = declarations[i];
        var stateId = declaration.Attribute.StateId;
        if (string.IsNullOrWhiteSpace(stateId) || boundStateIds.Contains(stateId))
        {
          continue;
        }

        findings.Add(new VfxDeclarationFinding
        {
          Kind = "state",
          IdentityId = stateId,
          FieldName = declaration.Field.DeclaringType.FullName + "." + declaration.Field.Name,
          Message = "持续视觉状态未绑定。",
        });
      }
    }

    private static Type[] GetTypesSafely(Assembly assembly)
    {
      if (assembly == null)
      {
        return Array.Empty<Type>();
      }

      try
      {
        return assembly.GetTypes();
      }
      catch (ReflectionTypeLoadException exception)
      {
        var loaded = new List<Type>();
        var types = exception.Types;
        for (var i = 0; i < types.Length; i++)
        {
          if (types[i] != null)
          {
            loaded.Add(types[i]);
          }
        }

        return loaded.ToArray();
      }
    }
  }
}

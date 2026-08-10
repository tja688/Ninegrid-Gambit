#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NineGrid.Content;
using NineGrid.Content.Vfx;
using NineGrid.Flow.Presentation;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
  public sealed class VfxBindingEditorSession
  {
    private VfxBindingCatalogDto diskCatalog;
    private string diskJson = string.Empty;
    private string diskPath = string.Empty;
    private readonly List<VfxCueBindingDto> workingCueRows = new List<VfxCueBindingDto>();
    private readonly List<VfxStateBindingDto> workingStateRows = new List<VfxStateBindingDto>();

    public string SavedJson => diskJson;

    public static VfxBindingEditorSession LoadFromDisk()
    {
      var session = new VfxBindingEditorSession();
      session.ReloadFromDisk();
      return session;
    }

    public void ReloadFromDisk()
    {
      var path = ResolveAbsolutePath(VfxBindingCatalogPaths.ManifestAssetPath);
      var json = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : string.Empty;
      LoadFromJson(json);
      diskPath = path;
    }

    public bool LoadFromJson(string json)
    {
      var candidate = json ?? string.Empty;
      if (!TryParseCatalogDto(candidate, out var parsed))
      {
        return false;
      }

      diskJson = candidate;
      diskCatalog = parsed;
      diskPath = string.Empty;
      workingCueRows.Clear();
      workingStateRows.Clear();

      var cueRows = diskCatalog.cueBindings ?? Array.Empty<VfxCueBindingDto>();
      for (var i = 0; i < cueRows.Length; i++)
      {
        if (cueRows[i] != null)
        {
          workingCueRows.Add(CloneCueDto(cueRows[i]));
        }
      }

      var stateRows = diskCatalog.stateBindings ?? Array.Empty<VfxStateBindingDto>();
      for (var i = 0; i < stateRows.Length; i++)
      {
        if (stateRows[i] != null)
        {
          workingStateRows.Add(CloneStateDto(stateRows[i]));
        }
      }

      return true;
    }

    public VfxBindingCatalogDto BuildWorkingCatalog()
    {
      return new VfxBindingCatalogDto
      {
        schemaVersion = Math.Max(1, diskCatalog?.schemaVersion ?? 1),
        ticket = diskCatalog?.ticket ?? "#193",
        cueBindings = workingCueRows.Select(CloneCueDto).ToArray(),
        stateBindings = workingStateRows.Select(CloneStateDto).ToArray(),
      };
    }

    public string BuildWorkingJson()
    {
      return JsonUtility.ToJson(BuildWorkingCatalog(), true);
    }

    public bool TrySaveCue(VfxCueBindingDto dto, out string error)
    {
      error = null;
      if (dto == null)
      {
        return true;
      }

      var candidateCueRows = BuildCueRowsForSave(dto);
      var candidateStateRows = workingStateRows.Select(CloneStateDto).ToList();
      if (!ValidateRows(candidateCueRows, candidateStateRows, out error))
      {
        return false;
      }

      var key = ComputeCueBindingKey(dto);
      var index = workingCueRows.FindIndex(row => string.Equals(ComputeCueBindingKey(row), key, StringComparison.Ordinal));
      var saved = CloneCueDto(dto);
      MarkHumanConfirmedCue(saved);
      if (index < 0)
      {
        workingCueRows.Add(saved);
      }
      else
      {
        workingCueRows[index] = saved;
      }

      dto.authoringStatus = saved.authoringStatus;

      if (!TryWriteCatalog(out var json, out error))
      {
        return false;
      }

      diskJson = json;
      if (!TryParseCatalogDto(json, out diskCatalog))
      {
        error = "保存后 VFX 绑定 JSON 无法解析。";
        return false;
      }

      return true;
    }

    public bool TrySaveAll(out string error)
    {
      error = null;
      var candidateCueRows = workingCueRows.Select(CloneCueDto).ToList();
      var candidateStateRows = workingStateRows.Select(CloneStateDto).ToList();
      if (!ValidateRows(candidateCueRows, candidateStateRows, out error))
      {
        return false;
      }

      for (var i = 0; i < workingCueRows.Count; i++)
      {
        MarkHumanConfirmedCue(workingCueRows[i]);
      }

      for (var i = 0; i < workingStateRows.Count; i++)
      {
        MarkHumanConfirmedState(workingStateRows[i]);
      }

      if (!TryWriteCatalog(out var json, out error))
      {
        return false;
      }

      diskJson = json;
      if (!TryParseCatalogDto(json, out diskCatalog))
      {
        error = "保存后 VFX 绑定 JSON 无法解析。";
        return false;
      }

      return true;
    }

    public static string ComputeCueBindingKey(VfxCueBindingDto dto)
    {
      if (dto == null)
      {
        return string.Empty;
      }

      return VfxBindingKey.Compose(
          dto.cueId,
          dto.selectorCardDefId,
          dto.selectorSkillId,
          dto.selectorRoomId,
          dto.selectorItemDefId,
          dto.selectorContentId);
    }

    public static string ComputeStateBindingKey(VfxStateBindingDto dto)
    {
      if (dto == null)
      {
        return string.Empty;
      }

      return VfxBindingKey.Compose(
          dto.stateId,
          dto.selectorCardDefId,
          dto.selectorSkillId,
          dto.selectorRoomId,
          dto.selectorItemDefId,
          dto.selectorContentId);
    }

    private static void MarkHumanConfirmedCue(VfxCueBindingDto dto)
    {
      if (dto != null)
      {
        dto.authoringStatus = VfxBindingAuthoringStatuses.HumanConfirmed;
      }
    }

    private static void MarkHumanConfirmedState(VfxStateBindingDto dto)
    {
      if (dto != null)
      {
        dto.authoringStatus = VfxBindingAuthoringStatuses.HumanConfirmed;
      }
    }

    private List<VfxCueBindingDto> BuildCueRowsForSave(VfxCueBindingDto candidate)
    {
      var rows = workingCueRows.Select(CloneCueDto).ToList();
      if (candidate == null)
      {
        return rows;
      }

      var key = ComputeCueBindingKey(candidate);
      var index = rows.FindIndex(row => string.Equals(ComputeCueBindingKey(row), key, StringComparison.Ordinal));
      var clone = CloneCueDto(candidate);
      if (index < 0)
      {
        rows.Add(clone);
      }
      else
      {
        rows[index] = clone;
      }

      return rows;
    }

    private bool ValidateRows(
        IReadOnlyList<VfxCueBindingDto> cueRows,
        IReadOnlyList<VfxStateBindingDto> stateRows,
        out string error)
    {
      error = null;
      var catalog = new VfxBindingCatalogDto
      {
        schemaVersion = Math.Max(1, diskCatalog?.schemaVersion ?? 1),
        ticket = diskCatalog?.ticket ?? "#193",
        cueBindings = cueRows?.Select(CloneCueDto).ToArray() ?? Array.Empty<VfxCueBindingDto>(),
        stateBindings = stateRows?.Select(CloneStateDto).ToArray() ?? Array.Empty<VfxStateBindingDto>(),
      };

      var keys = new HashSet<string>(StringComparer.Ordinal);
      for (var i = 0; i < catalog.cueBindings.Length; i++)
      {
        var key = ComputeCueBindingKey(catalog.cueBindings[i]);
        if (!string.IsNullOrEmpty(key) && !keys.Add(key))
        {
          error = "保存失败：多个 cue 绑定使用相同的 cue/内容选择器。";
          return false;
        }
      }

      keys.Clear();
      for (var i = 0; i < catalog.stateBindings.Length; i++)
      {
        var key = ComputeStateBindingKey(catalog.stateBindings[i]);
        if (!string.IsNullOrEmpty(key) && !keys.Add(key))
        {
          error = "保存失败：多个 state 绑定使用相同的 state/内容选择器。";
          return false;
        }
      }

      var declaredCueIds = CollectDeclaredCueIds();
      var declaredStateIds = CollectDeclaredStateIds();
      var materialIds = CollectFormalMaterialIds();
      var cueFindings = VfxBindingCatalogHygieneValidator.ValidateCueBindings(
          catalog,
          declaredCueIds,
          materialIds);
      var stateFindings = VfxBindingCatalogHygieneValidator.ValidateStateBindings(
          catalog,
          declaredStateIds,
          materialIds);

      for (var i = 0; i < cueFindings.Count; i++)
      {
        if (IsSaveBlockingCategory(cueFindings[i].Category))
        {
          error = "保存失败：" + cueFindings[i];
          return false;
        }
      }

      for (var i = 0; i < stateFindings.Count; i++)
      {
        if (IsSaveBlockingCategory(stateFindings[i].Category))
        {
          error = "保存失败：" + stateFindings[i];
          return false;
        }
      }

      return true;
    }

    private static bool IsSaveBlockingCategory(string category)
    {
      switch (category)
      {
        case "duplicate-binding-key":
        case "coverage-conflict":
        case "orphan-binding":
        case "missing-material":
        case "missing-player":
        case "empty-cue":
        case "empty-state":
        case "empty-note":
        case "forbidden-overrides":
        case "param-range":
        case "authoring-status":
        case "catalog-null":
        case "null-row":
        case "empty-pool":
          return true;
        default:
          return category != null
              && category.IndexOf("forbidden", StringComparison.OrdinalIgnoreCase) >= 0;
      }
    }

    private bool TryWriteCatalog(out string json, out string error)
    {
      error = null;
      var output = BuildWorkingCatalog();
      if (output.schemaVersion < 1)
      {
        output.schemaVersion = 1;
      }

      json = JsonUtility.ToJson(output, true);
      if (!TryParseCatalogDto(json, out _))
      {
        error = "工作副本无法序列化为合法 VFX Catalog。";
        return false;
      }

      if (string.IsNullOrEmpty(diskPath))
      {
        return true;
      }

      try
      {
        var directory = Path.GetDirectoryName(diskPath);
        if (!string.IsNullOrEmpty(directory))
        {
          Directory.CreateDirectory(directory);
        }

        File.WriteAllText(diskPath, json, new UTF8Encoding(false));
        AssetDatabase.ImportAsset(VfxBindingCatalogPaths.ManifestAssetPath, ImportAssetOptions.ForceUpdate);
        return true;
      }
      catch (Exception exception)
      {
        error = "VFX 绑定 JSON 写入失败：" + exception.Message;
        return false;
      }
    }

    private static bool TryParseCatalogDto(string json, out VfxBindingCatalogDto catalog)
    {
      catalog = null;
      if (!VfxBindingCatalog.TryFromJson(json ?? string.Empty, out _, out _))
      {
        return false;
      }

      try
      {
        catalog = JsonUtility.FromJson<VfxBindingCatalogDto>(json);
      }
      catch
      {
        return false;
      }

      if (catalog == null)
      {
        return false;
      }

      catalog.cueBindings ??= Array.Empty<VfxCueBindingDto>();
      catalog.stateBindings ??= Array.Empty<VfxStateBindingDto>();
      return true;
    }

    private static HashSet<string> CollectDeclaredCueIds()
    {
      var ids = new HashSet<string>(StringComparer.Ordinal);
      var scan = VfxDeclarationScanner.Scan(ResolvePresentationAssembly());
      for (var i = 0; i < scan.CueDeclarations.Count; i++)
      {
        var cueId = scan.CueDeclarations[i]?.Attribute?.CueId;
        if (!string.IsNullOrWhiteSpace(cueId))
        {
          ids.Add(cueId.Trim());
        }
      }

      return ids;
    }

    private static HashSet<string> CollectDeclaredStateIds()
    {
      var ids = new HashSet<string>(StringComparer.Ordinal);
      var scan = VfxDeclarationScanner.Scan(ResolvePresentationAssembly());
      for (var i = 0; i < scan.StateDeclarations.Count; i++)
      {
        var stateId = scan.StateDeclarations[i]?.Attribute?.StateId;
        if (!string.IsNullOrWhiteSpace(stateId))
        {
          ids.Add(stateId.Trim());
        }
      }

      return ids;
    }

    private static HashSet<string> CollectFormalMaterialIds()
    {
      var ids = new HashSet<string>(StringComparer.Ordinal);
      foreach (var entry in VisualEffectCatalog.All)
      {
        if (!string.IsNullOrWhiteSpace(entry?.id))
        {
          ids.Add(entry.id.Trim());
        }
      }

      return ids;
    }

    private static Assembly ResolvePresentationAssembly()
    {
      var presentation = AppDomain.CurrentDomain.GetAssemblies()
          .FirstOrDefault(assembly => string.Equals(
              assembly.GetName().Name,
              "NineGrid.Presentation",
              StringComparison.Ordinal));
      if (presentation != null)
      {
        return presentation;
      }

      return typeof(VfxCue).Assembly;
    }

    private static string ResolveAbsolutePath(string assetPath)
    {
      var normalized = (assetPath ?? string.Empty).Replace('\\', '/');
      if (Path.IsPathRooted(normalized))
      {
        return normalized;
      }

      return Path.Combine(Directory.GetCurrentDirectory(), normalized);
    }

    internal static VfxCueBindingDto CloneCueDto(VfxCueBindingDto source)
    {
      if (source == null)
      {
        return null;
      }

      return new VfxCueBindingDto
      {
        cueId = source.cueId,
        note = source.note,
        module = source.module,
        enabled = source.enabled,
        playerId = source.playerId,
        spatialOwnership = source.spatialOwnership,
        materialKey = source.materialKey,
        fps = source.fps,
        scale = source.scale,
        startOffsetSeconds = source.startOffsetSeconds,
        bindingDelaySeconds = source.bindingDelaySeconds,
        minimumIntervalSeconds = source.minimumIntervalSeconds,
        variants = CloneVariants(source.variants),
        paramOverrideWhitelist = CloneStrings(source.paramOverrideWhitelist),
        selectorCardDefId = source.selectorCardDefId,
        selectorSkillId = source.selectorSkillId,
        selectorRoomId = source.selectorRoomId,
        selectorItemDefId = source.selectorItemDefId,
        selectorContentId = source.selectorContentId,
        authoringStatus = source.authoringStatus,
      };
    }

    internal static VfxStateBindingDto CloneStateDto(VfxStateBindingDto source)
    {
      if (source == null)
      {
        return null;
      }

      return new VfxStateBindingDto
      {
        stateId = source.stateId,
        note = source.note,
        module = source.module,
        enabled = source.enabled,
        playerId = source.playerId,
        spatialOwnership = source.spatialOwnership,
        materialKey = source.materialKey,
        fps = source.fps,
        scale = source.scale,
        startOffsetSeconds = source.startOffsetSeconds,
        variants = CloneVariants(source.variants),
        paramOverrideWhitelist = CloneStrings(source.paramOverrideWhitelist),
        selectorCardDefId = source.selectorCardDefId,
        selectorSkillId = source.selectorSkillId,
        selectorRoomId = source.selectorRoomId,
        selectorItemDefId = source.selectorItemDefId,
        selectorContentId = source.selectorContentId,
        authoringStatus = source.authoringStatus,
      };
    }

    private static VfxMaterialVariantDto[] CloneVariants(VfxMaterialVariantDto[] source)
    {
      if (source == null || source.Length == 0)
      {
        return Array.Empty<VfxMaterialVariantDto>();
      }

      var result = new VfxMaterialVariantDto[source.Length];
      for (var i = 0; i < source.Length; i++)
      {
        var row = source[i];
        result[i] = row == null
            ? null
            : new VfxMaterialVariantDto
            {
              variantId = row.variantId,
              materialKey = row.materialKey,
              weight = row.weight,
              fpsTrim = row.fpsTrim,
              scaleTrim = row.scaleTrim,
              startOffsetSeconds = row.startOffsetSeconds,
            };
      }

      return result;
    }

    private static string[] CloneStrings(string[] source)
    {
      if (source == null || source.Length == 0)
      {
        return Array.Empty<string>();
      }

      var result = new string[source.Length];
      for (var i = 0; i < source.Length; i++)
      {
        result[i] = source[i];
      }

      return result;
    }
  }
}
#endif

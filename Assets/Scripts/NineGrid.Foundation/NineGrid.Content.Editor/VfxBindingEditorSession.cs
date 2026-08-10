#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NineGrid.Content.Vfx;
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

    public void LoadFromJson(string json)
    {
      diskJson = json ?? string.Empty;
      diskCatalog = ParseCatalog(diskJson);
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

      MarkHumanConfirmedCue(dto);
      var key = ComputeCueBindingKey(dto);
      var index = workingCueRows.FindIndex(row => string.Equals(ComputeCueBindingKey(row), key, StringComparison.Ordinal));
      if (index < 0)
      {
        workingCueRows.Add(CloneCueDto(dto));
      }
      else
      {
        workingCueRows[index] = CloneCueDto(dto);
      }

      if (!ValidateAllKeys(out error) || !TryWriteCatalog(out var json, out error))
      {
        return false;
      }

      diskJson = json;
      diskCatalog = ParseCatalog(json);
      return true;
    }

    public bool TrySaveAll(out string error)
    {
      error = null;
      for (var i = 0; i < workingCueRows.Count; i++)
      {
        MarkHumanConfirmedCue(workingCueRows[i]);
      }

      for (var i = 0; i < workingStateRows.Count; i++)
      {
        MarkHumanConfirmedState(workingStateRows[i]);
      }

      if (!ValidateAllKeys(out error) || !TryWriteCatalog(out var json, out error))
      {
        return false;
      }

      diskJson = json;
      diskCatalog = ParseCatalog(json);
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

    private bool ValidateAllKeys(out string error)
    {
      error = null;
      var keys = new HashSet<string>(StringComparer.Ordinal);
      for (var i = 0; i < workingCueRows.Count; i++)
      {
        var key = ComputeCueBindingKey(workingCueRows[i]);
        if (!string.IsNullOrEmpty(key) && !keys.Add(key))
        {
          error = "保存失败：多个 cue 绑定使用相同的 cue/内容选择器。";
          return false;
        }
      }

      keys.Clear();
      for (var i = 0; i < workingStateRows.Count; i++)
      {
        var key = ComputeStateBindingKey(workingStateRows[i]);
        if (!string.IsNullOrEmpty(key) && !keys.Add(key))
        {
          error = "保存失败：多个 state 绑定使用相同的 state/内容选择器。";
          return false;
        }
      }

      var cueFindings = VfxBindingCatalogHygieneValidator.ValidateCueBindings(
          BuildWorkingCatalog(),
          Array.Empty<string>(),
          Array.Empty<string>());
      for (var i = 0; i < cueFindings.Count; i++)
      {
        if (cueFindings[i].Category == "coverage-conflict")
        {
          error = "保存失败：cue 绑定存在同 specificity 覆盖冲突。";
          return false;
        }
      }

      var stateFindings = VfxBindingCatalogHygieneValidator.ValidateStateBindings(
          BuildWorkingCatalog(),
          Array.Empty<string>(),
          Array.Empty<string>());
      for (var i = 0; i < stateFindings.Count; i++)
      {
        if (stateFindings[i].Category == "coverage-conflict")
        {
          error = "保存失败：state 绑定存在同 specificity 覆盖冲突。";
          return false;
        }
      }

      return true;
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

    private static VfxBindingCatalogDto ParseCatalog(string json)
    {
      if (string.IsNullOrWhiteSpace(json))
      {
        return new VfxBindingCatalogDto
        {
          schemaVersion = 1,
          ticket = "#193",
          cueBindings = Array.Empty<VfxCueBindingDto>(),
          stateBindings = Array.Empty<VfxStateBindingDto>(),
        };
      }

      var catalog = JsonUtility.FromJson<VfxBindingCatalogDto>(json);
      if (catalog == null)
      {
        throw new InvalidDataException("JsonUtility returned null.");
      }

      catalog.cueBindings ??= Array.Empty<VfxCueBindingDto>();
      catalog.stateBindings ??= Array.Empty<VfxStateBindingDto>();
      return catalog;
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

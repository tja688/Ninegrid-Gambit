#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NineGrid.Content;
using NineGrid.Content.Vfx;

namespace NineGrid.Content.Editor
{
  /// <summary>#193 VFX Catalog 卫生：重复键、覆盖冲突、孤儿、player/素材与非法覆盖白名单。</summary>
  public static class VfxBindingCatalogHygieneValidator
  {
    public sealed class Finding
    {
      public string Category;
      public string BindingKey;
      public string IdentityId;
      public string Detail;

      public override string ToString()
      {
        return "[" + Category + "] "
            + (string.IsNullOrEmpty(IdentityId) ? string.Empty : IdentityId + " ")
            + Detail;
      }
    }

    public static List<Finding> ValidateCueBindings(
        VfxBindingCatalogDto catalog,
        IReadOnlyCollection<string> declaredCueIds,
        IReadOnlyCollection<string> formalMaterialIds)
    {
      var findings = new List<Finding>();
      if (catalog == null)
      {
        findings.Add(new Finding { Category = "catalog-null", Detail = "VFX 绑定 Catalog 为空。" });
        return findings;
      }

      var rows = catalog.cueBindings ?? Array.Empty<VfxCueBindingDto>();
      ValidateCueRows(rows, declaredCueIds, formalMaterialIds, findings);
      return findings;
    }

    public static List<Finding> ValidateStateBindings(
        VfxBindingCatalogDto catalog,
        IReadOnlyCollection<string> declaredStateIds,
        IReadOnlyCollection<string> formalMaterialIds)
    {
      var findings = new List<Finding>();
      if (catalog == null)
      {
        findings.Add(new Finding { Category = "catalog-null", Detail = "VFX 绑定 Catalog 为空。" });
        return findings;
      }

      var rows = catalog.stateBindings ?? Array.Empty<VfxStateBindingDto>();
      ValidateStateRows(rows, declaredStateIds, formalMaterialIds, findings);
      return findings;
    }

    private static void ValidateCueRows(
        VfxCueBindingDto[] rows,
        IReadOnlyCollection<string> declaredCueIds,
        IReadOnlyCollection<string> formalMaterialIds,
        List<Finding> findings)
    {
      var keys = new HashSet<string>(StringComparer.Ordinal);
      var declared = ToIdentitySet(declaredCueIds);
      var materials = ToIdentitySet(formalMaterialIds);
      var coverageCandidates = new List<VfxCueBindingDto>();

      for (var i = 0; i < rows.Length; i++)
      {
        var row = rows[i];
        if (row == null)
        {
          findings.Add(new Finding { Category = "null-row", Detail = "cueBindings[" + i + "] 为空。" });
          continue;
        }

        var key = VfxBindingEditorSession.ComputeCueBindingKey(row);
        if (string.IsNullOrWhiteSpace(row.cueId))
        {
          findings.Add(new Finding
          {
            Category = "empty-cue",
            BindingKey = key,
            Detail = "cueId 为空。",
          });
        }

        if (!keys.Add(key))
        {
          findings.Add(new Finding
          {
            Category = "duplicate-binding-key",
            BindingKey = key,
            IdentityId = row.cueId,
            Detail = "cue/内容选择器重复。",
          });
        }

        var cueId = row.cueId == null ? string.Empty : row.cueId.Trim();
        if (declared.Count > 0 && !string.IsNullOrWhiteSpace(cueId) && !declared.Contains(cueId))
        {
          findings.Add(new Finding
          {
            Category = "orphan-binding",
            BindingKey = key,
            IdentityId = row.cueId,
            Detail = "绑定没有对应视觉特效 cue 声明。",
          });
        }

        ValidateCommonRow(
            key,
            row.cueId,
            row.note,
            row.authoringStatus,
            row.playerId,
            row.materialKey,
            row.fps,
            row.scale,
            row.startOffsetSeconds,
            row.bindingDelaySeconds,
            row.minimumIntervalSeconds,
            row.paramOverrideWhitelist,
            row.variants,
            materials,
            supportsPulse: true,
            findings);
        coverageCandidates.Add(row);
      }

      ValidateCueCoverageConflicts(coverageCandidates, findings);
    }

    private static void ValidateStateRows(
        VfxStateBindingDto[] rows,
        IReadOnlyCollection<string> declaredStateIds,
        IReadOnlyCollection<string> formalMaterialIds,
        List<Finding> findings)
    {
      var keys = new HashSet<string>(StringComparer.Ordinal);
      var declared = ToIdentitySet(declaredStateIds);
      var materials = ToIdentitySet(formalMaterialIds);
      var coverageCandidates = new List<VfxStateBindingDto>();

      for (var i = 0; i < rows.Length; i++)
      {
        var row = rows[i];
        if (row == null)
        {
          findings.Add(new Finding { Category = "null-row", Detail = "stateBindings[" + i + "] 为空。" });
          continue;
        }

        var key = VfxBindingEditorSession.ComputeStateBindingKey(row);
        if (string.IsNullOrWhiteSpace(row.stateId))
        {
          findings.Add(new Finding
          {
            Category = "empty-state",
            BindingKey = key,
            Detail = "stateId 为空。",
          });
        }

        if (!keys.Add(key))
        {
          findings.Add(new Finding
          {
            Category = "duplicate-binding-key",
            BindingKey = key,
            IdentityId = row.stateId,
            Detail = "state/内容选择器重复。",
          });
        }

        var stateId = row.stateId == null ? string.Empty : row.stateId.Trim();
        if (declared.Count > 0 && !string.IsNullOrWhiteSpace(stateId) && !declared.Contains(stateId))
        {
          findings.Add(new Finding
          {
            Category = "orphan-binding",
            BindingKey = key,
            IdentityId = row.stateId,
            Detail = "绑定没有对应持续视觉状态声明。",
          });
        }

        ValidateCommonRow(
            key,
            row.stateId,
            row.note,
            row.authoringStatus,
            row.playerId,
            row.materialKey,
            row.fps,
            row.scale,
            row.startOffsetSeconds,
            0f,
            0f,
            row.paramOverrideWhitelist,
            row.variants,
            materials,
            supportsPulse: false,
            findings);
        coverageCandidates.Add(row);
      }

      ValidateStateCoverageConflicts(coverageCandidates, findings);
    }

    private static void ValidateCommonRow(
        string key,
        string identityId,
        string note,
        string authoringStatus,
        string playerId,
        string materialKey,
        float fps,
        float scale,
        float startOffsetSeconds,
        float bindingDelaySeconds,
        float minimumIntervalSeconds,
        string[] whitelist,
        VfxMaterialVariantDto[] variants,
        HashSet<string> materials,
        bool supportsPulse,
        List<Finding> findings)
    {
      if (string.IsNullOrWhiteSpace(note))
      {
        findings.Add(new Finding
        {
          Category = "empty-note",
          BindingKey = key,
          IdentityId = identityId,
          Detail = "视觉特效绑定缺少中文说明。",
        });
      }

      if (!string.IsNullOrWhiteSpace(authoringStatus)
          && !VfxBindingAuthoringStatuses.IsAiDraft(authoringStatus)
          && !VfxBindingAuthoringStatuses.IsHumanConfirmed(authoringStatus))
      {
        findings.Add(new Finding
        {
          Category = "authoring-status",
          BindingKey = key,
          IdentityId = identityId,
          Detail = "未知 authoringStatus：" + authoringStatus,
        });
      }

      if (string.IsNullOrWhiteSpace(playerId) || !VfxPlayerRegistry.IsKnownPlayerId(playerId))
      {
        findings.Add(new Finding
        {
          Category = "missing-player",
          BindingKey = key,
          IdentityId = identityId,
          Detail = "未知或缺失 playerId：" + playerId,
        });
      }
      else if (supportsPulse && !VfxPlayerRegistry.SupportsPulse(playerId))
      {
        findings.Add(new Finding
        {
          Category = "missing-player",
          BindingKey = key,
          IdentityId = identityId,
          Detail = "player 不支持 Pulse：" + playerId,
        });
      }
      else if (!supportsPulse && !VfxPlayerRegistry.SupportsState(playerId))
      {
        findings.Add(new Finding
        {
          Category = "missing-player",
          BindingKey = key,
          IdentityId = identityId,
          Detail = "player 不支持 State：" + playerId,
        });
      }

      if (VfxPlayerRegistry.IsMaterialPlayer(playerId))
      {
        ValidateMaterialBinding(key, identityId, materialKey, variants, materials, findings);
      }
      else if (VfxPlayerRegistry.IsParticlePlayer(playerId))
      {
        ValidateParticlePresetBinding(key, identityId, materialKey, variants, supportsPulse, findings);
      }
      else if (VfxPlayerRegistry.IsProjectilePlayer(playerId))
      {
        ValidateProjectilePresetBinding(key, identityId, materialKey, variants, findings);
      }

      if (fps < 0f || scale < 0f || startOffsetSeconds < 0f
          || bindingDelaySeconds < 0f || minimumIntervalSeconds < 0f)
      {
        findings.Add(new Finding
        {
          Category = "param-range",
          BindingKey = key,
          IdentityId = identityId,
          Detail = "fps/scale/起播点/延迟/间隔不得为负。",
        });
      }

      ValidateOverrideWhitelist(key, identityId, playerId, whitelist, findings);
    }

    private static void ValidateMaterialBinding(
        string key,
        string identityId,
        string materialKey,
        VfxMaterialVariantDto[] variants,
        HashSet<string> materials,
        List<Finding> findings)
    {
      if (materials.Count == 0)
      {
        return;
      }

      var pool = variants ?? Array.Empty<VfxMaterialVariantDto>();
      if (pool.Length == 0)
      {
        if (string.IsNullOrWhiteSpace(materialKey))
        {
          findings.Add(new Finding
          {
            Category = "missing-material",
            BindingKey = key,
            IdentityId = identityId,
            Detail = "素材型绑定缺少 materialKey。",
          });
          return;
        }

        if (!materials.Contains(materialKey.Trim()))
        {
          findings.Add(new Finding
          {
            Category = "missing-material",
            BindingKey = key,
            IdentityId = identityId,
            Detail = "materialKey 不在 visual_effects 索引：" + materialKey,
          });
        }

        return;
      }

      var hasValid = false;
      for (var i = 0; i < pool.Length; i++)
      {
        var variant = pool[i];
        if (variant == null || variant.weight <= 0f || string.IsNullOrWhiteSpace(variant.materialKey))
        {
          continue;
        }

        hasValid = true;
        if (!materials.Contains(variant.materialKey.Trim()))
        {
          findings.Add(new Finding
          {
            Category = "missing-material",
            BindingKey = key,
            IdentityId = identityId,
            Detail = "变体 materialKey 不在 visual_effects 索引：" + variant.materialKey,
          });
        }
      }

      if (!hasValid)
      {
        findings.Add(new Finding
        {
          Category = "empty-pool",
          BindingKey = key,
          IdentityId = identityId,
          Detail = "素材变体池没有任何有效条目。",
        });
      }
    }

    private static void ValidateParticlePresetBinding(
        string key,
        string identityId,
        string materialKey,
        VfxMaterialVariantDto[] variants,
        bool supportsPulse,
        List<Finding> findings)
    {
      var pool = variants ?? Array.Empty<VfxMaterialVariantDto>();
      if (pool.Length == 0)
      {
        if (string.IsNullOrWhiteSpace(materialKey))
        {
          findings.Add(new Finding
          {
            Category = "missing-material",
            BindingKey = key,
            IdentityId = identityId,
            Detail = "粒子型绑定缺少预设 materialKey。",
          });
          return;
        }

        ValidateSingleParticlePresetKey(key, identityId, materialKey, supportsPulse, findings);
        return;
      }

      var hasValid = false;
      for (var i = 0; i < pool.Length; i++)
      {
        var variant = pool[i];
        if (variant == null || variant.weight <= 0f || string.IsNullOrWhiteSpace(variant.materialKey))
        {
          continue;
        }

        hasValid = true;
        ValidateSingleParticlePresetKey(key, identityId, variant.materialKey, supportsPulse, findings);
      }

      if (!hasValid)
      {
        findings.Add(new Finding
        {
          Category = "empty-pool",
          BindingKey = key,
          IdentityId = identityId,
          Detail = "粒子预设变体池没有任何有效条目。",
        });
      }
    }

    private static void ValidateProjectilePresetBinding(
        string key,
        string identityId,
        string materialKey,
        VfxMaterialVariantDto[] variants,
        List<Finding> findings)
    {
      var pool = variants;
      if (pool == null || pool.Length == 0)
      {
        if (string.IsNullOrWhiteSpace(materialKey))
        {
          findings.Add(new Finding
          {
            Category = "missing-material",
            BindingKey = key,
            IdentityId = identityId,
            Detail = "弹道型绑定缺少预设 materialKey。",
          });
          return;
        }

        ValidateSingleProjectilePresetKey(key, identityId, materialKey, findings);
        return;
      }

      var hasValid = false;
      for (var i = 0; i < pool.Length; i++)
      {
        var variant = pool[i];
        if (variant == null || variant.weight <= 0f || string.IsNullOrWhiteSpace(variant.materialKey))
        {
          continue;
        }

        hasValid = true;
        ValidateSingleProjectilePresetKey(key, identityId, variant.materialKey, findings);
      }

      if (!hasValid)
      {
        findings.Add(new Finding
        {
          Category = "missing-material",
          BindingKey = key,
          IdentityId = identityId,
          Detail = "弹道预设变体池没有任何有效条目。",
        });
      }
    }

    private static void ValidateSingleProjectilePresetKey(
        string key,
        string identityId,
        string presetKey,
        List<Finding> findings)
    {
      if (!VfxProjectilePresetIds.IsKnown(presetKey))
      {
        findings.Add(new Finding
        {
          Category = "missing-material",
          BindingKey = key,
          IdentityId = identityId,
          Detail = "materialKey 不是已知弹道预设：" + presetKey,
        });
      }
    }

    private static void ValidateSingleParticlePresetKey(
        string key,
        string identityId,
        string presetKey,
        bool supportsPulse,
        List<Finding> findings)
    {
      if (!VfxParticlePresetIds.IsKnown(presetKey))
      {
        findings.Add(new Finding
        {
          Category = "missing-material",
          BindingKey = key,
          IdentityId = identityId,
          Detail = "materialKey 不是已知粒子预设：" + presetKey,
        });
        return;
      }

      var isLoop = VfxParticlePresetIds.IsLoopPreset(presetKey);
      if (supportsPulse && isLoop)
      {
        findings.Add(new Finding
        {
          Category = "missing-material",
          BindingKey = key,
          IdentityId = identityId,
          Detail = "loop 粒子预设不可用于 Pulse 绑定：" + presetKey,
        });
      }
      else if (!supportsPulse && !isLoop)
      {
        findings.Add(new Finding
        {
          Category = "missing-material",
          BindingKey = key,
          IdentityId = identityId,
          Detail = "State 绑定只接受 particle.loop.* 预设：" + presetKey,
        });
      }
    }

    private static void ValidateOverrideWhitelist(
        string key,
        string identityId,
        string playerId,
        string[] whitelist,
        List<Finding> findings)
    {
      var rows = whitelist ?? Array.Empty<string>();
      for (var i = 0; i < rows.Length; i++)
      {
        var param = rows[i];
        if (string.IsNullOrWhiteSpace(param))
        {
          continue;
        }

        if (!VfxPlayerRegistry.AllowsParamOverride(playerId, param))
        {
          findings.Add(new Finding
          {
            Category = "forbidden-overrides",
            BindingKey = key,
            IdentityId = identityId,
            Detail = "非法覆盖参数：" + param,
          });
        }
      }
    }

    private static void ValidateCueCoverageConflicts(List<VfxCueBindingDto> rows, List<Finding> findings)
    {
      for (var i = 0; i < rows.Count; i++)
      {
        var a = rows[i];
        if (a == null || string.IsNullOrWhiteSpace(a.cueId))
        {
          continue;
        }

        var aKey = VfxBindingEditorSession.ComputeCueBindingKey(a);
        var aSpec = CountSelectorSpecificity(a);
        for (var j = i + 1; j < rows.Count; j++)
        {
          var b = rows[j];
          if (b == null
              || !string.Equals(a.cueId, b.cueId, StringComparison.Ordinal)
              || CountSelectorSpecificity(b) != aSpec)
          {
            continue;
          }

          var bKey = VfxBindingEditorSession.ComputeCueBindingKey(b);
          if (string.Equals(aKey, bKey, StringComparison.Ordinal))
          {
            continue;
          }

          if (!SelectorsCanBothMatch(a, b))
          {
            continue;
          }

          findings.Add(new Finding
          {
            Category = "coverage-conflict",
            BindingKey = aKey,
            IdentityId = a.cueId,
            Detail = "与另一条同 cue/同 specificity 绑定可同时命中：" + bKey,
          });
        }
      }
    }

    private static void ValidateStateCoverageConflicts(List<VfxStateBindingDto> rows, List<Finding> findings)
    {
      for (var i = 0; i < rows.Count; i++)
      {
        var a = rows[i];
        if (a == null || string.IsNullOrWhiteSpace(a.stateId))
        {
          continue;
        }

        var aKey = VfxBindingEditorSession.ComputeStateBindingKey(a);
        var aSpec = CountSelectorSpecificity(a);
        for (var j = i + 1; j < rows.Count; j++)
        {
          var b = rows[j];
          if (b == null
              || !string.Equals(a.stateId, b.stateId, StringComparison.Ordinal)
              || CountSelectorSpecificity(b) != aSpec)
          {
            continue;
          }

          var bKey = VfxBindingEditorSession.ComputeStateBindingKey(b);
          if (string.Equals(aKey, bKey, StringComparison.Ordinal))
          {
            continue;
          }

          if (!SelectorsCanBothMatch(a, b))
          {
            continue;
          }

          findings.Add(new Finding
          {
            Category = "coverage-conflict",
            BindingKey = aKey,
            IdentityId = a.stateId,
            Detail = "与另一条同 state/同 specificity 绑定可同时命中：" + bKey,
          });
        }
      }
    }

    private static int CountSelectorSpecificity(VfxCueBindingDto row)
    {
      return VfxSelectorRules.CountSpecificity(
          row.selectorCardDefId,
          row.selectorSkillId,
          row.selectorRoomId,
          row.selectorItemDefId,
          row.selectorContentId);
    }

    private static int CountSelectorSpecificity(VfxStateBindingDto row)
    {
      return VfxSelectorRules.CountSpecificity(
          row.selectorCardDefId,
          row.selectorSkillId,
          row.selectorRoomId,
          row.selectorItemDefId,
          row.selectorContentId);
    }

    private static bool SelectorsCanBothMatch(VfxCueBindingDto a, VfxCueBindingDto b)
    {
      return VfxSelectorRules.SelectorsCanBothMatch(
          a.selectorCardDefId,
          a.selectorSkillId,
          a.selectorRoomId,
          a.selectorItemDefId,
          a.selectorContentId,
          b.selectorCardDefId,
          b.selectorSkillId,
          b.selectorRoomId,
          b.selectorItemDefId,
          b.selectorContentId);
    }

    private static bool SelectorsCanBothMatch(VfxStateBindingDto a, VfxStateBindingDto b)
    {
      return VfxSelectorRules.SelectorsCanBothMatch(
          a.selectorCardDefId,
          a.selectorSkillId,
          a.selectorRoomId,
          a.selectorItemDefId,
          a.selectorContentId,
          b.selectorCardDefId,
          b.selectorSkillId,
          b.selectorRoomId,
          b.selectorItemDefId,
          b.selectorContentId);
    }

    private static HashSet<string> ToIdentitySet(IReadOnlyCollection<string> values)
    {
      var set = new HashSet<string>(StringComparer.Ordinal);
      if (values == null)
      {
        return set;
      }

      foreach (var value in values)
      {
        if (!string.IsNullOrWhiteSpace(value))
        {
          set.Add(value.Trim());
        }
      }

      return set;
    }
  }
}
#endif

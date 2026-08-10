using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Content.Vfx
{
  [Serializable]
  public sealed class VfxBindingCatalogDto
  {
    public int schemaVersion;
    public string ticket;
    public VfxCueBindingDto[] cueBindings;
    public VfxStateBindingDto[] stateBindings;
  }

  [Serializable]
  public sealed class VfxMaterialVariantDto
  {
    public string variantId;
    public string materialKey;
    public float weight = 1f;
    public float fpsTrim;
    public float scaleTrim;
    public float startOffsetSeconds;
  }

  [Serializable]
  public sealed class VfxCueBindingDto
  {
    public string cueId;
    public string note;
    public string module;
    public bool enabled = true;
    public string playerId;
    public string spatialOwnership = "independent";
    public string materialKey;
    public float fps;
    public float speed = 1f;
    public float scale;
    public float startOffsetSeconds;
    public float tintR = 1f;
    public float tintG = 1f;
    public float tintB = 1f;
    public float tintA = 1f;
    public string timeBase = "scaled";
    public float bindingDelaySeconds;
    public float minimumIntervalSeconds;
    public VfxMaterialVariantDto[] variants;
    public string[] paramOverrideWhitelist;
    public string selectorCardDefId;
    public string selectorSkillId;
    public string selectorRoomId;
    public string selectorItemDefId;
    public string selectorContentId;
    public string authoringStatus;
  }

  [Serializable]
  public sealed class VfxStateBindingDto
  {
    public string stateId;
    public string note;
    public string module;
    public bool enabled = true;
    public string playerId;
    public string spatialOwnership = "attached";
    public string materialKey;
    public float fps;
    public float speed = 1f;
    public float scale;
    public float startOffsetSeconds;
    public float tintR = 1f;
    public float tintG = 1f;
    public float tintB = 1f;
    public float tintA = 1f;
    public string timeBase = "scaled";
    public VfxMaterialVariantDto[] variants;
    public string[] paramOverrideWhitelist;
    public string selectorCardDefId;
    public string selectorSkillId;
    public string selectorRoomId;
    public string selectorItemDefId;
    public string selectorContentId;
    public string authoringStatus;
    public string exitMode = "immediate";
    public int exitLoopLimit = 1;
  }

  public sealed class VfxCueBinding
  {
    public VfxCueBinding(VfxCueBindingDto dto)
    {
      Dto = dto;
      CueId = dto.cueId ?? string.Empty;
      Note = dto.note ?? string.Empty;
      Module = dto.module ?? string.Empty;
      Enabled = dto.enabled;
      PlayerId = dto.playerId ?? string.Empty;
      SpatialOwnership = VfxBindingCatalog.ParseSpatialOwnership(dto.spatialOwnership);
      MaterialKey = dto.materialKey ?? string.Empty;
      Fps = dto.fps;
      Speed = dto.speed;
      Scale = dto.scale;
      StartOffsetSeconds = dto.startOffsetSeconds;
      Tint = VfxBindingPlayback.ResolveTint(dto.tintR, dto.tintG, dto.tintB, dto.tintA);
      UseUnscaledTime = VfxBindingPlayback.IsUnscaledTimeBase(dto.timeBase);
      BindingDelaySeconds = dto.bindingDelaySeconds;
      MinimumIntervalSeconds = dto.minimumIntervalSeconds;
      Variants = dto.variants ?? Array.Empty<VfxMaterialVariantDto>();
      ParamOverrideWhitelist = dto.paramOverrideWhitelist ?? Array.Empty<string>();
      SelectorCardDefId = dto.selectorCardDefId ?? string.Empty;
      SelectorSkillId = dto.selectorSkillId ?? string.Empty;
      SelectorRoomId = dto.selectorRoomId ?? string.Empty;
      SelectorItemDefId = dto.selectorItemDefId ?? string.Empty;
      SelectorContentId = dto.selectorContentId ?? string.Empty;
      AuthoringStatus = dto.authoringStatus ?? string.Empty;
      BindingKey = VfxBindingKey.Compose(
          CueId,
          SelectorCardDefId,
          SelectorSkillId,
          SelectorRoomId,
          SelectorItemDefId,
          SelectorContentId);
    }

    public VfxCueBindingDto Dto { get; }
    public string CueId { get; }
    public string Note { get; }
    public string Module { get; }
    public bool Enabled { get; }
    public string PlayerId { get; }
    public VfxSpatialOwnership SpatialOwnership { get; }
    public string MaterialKey { get; }
    public float Fps { get; }
    public float Speed { get; }
    public float Scale { get; }
    public float StartOffsetSeconds { get; }
    public Color Tint { get; }
    public bool UseUnscaledTime { get; }
    public float BindingDelaySeconds { get; }
    public float MinimumIntervalSeconds { get; }
    public IReadOnlyList<VfxMaterialVariantDto> Variants { get; }
    public IReadOnlyList<string> ParamOverrideWhitelist { get; }
    public string SelectorCardDefId { get; }
    public string SelectorSkillId { get; }
    public string SelectorRoomId { get; }
    public string SelectorItemDefId { get; }
    public string SelectorContentId { get; }
    public string AuthoringStatus { get; }
    public string BindingKey { get; }

    public int SelectorSpecificity => VfxSelectorRules.CountSpecificity(
        SelectorCardDefId,
        SelectorSkillId,
        SelectorRoomId,
        SelectorItemDefId,
        SelectorContentId);

    public bool Matches(VfxCueRequest request)
    {
      return string.Equals(CueId, request.CueId, StringComparison.Ordinal)
          && VfxSelectorRules.Matches(
              SelectorCardDefId,
              SelectorSkillId,
              SelectorRoomId,
              SelectorItemDefId,
              SelectorContentId,
              request.CardDefId,
              request.SkillId,
              request.RoomId,
              request.ItemDefId,
              request.ContentId);
    }
  }

  public sealed class VfxStateBinding
  {
    public VfxStateBinding(VfxStateBindingDto dto)
    {
      Dto = dto;
      StateId = dto.stateId ?? string.Empty;
      Note = dto.note ?? string.Empty;
      Module = dto.module ?? string.Empty;
      Enabled = dto.enabled;
      PlayerId = dto.playerId ?? string.Empty;
      SpatialOwnership = VfxBindingCatalog.ParseSpatialOwnership(dto.spatialOwnership);
      MaterialKey = dto.materialKey ?? string.Empty;
      Fps = dto.fps;
      Speed = dto.speed;
      Scale = dto.scale;
      StartOffsetSeconds = dto.startOffsetSeconds;
      Tint = VfxBindingPlayback.ResolveTint(dto.tintR, dto.tintG, dto.tintB, dto.tintA);
      UseUnscaledTime = VfxBindingPlayback.IsUnscaledTimeBase(dto.timeBase);
      Variants = dto.variants ?? Array.Empty<VfxMaterialVariantDto>();
      ParamOverrideWhitelist = dto.paramOverrideWhitelist ?? Array.Empty<string>();
      SelectorCardDefId = dto.selectorCardDefId ?? string.Empty;
      SelectorSkillId = dto.selectorSkillId ?? string.Empty;
      SelectorRoomId = dto.selectorRoomId ?? string.Empty;
      SelectorItemDefId = dto.selectorItemDefId ?? string.Empty;
      SelectorContentId = dto.selectorContentId ?? string.Empty;
      AuthoringStatus = dto.authoringStatus ?? string.Empty;
      ExitMode = dto.exitMode ?? string.Empty;
      ExitLoopLimit = dto.exitLoopLimit > 0 ? dto.exitLoopLimit : 1;
      BindingKey = VfxBindingKey.Compose(
          StateId,
          SelectorCardDefId,
          SelectorSkillId,
          SelectorRoomId,
          SelectorItemDefId,
          SelectorContentId);
    }

    public VfxStateBindingDto Dto { get; }
    public string StateId { get; }
    public string Note { get; }
    public string Module { get; }
    public bool Enabled { get; }
    public string PlayerId { get; }
    public VfxSpatialOwnership SpatialOwnership { get; }
    public string MaterialKey { get; }
    public float Fps { get; }
    public float Speed { get; }
    public float Scale { get; }
    public float StartOffsetSeconds { get; }
    public Color Tint { get; }
    public bool UseUnscaledTime { get; }
    public IReadOnlyList<VfxMaterialVariantDto> Variants { get; }
    public IReadOnlyList<string> ParamOverrideWhitelist { get; }
    public string SelectorCardDefId { get; }
    public string SelectorSkillId { get; }
    public string SelectorRoomId { get; }
    public string SelectorItemDefId { get; }
    public string SelectorContentId { get; }
    public string AuthoringStatus { get; }
    public string ExitMode { get; }
    public int ExitLoopLimit { get; }
    public string BindingKey { get; }

    public int SelectorSpecificity => VfxSelectorRules.CountSpecificity(
        SelectorCardDefId,
        SelectorSkillId,
        SelectorRoomId,
        SelectorItemDefId,
        SelectorContentId);

    public bool Matches(VfxStateRequest request)
    {
      return string.Equals(StateId, request.StateId, StringComparison.Ordinal)
          && VfxSelectorRules.Matches(
              SelectorCardDefId,
              SelectorSkillId,
              SelectorRoomId,
              SelectorItemDefId,
              SelectorContentId,
              request.CardDefId,
              request.SkillId,
              request.RoomId,
              request.ItemDefId,
              request.ContentId);
    }
  }

  public static class VfxSelectorRules
  {
    public static int CountSpecificity(
        string selectorCardDefId,
        string selectorSkillId,
        string selectorRoomId,
        string selectorItemDefId,
        string selectorContentId)
    {
      return CountNonEmpty(selectorCardDefId)
          + CountNonEmpty(selectorSkillId)
          + CountNonEmpty(selectorRoomId)
          + CountNonEmpty(selectorItemDefId)
          + CountNonEmpty(selectorContentId);
    }

    public static bool Matches(
        string selectorCardDefId,
        string selectorSkillId,
        string selectorRoomId,
        string selectorItemDefId,
        string selectorContentId,
        string cardDefId,
        string skillId,
        string roomId,
        string itemDefId,
        string contentId)
    {
      return MatchesSelector(selectorCardDefId, cardDefId)
          && MatchesSelector(selectorSkillId, skillId)
          && MatchesSelector(selectorRoomId, roomId)
          && MatchesSelector(selectorItemDefId, itemDefId)
          && MatchesSelector(selectorContentId, contentId);
    }

    public static bool SelectorsCanBothMatch(
        string aCard,
        string aSkill,
        string aRoom,
        string aItem,
        string aContent,
        string bCard,
        string bSkill,
        string bRoom,
        string bItem,
        string bContent)
    {
      return SelectorPairCompatible(aCard, bCard)
          && SelectorPairCompatible(aSkill, bSkill)
          && SelectorPairCompatible(aRoom, bRoom)
          && SelectorPairCompatible(aItem, bItem)
          && SelectorPairCompatible(aContent, bContent);
    }

    private static bool MatchesSelector(string selector, string actual)
    {
      return string.IsNullOrEmpty(selector)
          || string.Equals(selector, actual ?? string.Empty, StringComparison.Ordinal);
    }

    private static bool SelectorPairCompatible(string left, string right)
    {
      return string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right)
          || string.Equals(left, right, StringComparison.Ordinal);
    }

    private static int CountNonEmpty(string value)
    {
      return string.IsNullOrEmpty(value) ? 0 : 1;
    }
  }

  public sealed class VfxBindingCatalog
  {
    private readonly List<VfxCueBinding> mCueBindings;
    private readonly List<VfxStateBinding> mStateBindings;

    private VfxBindingCatalog(List<VfxCueBinding> cueBindings, List<VfxStateBinding> stateBindings)
    {
      mCueBindings = cueBindings;
      mStateBindings = stateBindings;
    }

    public IReadOnlyList<VfxCueBinding> CueBindings => mCueBindings;
    public IReadOnlyList<VfxStateBinding> StateBindings => mStateBindings;

    public static bool TryFromJson(string json, out VfxBindingCatalog catalog, out string error)
    {
      catalog = null;
      error = null;
      if (string.IsNullOrWhiteSpace(json))
      {
        error = "catalog JSON is empty.";
        return false;
      }

      VfxBindingCatalogDto dto;
      try
      {
        dto = JsonUtility.FromJson<VfxBindingCatalogDto>(json);
      }
      catch (Exception exception)
      {
        error = "catalog JSON is malformed: " + exception.Message;
        return false;
      }

      if (dto == null)
      {
        error = "catalog DTO is null.";
        return false;
      }

      if (dto.cueBindings == null || dto.stateBindings == null)
      {
        error = "catalog cueBindings or stateBindings collection is null.";
        return false;
      }

      var cueRows = new List<VfxCueBinding>(dto.cueBindings.Length);
      for (var i = 0; i < dto.cueBindings.Length; i++)
      {
        var row = dto.cueBindings[i];
        if (row == null)
        {
          continue;
        }

        cueRows.Add(new VfxCueBinding(row));
      }

      var stateRows = new List<VfxStateBinding>(dto.stateBindings.Length);
      for (var i = 0; i < dto.stateBindings.Length; i++)
      {
        var row = dto.stateBindings[i];
        if (row == null)
        {
          continue;
        }

        stateRows.Add(new VfxStateBinding(row));
      }

      catalog = new VfxBindingCatalog(cueRows, stateRows);
      return true;
    }

    public static VfxBindingCatalog FromJson(string json)
    {
      if (!TryFromJson(json, out var catalog, out _))
      {
        return new VfxBindingCatalog(new List<VfxCueBinding>(), new List<VfxStateBinding>());
      }

      return catalog;
    }

    public static VfxBindingCatalog LoadFromResources()
    {
      var asset = Resources.Load<TextAsset>(VfxBindingCatalogPaths.ManifestResourcesKey);
      return FromJson(asset == null ? string.Empty : asset.text);
    }

    public bool TryResolveCue(VfxCueRequest request, out VfxCueBinding binding)
    {
      binding = null;
      if (string.IsNullOrWhiteSpace(request.CueId))
      {
        return false;
      }

      var bestSpecificity = -1;
      var bestIndex = -1;
      for (var i = 0; i < mCueBindings.Count; i++)
      {
        var candidate = mCueBindings[i];
        if (candidate == null || !candidate.Matches(request))
        {
          continue;
        }

        if (candidate.SelectorSpecificity > bestSpecificity
            || (candidate.SelectorSpecificity == bestSpecificity && i > bestIndex))
        {
          binding = candidate;
          bestSpecificity = candidate.SelectorSpecificity;
          bestIndex = i;
        }
      }

      return binding != null;
    }

    public bool TryResolveCueStrict(
        VfxCueRequest request,
        out VfxCueBinding binding,
        out VfxBindingResolveError resolveError)
    {
      binding = null;
      resolveError = null;
      if (string.IsNullOrWhiteSpace(request.CueId))
      {
        resolveError = new VfxBindingResolveError
        {
          Code = VfxBindingResolveCode.EmptyIdentity,
          Message = "cue ID 为空。",
          IdentityId = string.Empty,
        };
        return false;
      }

      var bestSpecificity = -1;
      var matches = new List<VfxCueBinding>();
      for (var i = 0; i < mCueBindings.Count; i++)
      {
        var candidate = mCueBindings[i];
        if (candidate == null || !candidate.Matches(request))
        {
          continue;
        }

        if (candidate.SelectorSpecificity > bestSpecificity)
        {
          bestSpecificity = candidate.SelectorSpecificity;
          matches.Clear();
          matches.Add(candidate);
        }
        else if (candidate.SelectorSpecificity == bestSpecificity)
        {
          matches.Add(candidate);
        }
      }

      if (matches.Count == 0)
      {
        resolveError = new VfxBindingResolveError
        {
          Code = VfxBindingResolveCode.NotFound,
          Message = "视觉特效 cue 绑定不存在。",
          IdentityId = request.CueId,
        };
        return false;
      }

      if (matches.Count > 1)
      {
        resolveError = new VfxBindingResolveError
        {
          Code = VfxBindingResolveCode.Ambiguous,
          Message = "同 cue/同 specificity 存在多条可命中绑定。",
          IdentityId = request.CueId,
          BindingKey = matches[0].BindingKey,
          ConflictingBindingKey = matches[1].BindingKey,
        };
        return false;
      }

      binding = matches[0];
      return true;
    }

    public bool TryResolveState(VfxStateRequest request, out VfxStateBinding binding)
    {
      binding = null;
      if (string.IsNullOrWhiteSpace(request.StateId))
      {
        return false;
      }

      var bestSpecificity = -1;
      var bestIndex = -1;
      for (var i = 0; i < mStateBindings.Count; i++)
      {
        var candidate = mStateBindings[i];
        if (candidate == null || !candidate.Matches(request))
        {
          continue;
        }

        if (candidate.SelectorSpecificity > bestSpecificity
            || (candidate.SelectorSpecificity == bestSpecificity && i > bestIndex))
        {
          binding = candidate;
          bestSpecificity = candidate.SelectorSpecificity;
          bestIndex = i;
        }
      }

      return binding != null;
    }

    public bool TryResolveStateStrict(
        VfxStateRequest request,
        out VfxStateBinding binding,
        out VfxBindingResolveError resolveError)
    {
      binding = null;
      resolveError = null;
      if (string.IsNullOrWhiteSpace(request.StateId))
      {
        resolveError = new VfxBindingResolveError
        {
          Code = VfxBindingResolveCode.EmptyIdentity,
          Message = "state ID 为空。",
          IdentityId = string.Empty,
        };
        return false;
      }

      var bestSpecificity = -1;
      var matches = new List<VfxStateBinding>();
      for (var i = 0; i < mStateBindings.Count; i++)
      {
        var candidate = mStateBindings[i];
        if (candidate == null || !candidate.Matches(request))
        {
          continue;
        }

        if (candidate.SelectorSpecificity > bestSpecificity)
        {
          bestSpecificity = candidate.SelectorSpecificity;
          matches.Clear();
          matches.Add(candidate);
        }
        else if (candidate.SelectorSpecificity == bestSpecificity)
        {
          matches.Add(candidate);
        }
      }

      if (matches.Count == 0)
      {
        resolveError = new VfxBindingResolveError
        {
          Code = VfxBindingResolveCode.NotFound,
          Message = "持续视觉状态绑定不存在。",
          IdentityId = request.StateId,
        };
        return false;
      }

      if (matches.Count > 1)
      {
        resolveError = new VfxBindingResolveError
        {
          Code = VfxBindingResolveCode.Ambiguous,
          Message = "同 state/同 specificity 存在多条可命中绑定。",
          IdentityId = request.StateId,
          BindingKey = matches[0].BindingKey,
          ConflictingBindingKey = matches[1].BindingKey,
        };
        return false;
      }

      binding = matches[0];
      return true;
    }

    internal static VfxSpatialOwnership ParseSpatialOwnership(string value)
    {
      if (string.Equals(value, "attached", StringComparison.OrdinalIgnoreCase))
      {
        return VfxSpatialOwnership.Attached;
      }

      return VfxSpatialOwnership.Independent;
    }

    public static string FormatSpatialOwnership(VfxSpatialOwnership ownership)
    {
      return ownership == VfxSpatialOwnership.Attached ? "attached" : "independent";
    }
  }

  public static class VfxBindingCatalogPaths
  {
    public const string ManifestAssetPath = "Assets/Resources/VFX/vfx_bindings.json";
    public const string ManifestResourcesKey = "VFX/vfx_bindings";
  }
}

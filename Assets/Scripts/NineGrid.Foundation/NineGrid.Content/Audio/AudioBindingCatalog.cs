using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Content.Audio
{
    [Serializable]
    public sealed class AudioBindingCatalogDto
    {
        public int schemaVersion;
        public string ticket;
        public AudioBindingDto[] bindings;
    }

    [Serializable]
    public sealed class AudioVariantDto
    {
        public string variantId;
        public string clipKey;
        public float weight = 1f;
        public float volumeTrimDb;
        public float startOffsetSeconds;
    }

    /// <summary>#178 作者态：AI 草稿可被后续自动绑定覆盖；人工确认后默认保留。</summary>
    public static class AudioBindingAuthoringStatuses
    {
        public const string AiDraft = "aiDraft";
        public const string HumanConfirmed = "humanConfirmed";

        public static bool IsHumanConfirmed(string status)
        {
            return string.Equals(status, HumanConfirmed, StringComparison.Ordinal);
        }

        public static bool IsAiDraft(string status)
        {
            return string.IsNullOrWhiteSpace(status)
                || string.Equals(status, AiDraft, StringComparison.Ordinal);
        }
    }

    [Serializable]
    public sealed class AudioBindingDto
    {
        public string cueId;
        public string note;
        public string module;
        public bool enabled = true;
        public string clipKey;
        public float volumeDb;
        public float startOffsetSeconds;
        public float bindingDelaySeconds;
        public float minimumIntervalSeconds;
        public AudioVariantDto[] variants;
        public string selectorCardDefId;
        public string selectorSkillId;
        public string selectorRoomId;
        public string selectorItemDefId;
        public string selectorContentId;
        /// <summary>aiDraft | humanConfirmed；缺省按草稿处理。</summary>
        public string authoringStatus;
    }

    public sealed class AudioBinding
    {
        public AudioBinding(
            string cueId,
            string note,
            string module,
            bool enabled,
            string clipKey,
            float volumeDb,
            float startOffsetSeconds,
            float bindingDelaySeconds,
            float minimumIntervalSeconds,
            AudioVariantDto[] variants,
            string selectorCardDefId,
            string selectorSkillId,
            string selectorRoomId,
            string selectorItemDefId,
            string selectorContentId,
            string authoringStatus = null)
        {
            CueId = cueId ?? string.Empty;
            Note = note ?? string.Empty;
            Module = module ?? string.Empty;
            Enabled = enabled;
            ClipKey = clipKey ?? string.Empty;
            VolumeDb = volumeDb;
            StartOffsetSeconds = startOffsetSeconds;
            BindingDelaySeconds = bindingDelaySeconds;
            MinimumIntervalSeconds = minimumIntervalSeconds;
            Variants = variants ?? Array.Empty<AudioVariantDto>();
            SelectorCardDefId = selectorCardDefId ?? string.Empty;
            SelectorSkillId = selectorSkillId ?? string.Empty;
            SelectorRoomId = selectorRoomId ?? string.Empty;
            SelectorItemDefId = selectorItemDefId ?? string.Empty;
            SelectorContentId = selectorContentId ?? string.Empty;
            AuthoringStatus = authoringStatus ?? string.Empty;
            BindingKey = AudioBindingKey.Compose(
                CueId,
                SelectorCardDefId,
                SelectorSkillId,
                SelectorRoomId,
                SelectorItemDefId,
                SelectorContentId);
        }

        public string CueId { get; }
        public string Note { get; }
        public string Module { get; }
        public bool Enabled { get; }
        public string ClipKey { get; }
        public float VolumeDb { get; }
        public float StartOffsetSeconds { get; }
        public float BindingDelaySeconds { get; }
        public float MinimumIntervalSeconds { get; }
        public IReadOnlyList<AudioVariantDto> Variants { get; }
        public string SelectorCardDefId { get; }
        public string SelectorSkillId { get; }
        public string SelectorRoomId { get; }
        public string SelectorItemDefId { get; }
        public string SelectorContentId { get; }
        public string AuthoringStatus { get; }
        public string BindingKey { get; }

        public bool IsBaseBinding => string.IsNullOrEmpty(SelectorCardDefId)
            && string.IsNullOrEmpty(SelectorSkillId)
            && string.IsNullOrEmpty(SelectorRoomId)
            && string.IsNullOrEmpty(SelectorItemDefId)
            && string.IsNullOrEmpty(SelectorContentId);

        public int SelectorSpecificity =>
            CountNonEmpty(SelectorCardDefId)
            + CountNonEmpty(SelectorSkillId)
            + CountNonEmpty(SelectorRoomId)
            + CountNonEmpty(SelectorItemDefId)
            + CountNonEmpty(SelectorContentId);

        public bool Matches(AudioCueRequest request)
        {
            return MatchesSelector(SelectorCardDefId, request.CardDefId)
                && MatchesSelector(SelectorSkillId, request.SkillId)
                && MatchesSelector(SelectorRoomId, request.RoomId)
                && MatchesSelector(SelectorItemDefId, request.ItemDefId)
                && MatchesSelector(SelectorContentId, request.ContentId);
        }

        private static bool MatchesSelector(string selector, string actual)
        {
            return string.IsNullOrEmpty(selector)
                || string.Equals(selector, actual ?? string.Empty, StringComparison.Ordinal);
        }

        private static int CountNonEmpty(string value)
        {
            return string.IsNullOrEmpty(value) ? 0 : 1;
        }
    }
    public static class AudioBindingKey
    {
        private const char Separator = '\u001f';

        public static string Compose(
            string cueId,
            string selectorCardDefId,
            string selectorSkillId,
            string selectorRoomId,
            string selectorItemDefId,
            string selectorContentId)
        {
            return string.Join(
                Separator.ToString(),
                cueId ?? string.Empty,
                selectorCardDefId ?? string.Empty,
                selectorSkillId ?? string.Empty,
                selectorRoomId ?? string.Empty,
                selectorItemDefId ?? string.Empty,
                selectorContentId ?? string.Empty);
        }
    }

    public sealed class AudioBindingCatalog
    {
        private readonly List<AudioBinding> mBindings;

        private AudioBindingCatalog(List<AudioBinding> bindings)
        {
            mBindings = bindings;
        }

        public IReadOnlyList<AudioBinding> Bindings => mBindings;

        /// <summary>
        /// 严格解析：空/非法 JSON、空 DTO、null bindings 均失败且不产出 catalog。
        /// 工作台热应用必须走此路径，避免坏补丁静默换成空 catalog。
        /// </summary>
        public static bool TryFromJson(string json, out AudioBindingCatalog catalog, out string error)
        {
            catalog = null;
            error = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "catalog JSON is empty.";
                return false;
            }

            AudioBindingCatalogDto dto;
            try
            {
                dto = JsonUtility.FromJson<AudioBindingCatalogDto>(json);
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

            if (dto.bindings == null)
            {
                error = "catalog bindings collection is null.";
                return false;
            }

            var result = new List<AudioBinding>(dto.bindings.Length);
            for (var i = 0; i < dto.bindings.Length; i++)
            {
                var row = dto.bindings[i];
                if (row == null)
                {
                    continue;
                }

                result.Add(new AudioBinding(
                    row.cueId,
                    row.note,
                    row.module,
                    row.enabled,
                    row.clipKey,
                    row.volumeDb,
                    row.startOffsetSeconds,
                    row.bindingDelaySeconds,
                    row.minimumIntervalSeconds,
                    row.variants,
                    row.selectorCardDefId,
                    row.selectorSkillId,
                    row.selectorRoomId,
                    row.selectorItemDefId,
                    row.selectorContentId,
                    row.authoringStatus));
            }

            catalog = new AudioBindingCatalog(result);
            return true;
        }

        /// <summary>容错加载：失败时返回空 catalog（运行时 Resources 装载用）。</summary>
        public static AudioBindingCatalog FromJson(string json)
        {
            if (!TryFromJson(json, out var catalog, out _))
            {
                return new AudioBindingCatalog(new List<AudioBinding>());
            }

            return catalog;
        }

        public static AudioBindingCatalog LoadFromResources()
        {
            var asset = Resources.Load<TextAsset>(AudioBindingCatalogPaths.ManifestResourcesKey);
            return FromJson(asset == null ? string.Empty : asset.text);
        }

        public bool TryResolve(AudioCueRequest request, out AudioBinding binding)
        {
            binding = null;
            var bestSpecificity = -1;
            var bestIndex = -1;
            for (var i = 0; i < mBindings.Count; i++)
            {
                var candidate = mBindings[i];
                if (candidate == null
                    || !string.Equals(candidate.CueId, request.CueId, StringComparison.Ordinal)
                    || !candidate.Matches(request))
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
    }

    public static class AudioBindingCatalogPaths
    {
        public const string ManifestAssetPath = "Assets/Resources/audio/audio_bindings.json";
        public const string ManifestResourcesKey = "audio/audio_bindings";
    }
}

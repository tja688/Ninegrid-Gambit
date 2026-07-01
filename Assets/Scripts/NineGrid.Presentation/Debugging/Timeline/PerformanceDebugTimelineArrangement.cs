using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NineGrid.Presentation.Debugging.Timeline
{
    [Serializable]
    internal sealed class TimelinePayloadEntryDto
    {
        public string key;
        public string value;
    }

    [Serializable]
    internal sealed class TimelineClipDto
    {
        public string clipId;
        public string moduleId;
        public float startTime;
        public int track;
        public TimelinePayloadEntryDto[] payload;
    }

    [Serializable]
    internal sealed class TimelineArrangementDto
    {
        public int trackCount = 4;
        public TimelineClipDto[] clips;
    }

    public sealed class PerformanceDebugTimelineArrangement
    {
        public const int DefaultTrackCount = 4;
        public const int MinTrackCount = 1;
        public const int MaxTrackCount = 16;

        private readonly List<PerformanceDebugTimelineClip> clips = new();

        public IReadOnlyList<PerformanceDebugTimelineClip> Clips => clips;
        public int TrackCount { get; set; } = DefaultTrackCount;

        public PerformanceDebugTimelineClip AddClip(
            string moduleId,
            float startTime,
            int track,
            PerformanceDebugPayload payload = null)
        {
            var clip = PerformanceDebugTimelineClip.Create(moduleId, startTime, track, payload);
            clips.Add(clip);
            return clip;
        }

        public bool RemoveClip(string clipId)
        {
            for (var i = 0; i < clips.Count; i++)
            {
                if (clips[i].ClipId == clipId)
                {
                    clips.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        public PerformanceDebugTimelineClip Duplicate(string clipId)
        {
            PerformanceDebugTimelineClip source = FindClip(clipId);
            if (source == null)
            {
                return null;
            }

            var duplicate = source.Clone();
            duplicate.StartTime += 0.5f;
            clips.Add(duplicate);
            return duplicate;
        }

        public PerformanceDebugTimelineClip FindClip(string clipId)
        {
            for (var i = 0; i < clips.Count; i++)
            {
                if (clips[i].ClipId == clipId)
                {
                    return clips[i];
                }
            }

            return null;
        }

        public void Clear()
        {
            clips.Clear();
            TrackCount = DefaultTrackCount;
        }

        public float GetDuration(PerformanceDebugCatalog catalog = null, PerformanceDebugContext context = null)
        {
            float end = 0f;
            for (var i = 0; i < clips.Count; i++)
            {
                PerformanceDebugTimelineClip clip = clips[i];
                float duration = 0.5f;
                if (catalog != null)
                {
                    IPerformanceDebugModule module = catalog.FindById(clip.ModuleId);
                    if (module != null && context != null)
                    {
                        duration = Mathf.Max(0.1f, module.TryGetExpectedDuration(context));
                    }
                }

                end = Mathf.Max(end, clip.StartTime + duration);
            }

            return Mathf.Max(end, 1f);
        }

        public string ToJson()
        {
            var dto = new TimelineArrangementDto
            {
                trackCount = TrackCount,
                clips = clips.Select(ToDto).ToArray(),
            };
            return JsonUtility.ToJson(dto, false);
        }

        public static PerformanceDebugTimelineArrangement FromJson(string json)
        {
            var arrangement = new PerformanceDebugTimelineArrangement();
            if (string.IsNullOrEmpty(json))
            {
                return arrangement;
            }

            try
            {
                TimelineArrangementDto dto = JsonUtility.FromJson<TimelineArrangementDto>(json);
                if (dto == null)
                {
                    return arrangement;
                }

                arrangement.TrackCount = Mathf.Clamp(dto.trackCount, MinTrackCount, MaxTrackCount);
                if (dto.clips == null)
                {
                    return arrangement;
                }

                for (var i = 0; i < dto.clips.Length; i++)
                {
                    PerformanceDebugTimelineClip clip = FromDto(dto.clips[i]);
                    if (clip != null)
                    {
                        arrangement.clips.Add(clip);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TimelineArrangement] Failed to parse JSON: {ex.Message}");
            }

            return arrangement;
        }

        private static TimelineClipDto ToDto(PerformanceDebugTimelineClip clip)
        {
            var entries = new List<TimelinePayloadEntryDto>();
            if (clip.Payload != null)
            {
                foreach (KeyValuePair<string, string> pair in clip.Payload.Values)
                {
                    entries.Add(new TimelinePayloadEntryDto { key = pair.Key, value = pair.Value });
                }
            }

            return new TimelineClipDto
            {
                clipId = clip.ClipId,
                moduleId = clip.ModuleId,
                startTime = clip.StartTime,
                track = clip.Track,
                payload = entries.ToArray(),
            };
        }

        private static PerformanceDebugTimelineClip FromDto(TimelineClipDto dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.moduleId))
            {
                return null;
            }

            var payload = new PerformanceDebugPayload();
            if (dto.payload != null)
            {
                for (var i = 0; i < dto.payload.Length; i++)
                {
                    TimelinePayloadEntryDto entry = dto.payload[i];
                    if (!string.IsNullOrEmpty(entry?.key))
                    {
                        payload.Set(entry.key, entry.value);
                    }
                }
            }

            return new PerformanceDebugTimelineClip
            {
                ClipId = string.IsNullOrEmpty(dto.clipId) ? Guid.NewGuid().ToString("N") : dto.clipId,
                ModuleId = dto.moduleId,
                StartTime = dto.startTime,
                Track = dto.track,
                Payload = payload,
            };
        }
    }
}

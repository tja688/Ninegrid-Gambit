using UnityEngine;

namespace NineGrid.Audio
{
    public enum AudioPriority
    {
        S,
        A,
        B,
        C,
    }

    public enum AudioCategory
    {
        SceneFlow,
        Battle,
        Forge,
        Prologue,
        UI,
        Selection,
        Environment,
        DataDriven,
    }

    public enum LoopMode
    {
        None,
        Loop,
        LoopWithDuration,
    }

    /// <summary>
    /// 单条音效配置条目：关联 AudioKey 与 Audio Manager Pro 的 SFXObject/Track，
    /// 并提供音量、音高、循环等覆盖参数。triggerContext 字段供 AI 语义分析音效库时参考。
    /// </summary>
    [System.Serializable]
    public class AudioCueEntry
    {
        [Header("Identity")]
        [Tooltip("The audio key that maps to the hit-point from the sound design document.")]
        public AudioKey key;

        [Tooltip("Human-readable name for Inspector display.")]
        public string displayName;

        [Tooltip("Detailed description of when and why this sound is triggered. Used by AI to semantically match audio library files.")]
        [TextArea(2, 5)] public string triggerContext;

        public AudioPriority priority;
        public AudioCategory category;

        [Header("Audio Reference (Audio Manager Pro)")]
        [Tooltip("SFXObject SO from Audio Manager Pro. Leave null if unassigned (AI will fill in later).")]
        public SFXObject sfxObject;

        [Tooltip("Track SO for BGM entries. Leave null for non-music cues.")]
        public Track musicTrack;

        [Tooltip("AudioClip for loop-based sounds (environment/ambient). Used with SFXLoopManager.")]
        public AudioClip loopClip;

        [Header("Volume / Pitch Override")]
        [Tooltip("If true, the volume below overrides the SFXObject's own volume setting.")]
        public bool overrideVolume = false;
        [Range(0f, 1f)] public float volume = 1f;

        [Tooltip("If true, the pitch below overrides the SFXObject's own pitch setting.")]
        public bool overridePitch = false;
        [Range(0f, 3f)] public float pitch = 1f;

        [Header("Loop Settings")]
        [Tooltip("None = one-shot. Loop = indefinite loop (use StopLoop to stop). LoopWithDuration = auto-stop after loopDuration seconds.")]
        public LoopMode loopMode = LoopMode.None;
        [Tooltip("Duration in seconds for LoopWithDuration mode. 0 = no auto-stop.")]
        [Min(0f)] public float loopDuration = 0f;

        [Header("Spatial")]
        [Tooltip("0 = 2D, 1 = 3D positional.")]
        [Range(0f, 1f)] public float spatialBlend = 0f;

        [Header("State")]
        public bool enabled = true;

        [Tooltip("Free-form notes for the designer or AI.")]
        [TextArea(1, 3)] public string notes;
    }
}

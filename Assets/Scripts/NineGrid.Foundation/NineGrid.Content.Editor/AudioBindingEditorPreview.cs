#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    /// <summary>非 Play Mode 下的 AudioUtil 试听缝；Play Mode 试听走 IAudioSystem.PreviewWorkbenchBinding。</summary>
    public static class AudioBindingEditorPreview
    {
        private static MethodInfo playPreviewClip;
        private static MethodInfo stopAllPreviewClips;
        private static bool initialized;

        public static bool Play(AudioClip clip, float startOffsetSeconds)
        {
            EnsureMethods();
            if (clip == null || playPreviewClip == null)
            {
                return false;
            }

            var startSample = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Max(0f, startOffsetSeconds) * clip.frequency),
                0,
                Mathf.Max(0, clip.samples - 1));
            try
            {
                playPreviewClip.Invoke(null, new object[] { clip, startSample, false });
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[AudioBindingEditor] 试听失败：" + exception.Message);
                return false;
            }
        }

        public static void Stop()
        {
            EnsureMethods();
            try
            {
                if (stopAllPreviewClips != null)
                {
                    stopAllPreviewClips.Invoke(null, null);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[AudioBindingEditor] 停止试听失败：" + exception.Message);
            }
        }

        private static void EnsureMethods()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            var audioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            if (audioUtil == null)
            {
                return;
            }

            playPreviewClip = audioUtil.GetMethod(
                "PlayPreviewClip",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(AudioClip), typeof(int), typeof(bool) },
                null);
            stopAllPreviewClips = audioUtil.GetMethod(
                "StopAllPreviewClips",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);
        }
    }
}
#endif

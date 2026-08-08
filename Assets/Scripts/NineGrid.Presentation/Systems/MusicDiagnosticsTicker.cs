#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// Development-only low-frequency Music-track audit driver.
    /// It owns no music state and never stops sources by itself.
    /// </summary>
    internal sealed class MusicDiagnosticsTicker : MonoBehaviour
    {
        private const float AuditIntervalSeconds = 1f;
        private MusicSystem musicSystem;
        private float nextAuditAt;

        public static MusicDiagnosticsTicker Install(MusicSystem system)
        {
            var host = new GameObject("MusicDiagnosticsTicker");
            DontDestroyOnLoad(host);
            var ticker = host.AddComponent<MusicDiagnosticsTicker>();
            ticker.musicSystem = system;
            ticker.nextAuditAt = Time.unscaledTime + AuditIntervalSeconds;
            return ticker;
        }

        public void Dispose()
        {
            if (this == null || gameObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }

        private void Update()
        {
            if (musicSystem == null || Time.unscaledTime < nextAuditAt)
            {
                return;
            }

            nextAuditAt = Time.unscaledTime + AuditIntervalSeconds;
            musicSystem.AuditMusicTrack("LowFrequency巡检");
        }
    }
}
#endif

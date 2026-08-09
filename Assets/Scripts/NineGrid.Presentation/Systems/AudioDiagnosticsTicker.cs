#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// Development-only low-frequency Sfx-track audit driver.
    /// </summary>
    internal sealed class AudioDiagnosticsTicker : MonoBehaviour
    {
        private const float AuditIntervalSeconds = 1f;

        private AudioDiagnosticsService mService;
        private float mNextAuditAt;

        public static AudioDiagnosticsTicker Install(AudioDiagnosticsService service)
        {
            var host = new GameObject(nameof(AudioDiagnosticsTicker));
            DontDestroyOnLoad(host);
            var ticker = host.AddComponent<AudioDiagnosticsTicker>();
            ticker.mService = service;
            ticker.mNextAuditAt = Time.unscaledTime + AuditIntervalSeconds;
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
            if (mService == null || Time.unscaledTime < mNextAuditAt)
            {
                return;
            }

            mNextAuditAt = Time.unscaledTime + AuditIntervalSeconds;
            mService.AuditSfxTrack("LowFrequency巡检");
        }
    }
}
#endif

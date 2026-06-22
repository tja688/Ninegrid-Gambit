using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace NineGrid.Presentation.Performance
{
    /// <summary>
    /// 卡牌颤抖表演器：支持单次冲击抖动（OneShot）和持续微颤（Continuous）两种模式。
    /// 挂载到任意 GameObject 上，调用 StartShake / StopShake 控制。
    /// Inspector 右键菜单可在运行时即时预览效果。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardShakePerformance : MonoBehaviour
    {
        public enum ShakeMode
        {
            /// <summary>单次冲击抖动，幅度逐渐衰减至零后自动停止。</summary>
            OneShot,
            /// <summary>持续微颤，调用 StopShake 或再次 StartShake 时停止。</summary>
            Continuous,
        }

        [Header("Target")]
        [Tooltip("被抖动的 Transform；为空时使用本物体。")]
        [SerializeField] private Transform shakeTarget;

        [Header("Shake Mode")]
        [SerializeField] private ShakeMode mode = ShakeMode.OneShot;

        [Header("Position Shake")]
        [Tooltip("位置抖动强度（世界单位）。2D 下仅 X/Y 生效。")]
        [SerializeField] private Vector3 positionStrength = new(0.08f, 0.08f, 0f);
        [Tooltip("每周期振荡次数，越高抖动越快。")]
        [SerializeField, Min(1)] private int positionVibrato = 10;
        [Tooltip("0-180，越高方向越随机；推荐 30-90。")]
        [SerializeField, Range(0f, 180f)] private float positionRandomness = 45f;

        [Header("Rotation Shake")]
        [Tooltip("旋转抖动强度（度）。2D 下仅 Z 轴生效。")]
        [SerializeField] private Vector3 rotationStrength = new(0f, 0f, 2f);
        [SerializeField, Min(1)] private int rotationVibrato = 8;
        [SerializeField, Range(0f, 180f)] private float rotationRandomness = 30f;

        [Header("Timing")]
        [Tooltip("OneShot 模式下为单次总时长；Continuous 模式下为每个循环的时长。")]
        [SerializeField, Min(0.01f)] private float duration = 0.35f;
        [Tooltip("OneShot 模式下幅度是否随时间衰减。")]
        [SerializeField] private bool fadeOut = true;

        [Header("Playback")]
        [SerializeField] private bool ignoreTimeScale;

        [Header("Smooth Stop")]
        [Tooltip("停止时用多长的过渡平滑归位（秒），0 = 立即归位。")]
        [SerializeField, Min(0f)] private float smoothStopDuration = 0.12f;

        [Header("Preview")]
        [Tooltip("预览用卡牌模板；为 null 时生成纯色 Sprite 占位。")]
        [SerializeField] private GameObject cardPreviewPrefab;

        private static Sprite fallbackPreviewSprite;

        private readonly List<Transform> spawnedPreviewActors = new();

        private Sequence activeSequence;
        private Coroutine smoothStopCoroutine;

        private Vector3 baselineLocalPosition;
        private Vector3 baselineLocalEulerAngles;
        private bool baselineCaptured;

        private Transform ResolvedTarget => shakeTarget != null ? shakeTarget : transform;

        public bool IsShaking => activeSequence != null && activeSequence.IsActive() && activeSequence.IsPlaying();

        private void OnDisable()
        {
            KillImmediate();
        }

        private void OnDestroy()
        {
            KillImmediate();
            TeardownPreviewActors();
        }

        private void OnValidate()
        {
            positionVibrato = Mathf.Max(1, positionVibrato);
            rotationVibrato = Mathf.Max(1, rotationVibrato);
            duration = Mathf.Max(0.01f, duration);
            smoothStopDuration = Mathf.Max(0f, smoothStopDuration);
        }

        // ─── Public API ─────────────────────────────────────────

        /// <summary>
        /// 启动颤抖。如果已有颤抖在运行，会先停止再重新开始。
        /// </summary>
        public void StartShake()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            Transform target = ResolvedTarget;

            KillTweensOnTarget(target);
            CancelSmoothStop();
            CaptureBaseline(target);
            BuildAndPlay(target, null);
        }

        /// <summary>
        /// 带方向偏置的启动（适合受击反馈）。direction 会叠加到位置抖动强度上。
        /// </summary>
        public void StartShake(Vector3 direction)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            Transform target = ResolvedTarget;

            KillTweensOnTarget(target);
            CancelSmoothStop();
            CaptureBaseline(target);

            Vector3 biasedPosition = positionStrength + new Vector3(
                Mathf.Abs(direction.x),
                Mathf.Abs(direction.y),
                Mathf.Abs(direction.z));

            BuildAndPlay(target, biasedPosition);
        }

        /// <summary>
        /// 停止颤抖并平滑归位。
        /// </summary>
        [ContextMenu("Stop Shake")]
        public void StopShake()
        {
            if (smoothStopDuration > 0f && IsShaking)
            {
                CancelSmoothStop();
                smoothStopCoroutine = StartCoroutine(SmoothStopRoutine());
            }
            else
            {
                KillImmediate();
            }
        }

        // ─── Context Menu Preview ───────────────────────────────

        [ContextMenu("▶ Preview Shake")]
        public void PreviewShake()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            KillImmediate();

            Transform target = ResolvedTarget;
            CaptureBaseline(target);
            BuildAndPlay(target, null);
        }

        [ContextMenu("▶ Preview Shake (Spawn Card)")]
        public void PreviewShakeWithCard()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            KillImmediate();
            TeardownPreviewActors();

            Transform actor = CreatePreviewActor(transform, 0);
            spawnedPreviewActors.Add(actor);

            CaptureBaseline(actor);
            BuildAndPlay(actor, null);
        }

        [ContextMenu("■ Stop Preview")]
        public void StopPreview()
        {
            StopShake();
            TeardownPreviewActors();
        }

        // ─── Internal ───────────────────────────────────────────

        private void CaptureBaseline(Transform target)
        {
            if (target == null)
            {
                return;
            }

            baselineLocalPosition = target.localPosition;
            baselineLocalEulerAngles = target.localEulerAngles;
            baselineCaptured = true;
        }

        private void BuildAndPlay(Transform target, Vector3? positionStrengthOverride)
        {
            if (target == null)
            {
                return;
            }

            Vector3 posStrength = positionStrengthOverride ?? positionStrength;

            var sequence = DOTween.Sequence()
                .SetTarget(this)
                .SetAutoKill(true);

            if (ignoreTimeScale)
            {
                sequence.SetUpdate(true);
            }

            bool shouldFade = mode == ShakeMode.OneShot && fadeOut;
            int loopCount = mode == ShakeMode.Continuous ? -1 : 1;

            if (posStrength.sqrMagnitude > 0.0001f)
            {
                Tween posTween = target.DOShakePosition(
                    duration, posStrength, positionVibrato,
                    positionRandomness, false, shouldFade,
                    ShakeRandomnessMode.Harmonic);

                ConfigureTween(posTween);
                sequence.Join(posTween);
            }

            if (rotationStrength.sqrMagnitude > 0.0001f)
            {
                Tween rotTween = target.DOShakeRotation(
                    duration, rotationStrength, rotationVibrato,
                    rotationRandomness, shouldFade,
                    ShakeRandomnessMode.Harmonic);

                ConfigureTween(rotTween);
                sequence.Join(rotTween);
            }

            sequence.SetLoops(loopCount);
            sequence.OnComplete(HandleSequenceComplete);
            sequence.OnKill(HandleSequenceKilled);

            activeSequence = sequence;
        }

        private void ConfigureTween(Tween tween)
        {
            tween.SetTarget(this);

            if (ignoreTimeScale)
            {
                tween.SetUpdate(true);
            }
        }

        private IEnumerator SmoothStopRoutine()
        {
            Transform target = ResolvedTarget;

            if (smoothStopDuration <= 0f || !baselineCaptured)
            {
                KillImmediate();
                yield break;
            }

            float elapsed = 0f;

            while (elapsed < smoothStopDuration)
            {
                elapsed += ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / smoothStopDuration);
                float scale = 1f - Mathf.SmoothStep(0f, 1f, progress);

                Vector3 currentPos = target.localPosition;
                Vector3 currentRot = target.localEulerAngles;

                target.localPosition = Vector3.Lerp(baselineLocalPosition, currentPos, scale);
                target.localEulerAngles = Vector3.Lerp(baselineLocalEulerAngles, currentRot, scale);

                yield return null;
            }

            KillImmediate();
            smoothStopCoroutine = null;
        }

        private void KillImmediate()
        {
            CancelSmoothStop();

            if (activeSequence != null && activeSequence.IsActive())
            {
                activeSequence.OnComplete(null);
                activeSequence.OnKill(null);
                activeSequence.Kill();
            }

            activeSequence = null;
            DOTween.Kill(this);

            Transform target = ResolvedTarget;
            DOTween.Kill(target);

            RestoreBaseline(target);
        }

        private void KillTweensOnTarget(Transform target)
        {
            if (activeSequence != null && activeSequence.IsActive())
            {
                activeSequence.OnComplete(null);
                activeSequence.OnKill(null);
                activeSequence.Kill();
            }

            activeSequence = null;
            DOTween.Kill(this);

            if (target != null)
            {
                DOTween.Kill(target);
            }
        }

        private void CancelSmoothStop()
        {
            if (smoothStopCoroutine != null)
            {
                StopCoroutine(smoothStopCoroutine);
                smoothStopCoroutine = null;
            }
        }

        private void RestoreBaseline(Transform target)
        {
            if (!baselineCaptured || target == null)
            {
                return;
            }

            target.localPosition = baselineLocalPosition;
            target.localEulerAngles = baselineLocalEulerAngles;
        }

        private void HandleSequenceComplete()
        {
            activeSequence = null;
            RestoreBaseline(ResolvedTarget);
        }

        private void HandleSequenceKilled()
        {
            if (activeSequence != null && !activeSequence.IsComplete())
            {
                activeSequence = null;
            }
        }

        // ─── Preview Helpers ────────────────────────────────────

        private Transform CreatePreviewActor(Transform parent, int index)
        {
            if (cardPreviewPrefab != null)
            {
                GameObject instance = Instantiate(cardPreviewPrefab, parent);
                instance.name = $"ShakePreviewCard_{index + 1}";
                return instance.transform;
            }

            var stub = new GameObject($"ShakePreviewCard_{index + 1}");
            stub.transform.SetParent(parent, worldPositionStays: false);
            stub.transform.localPosition = Vector3.zero;
            stub.transform.localEulerAngles = Vector3.zero;

            var spriteRenderer = stub.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = GetFallbackPreviewSprite();
            spriteRenderer.sortingOrder = 3;

            return stub.transform;
        }

        private void TeardownPreviewActors()
        {
            for (int i = spawnedPreviewActors.Count - 1; i >= 0; i--)
            {
                Transform actor = spawnedPreviewActors[i];
                if (actor == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(actor.gameObject);
                }
                else
                {
                    DestroyImmediate(actor.gameObject);
                }
            }

            spawnedPreviewActors.Clear();
        }

        private static Sprite GetFallbackPreviewSprite()
        {
            if (fallbackPreviewSprite != null)
            {
                return fallbackPreviewSprite;
            }

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, new Color(0.82f, 0.76f, 0.66f, 1f));
            texture.Apply();

            fallbackPreviewSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                100f);

            return fallbackPreviewSprite;
        }
    }
}

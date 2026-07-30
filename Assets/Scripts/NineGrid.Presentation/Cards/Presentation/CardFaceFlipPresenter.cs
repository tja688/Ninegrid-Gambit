using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards.Convergence;
using UnityEngine;

namespace NineGrid.Cards.Presentation
{
    /// <summary>
    /// 表现层翻牌 POC：在 FacePivot 上忠实采样 PixelartCardTCG <c>Flip.anim</c>
    /// （Y 旋转 + Scale 鼓起；源约 0.444s / 18fps，PlaybackSpeed=2 实播约 0.222s），中点切换 front/back。
    /// 不写 Core 朝向、不改 Binder Commit 镜像。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardFaceFlipPresenter : MonoBehaviour
    {
        /// <summary>源 Flip.anim 时长；实际播放 = Source / PlaybackSpeed。</summary>
        public const float SourceDurationSeconds = 0.44444445f;

        /// <summary>源 OnFlip 时刻。</summary>
        public const float SourceFaceSwapTimeSeconds = 0.22222222f;

        /// <summary>相对源 clip 的播放倍率（2 = 快一倍）。</summary>
        public const float PlaybackSpeed = 2f;

        /// <summary>实际翻牌时长。</summary>
        public static float DurationSeconds => SourceDurationSeconds / PlaybackSpeed;

        /// <summary>实际中点切面时刻。</summary>
        public static float FaceSwapTimeSeconds => SourceFaceSwapTimeSeconds / PlaybackSpeed;

        // Flip.anim 关键帧（Constant / Infinity slope → 阶跃采样；时间轴为源 clip）。
        private static readonly float[] KeyTimes =
        {
            0f,
            0.055555556f,
            0.11111111f,
            0.16666667f,
            0.22222222f,
            0.2777778f,
            0.33333334f,
            0.3888889f,
            0.44444445f,
        };

        private static readonly float[] KeyYDegrees =
        {
            0f,
            -14.062499f,
            -40f,
            -71.328125f,
            -90f,
            -75.93751f,
            -50.000008f,
            -18.671888f,
            0f,
        };

        private static readonly float[] KeyUniformScales =
        {
            1f,
            1.03125f,
            1.0888889f,
            1.158507f,
            1.2f,
            1.16875f,
            1.1111112f,
            1.041493f,
            1f,
        };

        private CardTransformTower _tower;
        private bool _playing;
        private bool _visualFaceUp = true;
        private CancellationTokenSource _playCts;

        public bool IsPlaying => _playing;

        /// <summary>表现层可见朝向（POC 本地态，非 Core FaceUp）。</summary>
        public bool VisualFaceUp => _visualFaceUp;

        private void Awake()
        {
            EnsureTower();
        }

        private void OnDisable()
        {
            CancelPlay();
            ResetPivotPose();
        }

        private void OnDestroy()
        {
            CancelPlay();
        }

        public UniTask ToggleFlipAsync(CancellationToken cancellationToken = default)
        {
            return PlayFlipAsync(!_visualFaceUp, cancellationToken);
        }

        /// <summary>不播动画，直接设可见朝向（测试 / 调试）。</summary>
        public void SnapVisualFace(bool faceUp)
        {
            CancelPlay();
            _visualFaceUp = faceUp;
            ApplyFaceOrientation(faceUp);
            ResetPivotPose();
        }

        public async UniTask PlayFlipAsync(bool targetFaceUp, CancellationToken cancellationToken = default)
        {
            if (_playing)
            {
                return;
            }

            var pivot = EnsureTower()?.FacePivot;
            if (pivot == null)
            {
                _visualFaceUp = targetFaceUp;
                ApplyFaceOrientation(targetFaceUp);
                return;
            }

            if (_visualFaceUp == targetFaceUp)
            {
                ApplyFaceOrientation(targetFaceUp);
                ResetPivotPose();
                return;
            }

            CancelPlay();
            _playCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, destroyCancellationToken);
            var token = _playCts.Token;

            _playing = true;
            var swapped = false;
            // 起手保持当前可见面，中点再切到目标面。
            ApplyFaceOrientation(_visualFaceUp);
            SamplePose(pivot, 0f);

            try
            {
                var elapsed = 0f;
                while (elapsed < DurationSeconds)
                {
                    token.ThrowIfCancellationRequested();
                    elapsed += Time.deltaTime;
                    var t = Mathf.Min(elapsed, DurationSeconds);
                    SamplePose(pivot, t);

                    if (!swapped && t >= FaceSwapTimeSeconds)
                    {
                        swapped = true;
                        ApplyFaceOrientation(targetFaceUp);
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }

                SamplePose(pivot, DurationSeconds);
                if (!swapped)
                {
                    ApplyFaceOrientation(targetFaceUp);
                }

                _visualFaceUp = targetFaceUp;
            }
            catch (System.OperationCanceledException)
            {
                ResetPivotPose();
                ApplyFaceOrientation(_visualFaceUp);
                throw;
            }
            finally
            {
                ResetPivotPose();
                _playing = false;
                if (_playCts != null)
                {
                    _playCts.Dispose();
                    _playCts = null;
                }
            }
        }

        /// <summary>测试/调试：按实际播放时间采样（已含 PlaybackSpeed）。</summary>
        public static void SampleFlipPose(float playbackTimeSeconds, out float yDegrees, out float uniformScale)
        {
            var sourceTime = Mathf.Clamp(playbackTimeSeconds * PlaybackSpeed, 0f, SourceDurationSeconds);
            yDegrees = SampleStepped(KeyTimes, KeyYDegrees, sourceTime);
            uniformScale = SampleStepped(KeyTimes, KeyUniformScales, sourceTime);
        }

        private void SamplePose(Transform pivot, float playbackTimeSeconds)
        {
            SampleFlipPose(playbackTimeSeconds, out var y, out var scale);
            var euler = pivot.localEulerAngles;
            euler.y = y;
            pivot.localEulerAngles = euler;
            pivot.localScale = new Vector3(scale, scale, scale);
        }

        private void ResetPivotPose()
        {
            var pivot = _tower != null ? _tower.FacePivot : null;
            if (pivot == null)
            {
                return;
            }

            var euler = pivot.localEulerAngles;
            euler.y = 0f;
            pivot.localEulerAngles = euler;
            pivot.localScale = Vector3.one;
        }

        private void ApplyFaceOrientation(bool faceUp)
        {
            var searchRoot = ResolveFaceSearchRoot();
            var front = FindNamedChild(searchRoot, "front", "Front", "正面", "CardFront");
            var back = FindNamedChild(searchRoot, "back", "Back", "背面", "CardBack");
            if (front != null)
            {
                front.gameObject.SetActive(faceUp);
            }

            if (back != null)
            {
                back.gameObject.SetActive(!faceUp);
            }
        }

        private Transform ResolveFaceSearchRoot()
        {
            EnsureTower();
            if (_tower != null && _tower.FacePivot != null && _tower.FacePivot.childCount > 0)
            {
                return _tower.FacePivot.GetChild(0);
            }

            return _tower != null ? _tower.FacePivot : transform;
        }

        private CardTransformTower EnsureTower()
        {
            if (_tower == null)
            {
                _tower = GetComponent<CardTransformTower>();
            }

            if (_tower == null)
            {
                _tower = gameObject.AddComponent<CardTransformTower>();
            }

            _tower.EnsureTower();
            return _tower;
        }

        private void CancelPlay()
        {
            if (_playCts == null)
            {
                return;
            }

            try
            {
                _playCts.Cancel();
            }
            catch (System.ObjectDisposedException)
            {
            }

            _playCts.Dispose();
            _playCts = null;
            _playing = false;
        }

        private static float SampleStepped(float[] times, float[] values, float t)
        {
            if (times == null || values == null || times.Length == 0 || values.Length == 0)
            {
                return 0f;
            }

            var last = Mathf.Min(times.Length, values.Length) - 1;
            if (t <= times[0])
            {
                return values[0];
            }

            if (t >= times[last])
            {
                return values[last];
            }

            for (var i = 0; i < last; i++)
            {
                if (t < times[i + 1])
                {
                    return values[i];
                }
            }

            return values[last];
        }

        private static Transform FindNamedChild(Transform root, params string[] names)
        {
            if (root == null || names == null)
            {
                return null;
            }

            for (var depth = 1; depth <= 8; depth++)
            {
                for (var i = 0; i < names.Length; i++)
                {
                    var found = FindChildIgnoreCase(root, names[i], depth);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }

        private static Transform FindChildIgnoreCase(Transform root, string name, int depthLimit)
        {
            if (root == null || string.IsNullOrEmpty(name) || depthLimit < 0)
            {
                return null;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child != null
                    && string.Equals(child.name, name, System.StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }

            if (depthLimit == 0)
            {
                return null;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindChildIgnoreCase(root.GetChild(i), name, depthLimit - 1);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}

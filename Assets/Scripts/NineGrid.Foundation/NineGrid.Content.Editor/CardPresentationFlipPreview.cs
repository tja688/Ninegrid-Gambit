#if UNITY_EDITOR
using System;
using NineGrid.Cards.Convergence;
using NineGrid.Cards.Presentation;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    /// <summary>
    /// 编辑器翻牌预览：与实战 <see cref="CardFaceFlipPresenter"/> 共用 Flip.anim 阶跃采样
    /// （Y 旋转 + Scale 鼓起、PlaybackSpeed、中点切正/背），只动 FacePivot。
    /// </summary>
    public sealed class CardPresentationFlipPreview
    {
        public static float DurationSeconds => CardFaceFlipPresenter.DurationSeconds;

        private Transform _pivot;
        private Transform _front;
        private Transform _back;
        private bool _playing;
        private bool _swapped;
        private bool _targetFaceUp;
        private float _elapsed;
        private Action _onCompleted;

        public bool IsPlaying => _playing;
        public bool TargetFaceUp => _targetFaceUp;

        public void Begin(Transform previewRoot, bool targetFaceUp, Action onCompleted = null)
        {
            Stop(resetRotation: false);
            if (previewRoot == null)
            {
                onCompleted?.Invoke();
                return;
            }

            _pivot = ResolvePivot(previewRoot);
            _front = FindFrontRoot(previewRoot);
            _back = FindBackRoot(previewRoot);
            _targetFaceUp = targetFaceUp;
            _elapsed = 0f;
            _swapped = false;
            _playing = true;
            _onCompleted = onCompleted;
            // 起手保持当前可见面，中点再切到目标面。
            ApplyOrientation(!_targetFaceUp);
            SamplePose(0f);
        }

        public void Tick(float deltaTime)
        {
            if (!_playing || _pivot == null)
            {
                return;
            }

            if (deltaTime < 0f)
            {
                deltaTime = 0f;
            }

            _elapsed += deltaTime;
            var t = Mathf.Min(_elapsed, DurationSeconds);
            SamplePose(t);

            if (!_swapped && t >= CardFaceFlipPresenter.FaceSwapTimeSeconds)
            {
                _swapped = true;
                ApplyOrientation(_targetFaceUp);
            }

            if (_elapsed >= DurationSeconds)
            {
                Complete();
            }
        }

        public void Stop(bool resetRotation = true)
        {
            _playing = false;
            _onCompleted = null;
            if (resetRotation)
            {
                ResetPivotPose();
            }

            _pivot = null;
            _front = null;
            _back = null;
        }

        public static Transform FindFrontRoot(Transform root)
        {
            return FindNamedChild(root, "Front", "正面", "CardFront", "front");
        }

        public static Transform FindBackRoot(Transform root)
        {
            return FindNamedChild(root, "Back", "背面", "CardBack", "back");
        }

        private void Complete()
        {
            _playing = false;
            ResetPivotPose();
            ApplyOrientation(_targetFaceUp);
            var done = _onCompleted;
            _onCompleted = null;
            done?.Invoke();
        }

        private void ApplyOrientation(bool faceUp)
        {
            if (_front != null)
            {
                _front.gameObject.SetActive(faceUp);
            }

            if (_back != null)
            {
                _back.gameObject.SetActive(!faceUp);
            }
        }

        private void SamplePose(float playbackTimeSeconds)
        {
            if (_pivot == null)
            {
                return;
            }

            CardFaceFlipPresenter.SampleFlipPose(playbackTimeSeconds, out var y, out var scale);
            var euler = _pivot.localEulerAngles;
            euler.y = y;
            _pivot.localEulerAngles = euler;
            _pivot.localScale = new Vector3(scale, scale, scale);
        }

        private void ResetPivotPose()
        {
            if (_pivot == null)
            {
                return;
            }

            var euler = _pivot.localEulerAngles;
            euler.y = 0f;
            _pivot.localEulerAngles = euler;
            _pivot.localScale = Vector3.one;
        }

        private static Transform ResolvePivot(Transform previewRoot)
        {
            if (previewRoot == null)
            {
                return null;
            }

            var tower = previewRoot.GetComponent<CardTransformTower>();
            if (tower == null)
            {
                tower = previewRoot.GetComponentInChildren<CardTransformTower>(true);
            }

            if (tower != null)
            {
                tower.EnsureTower();
                if (tower.FacePivot != null)
                {
                    return tower.FacePivot;
                }
            }

            return previewRoot;
        }

        private static Transform FindNamedChild(Transform root, params string[] names)
        {
            if (root == null || names == null || names.Length == 0)
            {
                return null;
            }

            for (var i = 0; i < names.Length; i++)
            {
                var found = FindChildIgnoreCase(root, names[i], depthLimit: 1);
                if (found != null)
                {
                    return found;
                }
            }

            for (var i = 0; i < names.Length; i++)
            {
                var found = FindChildIgnoreCase(root, names[i], depthLimit: 8);
                if (found != null)
                {
                    return found;
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
                    && string.Equals(child.name, name, StringComparison.OrdinalIgnoreCase))
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
                var child = root.GetChild(i);
                var found = FindChildIgnoreCase(child, name, depthLimit - 1);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
#endif

#if UNITY_EDITOR
using System;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    /// <summary>
    /// 编辑器翻牌预览：Y 轴 0→-90→0（约 0.45s，对齐 Flip.anim），中点切换正/背面可见性。
    /// </summary>
    public sealed class CardPresentationFlipPreview
    {
        public const float DurationSeconds = 0.45f;

        private Transform _root;
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
            _root = previewRoot;
            if (_root == null)
            {
                onCompleted?.Invoke();
                return;
            }

            _front = FindFrontRoot(_root);
            _back = FindBackRoot(_root);
            _targetFaceUp = targetFaceUp;
            _elapsed = 0f;
            _swapped = false;
            _playing = true;
            _onCompleted = onCompleted;
            ApplyOrientation(!_targetFaceUp);
            SetYRotation(0f);
        }

        public void Tick(float deltaTime)
        {
            if (!_playing || _root == null)
            {
                return;
            }

            if (deltaTime < 0f)
            {
                deltaTime = 0f;
            }

            _elapsed += deltaTime;
            var t = Mathf.Clamp01(_elapsed / DurationSeconds);
            var y = SampleYRotation(t);
            SetYRotation(y);

            if (!_swapped && t >= 0.5f)
            {
                _swapped = true;
                ApplyOrientation(_targetFaceUp);
            }

            if (t >= 1f)
            {
                Complete();
            }
        }

        public void Stop(bool resetRotation = true)
        {
            _playing = false;
            _onCompleted = null;
            if (resetRotation && _root != null)
            {
                SetYRotation(0f);
            }
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
            SetYRotation(0f);
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

        private void SetYRotation(float yDegrees)
        {
            if (_root == null)
            {
                return;
            }

            var euler = _root.localEulerAngles;
            euler.y = yDegrees;
            _root.localEulerAngles = euler;
        }

        private static float SampleYRotation(float t)
        {
            // Flip.anim：约 0→-90（前半）→0（后半）。
            if (t <= 0.5f)
            {
                return Mathf.Lerp(0f, -90f, t * 2f);
            }

            return Mathf.Lerp(-90f, 0f, (t - 0.5f) * 2f);
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

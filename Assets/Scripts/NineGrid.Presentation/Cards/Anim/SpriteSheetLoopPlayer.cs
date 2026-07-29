using UnityEngine;

namespace NineGrid.Cards.Anim
{
    /// <summary>
    /// 通用精灵表循环播放器：只改 sprite / localScale；支持 EditMode EditorTick。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpriteSheetLoopPlayer : MonoBehaviour
    {
        [SerializeField]
        private SpriteRenderer target;

        [SerializeField]
        private float fps = 12f;

        [SerializeField]
        private bool looping = true;

        private Sprite[] _frames = System.Array.Empty<Sprite>();
        private int _frameIndex;
        private float _elapsed;
        private bool _playing;
        private float _speed = 1f;

        public SpriteRenderer Target => target;
        public bool IsPlaying => _playing;
        public bool Looping
        {
            get => looping;
            set => looping = value;
        }

        public float Fps
        {
            get => fps;
            set => fps = Mathf.Max(0.01f, value);
        }

        public float Speed
        {
            get => _speed;
            set => _speed = Mathf.Max(0.01f, value);
        }

        public float UniformScale
        {
            get
            {
                EnsureTarget();
                return target != null ? target.transform.localScale.x : 1f;
            }
            set
            {
                EnsureTarget();
                if (target == null)
                {
                    return;
                }

                var s = Mathf.Max(0.01f, value);
                target.transform.localScale = new Vector3(s, s, 1f);
            }
        }

        private void Update()
        {
            EditorTick(Time.deltaTime);
        }

        /// <summary>EditMode 预览：由 EditorApplication.update 喂 delta。</summary>
        public void EditorTick(float deltaTime)
        {
            if (!_playing || _frames == null || _frames.Length <= 1)
            {
                return;
            }

            if (deltaTime < 0f)
            {
                deltaTime = 0f;
            }

            var rate = fps * _speed;
            if (rate < 0.01f)
            {
                rate = 0.01f;
            }

            _elapsed += deltaTime;
            var frameDuration = 1f / rate;
            while (_elapsed >= frameDuration)
            {
                _elapsed -= frameDuration;
                _frameIndex++;
                if (_frameIndex >= _frames.Length)
                {
                    if (looping)
                    {
                        _frameIndex = 0;
                    }
                    else
                    {
                        _frameIndex = _frames.Length - 1;
                        _playing = false;
                        ApplyCurrentFrame();
                        return;
                    }
                }

                ApplyCurrentFrame();
            }
        }

        public void SetFrames(Sprite[] frames, bool play = true)
        {
            EnsureTarget();
            _frames = frames ?? System.Array.Empty<Sprite>();
            _frameIndex = 0;
            _elapsed = 0f;
            if (_frames.Length == 0)
            {
                _playing = false;
                if (target != null)
                {
                    target.sprite = null;
                    target.enabled = false;
                }

                return;
            }

            ApplyCurrentFrame();
            if (target != null)
            {
                target.enabled = true;
            }

            _playing = play && _frames.Length > 1;
            if (!_playing && play && _frames.Length == 1)
            {
                // 单帧也算“在播”，便于 UI 状态一致。
                _playing = true;
            }
        }

        public void Play()
        {
            if (_frames == null || _frames.Length == 0)
            {
                return;
            }

            _playing = true;
        }

        public void Pause()
        {
            _playing = false;
        }

        public void Stop()
        {
            _playing = false;
            _frameIndex = 0;
            _elapsed = 0f;
            ApplyCurrentFrame();
        }

        public void BindTarget(SpriteRenderer renderer)
        {
            target = renderer;
        }

        private void ApplyCurrentFrame()
        {
            if (target == null || _frames == null || _frames.Length == 0)
            {
                return;
            }

            if (_frameIndex < 0 || _frameIndex >= _frames.Length)
            {
                return;
            }

            var frame = _frames[_frameIndex];
            if (frame != null)
            {
                target.sprite = frame;
            }
        }

        private void EnsureTarget()
        {
            if (target != null)
            {
                return;
            }

            target = GetComponent<SpriteRenderer>();
        }
    }
}

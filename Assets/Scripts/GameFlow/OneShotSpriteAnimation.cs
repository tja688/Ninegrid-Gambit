using UnityEngine;

namespace NineGrid.GameFlow
{
    /// <summary>
    /// 单向帧动画：热键触发后从第一帧播到最后一帧并停住，再按则从头重播。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class OneShotSpriteAnimation : MonoBehaviour
    {
        [SerializeField] SpriteRenderer targetRenderer;
        [SerializeField] Sprite[] frames;
        [SerializeField] float framesPerSecond = 10f;
        [SerializeField] KeyCode playKey = KeyCode.Keypad6;

        int _index;
        float _timer;
        bool _playing;

        void Awake()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<SpriteRenderer>();
            }

            ApplyFrame(0);
        }

        void OnValidate()
        {
            framesPerSecond = Mathf.Max(0.01f, framesPerSecond);
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<SpriteRenderer>();
            }
        }

        void Update()
        {
            if (DebugHotkeyInput.WasPressedThisFrame(playKey))
            {
                Play();
            }

            if (!_playing || targetRenderer == null || frames == null || frames.Length == 0)
            {
                return;
            }

            _timer += Time.deltaTime;
            var frameDuration = 1f / framesPerSecond;
            while (_playing && _timer >= frameDuration)
            {
                _timer -= frameDuration;
                if (_index >= frames.Length - 1)
                {
                    _playing = false;
                    ApplyFrame(frames.Length - 1);
                    break;
                }

                _index++;
                ApplyFrame(_index);
            }
        }

        public void Play()
        {
            if (frames == null || frames.Length == 0)
            {
                return;
            }

            _index = 0;
            _timer = 0f;
            _playing = true;
            ApplyFrame(0);
        }

        void ApplyFrame(int index)
        {
            if (targetRenderer == null || frames == null || frames.Length == 0)
            {
                return;
            }

            index = Mathf.Clamp(index, 0, frames.Length - 1);
            var frame = frames[index];
            if (frame != null)
            {
                targetRenderer.sprite = frame;
            }
        }
    }
}

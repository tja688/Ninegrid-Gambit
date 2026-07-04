using UnityEngine;

namespace NineGrid.Presentation.Visuals
{
    /// <summary>
    /// 在 SpriteRenderer（含 Tiled）上循环播放帧动画。
    /// </summary>
    public sealed class AnimatedTiledSprite : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private Sprite[] frames;
        [SerializeField] private float framesPerSecond = 8f;

        private int _index;
        private float _timer;

        private void Awake()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<SpriteRenderer>();
            }

            if (targetRenderer != null && frames != null && frames.Length > 0 && frames[0] != null)
            {
                targetRenderer.sprite = frames[0];
            }
        }

        private void OnValidate()
        {
            framesPerSecond = Mathf.Max(0.01f, framesPerSecond);
        }

        private void Update()
        {
            if (targetRenderer == null || frames == null || frames.Length == 0)
            {
                return;
            }

            _timer += Time.deltaTime;
            var frameDuration = 1f / framesPerSecond;
            while (_timer >= frameDuration)
            {
                _timer -= frameDuration;
                _index = (_index + 1) % frames.Length;
                var frame = frames[_index];
                if (frame != null)
                {
                    targetRenderer.sprite = frame;
                }
            }
        }
    }
}

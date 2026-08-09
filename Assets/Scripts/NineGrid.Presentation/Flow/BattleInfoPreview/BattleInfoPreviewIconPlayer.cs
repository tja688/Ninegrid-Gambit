using NineGrid.Cards.Anim;
using NineGrid.Content.CardPresentation;
using UnityEngine;

namespace NineGrid.Flow.BattleInfoPreview
{
    /// <summary>
    /// 预览槽图标播放：只切 SpriteRenderer.sprite，不改 transform / Mask 锚点
    /// （卡面 <see cref="NineGrid.Cards.Anim.CardSpriteAnimPlayer"/> 会写主视觉位移，不适配占位槽）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleInfoPreviewIconPlayer : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer target;
        [SerializeField] private float fps = 8f;

        private Sprite[] _frames = System.Array.Empty<Sprite>();
        private int _frameIndex;
        private float _elapsed;
        private bool _playing;

        public SpriteRenderer Target =>
            target != null ? target : (target = GetComponent<SpriteRenderer>());

        private void Awake()
        {
            if (target == null)
            {
                target = GetComponent<SpriteRenderer>();
            }
        }

        private void Update()
        {
            if (!_playing || _frames == null || _frames.Length <= 1 || Target == null)
            {
                return;
            }

            var step = 1f / Mathf.Max(0.01f, fps);
            _elapsed += Time.unscaledDeltaTime;
            while (_elapsed >= step)
            {
                _elapsed -= step;
                _frameIndex = (_frameIndex + 1) % _frames.Length;
                ApplyFrame();
            }
        }

        public void PlayIdleOrStatic(string defId)
        {
            Stop();
            var renderer = Target;
            if (renderer == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(defId)
                || !CardPresentationConfigCatalog.TryGet(defId, out var config)
                || config == null)
            {
                ClearVisual();
                return;
            }

            var kind = CardPresentationAnimResolve.Resolve(
                config,
                CardAnimSlotIds.Idle,
                out _,
                out var slotDto);

            if (kind == CardPresentationAnimResolve.Kind.Frames && slotDto != null)
            {
                var frames = CardAnimFrameSource.LoadFrames(slotDto.sourceType, slotDto.path);
                if (frames != null && frames.Length > 0)
                {
                    _frames = frames;
                    _frameIndex = 0;
                    _elapsed = 0f;
                    _playing = frames.Length > 1;
                    ApplyFrame();
                    renderer.enabled = true;
                    return;
                }
            }

            Sprite sprite = null;
            if (config.sprites != null && !string.IsNullOrWhiteSpace(config.sprites.mainIcon))
            {
                sprite = CardPresentationSpritePath.LoadSprite(config.sprites.mainIcon);
            }

            if (sprite == null)
            {
                ClearVisual();
                return;
            }

            _frames = System.Array.Empty<Sprite>();
            _playing = false;
            renderer.sprite = sprite;
            renderer.enabled = true;
        }

        public void ClearVisual()
        {
            Stop();
            _frames = System.Array.Empty<Sprite>();
            if (Target != null)
            {
                Target.sprite = null;
                Target.enabled = false;
            }
        }

        public void Stop()
        {
            _playing = false;
            _elapsed = 0f;
            _frameIndex = 0;
        }

        private void ApplyFrame()
        {
            if (Target == null || _frames == null || _frames.Length == 0)
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
                Target.sprite = frame;
            }
        }
    }
}

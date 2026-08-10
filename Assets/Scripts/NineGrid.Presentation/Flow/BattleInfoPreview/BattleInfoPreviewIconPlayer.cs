using NineGrid.Cards.Anim;
using NineGrid.Content.CardPresentation;
using UnityEngine;

namespace NineGrid.Flow.BattleInfoPreview
{
    /// <summary>
    /// 预览槽图标播放：切 Sprite，再按卡面 <c>mainVisual</c> + 分类外部缩放摆放。
    /// Idle 多帧：首帧摆一次，切帧只换 sprite。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleInfoPreviewIconPlayer : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer target;
        [SerializeField] private BattleInfoPreviewSlotView slotView;
        [SerializeField] private float fps = 8f;

        private Sprite[] _frames = System.Array.Empty<Sprite>();
        private int _frameIndex;
        private float _elapsed;
        private bool _playing;
        private CardPresentationMainVisualDto _mainVisual;
        private float _slotOffsetX;
        private float _slotOffsetY;
        private float _externalScale = 1f;

        public SpriteRenderer Target =>
            target != null ? target : (target = ResolveArtRenderer());

        public void BindTarget(SpriteRenderer art, BattleInfoPreviewSlotView slot)
        {
            target = art;
            slotView = slot;
        }

        private void Awake()
        {
            if (target == null)
            {
                target = ResolveArtRenderer();
            }

            if (slotView == null)
            {
                slotView = GetComponent<BattleInfoPreviewSlotView>();
            }
        }

        private SpriteRenderer ResolveArtRenderer()
        {
            var art = transform.Find("__Art");
            if (art != null)
            {
                var sr = art.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    return sr;
                }
            }

            return GetComponentInChildren<SpriteRenderer>(true);
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
                ApplyFrame(refit: false);
            }
        }

        public void PlayIdleOrStatic(string defId, float externalScale = 1f)
        {
            Stop();
            _mainVisual = null;
            _slotOffsetX = 0f;
            _slotOffsetY = 0f;
            _externalScale = externalScale > 0.0001f ? externalScale : 1f;
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

            _mainVisual = config.mainVisual;

            if (config.animations != null && config.animations.defaultFps > 0.01f)
            {
                fps = config.animations.defaultFps;
            }

            var kind = CardPresentationAnimResolve.Resolve(
                config,
                CardAnimSlotIds.Idle,
                out _,
                out var slotDto);

            if (slotDto != null)
            {
                _slotOffsetX = slotDto.offsetX;
                _slotOffsetY = slotDto.offsetY;
            }

            if (kind == CardPresentationAnimResolve.Kind.Frames && slotDto != null)
            {
                var frames = CardAnimFrameSource.LoadFrames(slotDto.sourceType, slotDto.path);
                if (frames != null && frames.Length > 0)
                {
                    _frames = frames;
                    _frameIndex = 0;
                    _elapsed = 0f;
                    _playing = frames.Length > 1;
                    ApplyFrame(refit: true);
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
            ApplyFit();
        }

        public void ClearVisual()
        {
            Stop();
            _frames = System.Array.Empty<Sprite>();
            _mainVisual = null;
            _slotOffsetX = 0f;
            _slotOffsetY = 0f;
            _externalScale = 1f;
            if (Target != null)
            {
                Target.sprite = null;
                Target.enabled = false;
                BattleInfoSlotArtFit.ResetArtTransform(Target);
            }
        }

        public void Stop()
        {
            _playing = false;
            _elapsed = 0f;
            _frameIndex = 0;
        }

        private void ApplyFrame(bool refit)
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

            if (refit)
            {
                ApplyFit();
            }
        }

        private void ApplyFit()
        {
            if (slotView != null)
            {
                slotView.ApplyMainVisual(_mainVisual, _slotOffsetX, _slotOffsetY, _externalScale);
                return;
            }

            BattleInfoSlotArtFit.ApplyMainVisual(
                Target,
                new Vector2(BattleInfoSlotArtFit.FallbackSlotSize, BattleInfoSlotArtFit.FallbackSlotSize),
                _mainVisual,
                _slotOffsetX,
                _slotOffsetY,
                _externalScale);
        }
    }
}

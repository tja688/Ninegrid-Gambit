using NineGrid.Cards.Slots;
using NineGrid.Content.CardPresentation;
using UnityEngine;

namespace NineGrid.Cards.Anim
{
    /// <summary>
    /// 卡面主视图帧动画播放器：只改 sprite，不按帧改 transform；
    /// 切槽时一次写入 mainVisual + slot offset（相对 Mask 脚底锚定 + 偏移）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardSpriteAnimPlayer : MonoBehaviour
    {
        [SerializeField]
        private SpriteRenderer target;

        [SerializeField]
        private float fps = 8f;

        [SerializeField]
        private bool looping = true;

        private CardPresentationConfigDto _config;
        private CardMainVisualMaskAnchor _maskAnchor;
        private Sprite[] _frames = System.Array.Empty<Sprite>();
        private int _frameIndex;
        private float _elapsed;
        private bool _playing;
        private string _activeSlotId = CardAnimSlotIds.Idle;
        private CardPresentationAnimSlotDto _activeSlot;

        public SpriteRenderer Target => target;
        public bool IsPlaying => _playing;
        public string ActiveSlotId => _activeSlotId;

        private void Awake()
        {
            EnsureTarget();
            _maskAnchor = CardMainVisualMaskAnchor.FindOrAdd(transform);
        }

        private void Update()
        {
            EditorTick(Time.deltaTime);
        }

        /// <summary>
        /// EditMode 预览用：EditorApplication.update 传入显式 delta（EditMode 不跑 MonoBehaviour.Update）。
        /// </summary>
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

            var rate = fps > 0.01f ? fps : 8f;
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

        public void BindConfig(CardPresentationConfigDto config)
        {
            _config = config;
            if (_config != null
                && _config.animations != null
                && _config.animations.defaultFps > 0.01f)
            {
                fps = _config.animations.defaultFps;
            }
        }

        public void BindContentId(string contentId)
        {
            if (string.IsNullOrWhiteSpace(contentId))
            {
                _config = null;
                return;
            }

            if (CardPresentationConfigCatalog.TryGet(contentId, out var dto))
            {
                BindConfig(dto);
            }
            else
            {
                _config = null;
            }
        }

        public void PlayIdleOrStatic()
        {
            Play(CardAnimSlotIds.Idle);
        }

        public void Play(string slotId)
        {
            EnsureTarget();
            if (target == null)
            {
                return;
            }

            var kind = CardPresentationAnimResolve.Resolve(
                _config,
                slotId,
                out var resolvedSlotId,
                out var slotDto);

            _activeSlotId = resolvedSlotId;
            _activeSlot = slotDto;

            if (kind == CardPresentationAnimResolve.Kind.StaticMainIcon
                || slotDto == null)
            {
                PlayStaticMainIcon();
                return;
            }

            var frames = CardAnimFrameSource.LoadFrames(slotDto.sourceType, slotDto.path);
            if (frames == null || frames.Length == 0)
            {
                // 有 path 但加载失败 → 再走 idle / static 链。
                if (!string.Equals(resolvedSlotId, CardAnimSlotIds.Idle, System.StringComparison.Ordinal))
                {
                    var idleKind = CardPresentationAnimResolve.Resolve(
                        _config,
                        CardAnimSlotIds.Idle,
                        out resolvedSlotId,
                        out slotDto);
                    _activeSlotId = resolvedSlotId;
                    _activeSlot = slotDto;
                    if (idleKind == CardPresentationAnimResolve.Kind.Frames && slotDto != null)
                    {
                        frames = CardAnimFrameSource.LoadFrames(slotDto.sourceType, slotDto.path);
                    }
                }

                if (frames == null || frames.Length == 0)
                {
                    PlayStaticMainIcon();
                    return;
                }
            }

            _frames = frames;
            _frameIndex = 0;
            _elapsed = 0f;
            _playing = _frames.Length > 1;
            looping = ShouldLoop(resolvedSlotId);
            var referenceSprite = _frames[0];
            ApplySlotTransformOnce(slotDto, referenceSprite);
            ApplyMaskInteractionForContext();
            ApplyCurrentFrame();
            target.enabled = true;
        }

        public void Stop()
        {
            _playing = false;
            _elapsed = 0f;
        }

        private void PlayStaticMainIcon()
        {
            _playing = false;
            _frames = System.Array.Empty<Sprite>();
            _frameIndex = 0;
            _elapsed = 0f;
            _activeSlotId = CardAnimSlotIds.Idle;
            _activeSlot = null;

            Sprite sprite = null;
            if (_config != null
                && _config.sprites != null
                && !string.IsNullOrWhiteSpace(_config.sprites.mainIcon))
            {
                sprite = CardPresentationSpritePath.LoadSprite(_config.sprites.mainIcon);
            }

            if (sprite == null && target != null && target.sprite != null)
            {
                // 保留 Binder 已写入的主图标。
                sprite = target.sprite;
            }

            ApplySlotTransformOnce(null, sprite);
            ApplyMaskInteractionForContext();
            if (target != null)
            {
                target.enabled = sprite != null || target.sprite != null;
            }
        }

        private void ApplyMaskInteractionForContext()
        {
            // PreviewRenderUtility 不跑 URP 2D Mask 模板：VisibleInsideMask → 全透明。
            // 预览实例只定位不裁剪；Play Mode / 局内仍走真实 SpriteMask。
            if (!Application.isPlaying && IsEditorPreviewInstance())
            {
                if (target != null)
                {
                    target.maskInteraction = SpriteMaskInteraction.None;
                }

                return;
            }

            ApplyMaskInteraction();
        }

        private bool IsEditorPreviewInstance()
        {
            if (target == null)
            {
                return false;
            }

            // CardFacePreviewHost 用 PreviewRenderUtility.AddSingleGO，实例 hideFlags 含 DontSave。
            var flags = target.gameObject.hideFlags;
            if ((flags & HideFlags.DontSave) != 0 || (flags & HideFlags.HideAndDontSave) != 0)
            {
                return true;
            }

            var root = transform.root != null ? transform.root.gameObject : gameObject;
            flags = root.hideFlags;
            return (flags & HideFlags.DontSave) != 0 || (flags & HideFlags.HideAndDontSave) != 0;
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

        private void ApplySlotTransformOnce(CardPresentationAnimSlotDto slotDto, Sprite referenceSprite)
        {
            EnsureTarget();
            if (target == null)
            {
                return;
            }

            if (_maskAnchor == null)
            {
                _maskAnchor = CardMainVisualMaskAnchor.FindOrAdd(transform);
            }

            var main = _config != null ? _config.mainVisual : null;
            var scale = main != null ? main.uniformScale : 1f;
            var offsetX = (main != null ? main.offsetX : 0f) + (slotDto != null ? slotDto.offsetX : 0f);
            var offsetY = (main != null ? main.offsetY : 0f) + (slotDto != null ? slotDto.offsetY : 0f);

            CardMainVisualPlacement.ApplyToRenderer(
                target,
                _maskAnchor,
                referenceSprite,
                scale,
                offsetX,
                offsetY);
        }

        private void ApplyMaskInteraction()
        {
            if (target == null)
            {
                return;
            }

            if (_maskAnchor == null)
            {
                _maskAnchor = CardMainVisualMaskAnchor.FindOrAdd(transform);
            }

            if (_maskAnchor != null)
            {
                _maskAnchor.ApplyMaskInteraction(target);
            }
        }

        private void EnsureTarget()
        {
            if (target != null)
            {
                return;
            }

            if (CardFaceSlotNodeMap.TryFindRenderer(
                    transform,
                    CardFaceSlotCodes.MainIcon,
                    out var renderer))
            {
                target = renderer;
            }
        }

        private static bool ShouldLoop(string slotId)
        {
            return string.Equals(slotId, CardAnimSlotIds.Idle, System.StringComparison.Ordinal)
                   || string.Equals(slotId, CardAnimSlotIds.Lunch, System.StringComparison.Ordinal);
        }
    }
}

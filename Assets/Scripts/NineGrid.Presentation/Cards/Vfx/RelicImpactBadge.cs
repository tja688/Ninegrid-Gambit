using UnityEngine;

namespace NineGrid.Cards.Vfx
{
    /// <summary>
    /// 遗物影响场地目标时弹出的单个遗物图标徽章。
    /// 负责轻量弹入、悬停保持、淡出消失，以及随多遗物动态排版平滑滑动。
    /// 纯表现装饰层：不参与 Core 锁步与 Batch-ack（ADR-0001 / ADR-0051）。
    /// </summary>
    public sealed class RelicImpactBadge : MonoBehaviour
    {
        private const float DefaultFadeInDuration = 0.12f;
        private const float DefaultHoldDuration = 0.80f;
        private const float DefaultFadeOutDuration = 0.25f;
        private const float PositionLerpSpeed = 16f;

        private SpriteRenderer _spriteRenderer;
        private string _relicDefId = string.Empty;
        private int _slotIndex;
        private Vector3 _slotBaseWorldPos;
        private Vector3 _currentLocalOffset;
        private Vector3 _targetLocalOffset;

        private float _elapsedTime;
        private float _fadeInDuration = DefaultFadeInDuration;
        private float _holdDuration = DefaultHoldDuration;
        private float _fadeOutDuration = DefaultFadeOutDuration;
        private float _punchTimer;
        private bool _isFinished;

        public string RelicDefId => _relicDefId;
        public int SlotIndex => _slotIndex;
        public bool IsFinished => _isFinished;

        public Vector3 TargetLocalOffset
        {
            get => _targetLocalOffset;
            set => _targetLocalOffset = value;
        }

        private void Awake()
        {
            EnsureSpriteRenderer();
        }

        public void Initialize(
            string relicDefId,
            Sprite sprite,
            Vector3 slotBaseWorldPos,
            int slotIndex,
            Vector3 initialLocalOffset,
            string sortingLayerName,
            int sortingOrder)
        {
            EnsureSpriteRenderer();
            _relicDefId = relicDefId ?? string.Empty;
            _slotIndex = slotIndex;
            _slotBaseWorldPos = slotBaseWorldPos;
            _currentLocalOffset = initialLocalOffset;
            _targetLocalOffset = initialLocalOffset;
            _elapsedTime = 0f;
            _punchTimer = 0f;
            _isFinished = false;

            if (_spriteRenderer != null)
            {
                _spriteRenderer.sprite = sprite;
                _spriteRenderer.sortingLayerName = sortingLayerName;
                _spriteRenderer.sortingOrder = sortingOrder;
                _spriteRenderer.color = new Color(1f, 1f, 1f, 0f);
            }

            transform.position = _slotBaseWorldPos + _currentLocalOffset;
            transform.localScale = new Vector3(0.65f, 0.65f, 1f);
            gameObject.SetActive(true);
        }

        /// <summary>
        /// 同一遗物在展示期内再次命中时刷新停留时间，并附带轻微缩放呼吸。
        /// </summary>
        public void RefreshLifetime()
        {
            // 重设已耗时为淡入结束点，重新保持完整的 hold 时长
            if (_elapsedTime > _fadeInDuration)
            {
                _elapsedTime = _fadeInDuration;
            }

            _punchTimer = 0.15f;
            _isFinished = false;
        }

        public void Tick(float deltaTime)
        {
            if (_isFinished)
            {
                return;
            }

            _elapsedTime += deltaTime;
            var totalDuration = _fadeInDuration + _holdDuration + _fadeOutDuration;

            // 平滑滑动到目标偏移
            _currentLocalOffset = Vector3.Lerp(_currentLocalOffset, _targetLocalOffset, Mathf.Clamp01(deltaTime * PositionLerpSpeed));
            transform.position = _slotBaseWorldPos + _currentLocalOffset;

            float alpha;
            float scale;

            if (_elapsedTime < _fadeInDuration)
            {
                // 淡入阶段：0.65x -> 1.0x，Alpha 0 -> 1
                var t = Mathf.Clamp01(_elapsedTime / Mathf.Max(0.001f, _fadeInDuration));
                var easedT = EaseOutBack(t);
                scale = Mathf.LerpUnclamped(0.65f, 1.0f, easedT);
                alpha = Mathf.Clamp01(t);
            }
            else if (_elapsedTime < _fadeInDuration + _holdDuration)
            {
                // 停留阶段：保持 1.0x，Alpha 1
                alpha = 1f;
                scale = 1f;

                if (_punchTimer > 0f)
                {
                    _punchTimer -= deltaTime;
                    var punchT = 1f - Mathf.Clamp01(_punchTimer / 0.15f);
                    scale += Mathf.Sin(punchT * Mathf.PI) * 0.2f;
                }
            }
            else if (_elapsedTime < totalDuration)
            {
                // 淡出阶段：1.0x -> 0.85x，Alpha 1 -> 0
                var fadeOutElapsed = _elapsedTime - (_fadeInDuration + _holdDuration);
                var t = Mathf.Clamp01(fadeOutElapsed / Mathf.Max(0.001f, _fadeOutDuration));
                scale = Mathf.Lerp(1.0f, 0.85f, t);
                alpha = Mathf.Lerp(1.0f, 0f, t * t);
            }
            else
            {
                alpha = 0f;
                scale = 0.85f;
                _isFinished = true;
            }

            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = new Color(1f, 1f, 1f, alpha);
            }

            transform.localScale = new Vector3(scale, scale, 1f);

            if (_isFinished)
            {
                gameObject.SetActive(false);
            }
        }

        public void HideImmediate()
        {
            _isFinished = true;
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = new Color(1f, 1f, 1f, 0f);
            }

            gameObject.SetActive(false);
        }

        private void EnsureSpriteRenderer()
        {
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
                if (_spriteRenderer == null)
                {
                    _spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                }
            }
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }
    }
}

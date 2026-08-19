using UnityEngine;

namespace NineGrid.Cards.Vfx
{
    /// <summary>
    /// 遗物影响场地目标时弹出的单个遗物图标徽章。
    /// 弹出：由小放大并带轻微缩放过冲；浮现后立刻上浮并快速淡出离场。
    /// 完整生命周期约 0.3 秒。另负责多遗物动态排版时的水平滑动。
    /// 纯表现装饰层：不参与 Core 锁步与 Batch-ack（ADR-0001 / ADR-0051）。
    /// </summary>
    public sealed class RelicImpactBadge : MonoBehaviour
    {
        internal const float PopInDuration = 0.12f;
        internal const float FadeOutDuration = 0.18f;
        internal const float TotalDuration = PopInDuration + FadeOutDuration;
        internal const float RiseDistance = 0.50f;
        internal const float StartScale = 0f;
        internal const float RestScale = 1f;

        /// <summary>弹出阶段内 Alpha 灌满的归一化时间，保证过冲峰值时已完全可见。</summary>
        private const float AlphaFillNormalized = 0.55f;
        private const float PositionLerpSpeed = 16f;

        private SpriteRenderer _spriteRenderer;
        private string _relicDefId = string.Empty;
        private int _slotIndex;
        private Vector3 _slotBaseWorldPos;
        private Vector3 _currentLocalOffset;
        private Vector3 _targetLocalOffset;

        private float _elapsedTime;
        private float _riseY;
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
            ResetPlayback();

            if (_spriteRenderer != null)
            {
                _spriteRenderer.sprite = sprite;
                _spriteRenderer.sortingLayerName = sortingLayerName;
                _spriteRenderer.sortingOrder = sortingOrder;
                _spriteRenderer.color = new Color(1f, 1f, 1f, 0f);
            }

            ApplyPose(StartScale, 0f);
            gameObject.SetActive(true);
        }

        /// <summary>
        /// 同一遗物在展示期内再次命中时从头重播弹出，避免短生命周期内“续命停住”。
        /// </summary>
        public void RefreshLifetime()
        {
            ResetPlayback();
            ApplyPose(StartScale, 0f);
        }

        public void Tick(float deltaTime)
        {
            if (_isFinished)
            {
                return;
            }

            _elapsedTime += deltaTime;

            _currentLocalOffset = Vector3.Lerp(
                _currentLocalOffset,
                _targetLocalOffset,
                Mathf.Clamp01(deltaTime * PositionLerpSpeed));

            float alpha;
            float scale;

            if (_elapsedTime < PopInDuration)
            {
                var t = Mathf.Clamp01(_elapsedTime / PopInDuration);
                scale = Mathf.LerpUnclamped(StartScale, RestScale, EaseOutBack(t));
                alpha = Mathf.Clamp01(t / AlphaFillNormalized);
                _riseY = 0f;
            }
            else if (_elapsedTime < TotalDuration)
            {
                var t = Mathf.Clamp01((_elapsedTime - PopInDuration) / FadeOutDuration);
                scale = RestScale;
                alpha = 1f - EaseInQuad(t);
                _riseY = RiseDistance * EaseOutCubic(t);
            }
            else
            {
                alpha = 0f;
                scale = RestScale;
                _riseY = RiseDistance;
                _isFinished = true;
            }

            ApplyPose(scale, alpha);

            if (_isFinished)
            {
                gameObject.SetActive(false);
            }
        }

        public void HideImmediate()
        {
            _isFinished = true;
            _riseY = 0f;
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = new Color(1f, 1f, 1f, 0f);
            }

            gameObject.SetActive(false);
        }

        private void ResetPlayback()
        {
            _elapsedTime = 0f;
            _riseY = 0f;
            _isFinished = false;
        }

        private void ApplyPose(float scale, float alpha)
        {
            transform.position = _slotBaseWorldPos + _currentLocalOffset + new Vector3(0f, _riseY, 0f);
            transform.localScale = new Vector3(scale, scale, 1f);
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = new Color(1f, 1f, 1f, alpha);
            }
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

        private static float EaseOutCubic(float t)
        {
            var u = 1f - t;
            return 1f - u * u * u;
        }

        private static float EaseInQuad(float t)
        {
            return t * t;
        }
    }
}

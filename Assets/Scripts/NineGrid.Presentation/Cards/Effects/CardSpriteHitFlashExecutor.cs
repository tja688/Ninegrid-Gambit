using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace NineGrid.Cards
{
    public readonly struct CardSpriteHitFlashParams
    {
        public CardSpriteHitFlashParams(
            Material materialTemplate,
            Color flashColor,
            float flashPeakAmount,
            float flashInDuration,
            float flashOutDuration,
            int flashCount)
        {
            MaterialTemplate = materialTemplate;
            FlashColor = flashColor;
            FlashPeakAmount = flashPeakAmount;
            FlashInDuration = flashInDuration;
            FlashOutDuration = flashOutDuration;
            FlashCount = flashCount;
        }

        public Material MaterialTemplate { get; }

        public Color FlashColor { get; }

        public float FlashPeakAmount { get; }

        public float FlashInDuration { get; }

        public float FlashOutDuration { get; }

        public int FlashCount { get; }
    }

    /// <summary>
    /// 卡牌受击闪白运行时执行器：将目标 SpriteRenderer 切换为 HitFlash 材质实例，通过 _HitFlashAmount 驱动闪白。
    /// 由 <see cref="CardSpriteHitFlashEffectSO"/> 驱动，不单独挂载配置。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardSpriteHitFlashExecutor : MonoBehaviour
    {
        private static readonly int HitFlashAmountId = Shader.PropertyToID("_HitFlashAmount");
        private static readonly int HitFlashColorId = Shader.PropertyToID("_HitFlashColor");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private SpriteRenderer[] _targetRenderers;
        private Material[] _flashMaterials;
        private Tween _activeTween;
        private float _flashAmount;
        private Color _flashColor = Color.white;
        private bool _initialized;

        public static CardSpriteHitFlashExecutor GetOrCreate(Transform root)
        {
            if (!root.TryGetComponent<CardSpriteHitFlashExecutor>(out var executor))
            {
                executor = root.gameObject.AddComponent<CardSpriteHitFlashExecutor>();
            }

            return executor;
        }

        public async UniTask PlayAsync(CardSpriteHitFlashParams parameters, CancellationToken cancellationToken)
        {
            EnsureInitialized(parameters);
            _activeTween?.Kill();

            var peak = Mathf.Clamp01(parameters.FlashPeakAmount);
            var loops = Mathf.Max(1, parameters.FlashCount);
            SetFlashAmount(0f);

            var completed = false;
            var sequence = DOTween.Sequence()
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

            for (var i = 0; i < loops; i++)
            {
                sequence
                    .Append(DOTween.To(
                        () => _flashAmount,
                        SetFlashAmount,
                        peak,
                        Mathf.Max(0f, parameters.FlashInDuration)))
                    .Append(DOTween.To(
                        () => _flashAmount,
                        SetFlashAmount,
                        0f,
                        Mathf.Max(0f, parameters.FlashOutDuration)));
            }

            sequence.OnComplete(() => completed = true);
            sequence.OnKill(() => completed = true);
            _activeTween = sequence;

            try
            {
                await UniTask.WaitUntil(() => completed, cancellationToken: cancellationToken);
            }
            catch (System.OperationCanceledException)
            {
                Stop();
                throw;
            }
        }

        public void Stop()
        {
            _activeTween?.Kill();
            _activeTween = null;
            SetFlashAmount(0f);
        }

        private void OnDestroy()
        {
            Stop();
            DestroyFlashMaterials();
        }

        private void EnsureInitialized(CardSpriteHitFlashParams parameters)
        {
            _flashColor = parameters.FlashColor;

            if (_initialized)
            {
                SyncMaterialSpriteData();
                return;
            }

            if (parameters.MaterialTemplate == null)
            {
                return;
            }

            if (_targetRenderers == null || _targetRenderers.Length == 0)
            {
                _targetRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            }

            DestroyFlashMaterials();
            _flashMaterials = new Material[_targetRenderers.Length];

            for (var i = 0; i < _targetRenderers.Length; i++)
            {
                var renderer = _targetRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                var flashMaterial = new Material(parameters.MaterialTemplate)
                {
                    name = $"{renderer.name}_HitFlash"
                };

                SyncRendererToFlashMaterial(renderer, flashMaterial);
                renderer.material = flashMaterial;
                _flashMaterials[i] = flashMaterial;
            }

            ApplyFlashAmount(_flashAmount);
            _initialized = true;
        }

        private void SyncMaterialSpriteData()
        {
            if (_targetRenderers == null || _flashMaterials == null)
            {
                return;
            }

            var count = Mathf.Min(_targetRenderers.Length, _flashMaterials.Length);
            for (var i = 0; i < count; i++)
            {
                var renderer = _targetRenderers[i];
                var flashMaterial = _flashMaterials[i];
                if (renderer == null || flashMaterial == null)
                {
                    continue;
                }

                SyncRendererToFlashMaterial(renderer, flashMaterial);
            }
        }

        private void SyncRendererToFlashMaterial(SpriteRenderer renderer, Material flashMaterial)
        {
            var sprite = renderer.sprite;
            if (sprite != null && sprite.texture != null)
            {
                flashMaterial.SetTexture(MainTexId, sprite.texture);
            }

            flashMaterial.SetColor(ColorId, renderer.color);
            flashMaterial.SetColor(HitFlashColorId, _flashColor);
        }

        private void SetFlashAmount(float amount)
        {
            _flashAmount = Mathf.Clamp01(amount);
            ApplyFlashAmount(_flashAmount);
        }

        private void ApplyFlashAmount(float amount)
        {
            if (_flashMaterials == null)
            {
                return;
            }

            for (var i = 0; i < _flashMaterials.Length; i++)
            {
                var material = _flashMaterials[i];
                if (material != null)
                {
                    material.SetFloat(HitFlashAmountId, amount);
                }
            }
        }

        private void DestroyFlashMaterials()
        {
            if (_flashMaterials == null)
            {
                return;
            }

            for (var i = 0; i < _flashMaterials.Length; i++)
            {
                if (_flashMaterials[i] != null)
                {
                    Destroy(_flashMaterials[i]);
                }
            }

            _flashMaterials = null;
            _initialized = false;
        }
    }
}

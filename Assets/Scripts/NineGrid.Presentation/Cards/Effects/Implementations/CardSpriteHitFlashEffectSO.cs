using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NineGrid.Cards
{
    [CreateAssetMenu(
        fileName = "CardSpriteHitFlashEffect",
        menuName = "NineGrid/Cards/Effects/Sprite Hit Flash")]
    public sealed class CardSpriteHitFlashEffectSO : CardEffectSO
    {
        private const string DefaultHitFlashMaterialPath =
            "Assets/Resources/Arts/VisualProfiles/TableNineSpriteHitFlash.mat";

        [Header("Material")]
        [Tooltip("HitFlash 材质模板（TableNine/SpriteHitFlash）；留空时编辑器下从默认路径加载。")]
        [SerializeField] private Material hitFlashMaterialTemplate;

        [Header("Flash")]
        [Tooltip("闪白叠色。")]
        [SerializeField] private Color flashColor = Color.white;

        [Tooltip("闪白峰值强度（0~1，对应 _HitFlashAmount）。")]
        [SerializeField, Range(0f, 1f)] private float flashPeakAmount = 1f;

        [Tooltip("闪白升起时长（秒）。")]
        [SerializeField] private float flashInDuration = 0.03f;

        [Tooltip("闪白消退时长（秒）。")]
        [SerializeField] private float flashOutDuration = 0.12f;

        [Tooltip("单次受击闪白次数。")]
        [SerializeField, Min(1)] private int flashCount = 3;

        public override async UniTask PlayAsync(CardEffectPlayContext context)
        {
            var root = context.Root;
            if (root == null)
            {
                return;
            }

            var template = ResolveMaterialTemplate();
            if (template == null)
            {
                Debug.LogWarning(
                    $"[CardSpriteHitFlashEffectSO] {DisplayName} 缺少 HitFlash 材质模板，闪白不会生效。");
                return;
            }

            var executor = CardSpriteHitFlashExecutor.GetOrCreate(root);
            var parameters = new CardSpriteHitFlashParams(
                template,
                flashColor,
                flashPeakAmount,
                flashInDuration,
                flashOutDuration,
                flashCount);

            await executor.PlayAsync(parameters, context.CancellationToken);
        }

        public override void Stop(CardEffectPlayContext context)
        {
            if (context.Root == null)
            {
                return;
            }

            if (context.Root.TryGetComponent<CardSpriteHitFlashExecutor>(out var executor))
            {
                executor.Stop();
            }
        }

        private Material ResolveMaterialTemplate()
        {
            if (hitFlashMaterialTemplate != null)
            {
                return hitFlashMaterialTemplate;
            }

            return CardChassisPaths.LoadAsset<Material>(DefaultHitFlashMaterialPath);
        }

        private void OnValidate()
        {
            flashCount = Mathf.Max(1, flashCount);
            var loopDuration = (flashInDuration + flashOutDuration) * flashCount;
            SetEstimatedDuration(Mathf.Max(0f, loopDuration));
        }
    }
}

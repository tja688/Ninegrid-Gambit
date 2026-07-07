using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 卡牌表现反馈效果 SO 基类。每个资产可独立制作与装配。
    /// </summary>
    public abstract class CardEffectSO : ScriptableObject
    {
        [Tooltip("效果唯一标识，留空时使用资产名。")]
        [SerializeField] private string effectId;

        [Tooltip("在 Inspector 与编排工具中显示的名称。")]
        [SerializeField] private string displayName;

        [TextArea(2, 4)]
        [Tooltip("效果说明，供外部编排与调试参考。")]
        [SerializeField] private string description;

        [Tooltip("效果所属种类，应与 CardEffectManager 装配槽一致。")]
        [SerializeField] private CardEffectKind kind;

        [Tooltip("预估播放时长（秒），供编排层等待或叠加序列参考。")]
        [SerializeField] private float estimatedDuration = 0.2f;

        [Tooltip("是否支持按 CardBoardDirection 做方向变体（元数据，供编排参考）。")]
        [SerializeField] private bool supportsDirectionVariants;

        [Tooltip("普通攻击组合编排延迟（秒）；基础攻击与左右受击 SO 适用，由编排层在攻击→受击间 await。")]
        [SerializeField] private float orchestrationDelay;

        [Tooltip("是否参与普通攻击组合的受击同步延迟（基础攻击 + 左右受击）。")]
        [SerializeField] private bool usesBasicAttackComboDelay;

        public string EffectId => string.IsNullOrWhiteSpace(effectId) ? name : effectId;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;

        public string Description => description ?? string.Empty;

        public CardEffectKind Kind => kind;

        public float EstimatedDuration => estimatedDuration;

        public bool SupportsDirectionVariants => supportsDirectionVariants;

        public float OrchestrationDelay => Mathf.Max(0f, orchestrationDelay);

        public bool UsesBasicAttackComboDelay => usesBasicAttackComboDelay;

        public abstract UniTask PlayAsync(CardEffectPlayContext context);

        protected void SetEstimatedDuration(float value)
        {
            estimatedDuration = Mathf.Max(0f, value);
        }

        public virtual void Stop(CardEffectPlayContext context)
        {
            if (context.Root != null)
            {
                CardDeckTween.KillMotion(context.Root);
            }
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(effectId))
            {
                effectId = name;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = name;
            }

            estimatedDuration = Mathf.Max(0f, estimatedDuration);
        }
    }
}

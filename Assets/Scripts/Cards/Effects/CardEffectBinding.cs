using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Cards
{
    [Serializable]
    public sealed class CardDirectionalEffectOverride
    {
        [Tooltip("命中此八向时使用 overrideEffect，否则回退到 defaultEffect。")]
        public CardBoardDirection direction = CardBoardDirection.None;

        [Tooltip("该方向对应的表演 SO。")]
        public CardEffectSO overrideEffect;
    }

    [Serializable]
    public sealed class CardEffectBinding
    {
        [Tooltip("本绑定槽对应的效果种类。")]
        public CardEffectKind kind = CardEffectKind.Attack;

        [Tooltip("默认表演 SO；无方向命中或方向为 None 时使用。")]
        public CardEffectSO defaultEffect;

        [Tooltip("按八向覆盖的 SO 列表。")]
        public List<CardDirectionalEffectOverride> directionalOverrides = new();

        public CardEffectSO Resolve(CardBoardDirection selfDirection)
        {
            if (directionalOverrides != null && selfDirection != CardBoardDirection.None)
            {
                for (var i = 0; i < directionalOverrides.Count; i++)
                {
                    var entry = directionalOverrides[i];
                    if (entry != null
                        && entry.direction == selfDirection
                        && entry.overrideEffect != null)
                    {
                        return entry.overrideEffect;
                    }
                }
            }

            return defaultEffect;
        }
    }
}

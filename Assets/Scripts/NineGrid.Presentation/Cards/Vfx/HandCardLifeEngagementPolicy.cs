using UnityEngine;

namespace NineGrid.Cards.Vfx
{
    /// <summary>
    /// 手牌 L1 飘动让位判定（纯函数，可 EditMode 回归）。
    /// 占用权威：手牌管理器「活 hover」——不是 <see cref="CardVisualTarget"/> 枚举。
    /// </summary>
    internal enum HandCardLifeEngagementOutcome
    {
        /// <summary>L1 让位归零（真搬动 / 活 hover 弹出）。</summary>
        Yield = 0,

        /// <summary>允许 L1 飘动。</summary>
        Float = 1,

        /// <summary>搬动中断残留：L0 须贴回槽位布局后再恢复飘动。</summary>
        SnapAndFloat = 2,
    }

    internal struct HandCardLifeEngagementState
    {
        public Vector3 LastWorldPos;
        public float StillSeconds;
    }

    internal static class HandCardLifeEngagementPolicy
    {
        /// <summary>L0 距手牌锚点超过此值视为离锚（hover 上浮 / ripple 全程远超此值）。</summary>
        public const float AnchorEngagedEpsilon = 0.05f;

        public const float AnchorEngagedEpsilonSq = AnchorEngagedEpsilon * AnchorEngagedEpsilon;

        /// <summary>L0 帧间位移低于此值视为搬动已停。</summary>
        public const float StillEpsilon = 0.0002f;

        public const float StillEpsilonSq = StillEpsilon * StillEpsilon;

        /// <summary>离锚且静止超过此秒数判定为搬动中断残留。</summary>
        public const float StuckGraceSeconds = 0.25f;

        public static HandCardLifeEngagementOutcome Evaluate(
            CardDisplayMode displayMode,
            bool hasAnchor,
            Vector3 currentPos,
            Vector3 anchorPos,
            bool isLiveHandHover,
            ref HandCardLifeEngagementState state,
            float dt)
        {
            if (displayMode != CardDisplayMode.HandCardMode)
            {
                return HandCardLifeEngagementOutcome.Yield;
            }

            if (!hasAnchor)
            {
                state.LastWorldPos = currentPos;
                state.StillSeconds = 0f;
                return HandCardLifeEngagementOutcome.Float;
            }

            var delta = currentPos - anchorPos;
            delta.z = 0f;
            if (delta.sqrMagnitude <= AnchorEngagedEpsilonSq)
            {
                state.LastWorldPos = currentPos;
                state.StillSeconds = 0f;
                return HandCardLifeEngagementOutcome.Float;
            }

            if (isLiveHandHover)
            {
                state.LastWorldPos = currentPos;
                state.StillSeconds = 0f;
                return HandCardLifeEngagementOutcome.Yield;
            }

            if ((currentPos - state.LastWorldPos).sqrMagnitude > StillEpsilonSq)
            {
                state.LastWorldPos = currentPos;
                state.StillSeconds = 0f;
                return HandCardLifeEngagementOutcome.Yield;
            }

            state.StillSeconds += dt;
            if (state.StillSeconds < StuckGraceSeconds)
            {
                return HandCardLifeEngagementOutcome.Yield;
            }

            state.StillSeconds = StuckGraceSeconds;
            state.LastWorldPos = currentPos;
            return HandCardLifeEngagementOutcome.SnapAndFloat;
        }
    }
}

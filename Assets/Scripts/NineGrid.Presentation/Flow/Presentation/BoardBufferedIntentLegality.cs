using System;
using QFramework;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 经 <see cref="BoardIntentLegality"/> 重校 Explore / Attack / UseItem；未知 kind 放行。
    /// </summary>
    public sealed class BoardBufferedIntentLegality : IBufferedIntentLegality
    {
        private readonly IArchitecture mArch;

        public BoardBufferedIntentLegality(IArchitecture architecture)
        {
            if (architecture == null)
            {
                throw new ArgumentNullException("architecture");
            }

            mArch = architecture;
        }

        public bool IsStillLegal(InputIntent intent)
        {
            string reason;
            if (string.Equals(intent.Kind, InputIntentKinds.Explore, StringComparison.Ordinal))
            {
                return BoardIntentLegality.TryExplainExplore(mArch, intent.TargetId, out reason);
            }

            if (string.Equals(intent.Kind, InputIntentKinds.Attack, StringComparison.Ordinal))
            {
                return BoardIntentLegality.TryExplainAttack(mArch, intent.TargetId, out reason);
            }

            if (string.Equals(intent.Kind, InputIntentKinds.UseItem, StringComparison.Ordinal))
            {
                return BoardIntentLegality.TryExplainUseItem(
                    mArch,
                    intent.TargetId,
                    intent.SelectedCardUids,
                    intent.SelectedOption,
                    out reason);
            }

            if (string.Equals(intent.Kind, InputIntentKinds.Pickup, StringComparison.Ordinal))
            {
                return BoardIntentLegality.TryExplainPickup(mArch, intent.TargetId, out reason);
            }

            if (string.Equals(intent.Kind, InputIntentKinds.RevealFace, StringComparison.Ordinal))
            {
                return BoardIntentLegality.TryExplainRevealFace(mArch, intent.TargetId, out reason);
            }

            return true;
        }
    }
}
using System;
using NineGrid.Core.Stats;

namespace NineGrid.Core
{
    /// <summary>
    /// Avatar HP≤0 时挂 <see cref="DefeatIfAvatarDeadAction"/> follow-up（ADR-0039）。
    /// </summary>
    internal static class AvatarDefeatFollowUp
    {
        public static GameActionResult AppendIfAvatarHpZero(CardInstance card, GameActionResult result)
        {
            if (card == null || card.Kind != CardKind.Avatar)
            {
                return result;
            }

            if ((int)Math.Round(card.Stats.GetBase(StatId.Hp)) > 0)
            {
                return result;
            }

            return result.AddFollowUp(new DefeatIfAvatarDeadAction());
        }

        public static bool IsAvatarHpZero(GameActionContext context)
        {
            var board = context.GetModel<BoardModel>();
            var avatarUid = board.AvatarUid.Value;
            if (avatarUid <= 0)
            {
                return true;
            }

            CardInstance avatar;
            if (!context.GetModel<CardRegistry>().TryGet(avatarUid, out avatar))
            {
                return true;
            }

            return (int)Math.Round(avatar.Stats.GetBase(StatId.Hp)) <= 0;
        }
    }
}

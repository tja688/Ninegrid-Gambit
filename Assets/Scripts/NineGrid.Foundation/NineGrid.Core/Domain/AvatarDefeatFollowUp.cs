using System;
using NineGrid.Core.Stats;

namespace NineGrid.Core
{
    /// <summary>
    /// Avatar HP≤0 时挂 <see cref="DefeatIfAvatarDeadAction"/> follow-up（ADR-0039）。
    /// 同时充当「Avatar 判死」的唯一谓词：合法指令裁决（PhaseSystem）与战败收束
    /// （DefeatIfAvatarDeadAction）必须共用同一判定，否则会出现「合法指令进僵尸集、
    /// 战败又永不触发」的永久软锁战场。
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

        /// <summary>
        /// ADR-0039：判死只读一手 HP（uid 缺失 / 未注册作防御性判死）。
        /// 不看 Zone——zone 被异常置死（Removed/Graveyard）但 HP&gt;0 属非法状态，
        /// 由 StartNode 归位自愈（PhaseSystem），不得据此锁死合法指令。
        /// </summary>
        public static bool IsAvatarDefeated(BoardModel board, CardRegistry registry)
        {
            if (board == null || registry == null)
            {
                return true;
            }

            var avatarUid = board.AvatarUid.Value;
            if (avatarUid <= 0)
            {
                return true;
            }

            CardInstance avatar;
            if (!registry.TryGet(avatarUid, out avatar) || avatar == null)
            {
                return true;
            }

            return (int)Math.Round(avatar.Stats.GetBase(StatId.Hp)) <= 0;
        }

        public static bool IsAvatarHpZero(GameActionContext context)
        {
            return IsAvatarDefeated(
                context.GetModel<BoardModel>(),
                context.GetModel<CardRegistry>());
        }
    }
}

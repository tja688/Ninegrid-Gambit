using NineGrid.Cards;
using NineGrid.Cards.Presentation;
using NineGrid.Core;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Flow;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// Avatar 血/甲 HUD：在 Impact（及 MaxHp 的 Settled）用指令绝对值刷新，不直读内核。
    /// 对 UpdateHp/UpdateArmor/ModifyBaseStat 只旁路写 HUD 并返回 false，留给卡面处理器认领。
    /// 护甲写入仅限「基础护甲」（ModifyBaseStat Armor）；战斗中当前护甲变动只走卡面，不刷基础护甲 HUD。
    /// </summary>
    public sealed class PlayerInfoHudBeatHandler : IBattleBeatHandler
    {
        public bool TryApply(PresentationInstruction instruction)
        {
            if (instruction == null || instruction.Event == null)
            {
                return false;
            }

            if (!TryResolveAvatar(instruction.Event, out _))
            {
                return false;
            }

            var hud = PlayerInfoHudPresenter.TryGetInstance();
            if (hud == null)
            {
                return false;
            }

            switch (instruction.Kind)
            {
                case PresentationInstructionKind.UpdateHp:
                    hud.ApplyHp(instruction.Event.RemainingHp, animate: true);
                    return false;
                case PresentationInstructionKind.UpdateArmor:
                    // 战斗中当前护甲（真实护甲）只走卡面，基础护甲 HUD 不动。
                    return false;
                case PresentationInstructionKind.ModifyBaseStat:
                    ApplyBaseStat(hud, instruction.Event);
                    return false;
                default:
                    return false;
            }
        }

        private static void ApplyBaseStat(PlayerInfoHudPresenter hud, CoreGameEvent gameEvent)
        {
            var stat = (StatId)gameEvent.Amount;
            var value = UnityEngine.Mathf.Max(0, gameEvent.ResultValue);
            switch (stat)
            {
                case StatId.Hp:
                    hud.ApplyHp(value, animate: true);
                    break;
                case StatId.MaxHp:
                    // ResultValue=新上限；RemainingHp=耦合后的当前血（Core ModifyBaseStat 约定）。
                    hud.ApplyHpAndMaxHp(gameEvent.RemainingHp, value, animate: true);
                    break;
                case StatId.Armor:
                    // 基础护甲被遗物 / 效果永久修改：刷新基础护甲 HUD（战斗当前护甲改动不在此列）。
                    // HUD 显示有效护甲（ADR-0028）：ResultValue 只是基础甲新值，遗物/图腾
                    // Modifier 加成需另行计入，否则带遗物时刷新后数值落后于真实有效甲。
                    hud.ApplyArmor(ResolveEffectiveArmor(gameEvent), animate: true);
                    break;
            }
        }

        private static int ResolveEffectiveArmor(CoreGameEvent gameEvent)
        {
            var arch = NineGridArchitecture.Current;
            if (arch == null)
            {
                return UnityEngine.Mathf.Max(0, gameEvent.ResultValue);
            }

            var board = arch.GetModel<BoardModel>();
            var avatarUid = board != null && board.AvatarUid != null ? board.AvatarUid.Value : 0;
            CardInstance avatar;
            if (avatarUid <= 0 || !arch.GetModel<CardRegistry>().TryGet(avatarUid, out avatar))
            {
                return UnityEngine.Mathf.Max(0, gameEvent.ResultValue);
            }

            return StatArmorUtility.GetEffectiveArmor(arch.GetSystem<IStatSystem>(), avatar);
        }

        private static bool TryResolveAvatar(CoreGameEvent gameEvent, out ManagedCard card)
        {
            var uid = gameEvent.CardUid > 0 ? gameEvent.CardUid : gameEvent.TargetUid;
            card = null;
            if (uid <= 0)
            {
                return false;
            }

            if (CardEntityLifecycleHook.TryGetCard(uid, out card)
                && card != null
                && card.CoreKind == CardPresentationKind.Avatar)
            {
                return true;
            }

            card = null;
            var arch = NineGridArchitecture.Current;
            if (arch == null)
            {
                return false;
            }

            var avatarUid = arch.GetModel<BoardModel>().AvatarUid != null
                ? arch.GetModel<BoardModel>().AvatarUid.Value
                : 0;
            return avatarUid > 0 && uid == avatarUid;
        }
    }
}

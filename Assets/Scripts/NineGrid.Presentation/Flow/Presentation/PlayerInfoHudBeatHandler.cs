using NineGrid.Cards;
using NineGrid.Cards.Presentation;
using NineGrid.Core;
using NineGrid.Flow;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// Avatar 血/甲 HUD：在 Impact（及 MaxHp 的 Settled）用指令绝对值刷新，不直读内核。
    /// 对 UpdateHp/UpdateArmor/ModifyBaseStat 只旁路写 HUD 并返回 false，留给卡面处理器认领。
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
                    hud.ApplyArmor(instruction.Event.RemainingArmor, animate: true);
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
                    hud.ApplyMaxHp(value, animate: true);
                    break;
                case StatId.Armor:
                case StatId.CurrentArmor:
                    hud.ApplyArmor(value, animate: true);
                    break;
            }
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

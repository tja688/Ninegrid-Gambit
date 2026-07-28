using NineGrid.Cards;
using NineGrid.Cards.Presentation;
using NineGrid.Core;
using UnityEngine;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 卡面数值处理器：只从结算指令绝对值赋值，不回头读 CardRegistry / IStatSystem。
    /// </summary>
    public sealed class CardFaceStatHandler : IBattleBeatHandler
    {
        public bool TryApply(PresentationInstruction instruction)
        {
            if (instruction == null || instruction.Event == null)
            {
                return false;
            }

            switch (instruction.Kind)
            {
                case PresentationInstructionKind.UpdateHp:
                    ApplyHp(instruction.Event);
                    return true;
                case PresentationInstructionKind.UpdateArmor:
                    ApplyArmor(instruction.Event);
                    return true;
                case PresentationInstructionKind.ModifyBaseStat:
                    ApplyBaseStat(instruction.Event);
                    return true;
                case PresentationInstructionKind.KillCard:
                    ApplyKill(instruction.Event);
                    return true;
                case PresentationInstructionKind.SpawnCard:
                case PresentationInstructionKind.DealCard:
                case PresentationInstructionKind.ShowAvatar:
                    ApplySpawnFace(instruction.Event);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>测试与开局引导用：等价于 <see cref="TryApply"/> 且忽略返回值。</summary>
        public void Apply(PresentationInstruction instruction)
        {
            TryApply(instruction);
        }

        private static void ApplyHp(CoreGameEvent gameEvent)
        {
            if (!TryResolveCard(gameEvent, out var card))
            {
                return;
            }

            CommitNumeric(card, attack: null, armor: null, hp: Mathf.Max(0, gameEvent.RemainingHp));
        }

        private static void ApplyArmor(CoreGameEvent gameEvent)
        {
            if (!TryResolveCard(gameEvent, out var card))
            {
                return;
            }

            CommitNumeric(card, attack: null, armor: Mathf.Max(0, gameEvent.RemainingArmor), hp: null);
        }

        private static void ApplyBaseStat(CoreGameEvent gameEvent)
        {
            if (!TryResolveCard(gameEvent, out var card))
            {
                return;
            }

            var stat = (StatId)gameEvent.Amount;
            var value = Mathf.Max(0, gameEvent.ResultValue);
            switch (stat)
            {
                case StatId.Attack:
                    CommitNumeric(card, attack: value, armor: null, hp: null);
                    break;
                case StatId.Armor:
                case StatId.CurrentArmor:
                    CommitNumeric(card, attack: null, armor: value, hp: null);
                    break;
                case StatId.Hp:
                case StatId.MaxHp:
                    CommitNumeric(card, attack: null, armor: null, hp: value);
                    break;
            }
        }

        private static void ApplyKill(CoreGameEvent gameEvent)
        {
            if (!TryResolveCard(gameEvent, out var card))
            {
                return;
            }

            // CardKilled：可见血量取指令携带的剩余血量（通常为 0）；禁止本地硬编码置零旁路。
            CommitNumeric(card, attack: null, armor: null, hp: Mathf.Max(0, gameEvent.RemainingHp));
        }

        private static void ApplySpawnFace(CoreGameEvent gameEvent)
        {
            if (!TryResolveCard(gameEvent, out var card))
            {
                return;
            }

            // 生成/发牌/亮相：攻=ResultValue，血/甲=Remaining*，与后续增量同一 Commit 出口。
            CommitNumeric(
                card,
                attack: Mathf.Max(0, gameEvent.ResultValue),
                armor: Mathf.Max(0, gameEvent.RemainingArmor),
                hp: Mathf.Max(0, gameEvent.RemainingHp));
        }

        private static bool TryResolveCard(CoreGameEvent gameEvent, out ManagedCard card)
        {
            var uid = gameEvent.CardUid > 0
                ? gameEvent.CardUid
                : gameEvent.TargetUid;
            if (uid <= 0)
            {
                card = null;
                return false;
            }

            return CardEntityLifecycleHook.TryGetCard(uid, out card) && card != null;
        }

        private static void CommitNumeric(ManagedCard card, int? attack, int? armor, int? hp)
        {
            if (card.View == null)
            {
                return;
            }

            var previous = card.CommittedPresentation;
            var snapshot = previous != null
                ? CloneSnapshot(previous)
                : new CardPresentationSnapshot
                {
                    Kind = card.CoreKind,
                    DefId = card.DefId ?? string.Empty,
                    FaceUp = true,
                };

            if (attack.HasValue)
            {
                snapshot.Attack = attack.Value;
            }

            if (armor.HasValue)
            {
                snapshot.Armor = armor.Value;
            }

            if (hp.HasValue)
            {
                snapshot.Hp = hp.Value;
            }

            card.CommitPresentation(snapshot);
        }

        private static CardPresentationSnapshot CloneSnapshot(CardPresentationSnapshot source)
        {
            return new CardPresentationSnapshot
            {
                Kind = source.Kind,
                DefId = source.DefId ?? string.Empty,
                DisplayName = source.DisplayName ?? string.Empty,
                MainIcon = source.MainIcon,
                FaceBackground = source.FaceBackground,
                BackBorder = source.BackBorder,
                BackShirt = source.BackShirt,
                BackLogo = source.BackLogo,
                CardFrame = source.CardFrame,
                Banner = source.Banner,
                Attack = source.Attack,
                Armor = source.Armor,
                Hp = source.Hp,
                ActionCount = source.ActionCount,
                FaceUp = source.FaceUp,
                BasicDescription = source.BasicDescription ?? string.Empty,
                DetailDescription = source.DetailDescription ?? string.Empty,
            };
        }
    }
}

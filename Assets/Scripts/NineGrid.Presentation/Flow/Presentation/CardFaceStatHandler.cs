using NineGrid.Cards;
using NineGrid.Cards.Presentation;
using NineGrid.Core;
using UnityEngine;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 卡面数值处理器：只从结算指令绝对值赋值，不回头读 CardRegistry / IStatSystem。
    /// </summary>
    public sealed class CardFaceStatHandler
    {
        public void Apply(PresentationInstruction instruction)
        {
            if (instruction == null || instruction.Event == null)
            {
                return;
            }

            switch (instruction.Kind)
            {
                case PresentationInstructionKind.UpdateHp:
                    ApplyHp(instruction.Event);
                    break;
                case PresentationInstructionKind.UpdateArmor:
                    ApplyArmor(instruction.Event);
                    break;
                case PresentationInstructionKind.ModifyBaseStat:
                    ApplyBaseStat(instruction.Event);
                    break;
                case PresentationInstructionKind.KillCard:
                    ApplyKill(instruction.Event);
                    break;
                case PresentationInstructionKind.SpawnCard:
                    // 首次数值由 Spawn 路径首次 Commit / 后续指令覆盖；本版不回头读 Core。
                    break;
            }
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

            // CardKilled 语义：可见血量归零（绝对值由命中帧 HpChanged 通常已写入；此处兜底）。
            CommitNumeric(card, attack: null, armor: null, hp: 0);
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

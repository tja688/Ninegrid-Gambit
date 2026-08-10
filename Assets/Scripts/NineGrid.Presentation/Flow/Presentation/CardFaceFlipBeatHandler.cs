using System.Collections.Generic;
using NineGrid.Cards;
using NineGrid.Cards.Presentation;
using NineGrid.Core;
using UnityEngine;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 牌面朝向处理器：Settled 消费 UpdateFaceUp → Commit FaceUp → 经 FlipPlaybackCoordinator 串行播翻或 Snap。
    /// </summary>
    public sealed class CardFaceFlipBeatHandler : IBattleBeatHandler
    {
        public bool TryApply(PresentationInstruction instruction)
        {
            if (instruction == null || instruction.Kind != PresentationInstructionKind.UpdateFaceUp)
            {
                return false;
            }

            var gameEvent = instruction.Event;
            if (gameEvent == null || gameEvent.CardUid <= 0)
            {
                return true;
            }

            var cards = CardEntityLifecycleHook.CardsOrNull();
            if (cards == null || !cards.TryGet(gameEvent.CardUid, out var card) || card == null)
            {
                return true;
            }

            var targetFaceUp = gameEvent.ResultValue != 0;
            ApplyFaceUp(card, targetFaceUp);
            return true;
        }

        private static void ApplyFaceUp(ManagedCard card, bool targetFaceUp)
        {
            var previous = card.CommittedPresentation;
            CardPresentationSnapshot snapshot;
            if (previous != null)
            {
                snapshot = CloneSnapshot(previous);
                snapshot.FaceUp = targetFaceUp;
            }
            else
            {
                snapshot = new CardPresentationSnapshot
                {
                    Kind = card.CoreKind,
                    DefId = card.DefId ?? string.Empty,
                    FaceUp = targetFaceUp,
                };
            }

            var presenter = card.GameObject != null
                ? card.GameObject.GetComponent<CardFaceFlipPresenter>()
                : null;
            var fromVisual = presenter != null
                ? presenter.VisualFaceUp
                : previous == null || previous.FaceUp;

            if (presenter != null
                && Application.isPlaying
                && fromVisual != targetFaceUp)
            {
                // 先写入已提交镜像但不立刻切面，由翻牌动画驱动视觉。
                card.StoreCommittedPresentation(snapshot);
                if (card.View != null)
                {
                    card.View.RecordCommittedFaceUp(targetFaceUp);
                }

                FlipPlaybackCoordinator.Enqueue(presenter, targetFaceUp);
                return;
            }

            card.CommitPresentation(snapshot);
            presenter?.SnapVisualFace(targetFaceUp);
        }

        private static CardPresentationSnapshot CloneSnapshot(CardPresentationSnapshot source)
        {
            return new CardPresentationSnapshot
            {
                Kind = source.Kind,
                DefId = source.DefId,
                DisplayName = source.DisplayName,
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
                AttackPattern = source.AttackPattern,
                HasSyncRhythmSkills = source.HasSyncRhythmSkills,
                HasActiveRhythm = source.HasActiveRhythm,
                ShowActionCount = source.ShowActionCount,
                FaceUp = source.FaceUp,
                BasicDescription = source.BasicDescription,
                DetailDescription = source.DetailDescription,
                FaceIntro = source.FaceIntro,
                FrameColor = source.FrameColor,
                CommittedCountdownRemaining = CopyCommittedRemaining(source.CommittedCountdownRemaining),
            };
        }

        private static Dictionary<string, string> CopyCommittedRemaining(
            IReadOnlyDictionary<string, string> source)
        {
            var copy = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            if (source != null)
            {
                foreach (var pair in source)
                {
                    copy[pair.Key] = pair.Value;
                }
            }

            return copy;
        }
    }
}

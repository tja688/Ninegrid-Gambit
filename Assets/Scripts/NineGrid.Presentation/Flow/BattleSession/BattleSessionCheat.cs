using System.Collections.Generic;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// Dev/QuickTest 作弊：不进入 <see cref="IBattleSessionSystem"/> 生产 interface。
    /// </summary>
    public static class BattleSessionCheat
    {
        public static bool TrySetAvatarHp(int hp)
        {
            if (hp <= 0)
            {
                return false;
            }

            var arch = NineGridArchitecture.Current;
            if (arch == null)
            {
                return false;
            }

            var board = arch.GetModel<BoardModel>();
            var avatarUid = board.AvatarUid.Value;
            if (avatarUid <= 0
                || !arch.GetModel<CardRegistry>().TryGet(avatarUid, out var avatar))
            {
                Debug.LogWarning("[BattleSessionCheat] Avatar 不存在，无法改血。");
                return false;
            }

            avatar.Stats.SetBase(StatId.MaxHp, hp);
            avatar.Stats.SetBase(StatId.Hp, hp);

            var cards = CardEntityLifecycleHook.CardsOrNull();
            if (cards != null && cards.TryGet(avatarUid, out var view) && view != null)
            {
                CoreCardPresentationMapper.ApplyToManagedCard(view);
            }

            PresentationOutputProjector.UpdateAvatarDebugText();
            Debug.Log($"[BattleSessionCheat] Avatar#{avatarUid} MaxHp/Hp → {hp}");
            return true;
        }

        public static bool TrySetAvatarAttack(int attack)
        {
            if (attack < 0)
            {
                return false;
            }

            var arch = NineGridArchitecture.Current;
            if (arch == null)
            {
                return false;
            }

            var board = arch.GetModel<BoardModel>();
            var avatarUid = board.AvatarUid.Value;
            if (avatarUid <= 0
                || !arch.GetModel<CardRegistry>().TryGet(avatarUid, out var avatar))
            {
                Debug.LogWarning("[BattleSessionCheat] Avatar 不存在，无法改攻。");
                return false;
            }

            avatar.Stats.SetBase(StatId.Attack, attack);

            var cards = CardEntityLifecycleHook.CardsOrNull();
            if (cards != null && cards.TryGet(avatarUid, out var view) && view != null)
            {
                CoreCardPresentationMapper.ApplyToManagedCard(view);
            }

            PresentationOutputProjector.UpdateAvatarDebugText();
            PlayerInfoHudPresenter.TryGetInstance()?.SyncFromCore(animate: false);
            Debug.Log($"[BattleSessionCheat] Avatar#{avatarUid} Attack → {attack}");
            return true;
        }

        public static bool TryForceNodeVictory()
        {
            var session = BattleSessionSystem.EnsureRegistered();
            var arch = NineGridArchitecture.Current;
            var phase = arch.GetSystem<IPhaseSystem>().CurrentPhase;
            if (phase == GamePhase.RewardItemChoice)
            {
                session.TryEnterNodeSettlement();
                return true;
            }

            if (phase != GamePhase.InteractionLoop)
            {
                Debug.LogWarning($"[BattleSessionCheat] 强制胜利失败：phase={phase}（需 InteractionLoop）。");
                return false;
            }

            MakeNodeCleared(arch);
            NineGridArchitecture.Interface?
                .GetSystem<IFieldBattlePresentationSystem>()?
                .CancelBattleWork();
            PresentationInputGates.ForceEndExternalHold("CheatForceNodeVictory");
            session.CancelPresentationWork();

            var pipeline = arch.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new ChangePhaseAction(GamePhase.ClearCheck));
            pipeline.Enqueue(new ChangePhaseAction(GamePhase.NodeCompleted));
            pipeline.Enqueue(new NodeCompletedAction());
            pipeline.RunToCompletion();
            arch.GetSystem<IEconomySystem>().SettleUnusedHelpCards();
            pipeline.Enqueue(new ChangePhaseAction(GamePhase.RewardItemChoice));
            pipeline.Enqueue(new OfferRewardChoiceAction("help.choice", 3));
            pipeline.RunToCompletion();

            Debug.Log("[BattleSessionCheat] 强制节点胜利 → RewardItemChoice");
            session.TryEnterNodeSettlement();
            return true;
        }

        private static void MakeNodeCleared(IArchitecture arch)
        {
            var registry = arch.GetModel<CardRegistry>();
            var board = arch.GetModel<BoardModel>();
            var deck = arch.GetModel<DeckModel>();
            var field = GroundFieldGeometryHook.FieldOrNull();

            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                var uid = board.GetCardUid(slot);
                if (uid <= 0
                    || !registry.TryGet(uid, out var card)
                    || card.Kind != CardKind.Monster)
                {
                    continue;
                }

                board.ClearSlot(slot);
                card.Zone.Value = ZoneId.None;
                card.Slot.Value = SlotId.None;
                field?.RequestRemoveFromField(uid, animate: false, skipBusyGuard: true);
            }

            var drawUids = new List<int>(deck.DrawPileUids);
            for (var i = 0; i < drawUids.Count; i++)
            {
                if (registry.TryGet(drawUids[i], out var card))
                {
                    deck.RemoveCard(card);
                }
                else
                {
                    deck.RemoveUid(drawUids[i]);
                }
            }

            var enemyPoolUids = new List<int>(deck.EnemyCardPoolUids);
            for (var i = 0; i < enemyPoolUids.Count; i++)
            {
                if (registry.TryGet(enemyPoolUids[i], out var card))
                {
                    deck.RemoveCard(card);
                }
                else
                {
                    deck.RemoveUid(enemyPoolUids[i]);
                }
            }
        }
    }
}

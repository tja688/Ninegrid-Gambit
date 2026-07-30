using System.Collections.Generic;
using NineGrid.Cards;
using NineGrid.Cards.Presentation;
using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Content;
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

        /// <summary>
        /// QuickTest：把技能挂到场上已有怪物，并用技能 DesignText 覆写卡面描述。
        /// </summary>
        public static int TryAttachSkillsToBoardMonsters(IReadOnlyList<string> skillIds)
        {
            if (skillIds == null || skillIds.Count == 0)
            {
                return 0;
            }

            var arch = NineGridArchitecture.Current;
            if (arch == null)
            {
                return 0;
            }

            var content = arch.GetSystem<IContentSystem>();
            if (content == null || !content.HasCatalog)
            {
                Debug.LogWarning("[BattleSessionCheat] ContentSystem 未就绪，无法挂技能。");
                return 0;
            }

            var description = BuildSkillDescription(content.Catalog, skillIds);
            var registry = arch.GetModel<CardRegistry>();
            var board = arch.GetModel<BoardModel>();
            var cards = CardEntityLifecycleHook.CardsOrNull();
            var mountedHosts = 0;
            var activatedInstances = 0;

            foreach (var uid in board.BoardCardUids())
            {
                if (!registry.TryGet(uid, out var card) || card.Kind != CardKind.Monster)
                {
                    continue;
                }

                var instances = content.ActivateSkillsOnCard(card, skillIds);
                var count = instances != null ? instances.Count : 0;
                activatedInstances += count;
                mountedHosts++;
                if (count <= 0)
                {
                    Debug.LogWarning(
                        $"[BattleSessionCheat] QuickTest 挂技能失败：uid={uid} defId={card.DefId}"
                        + $" skillIds={skillIds.Count}（卡面描述仍可能写入，但无 Effect 实例）");
                }

                if (cards != null && cards.TryGet(uid, out var view) && view != null)
                {
                    ApplySkillDescription(view, description);
                }
            }

            Debug.Log(
                $"[BattleSessionCheat] QuickTest 挂技能 hosts={mountedHosts} activatedInstances={activatedInstances}"
                + $" skills={skillIds.Count}"
                + (string.IsNullOrEmpty(description) ? string.Empty : " desc=" + description));
            if (mountedHosts > 0 && activatedInstances <= 0)
            {
                Debug.LogError(
                    "[BattleSessionCheat] QuickTest 场上有宿主但激活实例为 0——技能只会显示描述、不会在移除时生效");
            }

            return mountedHosts;
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

        private static string BuildSkillDescription(GameContentCatalog catalog, IReadOnlyList<string> skillIds)
        {
            if (catalog == null || skillIds == null || skillIds.Count == 0)
            {
                return string.Empty;
            }

            var briefs = new List<string>(skillIds.Count);
            for (var i = 0; i < skillIds.Count; i++)
            {
                if (!catalog.TryGetSkill(skillIds[i], out var skill)
                    || string.IsNullOrWhiteSpace(skill.DesignText))
                {
                    continue;
                }

                briefs.Add(skill.DesignText.Trim());
            }

            return EffectDesignTextParameterizer.JoinBriefs(briefs);
        }

        private static void ApplySkillDescription(ManagedCard card, string description)
        {
            if (card == null || string.IsNullOrWhiteSpace(description))
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
                };

            snapshot.BasicDescription = description;
            snapshot.DetailDescription = CardDetailDescriptionComposer.Compose(
                description,
                CardFacePresentationBinder.PeekDescriptionIconCatalog());
            card.CommitPresentation(snapshot);
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
                FaceUp = source.FaceUp,
                BasicDescription = source.BasicDescription,
                DetailDescription = source.DetailDescription,
                FrameColor = source.FrameColor,
            };
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

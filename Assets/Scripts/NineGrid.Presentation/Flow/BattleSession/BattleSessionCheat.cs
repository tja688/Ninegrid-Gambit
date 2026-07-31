using System.Collections.Generic;
using NineGrid.Cards;
using NineGrid.Cards.Presentation;
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
        /// QuickTest 补宿主用的白板怪。正式配表无技能；仅动态挂载通道技能。
        /// </summary>
        private const string QuickTestBlankHostMonsterDefId = "monster.melee_3";
        private const string QuickTestFlameHelpDefId = "help.flame";
        private const int QuickTestFlamePropCount = 2;

        /// <summary>
        /// QuickTest：一怪一技分发到场上怪物（按格号升序）；宿主不够则再发白板怪；
        /// 另保至少一只无技能白板怪作邻接/翻面同伴。卡面短描述按单技能覆写（&lt;16 字）。
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

            var registry = arch.GetModel<CardRegistry>();
            var board = arch.GetModel<BoardModel>();
            EnsureQuickTestMonsterHosts(arch, board, registry, skillIds.Count);

            var hosts = CollectBoardMonstersBySlot(board, registry);
            var cards = CardEntityLifecycleHook.CardsOrNull();
            var mountedHosts = 0;
            var activatedInstances = 0;
            var assignCount = skillIds.Count < hosts.Count ? skillIds.Count : hosts.Count;

            for (var i = 0; i < assignCount; i++)
            {
                var skillId = skillIds[i];
                if (string.IsNullOrEmpty(skillId))
                {
                    continue;
                }

                var single = new[] { skillId };
                var card = hosts[i];
                var instances = content.ActivateSkillsOnCard(card, single);
                var count = instances != null ? instances.Count : 0;
                activatedInstances += count;
                mountedHosts++;
                if (count <= 0)
                {
                    Debug.LogWarning(
                        $"[BattleSessionCheat] QuickTest 挂技能失败：uid={card.Uid} defId={card.DefId}"
                        + $" skillId={skillId}（卡面描述仍可能写入，但无 Effect 实例）");
                }

                var description = QuickTestDeckCatalog.BuildCardFaceDescription(content.Catalog, single);
                if (cards != null && cards.TryGet(card.Uid, out var view) && view != null)
                {
                    ApplySkillDescription(view, description);
                }

                Debug.Log(
                    $"[BattleSessionCheat] QuickTest 一怪一技 slot={card.Slot.Value.Index}"
                    + $" uid={card.Uid} defId={card.DefId} skill={skillId}"
                    + (string.IsNullOrEmpty(description) ? string.Empty : " desc=" + description));
            }

            var blankPeers = hosts.Count - assignCount;
            Debug.Log(
                $"[BattleSessionCheat] QuickTest 挂技能 mounted={mountedHosts}/{skillIds.Count}"
                + $" activatedInstances={activatedInstances} boardMonsters={hosts.Count}"
                + $" blankPeers={blankPeers}");
            if (mountedHosts > 0 && activatedInstances <= 0)
            {
                Debug.LogError(
                    "[BattleSessionCheat] QuickTest 已选宿主但激活实例为 0——技能只会显示描述、不会在规则链生效");
            }

            if (assignCount < skillIds.Count)
            {
                Debug.LogWarning(
                    $"[BattleSessionCheat] QuickTest 宿主不足：只挂了 {assignCount}/{skillIds.Count}，"
                    + "空槽或 Spawn 失败，未挂技能无法单验");
            }

            return mountedHosts;
        }

        /// <summary>
        /// 通道含 <c>skill.flame_boiling</c> 时往道具栏塞烈焰（红卡 weight=0 不会自然出现）。
        /// 验收：互动满 5 → 用烈焰 → 对玩家伤害 4→5（可再叠）。
        /// </summary>
        public static int EnsureQuickTestFlamePropsIfNeeded(IReadOnlyList<string> skillIds)
        {
            if (skillIds == null || skillIds.Count == 0)
            {
                return 0;
            }

            var needFlame = false;
            for (var i = 0; i < skillIds.Count; i++)
            {
                if (skillIds[i] == "skill.flame_boiling")
                {
                    needFlame = true;
                    break;
                }
            }

            if (!needFlame)
            {
                return 0;
            }

            var arch = NineGridArchitecture.Current;
            if (arch == null)
            {
                return 0;
            }

            var pipeline = arch.GetSystem<IActionPipelineSystem>();
            var deck = arch.GetModel<DeckModel>();
            if (pipeline?.EventLog == null || deck == null)
            {
                return 0;
            }

            var already = 0;
            for (var i = 0; i < deck.ItemSlotUids.Count; i++)
            {
                var uid = deck.ItemSlotUids[i];
                var registry = arch.GetModel<CardRegistry>();
                if (registry != null
                    && registry.TryGet(uid, out var card)
                    && card != null
                    && card.DefId == QuickTestFlameHelpDefId)
                {
                    already++;
                }
            }

            var toSpawn = QuickTestFlamePropCount - already;
            if (toSpawn <= 0)
            {
                return already;
            }

            var start = pipeline.EventLog.Entries.Count;
            for (var i = 0; i < toSpawn; i++)
            {
                pipeline.Enqueue(
                    new SpawnCardAction(
                        QuickTestFlameHelpDefId,
                        CardKind.HelpCard,
                        ZoneId.ItemSlots,
                        SlotId.None,
                        1,
                        "QuickTestFlameProp"));
            }

            pipeline.RunToCompletion();
            BattleBeatFlush.PresentEventLogSlice(arch, start);
            Debug.Log(
                $"[BattleSessionCheat] QuickTest 塞烈焰 props+={toSpawn}"
                + $" defId={QuickTestFlameHelpDefId}（验沸腾：互动5后用烈焰，玩家受伤 4+N）");
            return already + toSpawn;
        }

        /// <summary>
        /// 保证场上至少有 skillCount 只技能宿主 + 1 只白板同伴；不够则 Spawn 白板怪并 Present。
        /// </summary>
        private static void EnsureQuickTestMonsterHosts(
            IArchitecture arch,
            BoardModel board,
            CardRegistry registry,
            int skillCount)
        {
            if (skillCount <= 0)
            {
                return;
            }

            var needed = skillCount + 1;
            var have = CountBoardMonsters(board, registry);
            while (have < needed)
            {
                if (!TrySpawnQuickTestBlankHost(arch, board))
                {
                    Debug.LogWarning(
                        $"[BattleSessionCheat] QuickTest 无法补宿主：have={have} needed={needed}");
                    break;
                }

                have++;
            }
        }

        private static int CountBoardMonsters(BoardModel board, CardRegistry registry)
        {
            var count = 0;
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var uid = board.GetCardUid(SlotId.Board(i));
                if (uid <= 0 || !registry.TryGet(uid, out var card) || card.Kind != CardKind.Monster)
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private static List<CardInstance> CollectBoardMonstersBySlot(BoardModel board, CardRegistry registry)
        {
            var hosts = new List<CardInstance>(SlotId.MaxBoardIndex);
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var uid = board.GetCardUid(SlotId.Board(i));
                if (uid <= 0 || !registry.TryGet(uid, out var card) || card.Kind != CardKind.Monster)
                {
                    continue;
                }

                hosts.Add(card);
            }

            return hosts;
        }

        private static bool TrySpawnQuickTestBlankHost(IArchitecture arch, BoardModel board)
        {
            if (!TryFindEmptyBoardSlot(board, out var slot))
            {
                return false;
            }

            var pipeline = arch.GetSystem<IActionPipelineSystem>();
            if (pipeline?.EventLog == null)
            {
                return false;
            }

            var start = pipeline.EventLog.Entries.Count;
            pipeline.Enqueue(
                new SpawnCardAction(
                    QuickTestBlankHostMonsterDefId,
                    CardKind.Monster,
                    ZoneId.Board,
                    slot,
                    1,
                    "QuickTestHost"));
            pipeline.RunToCompletion();
            BattleBeatFlush.PresentEventLogSlice(arch, start);

            var uid = board.GetCardUid(slot);
            if (uid <= 0)
            {
                Debug.LogWarning(
                    $"[BattleSessionCheat] QuickTest 补宿主后格 {slot.Index} 仍空");
                return false;
            }

            Debug.Log(
                $"[BattleSessionCheat] QuickTest 补白板宿主 slot={slot.Index} uid={uid}"
                + $" defId={QuickTestBlankHostMonsterDefId}");
            return true;
        }

        private static bool TryFindEmptyBoardSlot(BoardModel board, out SlotId slot)
        {
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var candidate = SlotId.Board(i);
                if (candidate == board.AvatarSlot.Value || !board.IsEmpty(candidate))
                {
                    continue;
                }

                slot = candidate;
                return true;
            }

            slot = SlotId.None;
            return false;
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
                FaceIntro = source.FaceIntro,
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

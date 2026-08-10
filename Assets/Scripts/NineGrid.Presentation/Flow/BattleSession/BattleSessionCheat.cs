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
            PlayerInfoHudPresenter.TryGetInstance()?.SyncFromCore(animate: false);
            Debug.Log($"[BattleSessionCheat] Avatar#{avatarUid} Attack → {attack}");
            return true;
        }

        /// <summary>
        /// QuickTest 补宿主用的白板怪。正式配表无技能；仅动态挂载通道技能。
        /// </summary>
        private const string QuickTestBlankHostMonsterDefId = "monster.melee_3";
        private const string QuickTestFlameTrapDefId = "trap.flame";
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
            var pipeline = arch.GetSystem<IActionPipelineSystem>();
            var flushStart = pipeline?.EventLog != null ? pipeline.EventLog.Entries.Count : -1;

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

            // OnActivate（如链接战术光环）只入队 AddStatModifier；必须当场冲刷，否则卡面/有效攻滞后到下一次互动。
            if (pipeline != null && pipeline.PendingCount > 0)
            {
                pipeline.RunToCompletion();
                if (flushStart >= 0)
                {
                    BattleBeatFlush.PresentEventLogSlice(arch, flushStart);
                }
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
        /// 通道含 <c>skill.flame_boiling</c> 时往场上塞烈焰机关（红卡 weight=0 不会自然出现）。
        /// 验收：互动满 5 → 邻接移动烈焰 → 对玩家伤害 1→2（可再叠）。
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
            var board = arch.GetModel<BoardModel>();
            var registry = arch.GetModel<CardRegistry>();
            if (pipeline?.EventLog == null || board == null || registry == null)
            {
                return 0;
            }

            var already = 0;
            for (var i = 1; i <= 9; i++)
            {
                var uid = board.GetCardUid(SlotId.Board(i));
                if (uid != 0
                    && registry.TryGet(uid, out var card)
                    && card != null
                    && card.DefId == QuickTestFlameTrapDefId)
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
            var spawned = 0;
            for (var slot = 1; slot <= 9 && spawned < toSpawn; slot++)
            {
                var boardSlot = SlotId.Board(slot);
                if (!board.IsEmpty(boardSlot))
                {
                    continue;
                }

                pipeline.Enqueue(
                    new SpawnCardAction(
                        QuickTestFlameTrapDefId,
                        CardKind.Trap,
                        ZoneId.Board,
                        boardSlot,
                        1,
                        "QuickTestFlameProp"));
                spawned++;
            }

            if (spawned > 0)
            {
                pipeline.RunToCompletion();
                BattleBeatFlush.PresentEventLogSlice(arch, start);
            }

            Debug.Log(
                $"[BattleSessionCheat] QuickTest 塞烈焰机关 board+={spawned}"
                + $" defId={QuickTestFlameTrapDefId}（验沸腾：互动5后邻移烈焰，玩家受伤 1+N）");
            return already + spawned;
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
            var phaseSystem = arch.GetSystem<IPhaseSystem>();
            var phase = phaseSystem.CurrentPhase;
            if (phase == GamePhase.RoomChoice || phase == GamePhase.RewardItemChoice)
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

            // ADR-0021：与 CompleteNodeIfCleared 同路 — RoomChoice + 选房/导航 Offer，不再走通关三选一。
            var complete = phaseSystem.TryCompleteClearedNode();
            if (!complete.Accepted)
            {
                Debug.LogWarning($"[BattleSessionCheat] TryCompleteClearedNode 被拒: {complete.Reason}");
                return false;
            }

            // 正常击杀清关会经 post-kill board present 卸掉机关/帮助等残留视图；
            // 作弊路径只改了 Core，须在此补表现收口，否则会盖住房间图标。
            // 卡组抽牌堆残留由 TryEnterNodeSettlement → RaiseSettlementReady 同步清掉。
            ClearResidualCombatFieldViews(arch);

            Debug.Log(
                $"[BattleSessionCheat] 强制节点胜利 → {phaseSystem.CurrentPhase} pending={arch.GetModel<PendingChoiceModel>().Kind.Value}");
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
                AttackPattern = source.AttackPattern,
                HasSyncRhythmSkills = source.HasSyncRhythmSkills,
                HasActiveRhythm = source.HasActiveRhythm,
                ShowActionCount = source.ShowActionCount,
                FaceUp = source.FaceUp,
                BasicDescription = source.BasicDescription,
                DetailDescription = source.DetailDescription,
                FaceIntro = source.FaceIntro,
                FrameColor = source.FrameColor,
            };
        }

        private static void MakeNodeCleared(IArchitecture arch)
        {
            // ADR-0026 / #113：QuickTest 跳关与击破离开机关等价 — 置清关标志即可走 CompleteNodeIfCleared。
            arch.GetModel<BattleContextModel>().MarkLeaveTrapBroken();
        }

        /// <summary>
        /// Core 清关后场上应只剩 Avatar。作弊无 post-kill board present，
        /// 须显式卸掉机关 / 帮助 / 漏网怪等残留视图，避免盖住房间图标（ADR-0020/0021）。
        /// </summary>
        private static void ClearResidualCombatFieldViews(IArchitecture arch)
        {
            var field = GroundFieldGeometryHook.FieldOrNull();
            var cards = CardEntityLifecycleHook.CardsOrNull();
            if (field == null)
            {
                return;
            }

            var avatarUid = arch.GetModel<BoardModel>()?.AvatarUid.Value ?? 0;
            var toRemove = new List<int>(8);
            var seen = new HashSet<int>();

            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                if (!field.TryGetCardAt(i, out var card) || card == null || card.Uid <= 0)
                {
                    continue;
                }

                if (card.Uid == avatarUid || !seen.Add(card.Uid))
                {
                    continue;
                }

                toRemove.Add(card.Uid);
            }

            // 飞牌取消 / 占格漏登时仍可能留着 GroundCardMode 孤儿视图。
            if (cards != null)
            {
                foreach (var card in cards.EnumerateCards())
                {
                    if (card == null
                        || card.Uid <= 0
                        || card.Uid == avatarUid
                        || card.DisplayMode != CardDisplayMode.GroundCardMode
                        || !seen.Add(card.Uid))
                    {
                        continue;
                    }

                    toRemove.Add(card.Uid);
                }
            }

            for (var i = 0; i < toRemove.Count; i++)
            {
                var uid = toRemove[i];
                field.CancelDealFlightForUid(uid, "CheatForceNodeVictory.ClearResidual");
                if (field.RequestRemoveFromField(
                        uid,
                        animate: false,
                        skipBusyGuard: true,
                        startExplore: false))
                {
                    continue;
                }

                field.TryClearOccupancyForUid(uid, skipBusyGuard: true);
                if (cards != null && cards.TryGet(uid, out var orphan) && orphan != null)
                {
                    cards.Release(orphan, "CheatForceNodeVictory.ClearResidualOrphan");
                }
            }
        }
    }
}

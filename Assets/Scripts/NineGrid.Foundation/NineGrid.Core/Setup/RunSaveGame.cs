using System;
using System.Collections.Generic;
using System.Globalization;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NineGrid.Core.Content;
using QFramework;

namespace NineGrid.Core
{
    [Serializable]
    public sealed class RunSaveStatEntry
    {
        public int stat;
        public float value;
    }

    [Serializable]
    public sealed class RunSaveCounterEntry
    {
        public string key;
        public int value;
    }

    [Serializable]
    public sealed class RunSaveContributionEntry
    {
        public string relicDefId;
        public int stat;
        public int value;
    }

    /// <summary>
    /// 跑图存档快照（v1）：在正式局战斗节点开始前（BuildNodeDeckOptions 消耗 RNG 之前）捕获。
    /// 纯数据 DTO，公开字段供 JsonUtility（表现层）序列化；Core 本体不做 JSON。
    /// 恢复语义：以 <see cref="seed"/> 重建初始局（InitialGameFactory.Create）后调用
    /// <see cref="RunSaveGame.RestoreAfterCreate"/> 覆盖恢复，再由流程壳从该战斗节点重新入场；
    /// RNG 内部状态一并恢复，故开局发牌与遭遇与存档时完全一致。
    /// </summary>
    [Serializable]
    public sealed class RunSaveSnapshot
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;

        // ---- 展示元信息 ----
        /// <summary>写盘时间（DateTime.Ticks 十进制字符串；JsonUtility 不稳定支持 long 显示语义，统一走字符串）。</summary>
        public string savedAtTicks = string.Empty;
        public string avatarDisplayName = string.Empty;
        public int metaHp;
        public int metaMaxHp;

        // ---- 跑图进度（RunModel）----
        public int floor = 1;
        /// <summary>0-based（RunModel.NodeIndex 语义）。</summary>
        public int nodeIndex;
        /// <summary>本局种子（ulong 十进制字符串）。</summary>
        public string seed = "1";
        public int room;
        public string floorMonsterDeckId = string.Empty;
        public string[] usedMonsterDeckIds = Array.Empty<string>();
        public string[] attributePickDefIds = Array.Empty<string>();
        /// <summary>选人难度档 id（normal / advanced / hard）。</summary>
        public string difficultyId = RunDifficultyIds.Normal;
        /// <summary>壳层全局节点序号（GameFlowShellSystem.NodeIndex，捕获时即将开打的节点）。</summary>
        public int shellGlobalNodeIndex = 1;

        // ---- 玩家（PlayerModel）----
        public string professionId = string.Empty;
        public int coins;
        public int interactionCount;
        public int itemDeckCapacity;
        public int itemSlotsCapacity;
        public int itemStatBonus;
        public string[] relicDefIds = Array.Empty<string>();
        /// <summary>已消费的一次性遗物效果 id（如黄金鱼竿给宝箱卡），恢复时先写回再重装遗物，防重复发放。</summary>
        public string[] consumedRelicEffectIds = Array.Empty<string>();
        public string[] itemSourcePoolDefIds = Array.Empty<string>();
        public string[] fixedItemCardDefIds = Array.Empty<string>();

        // ---- 道具卡格（DeckModel.ItemSlotUids → defIds，按持有顺序）----
        public string[] itemSlotDefIds = Array.Empty<string>();

        // ---- Avatar（基础属性 + 计数器）----
        public RunSaveStatEntry[] avatarBaseStats = Array.Empty<RunSaveStatEntry>();
        public RunSaveCounterEntry[] avatarCounters = Array.Empty<RunSaveCounterEntry>();

        // ---- 遗物 run 贡献（#120）----
        public RunSaveContributionEntry[] relicRunContributions = Array.Empty<RunSaveContributionEntry>();

        // ---- RNG 内部状态 ----
        public uint rngX;
        public uint rngY;
        public uint rngZ;
        public uint rngW;
        public string rngStep = "0";

        public ulong SeedValue
        {
            get { return ParseUlong(seed, 1UL); }
        }

        public ulong RngStepValue
        {
            get { return ParseUlong(rngStep, 0UL); }
        }

        public long SavedAtTicksValue
        {
            get
            {
                long ticks;
                return long.TryParse(
                    savedAtTicks,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out ticks)
                    ? ticks
                    : 0L;
            }
        }

        /// <summary>玩家可读节点号（1-8）。</summary>
        public int DisplayNode
        {
            get { return MapNodeProgression.ToDisplayNode(nodeIndex); }
        }

        public void StampSavedAtNow()
        {
            savedAtTicks = DateTime.Now.Ticks.ToString(CultureInfo.InvariantCulture);
        }

        private static ulong ParseUlong(string text, ulong fallback)
        {
            ulong value;
            return ulong.TryParse(
                text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value)
                ? value
                : fallback;
        }
    }

    /// <summary>
    /// 跑图存档的 Core 侧捕获 / 恢复。表现层负责触发时机、序列化与落盘（ES3）。
    /// </summary>
    public static class RunSaveGame
    {
        /// <summary>
        /// 捕获当前 Core 状态为快照。调用时机契约：战斗节点即将开始、
        /// 且本节点的 BuildNodeDeckOptions 尚未消耗 RNG（GameFlowOrchestrator.PlayRealBattleAsync）。
        /// </summary>
        public static RunSaveSnapshot Capture(IArchitecture architecture, int shellGlobalNodeIndex)
        {
            if (architecture == null)
            {
                throw new ArgumentNullException("architecture");
            }

            var run = architecture.GetModel<RunModel>();
            var player = architecture.GetModel<PlayerModel>();
            var deck = architecture.GetModel<DeckModel>();
            var registry = architecture.GetModel<CardRegistry>();
            var board = architecture.GetModel<BoardModel>();
            var contributions = architecture.GetModel<RelicRunContributionModel>();
            var rng = architecture.GetUtility<IRngUtility>();

            var snapshot = new RunSaveSnapshot
            {
                floor = run.Floor.Value,
                nodeIndex = run.NodeIndex.Value,
                seed = run.Seed.Value.ToString(CultureInfo.InvariantCulture),
                room = (int)run.Room.Value,
                floorMonsterDeckId = run.FloorMonsterDeckId.Value ?? string.Empty,
                usedMonsterDeckIds = CopyList(run.UsedMonsterDeckIds),
                attributePickDefIds = CopyList(run.AttributePickDefIds),
                difficultyId = run.DifficultyId?.Value ?? RunDifficultyIds.Normal,
                shellGlobalNodeIndex = shellGlobalNodeIndex,
                professionId = player.ProfessionId.Value ?? string.Empty,
                coins = player.Coins.Value,
                interactionCount = player.InteractionCount.Value,
                itemDeckCapacity = player.ItemDeckCapacity,
                itemSlotsCapacity = player.ItemSlotsCapacity,
                itemStatBonus = player.ItemStatBonus,
                relicDefIds = CopyList(player.RelicDefIds),
                consumedRelicEffectIds = CopyList(player.ConsumedRelicEffectIds),
                itemSourcePoolDefIds = CopyList(player.ItemSourcePoolDefIds),
                fixedItemCardDefIds = CopyList(player.FixedItemCardDefIds),
            };

            // 道具卡格按持有顺序存 defId；恢复时按 defId 重造（与跨层 preserveRunInventory 同语义）。
            var itemSlots = deck.ItemSlotUids;
            var itemDefIds = new List<string>(itemSlots.Count);
            for (var i = 0; i < itemSlots.Count; i++)
            {
                CardInstance itemCard;
                if (registry.TryGet(itemSlots[i], out itemCard)
                    && itemCard != null
                    && !string.IsNullOrEmpty(itemCard.DefId))
                {
                    itemDefIds.Add(itemCard.DefId);
                }
            }

            snapshot.itemSlotDefIds = itemDefIds.ToArray();

            // Avatar 基础属性 + 计数器（含遗物 Run 作用域倒计时进度，ADR-0035）。
            CardInstance avatar = null;
            var avatarUid = board.AvatarUid.Value;
            if (avatarUid > 0)
            {
                registry.TryGet(avatarUid, out avatar);
            }

            if (avatar == null)
            {
                throw new InvalidOperationException("RunSaveGame.Capture：Avatar 不存在，无法捕获存档。");
            }

            var stats = new List<RunSaveStatEntry>();
            foreach (var pair in avatar.Stats.BaseValues)
            {
                stats.Add(new RunSaveStatEntry { stat = (int)pair.Key, value = pair.Value });
            }

            snapshot.avatarBaseStats = stats.ToArray();

            var counters = new List<RunSaveCounterEntry>();
            foreach (var pair in avatar.Counters.Values)
            {
                counters.Add(new RunSaveCounterEntry { key = pair.Key, value = pair.Value });
            }

            snapshot.avatarCounters = counters.ToArray();

            var contributionEntries = new List<RunSaveContributionEntry>();
            foreach (var pair in contributions.RawEntries)
            {
                string relicDefId;
                StatId stat;
                if (!RelicRunContributionModel.TryParseKey(pair.Key, out relicDefId, out stat))
                {
                    continue;
                }

                contributionEntries.Add(new RunSaveContributionEntry
                {
                    relicDefId = relicDefId,
                    stat = (int)stat,
                    value = pair.Value,
                });
            }

            snapshot.relicRunContributions = contributionEntries.ToArray();

            var rngState = rng.CaptureState();
            snapshot.rngX = rngState.X;
            snapshot.rngY = rngState.Y;
            snapshot.rngZ = rngState.Z;
            snapshot.rngW = rngState.W;
            snapshot.rngStep = rngState.Step.ToString(CultureInfo.InvariantCulture);

            snapshot.metaHp = (int)Math.Round(avatar.Stats.GetBase(StatId.Hp));
            snapshot.metaMaxHp = (int)Math.Round(avatar.Stats.GetBase(StatId.MaxHp));
            snapshot.avatarDisplayName = ResolveAvatarDisplayName(architecture, avatar);
            snapshot.StampSavedAtNow();
            return snapshot;
        }

        /// <summary>
        /// 在 InitialGameFactory.Create（以快照 seed 建好初始局）之后覆盖恢复。
        /// 恢复顺序契约：遗物对齐（含 OnActivate 冲刷）→ 道具卡格重造 → Avatar 基础属性/计数器覆写
        /// → 遗物 run 贡献 + Persistent modifier 重装 → 金币/互动数绝对对齐 → 跑图进度 → RNG 状态（最后）。
        /// 完成后相位为 NodeCompleted：流程壳可直接 StartNode 进入该战斗节点。
        /// </summary>
        public static void RestoreAfterCreate(IArchitecture architecture, RunSaveSnapshot snapshot)
        {
            if (architecture == null)
            {
                throw new ArgumentNullException("architecture");
            }

            if (snapshot == null)
            {
                throw new ArgumentNullException("snapshot");
            }

            var run = architecture.GetModel<RunModel>();
            var player = architecture.GetModel<PlayerModel>();
            var deck = architecture.GetModel<DeckModel>();
            var registry = architecture.GetModel<CardRegistry>();
            var board = architecture.GetModel<BoardModel>();
            var contributions = architecture.GetModel<RelicRunContributionModel>();
            var content = architecture.GetSystem<IContentSystem>();
            var statSystem = architecture.GetSystem<IStatSystem>();
            var pipeline = architecture.GetSystem<IActionPipelineSystem>();
            var rng = architecture.GetUtility<IRngUtility>();

            // 1. 职业与容量 / 来源池 / 固定卡 / 强化。
            if (!string.IsNullOrEmpty(snapshot.professionId))
            {
                player.SetProfession(snapshot.professionId);
            }

            player.SetItemDeckCapacity(snapshot.itemDeckCapacity);
            if (snapshot.itemSlotsCapacity > 0)
            {
                player.SetItemSlotsCapacity(snapshot.itemSlotsCapacity);
            }

            // 来源池写回守卫（ADR-0033）：旧存档可能含宝箱卡等特殊卡，过滤后再恢复。
            var restoreCatalog = content != null && content.HasCatalog ? content.Catalog : null;
            player.ReplaceItemSourcePool(Content.HelpCardDecks.FilterRegularSourcePool(
                restoreCatalog,
                snapshot.itemSourcePoolDefIds));
            // 固定卡每关重进卡组，须同为 live 常规档；旧存档的归档卡/特殊卡在此拦截。
            player.ReplaceFixedItemCards(Content.HelpCardDecks.FilterRegularSourcePool(
                restoreCatalog,
                snapshot.fixedItemCardDefIds));
            player.SetItemStatBonus(snapshot.itemStatBonus);
            run.SetAttributePicks(snapshot.attributePickDefIds);

            // 2. 遗物对齐：先弃掉初始局多出的（如职业初始遗物已被玩家丢弃），再补装快照遗物。
            // 消费标记必须先于遗物重装写回，否则一次性效果（黄金鱼竿给宝箱卡）会在恢复时重复发放。
            player.ReplaceConsumedRelicEffects(snapshot.consumedRelicEffectIds);
            var wanted = new HashSet<string>(StringComparer.Ordinal);
            if (snapshot.relicDefIds != null)
            {
                for (var i = 0; i < snapshot.relicDefIds.Length; i++)
                {
                    if (!string.IsNullOrEmpty(snapshot.relicDefIds[i]))
                    {
                        wanted.Add(snapshot.relicDefIds[i]);
                    }
                }
            }

            var existing = new List<string>(player.RelicDefIds);
            for (var i = 0; i < existing.Count; i++)
            {
                if (!wanted.Contains(existing[i]))
                {
                    pipeline.Enqueue(new DiscardRelicAction(existing[i]));
                }
            }

            pipeline.RunToCompletion();

            if (snapshot.relicDefIds != null)
            {
                var alreadyActive = new HashSet<string>(player.RelicDefIds, StringComparer.Ordinal);
                for (var i = 0; i < snapshot.relicDefIds.Length; i++)
                {
                    var defId = snapshot.relicDefIds[i];
                    if (string.IsNullOrEmpty(defId) || alreadyActive.Contains(defId))
                    {
                        continue;
                    }

                    player.AddRelic(defId);
                    if (content != null)
                    {
                        content.ActivateRelic(defId);
                    }
                }
            }

            // OnActivate（光环等）只入队 AddStatModifier；当场冲刷，避免滞后到首次互动。
            pipeline.RunToCompletion();

            // 3. 道具卡格：按 defId 重造（与跨层 preserveRunInventory 同语义）。
            // 写回守卫（ADR-0033 修订）：只放行 live 道具卡（含特殊档）；归档卡（倍增塔/瞭望塔等）
            // 仍在 Catalog 中可被重造，旧存档不得把老道具带回正式局。
            if (snapshot.itemSlotDefIds != null && content != null)
            {
                var liveItemSlots = Content.HelpCardDecks.FilterLiveHelpCards(
                    restoreCatalog,
                    snapshot.itemSlotDefIds);
                for (var i = 0; i < liveItemSlots.Count; i++)
                {
                    var draft = content.CreateDraft(liveItemSlots[i]);
                    if (draft == null || draft.Kind == CardKind.Unknown)
                    {
                        continue;
                    }

                    deck.AddToItemSlots(draft.Create(registry));
                }
            }

            // 4. Avatar 基础属性 + 计数器覆写（快照捕获于遗物已激活状态，直接覆盖即为终值）。
            CardInstance avatar = null;
            var avatarUid = board.AvatarUid.Value;
            if (avatarUid > 0)
            {
                registry.TryGet(avatarUid, out avatar);
            }

            if (avatar == null)
            {
                throw new InvalidOperationException("RunSaveGame.RestoreAfterCreate：Avatar 不存在。");
            }

            if (snapshot.avatarBaseStats != null)
            {
                for (var i = 0; i < snapshot.avatarBaseStats.Length; i++)
                {
                    var entry = snapshot.avatarBaseStats[i];
                    avatar.Stats.SetBase((StatId)entry.stat, entry.value);
                }
            }

            avatar.Counters.Clear();
            if (snapshot.avatarCounters != null)
            {
                for (var i = 0; i < snapshot.avatarCounters.Length; i++)
                {
                    var entry = snapshot.avatarCounters[i];
                    if (!string.IsNullOrEmpty(entry.key))
                    {
                        avatar.Counters.Set(entry.key, entry.value);
                    }
                }
            }

            // 5. 遗物 run 贡献：写回 Model 并重装 Persistent modifier（对齐 ModifyRelicRunContributionAction 安装方式）。
            contributions.ClearAll();
            if (snapshot.relicRunContributions != null)
            {
                for (var i = 0; i < snapshot.relicRunContributions.Length; i++)
                {
                    var entry = snapshot.relicRunContributions[i];
                    if (string.IsNullOrEmpty(entry.relicDefId) || entry.value <= 0)
                    {
                        continue;
                    }

                    var stat = (StatId)entry.stat;
                    contributions.Set(entry.relicDefId, stat, entry.value);
                    var source = new ModifierSource(
                        RelicRunContributionModel.BuildModifierSourceId(entry.relicDefId, stat));
                    statSystem.RemoveModifiersBySource(avatar, source);
                    statSystem.AddModifier(
                        avatar,
                        new StatModifier(
                            stat,
                            ModifierOp.Add,
                            entry.value,
                            ModifierLayer.Persistent,
                            source,
                            ModifierScope.Permanent,
                            null));
                }
            }

            // 6. 金币 / 互动数绝对对齐（遗物激活可能已发过金，最后统一收敛到快照值）。
            player.AddCoins(snapshot.coins - player.Coins.Value);
            player.AddInteractionCount(snapshot.interactionCount - player.InteractionCount.Value);

            // 7. 跑图进度 + 相位（NodeCompleted 使 StartNode 合法，由流程壳入场）。
            run.RestoreProgress(
                snapshot.floor,
                snapshot.nodeIndex,
                snapshot.SeedValue,
                (RoomKind)snapshot.room,
                snapshot.floorMonsterDeckId,
                snapshot.usedMonsterDeckIds,
                snapshot.difficultyId);
            run.SetPhase(GamePhase.NodeCompleted);

            // 8. RNG 状态最后恢复：覆盖以上步骤可能造成的消耗，保证发牌复现。
            rng.RestoreState(new RngState(
                snapshot.rngX,
                snapshot.rngY,
                snapshot.rngZ,
                snapshot.rngW,
                snapshot.RngStepValue));
        }

        private static string ResolveAvatarDisplayName(IArchitecture architecture, CardInstance avatar)
        {
            var content = architecture.GetSystem<IContentSystem>();
            if (content != null
                && content.HasCatalog
                && content.Catalog != null
                && content.Catalog.Cards != null
                && !string.IsNullOrEmpty(avatar.DefId))
            {
                Content.CardContentDefinition def;
                if (content.Catalog.Cards.TryGetValue(avatar.DefId, out def)
                    && def != null
                    && !string.IsNullOrEmpty(def.DisplayName))
                {
                    return def.DisplayName;
                }
            }

            // Core Catalog.Cards 不含 Avatar 条目（表现层 JSON 才有 displayName）；
            // 本轮固定职业为战士（ProfessionCatalog），兜底用其名。
            return Localization.L10n.Tr("charselect.warrior_name", "战士");
        }

        private static string[] CopyList(IReadOnlyList<string> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<string>();
            }

            var copy = new string[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                copy[i] = source[i] ?? string.Empty;
            }

            return copy;
        }
    }
}

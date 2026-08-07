using System.Collections.Generic;
using NineGrid.Cards;
using NineGrid.Cards.Presentation;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using QFramework;
using UnityEngine;
namespace NineGrid.Flow
{
    public struct CardPresentationRead
    {
        public CardPresentationKind Kind;
        public int Attack;
        public int Hp;
        public int Armor;
        public string DefId;
        public bool FaceUp;
    }

    /// <summary>
    /// 从 Core/Content 构建胖投影，经编排侧 Commit 出口提交到卡牌底盘宿主。
    /// 正式路径：Build → CommitPresentation → ApplyPresentation → Binder；禁止旁路 Set* 直刷卡面。
    /// SyncAllSpawnedCards 对账已提交投影，不直读最新 Core。
    /// </summary>
    public static class CoreCardPresentationMapper
    {
        /// <summary>
        /// 绑定生产内容 Catalog。Editor 下（含 Disable Domain Reload）每次从磁盘重载，
        /// 避免「JSON 已改 / 已清空挂载，战斗仍用上一局内存 Catalog」。
        /// Player 仍只在尚未绑定时装载一次。
        /// </summary>
        public static void EnsureContentCatalogLoaded()
        {
            var arch = NineGridArchitecture.Current;
            var content = arch.GetSystem<IContentSystem>();
#if !UNITY_EDITOR
            if (content.HasCatalog)
            {
                return;
            }
#endif
            BindFreshContentCatalog(arch, content);
        }

        /// <summary>
        /// 强制从 Arts/StreamingAssets 重投影并写入 ContentSystem + DefaultCatalog。
        /// </summary>
        public static void ReloadContentCatalog()
        {
            var arch = NineGridArchitecture.Current;
            BindFreshContentCatalog(arch, arch.GetSystem<IContentSystem>());
        }

        private static void BindFreshContentCatalog(IArchitecture arch, IContentSystem content)
        {
            var catalog = ContentCatalogBootstrap.Load();
            content.Load(catalog);
            // CreateDraft 等路径会 TryReloadFromConfig；必须与刚绑定的 Catalog 同源，
            // 否则 Disable Domain Reload 下可能被 EditMode 测试残留的 DefaultCatalog 盖回旧表。
            arch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, catalog);
        }

        public static bool TryRead(int uid, out CardPresentationRead read)
        {
            read = default;
            if (uid <= 0)
            {
                return false;
            }

            var arch = NineGridArchitecture.Current;
            var registry = arch.GetModel<CardRegistry>();
            if (!registry.TryGet(uid, out var card))
            {
                return false;
            }

            var statSystem = arch.GetSystem<IStatSystem>();
            read = new CardPresentationRead
            {
                Kind = ToPresentationKind(card.Kind),
                Attack = statSystem.GetEffectiveInt(card, StatId.Attack),
                Hp = statSystem.GetEffectiveInt(card, StatId.Hp),
                // 卡面护甲 = 当前护甲（本关临时资源）；PlayerInfoText 护甲栏走有效护甲。
                Armor = StatArmorUtility.GetCurrentArmor(card),
                DefId = card.DefId ?? string.Empty,
                FaceUp = card.FaceUp,
            };
            return true;
        }

        /// <summary>
        /// 编排串行主线 Commit：读 Content 视觉 → 胖投影 → 底盘宿主分发 Binder。
        /// 若卡面已有提交投影，数值字段保留已提交值（不直读 Core 覆写）；仅刷新视觉。
        /// 战中/生成数值唯一出口：<see cref="NineGrid.Flow.Presentation.CardFaceStatHandler"/>。
        /// </summary>
        public static void ApplyToManagedCard(ManagedCard card, bool animate = false)
        {
            // animate 保留调用方签名兼容；卡面数值改由 Binder 文本 Commit，不再走底盘 digit punch。
            _ = animate;
            if (card?.View == null)
            {
                return;
            }

            if (card.CommittedPresentation != null)
            {
                ApplyVisualsPreservingCommittedStats(card);
                return;
            }

            // 首次只刷视觉；攻/甲/血等 Settled 的 SpawnCard/DealCard/ShowAvatar 指令赋值。
            if (!string.IsNullOrEmpty(card.DefId))
            {
                ApplyVisualsByDefId(card, card.CoreKind, clearCombatStats: true);
            }
        }

        /// <summary>
        /// 只刷新视觉字段，保留已提交的攻/甲/血/行动计数；无已提交快照时回退首次视觉 Commit。
        /// </summary>
        public static void ApplyVisualsPreservingCommittedStats(ManagedCard card)
        {
            if (card?.View == null)
            {
                return;
            }

            var previous = card.CommittedPresentation;
            if (previous == null)
            {
                ApplyToManagedCard(card, animate: false);
                return;
            }

            var snapshot = new CardPresentationSnapshot
            {
                Kind = previous.Kind != CardPresentationKind.Unknown
                    ? previous.Kind
                    : card.CoreKind,
                DefId = !string.IsNullOrEmpty(previous.DefId) ? previous.DefId : card.DefId,
                DisplayName = previous.DisplayName,
                MainIcon = previous.MainIcon,
                FaceBackground = previous.FaceBackground,
                BackBorder = previous.BackBorder,
                BackShirt = previous.BackShirt,
                BackLogo = previous.BackLogo,
                CardFrame = previous.CardFrame,
                Banner = previous.Banner,
                Attack = previous.Attack,
                Armor = previous.Armor,
                Hp = previous.Hp,
                ActionCount = previous.ActionCount,
                FaceUp = previous.FaceUp,
                BasicDescription = previous.BasicDescription,
                DetailDescription = previous.DetailDescription,
                FaceIntro = previous.FaceIntro,
                FrameColor = previous.FrameColor,
                CommittedCountdownRemaining = CopyCommittedRemaining(previous.CommittedCountdownRemaining),
            };

            ApplyVisualFields(snapshot, snapshot.DefId, snapshot.Kind);
            // JSON/视觉刷新后强制写回已提交数值，防止视觉路径误带出生值。
            snapshot.Attack = previous.Attack;
            snapshot.Armor = previous.Armor;
            snapshot.Hp = previous.Hp;
            snapshot.ActionCount = previous.ActionCount;
            card.CommitPresentation(snapshot);
        }

        /// <summary>
        /// Settled 倒计时剩余提交（ADR-0035 / 倒计时票）：把已提交剩余写入卡面投影快照并
        /// 携带重投影局内描述（Instance 模式，命中 liveTemplate 的「N 次后…」键即替换）。
        /// 剩余只经结算指令到达本出口，禁止 View 直读 Core 计数器；检查侧（Inspect）不消费。
        /// </summary>
        public static void CommitCountdownRemaining(ManagedCard card, string tokenKey, string remaining)
        {
            if (card?.View == null || string.IsNullOrWhiteSpace(tokenKey))
            {
                return;
            }

            var previous = card.CommittedPresentation;
            var snapshot = previous != null
                ? CloneForCountdownCommit(previous)
                : BuildVisualSnapshotFromDefId(card.DefId, card.CoreKind);

            var committed = CopyCommittedRemaining(snapshot.CommittedCountdownRemaining);
            committed[tokenKey] = remaining ?? "0";
            snapshot.CommittedCountdownRemaining = committed;

            if (CardPresentationConfigCatalog.TryGet(card.DefId, out var dto) && dto != null)
            {
                var projected = CardFaceDescriptionProjector.Project(
                    CardDescriptionProjectionMode.Instance,
                    dto.description,
                    dto.liveTemplate,
                    dto.effectAssemblies,
                    snapshot.CommittedCountdownRemaining);
                if (!string.IsNullOrWhiteSpace(projected))
                {
                    snapshot.BasicDescription = projected;
                }

                snapshot.DetailDescription = CardDetailDescriptionComposer.Compose(
                    snapshot.BasicDescription,
                    CardFacePresentationBinder.PeekDescriptionIconCatalog());
            }

            card.CommitPresentation(snapshot);
        }

        /// <summary>
        /// Settled 倒计时投影清除（ADR-0035 / #157）：效果卸载或离战重置后移除该投影键的
        /// 已提交剩余并重投影局内描述（Instance 模式）。键不命中则 no-op；
        /// 剩余只经结算指令到达本出口，禁止 View 直读 Core 计数器；检查侧（Inspect）不消费。
        /// </summary>
        public static void ClearCountdownRemaining(ManagedCard card, string tokenKey)
        {
            if (card?.View == null || string.IsNullOrWhiteSpace(tokenKey))
            {
                return;
            }

            var previous = card.CommittedPresentation;
            if (previous == null || previous.CommittedCountdownRemaining == null)
            {
                return;
            }

            var committed = CopyCommittedRemaining(previous.CommittedCountdownRemaining);
            if (!committed.Remove(tokenKey) && !RemoveCaseInsensitive(committed, tokenKey))
            {
                return;
            }

            var snapshot = CloneForCountdownCommit(previous);
            snapshot.CommittedCountdownRemaining = committed;

            if (CardPresentationConfigCatalog.TryGet(card.DefId, out var dto) && dto != null)
            {
                var projected = CardFaceDescriptionProjector.Project(
                    CardDescriptionProjectionMode.Instance,
                    dto.description,
                    dto.liveTemplate,
                    dto.effectAssemblies,
                    snapshot.CommittedCountdownRemaining);
                if (!string.IsNullOrWhiteSpace(projected))
                {
                    snapshot.BasicDescription = projected;
                }

                snapshot.DetailDescription = CardDetailDescriptionComposer.Compose(
                    snapshot.BasicDescription,
                    CardFacePresentationBinder.PeekDescriptionIconCatalog());
            }

            card.CommitPresentation(snapshot);
        }

        private static bool RemoveCaseInsensitive(Dictionary<string, string> committed, string tokenKey)
        {
            string found = null;
            foreach (var pair in committed)
            {
                if (string.Equals(pair.Key, tokenKey, System.StringComparison.OrdinalIgnoreCase))
                {
                    found = pair.Key;
                    break;
                }
            }

            if (found == null)
            {
                return false;
            }

            committed.Remove(found);
            return true;
        }

        private static CardPresentationSnapshot CloneForCountdownCommit(CardPresentationSnapshot source)
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
                FaceIntro = source.FaceIntro ?? string.Empty,
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

        /// <summary>
        /// 按 defId 构建投影并 Commit（Bounce 选卡 / 无 Core uid 的展示卡）。
        /// </summary>
        public static void ApplyVisualsByDefId(
            ManagedCard card,
            CardPresentationKind kindHint = CardPresentationKind.Unknown,
            bool clearCombatStats = false)
        {
            if (card?.View == null || string.IsNullOrEmpty(card.DefId))
            {
                return;
            }

            var kind = kindHint != CardPresentationKind.Unknown
                ? kindHint
                : InferPresentationKind(card.DefId);

            var snapshot = BuildSnapshotFromDefId(card.DefId, kind, clearCombatStats);
            card.CommitPresentation(snapshot);
        }

        /// <summary>
        /// 编排主线全场 Commit：无已提交投影时只刷视觉；已提交则只刷视觉。
        /// 战中/生成数值请走锚点排期或 <see cref="NineGrid.Flow.Presentation.CardFaceGenerationBootstrap"/>。
        /// </summary>
        public static void CommitAllSpawnedCards()
        {
            var cardManager = CardEntityLifecycleHook.CardsOrNull();
            if (cardManager == null)
            {
                return;
            }

            foreach (var pair in cardManager.CardsByUid)
            {
                ApplyToManagedCard(pair.Value, animate: false);
            }
        }

        /// <summary>
        /// 只刷新已有提交投影的视觉字段；无投影的卡跳过（不直读 Core 写数值）。
        /// 用牌等 Present 开头用此出口，把数值留给 Impact/Settled 锚点。
        /// </summary>
        public static void RefreshVisualsPreservingCommittedStatsOnAllSpawned()
        {
            var cardManager = CardEntityLifecycleHook.CardsOrNull();
            if (cardManager == null)
            {
                return;
            }

            foreach (var pair in cardManager.CardsByUid)
            {
                var card = pair.Value;
                if (card?.CommittedPresentation == null)
                {
                    continue;
                }

                ApplyVisualsPreservingCommittedStats(card);
            }
        }

        /// <summary>
        /// 全场对账：重放各卡已提交投影；无已提交快照时跳过（不直刷最新 Core）。
        /// </summary>
        public static void SyncAllSpawnedCards()
        {
            var cardManager = CardEntityLifecycleHook.CardsOrNull();
            if (cardManager == null)
            {
                return;
            }

            foreach (var pair in cardManager.CardsByUid)
            {
                pair.Value?.ReapplyCommittedPresentation();
            }
        }

        public static CardPresentationSnapshot BuildSnapshot(CardPresentationRead read)
        {
            var snapshot = new CardPresentationSnapshot
            {
                Kind = read.Kind,
                DefId = read.DefId ?? string.Empty,
                Attack = Mathf.Max(0, read.Attack),
                Armor = Mathf.Max(0, read.Armor),
                Hp = Mathf.Max(0, read.Hp),
                ActionCount = 0,
                FaceUp = read.FaceUp,
            };

            ApplyVisualFields(snapshot, read.DefId, read.Kind);
            return snapshot;
        }

        /// <summary>
        /// 按 defId 构建纯视觉投影（Bounce / 右键详述 / 无 Core uid 展示）。
        /// </summary>
        public static CardPresentationSnapshot BuildVisualSnapshotFromDefId(
            string defId,
            CardPresentationKind kindHint = CardPresentationKind.Unknown,
            bool clearCombatStats = true)
        {
            var kind = kindHint != CardPresentationKind.Unknown
                ? kindHint
                : InferPresentationKind(defId);
            return BuildSnapshotFromDefId(defId, kind, clearCombatStats);
        }

        private static CardPresentationSnapshot BuildSnapshotFromDefId(
            string defId,
            CardPresentationKind kind,
            bool clearCombatStats)
        {
            var snapshot = new CardPresentationSnapshot
            {
                Kind = kind,
                DefId = defId ?? string.Empty,
                Attack = 0,
                Armor = 0,
                Hp = 0,
                ActionCount = 0,
                FaceUp = true,
            };

            _ = clearCombatStats; // 展示卡战斗数值恒 0；参数保留调用方语义。
            ApplyVisualFields(snapshot, defId, kind);
            return snapshot;
        }

        private static void ApplyVisualFields(
            CardPresentationSnapshot snapshot,
            string defId,
            CardPresentationKind kind)
        {
            if (snapshot == null || string.IsNullOrEmpty(defId))
            {
                return;
            }

            // #69：表现只读一卡一文件 JSON；不再咨询 Luban ContentVisual / VisualCatalog SO。
            if (TryApplyJsonPresentation(snapshot, defId))
            {
                // 部分遗物 JSON 仍缺 mainIcon：用 RelicVisualCatalog bootstrap 补洞，避免选择卡/栏位空图标。
                if (snapshot.MainIcon == null && kind == CardPresentationKind.Relic)
                {
                    TryApplyLegacyRelicIcon(snapshot, defId);
                }

                return;
            }

            if (kind == CardPresentationKind.Relic)
            {
                TryApplyLegacyRelicIcon(snapshot, defId);
            }

            if (string.IsNullOrEmpty(snapshot.DisplayName))
            {
                snapshot.DisplayName = defId;
            }

            _ = kind;
        }

        private static void TryApplyLegacyRelicIcon(CardPresentationSnapshot snapshot, string defId)
        {
            if (snapshot == null || snapshot.MainIcon != null || string.IsNullOrEmpty(defId))
            {
                return;
            }

            var set = ContentVisualSpriteCatalogBootstrapSO.TryLoadCatalogSet();
            if (set?.relics == null
                || !set.relics.TryGet(defId, out var icon, out var face))
            {
                return;
            }

            snapshot.MainIcon = icon != null ? icon : face;
        }

        private static bool TryApplyJsonPresentation(CardPresentationSnapshot snapshot, string defId)
        {
            if (!CardPresentationConfigCatalog.TryGet(defId, out var dto) || dto == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(dto.displayName))
            {
                snapshot.DisplayName = dto.displayName;
            }
            else if (string.IsNullOrEmpty(snapshot.DisplayName))
            {
                snapshot.DisplayName = defId;
            }

            // 卡背：卡组级优先，单卡 sprites.back* 可覆写；皆空则留 null 由模板兜底。
            ApplyDeckBackSprites(snapshot, dto.deckId);
            if (dto.sprites != null)
            {
                ApplyJsonSprite(ref snapshot.MainIcon, dto.sprites.mainIcon);
                ApplyJsonSprite(ref snapshot.FaceBackground, dto.sprites.faceBackground);
                ApplyJsonSprite(ref snapshot.BackBorder, dto.sprites.backBorder);
                ApplyJsonSprite(ref snapshot.BackShirt, dto.sprites.backShirt);
                ApplyJsonSprite(ref snapshot.BackLogo, dto.sprites.backLogo);
                ApplyJsonSprite(ref snapshot.CardFrame, dto.sprites.cardFrame);
                ApplyJsonSprite(ref snapshot.Banner, dto.sprites.banner);
            }

            // ADR-0035 投影缝：实例/预览表面走统一投影层；无局内模板时与检查描述同文。
            // 右键检查侧不消费本出口（见 CardInspectOverlayPresenter 的 Inspect 再投影）。
            // 已提交倒计时剩余（Settled 写入）随快照携带，投影时命中即盖过初始装配实参。
            var filledDescription = CardFaceDescriptionProjector.Project(
                CardDescriptionProjectionMode.Instance,
                dto.description,
                dto.liveTemplate,
                dto.effectAssemblies,
                snapshot.CommittedCountdownRemaining);
            if (!string.IsNullOrWhiteSpace(filledDescription))
            {
                snapshot.BasicDescription = filledDescription;
            }

            snapshot.FaceIntro = CardFaceDescriptionParamFiller.FillFromAssemblies(
                dto.faceIntro,
                dto.effectAssemblies);
            snapshot.DetailDescription = CardDetailDescriptionComposer.Compose(
                snapshot.BasicDescription,
                CardFacePresentationBinder.PeekDescriptionIconCatalog());

            snapshot.FrameColor = ResolveFrameColor(dto);
            // stats 是内容作者定义的出生值，经 Catalog 造卡用；表现层投影提交不读 JSON stats。
            return true;
        }

        private static void ApplyDeckBackSprites(CardPresentationSnapshot snapshot, string deckId)
        {
            if (string.IsNullOrWhiteSpace(deckId)
                || !CardPresentationConfigCatalog.TryGet(deckId.Trim(), out var deck)
                || deck?.sprites == null)
            {
                return;
            }

            ApplyJsonSprite(ref snapshot.BackBorder, deck.sprites.backBorder);
            ApplyJsonSprite(ref snapshot.BackShirt, deck.sprites.backShirt);
            ApplyJsonSprite(ref snapshot.BackLogo, deck.sprites.backLogo);
        }

        private static Color ResolveFrameColor(CardPresentationConfigDto dto)
        {
            if (dto == null)
            {
                return new Color(0f, 0f, 0f, 0f);
            }

            if (System.Enum.TryParse(dto.rarity ?? string.Empty, ignoreCase: true, out ContentRarity rarity)
                && rarity != ContentRarity.None)
            {
                return ToUnityColor(ContentVisualResolver.ResolveFrameColorForRarity(rarity));
            }

            if (dto.isBoss)
            {
                return ToUnityColor(ContentVisualResolver.ResolveFrameColor(
                    CardFrameStyleCatalog.StyleBoss,
                    null));
            }

            if (dto.isElite)
            {
                return ToUnityColor(ContentVisualResolver.ResolveFrameColor(
                    CardFrameStyleCatalog.StyleElite,
                    null));
            }

            return new Color(0f, 0f, 0f, 0f);
        }

        private static Color ToUnityColor(ContentColor color)
        {
            return new Color(color.R, color.G, color.B, color.A);
        }

        private static void ApplyJsonSprite(ref Sprite target, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var sprite = CardPresentationSpritePath.LoadSprite(path);
            if (sprite != null)
            {
                target = sprite;
            }
        }

        /// <summary>
        /// Spawn 前解析表现种：优先 Core CardKind，其次 DefId 前缀（不为 skill./PlayerSkill 开卡面）。
        /// </summary>
        public static CardPresentationKind ResolvePresentationKind(int uid, string defIdFallback = null)
        {
            if (uid > 0 && TryRead(uid, out var read) && read.Kind != CardPresentationKind.Unknown)
            {
                return read.Kind;
            }

            return ResolvePresentationKindFromDefId(defIdFallback);
        }

        /// <summary>
        /// 按 DefId 推断表现种。skill.* 返回 Unknown（不为 PlayerSkill 开 Spawn 卡面）。
        /// </summary>
        public static CardPresentationKind ResolvePresentationKindFromDefId(string defId)
        {
            return InferPresentationKind(defId);
        }

        private static CardPresentationKind InferPresentationKind(string defId)
        {
            if (string.IsNullOrEmpty(defId))
            {
                return CardPresentationKind.Unknown;
            }

            if (defId.StartsWith("monster.", System.StringComparison.Ordinal))
            {
                return CardPresentationKind.Monster;
            }

            if (defId.StartsWith("trap.", System.StringComparison.Ordinal))
            {
                return CardPresentationKind.Trap;
            }

            if (defId.StartsWith("help.", System.StringComparison.Ordinal)
                || defId.StartsWith("player.", System.StringComparison.Ordinal))
            {
                return CardPresentationKind.HelpCard;
            }

            if (defId.StartsWith("relic.", System.StringComparison.Ordinal))
            {
                return CardPresentationKind.Relic;
            }

            if (defId.StartsWith("avatar.", System.StringComparison.Ordinal))
            {
                return CardPresentationKind.Avatar;
            }

            if (string.Equals(defId, "Attack", System.StringComparison.Ordinal)
                || string.Equals(defId, "Armor", System.StringComparison.Ordinal)
                || string.Equals(defId, "Hp", System.StringComparison.Ordinal))
            {
                return CardPresentationKind.Item;
            }

            return CardPresentationKind.Unknown;
        }

        private static CardPresentationKind ToPresentationKind(CardKind kind)
        {
            return (CardPresentationKind)(int)kind;
        }
    }
}

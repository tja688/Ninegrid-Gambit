using NineGrid.Cards;
using NineGrid.Cards.Presentation;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using QFramework;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NineGrid.Flow
{
    public struct CardPresentationRead
    {
        public CardPresentationKind Kind;
        public int Attack;
        public int Hp;
        public int Armor;
        public string DefId;
    }

    /// <summary>
    /// 从 Core/Content 构建胖投影，经编排侧 Commit 出口提交到卡牌底盘宿主。
    /// 正式路径：Build → CommitPresentation → ApplyPresentation → Binder；禁止旁路 Set* 直刷卡面。
    /// SyncAllSpawnedCards 对账已提交投影，不直读最新 Core。
    /// </summary>
    public static class CoreCardPresentationMapper
    {
        private const string VisualAssetFolder = "Assets/Arts/ContentVisual/";

        private static ContentVisualCatalog _visualCatalog;
        private static CardFrameStyleCatalog _frameStyleCatalog;
        private static ContentVisualSpriteCatalogSet _spriteCatalogs;
        private static bool _visualsResolved;

        public static void EnsureContentCatalogLoaded()
        {
            var arch = NineGridArchitecture.Current;
            var content = arch.GetSystem<IContentSystem>();
            if (content.HasCatalog)
            {
                return;
            }

            var catalog = ContentCatalogBootstrap.Load();
            content.Load(catalog);
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
                // 卡面护甲 = 当前护甲（本关临时资源）；PlayerInfoText 防御栏走有效护甲。
                Armor = StatArmorUtility.GetCurrentArmor(card),
                DefId = card.DefId ?? string.Empty,
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

        public static void UpdateAvatarDebugText(UiPanelRouter router, bool animate = true)
        {
            // Card Info Text 已专用于悬停描述；玩家数值走 PlayerInfoText（Hp/Attack/防御=有效护甲/Gold/Name）。
            _ = router;
            var hud = PlayerInfoHudPresenter.TryGetInstance() ?? PlayerInfoHudPresenter.Instance;
            hud.SyncFromCore(animate);
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
                FaceUp = true,
            };

            ApplyVisualFields(snapshot, read.DefId, read.Kind);
            return snapshot;
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

            // 重叠字段单真相：有 CardPresentation JSON 则整段用 JSON（不混读 SO/Luban 同名字段）。
            // 缺图/缺名时在 JSON 分支内用 defId 填空，不回落半套旧源。
            if (TryApplyJsonPresentation(snapshot, defId))
            {
                return;
            }

            // 无 JSON：Luban ContentVisual + SO 图作为过渡 fallback。
            EnsureVisualsLoaded();

            var arch = NineGridArchitecture.Current;
            var content = arch.GetSystem<IContentSystem>();
            if (content.HasCatalog
                && _visualCatalog != null
                && ContentVisualResolver.TryResolve(
                    defId,
                    content.Catalog,
                    _visualCatalog,
                    _frameStyleCatalog,
                    _spriteCatalogs,
                    out var resolved))
            {
                // Luban/SO fallback：名字 + 主图标；显式装配的 Face/Back 覆盖。
                snapshot.DisplayName = resolved.DisplayName ?? string.Empty;
                snapshot.MainIcon = resolved.Icon;
                snapshot.FaceBackground = resolved.Face;
                snapshot.BackBorder = resolved.BackBorder;
                snapshot.BackShirt = resolved.BackShirt;
                snapshot.BackLogo = resolved.BackLogo;
                return;
            }

            if (TryGetSpritesDirect(defId, kind, out var directIcon, out _))
            {
                snapshot.MainIcon = directIcon;
            }

            if (string.IsNullOrEmpty(snapshot.DisplayName))
            {
                snapshot.DisplayName = defId;
            }
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

            if (!string.IsNullOrWhiteSpace(dto.description))
            {
                snapshot.BasicDescription = dto.description;
            }

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

            // stats 是内容作者定义的出生值，经 Catalog 造卡用；表现层投影提交不读 JSON stats。
            return true;
        }

        private static void ApplyJsonSprite(ref Sprite target, string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return;
            }

            var sprite = CardPresentationSpritePath.LoadSprite(assetPath);
            if (sprite != null)
            {
                target = sprite;
            }
        }

        private static bool TryGetSpritesDirect(
            string defId,
            CardPresentationKind kind,
            out Sprite icon,
            out Sprite face)
        {
            icon = null;
            face = null;
            if (_spriteCatalogs == null)
            {
                return false;
            }

            var visualKind = InferVisualKind(defId, kind);
            return _spriteCatalogs.TryGet(visualKind, defId, out icon, out face);
        }

        private static ContentVisualKind InferVisualKind(string defId, CardPresentationKind kind)
        {
            if (!string.IsNullOrEmpty(defId))
            {
                if (defId.StartsWith("monster.", System.StringComparison.Ordinal))
                {
                    return ContentVisualKind.Monster;
                }

                if (defId.StartsWith("help.", System.StringComparison.Ordinal))
                {
                    return ContentVisualKind.HelpCard;
                }

                if (defId.StartsWith("player.", System.StringComparison.Ordinal))
                {
                    return ContentVisualKind.HelpCard;
                }

                if (defId.StartsWith("relic.", System.StringComparison.Ordinal))
                {
                    return ContentVisualKind.Relic;
                }

                if (defId.StartsWith("skill.", System.StringComparison.Ordinal))
                {
                    return ContentVisualKind.Skill;
                }

                if (defId.StartsWith("avatar.", System.StringComparison.Ordinal))
                {
                    return ContentVisualKind.Avatar;
                }

                if (IsChoiceOptionDefId(defId))
                {
                    return ContentVisualKind.ChoiceOption;
                }
            }

            switch (kind)
            {
                case CardPresentationKind.Monster:
                    return ContentVisualKind.Monster;
                case CardPresentationKind.HelpCard:
                case CardPresentationKind.PlayerCard:
                case CardPresentationKind.Item:
                    return ContentVisualKind.HelpCard;
                case CardPresentationKind.Relic:
                    return ContentVisualKind.Relic;
                case CardPresentationKind.Avatar:
                    return ContentVisualKind.Avatar;
                default:
                    return ContentVisualKind.Unknown;
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
            var visualKind = InferVisualKind(defId, CardPresentationKind.Unknown);
            switch (visualKind)
            {
                case ContentVisualKind.Monster:
                    return CardPresentationKind.Monster;
                case ContentVisualKind.HelpCard:
                    return CardPresentationKind.HelpCard;
                case ContentVisualKind.Relic:
                    return CardPresentationKind.Relic;
                case ContentVisualKind.Avatar:
                    return CardPresentationKind.Avatar;
                case ContentVisualKind.ChoiceOption:
                    return CardPresentationKind.Item;
                default:
                    return CardPresentationKind.Unknown;
            }
        }

        private static bool IsChoiceOptionDefId(string defId)
        {
            return string.Equals(defId, "Attack", System.StringComparison.Ordinal)
                   || string.Equals(defId, "Armor", System.StringComparison.Ordinal)
                   || string.Equals(defId, "Hp", System.StringComparison.Ordinal);
        }

        private static void EnsureVisualsLoaded()
        {
            if (_visualsResolved)
            {
                return;
            }

            _visualsResolved = true;
            ContentVisualBootstrap.TryLoad(
                ContentVisualBootstrap.ResolveLubanDataDirectory(),
                out _visualCatalog,
                out _frameStyleCatalog);

            _spriteCatalogs = ContentVisualSpriteCatalogBootstrapSO.TryLoadCatalogSet();
#if UNITY_EDITOR
            if (_spriteCatalogs == null)
            {
                _spriteCatalogs = LoadSpriteCatalogsEditorFallback();
            }
#endif
        }

#if UNITY_EDITOR
        private static ContentVisualSpriteCatalogSet LoadSpriteCatalogsEditorFallback()
        {
            return new ContentVisualSpriteCatalogSet
            {
                helpCards = AssetDatabase.LoadAssetAtPath<HelpCardVisualCatalogSO>(
                    VisualAssetFolder + "HelpCardVisualCatalog.asset"),
                monsters = AssetDatabase.LoadAssetAtPath<MonsterVisualCatalogSO>(
                    VisualAssetFolder + "MonsterVisualCatalog.asset"),
                relics = AssetDatabase.LoadAssetAtPath<RelicVisualCatalogSO>(
                    VisualAssetFolder + "RelicVisualCatalog.asset"),
                skills = AssetDatabase.LoadAssetAtPath<SkillVisualCatalogSO>(
                    VisualAssetFolder + "SkillVisualCatalog.asset"),
                misc = AssetDatabase.LoadAssetAtPath<MiscVisualCatalogSO>(
                    VisualAssetFolder + "MiscVisualCatalog.asset"),
                choiceOptions = AssetDatabase.LoadAssetAtPath<ChoiceOptionVisualCatalogSO>(
                    VisualAssetFolder + "ChoiceOptionVisualCatalog.asset"),
            };
        }
#endif

        private static CardPresentationKind ToPresentationKind(CardKind kind)
        {
            return (CardPresentationKind)(int)kind;
        }
    }
}

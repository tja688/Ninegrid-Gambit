using NineGrid.Cards;
using NineGrid.Content;
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
    /// 表现层只读 Core 模型，同步 StandardCardView 数值与 ContentVisual 图标（不走 Snapshot）。
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
                Armor = statSystem.GetEffectiveInt(card, StatId.Armor),
                DefId = card.DefId ?? string.Empty,
            };
            return true;
        }

        public static void ApplyToManagedCard(ManagedCard card, bool animate = false)
        {
            if (card?.View == null || !TryRead(card.Uid, out var read))
            {
                return;
            }

            card.CoreKind = read.Kind;
            card.View.SetAttack(read.Attack, animate);
            card.View.SetHealth(read.Hp, animate);
            card.View.SetArmor(read.Armor, animate);
            ApplyVisuals(card.View, read.DefId, read.Kind);
        }

        /// <summary>
        /// 按 defId 套用 ContentVisual（Bounce 选卡 / 无 Core uid 的展示卡）。
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

            if (kindHint != CardPresentationKind.Unknown)
            {
                card.CoreKind = kindHint;
            }
            else
            {
                card.CoreKind = InferPresentationKind(card.DefId);
            }

            if (clearCombatStats)
            {
                card.View.SetAttack(0, animate: false);
                card.View.SetHealth(0, animate: false);
                card.View.SetArmor(0, animate: false);
            }

            ApplyVisuals(card.View, card.DefId, card.CoreKind);
        }

        public static void SyncAllSpawnedCards()
        {
            var cardManager = CardManagerSingleton.TryGetInstance();
            if (cardManager == null)
            {
                return;
            }

            foreach (var pair in cardManager.CardsByUid)
            {
                ApplyToManagedCard(pair.Value, animate: false);
            }
        }

        public static void UpdateAvatarDebugText(UiPanelRouter router, bool animate = true)
        {
            // Card Info Text 已专用于悬停描述；玩家数值走 PlayerInfoText（Hp/Attack/Armor/Gold/Name）。
            _ = router;
            var hud = PlayerInfoHudPresenter.TryGetInstance() ?? PlayerInfoHudPresenter.Instance;
            hud.SyncFromCore(animate);
        }

        private static void ApplyVisuals(StandardCardView view, string defId, CardPresentationKind kind)
        {
            if (view == null || string.IsNullOrEmpty(defId))
            {
                return;
            }

            EnsureVisualsLoaded();

            Sprite icon = null;
            Sprite face = null;
            var frameColor = ContentColor.White;
            var hasVisual = false;

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
                icon = resolved.Icon;
                face = resolved.Face;
                frameColor = resolved.FrameColor;
                hasVisual = true;
            }

            if (!hasVisual && TryGetSpritesDirect(defId, kind, out var directIcon, out var directFace))
            {
                icon = directIcon;
                face = directFace;
                hasVisual = true;
                frameColor = ResolveFrameColorFallback(defId, kind, content);
            }

            if (!hasVisual)
            {
                return;
            }

            if (face != null)
            {
                view.SetCardFace(face);
            }

            if (icon != null)
            {
                view.SetMainIcon(icon);
            }
            else
            {
                view.SetMainIcon(null);
            }

            var color = frameColor;
            view.SetFrameColor(new Color(color.R, color.G, color.B, color.A));
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

        private static ContentColor ResolveFrameColorFallback(
            string defId,
            CardPresentationKind kind,
            IContentSystem content)
        {
            if (!content.HasCatalog || _frameStyleCatalog == null)
            {
                return ContentColor.White;
            }

            var visualKind = InferVisualKind(defId, kind);
            ContentVisualDefinition visual = null;
            if (_visualCatalog != null)
            {
                _visualCatalog.TryGet(defId, out visual);
            }

            if (visual == null)
            {
                visual = new ContentVisualDefinition(defId, visualKind, string.Empty);
            }

            var frameStyleId = ContentVisualResolver.ResolveFrameStyleId(
                defId,
                visual,
                content.Catalog);
            return ContentVisualResolver.ResolveFrameColor(frameStyleId, _frameStyleCatalog);
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

#if UNITY_EDITOR
            _spriteCatalogs = new ContentVisualSpriteCatalogSet
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
#endif
        }

        private static CardPresentationKind ToPresentationKind(CardKind kind)
        {
            return (CardPresentationKind)(int)kind;
        }
    }
}

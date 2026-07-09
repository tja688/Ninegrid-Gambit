using NineGrid.Cards;
using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using QFramework;
using TMPro;
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
            ApplyVisuals(card.View, read.DefId);
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

        public static void UpdateAvatarDebugText(UiPanelRouter router)
        {
            if (router == null)
            {
                return;
            }

            router.EnsureBindings();
            var root = router.InGameInfoText;
            if (root == null)
            {
                return;
            }

            var text = root.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text == null)
            {
                return;
            }

            var arch = NineGridArchitecture.Current;
            var avatarUid = arch.GetModel<BoardModel>().AvatarUid.Value;
            if (avatarUid <= 0 || !TryRead(avatarUid, out var read))
            {
                text.text = string.Empty;
                return;
            }

            text.text = $"Avatar HP {read.Hp} / Armor {read.Armor}";
        }

        private static void ApplyVisuals(StandardCardView view, string defId)
        {
            if (view == null || string.IsNullOrEmpty(defId))
            {
                return;
            }

            EnsureVisualsLoaded();
            var arch = NineGridArchitecture.Current;
            var content = arch.GetSystem<IContentSystem>();
            if (!content.HasCatalog || _visualCatalog == null)
            {
                return;
            }

            if (!ContentVisualResolver.TryResolve(
                    defId,
                    content.Catalog,
                    _visualCatalog,
                    _frameStyleCatalog,
                    _spriteCatalogs,
                    out var resolved))
            {
                return;
            }

            var icon = resolved.Icon ?? resolved.Face;
            if (icon != null)
            {
                view.SetMainIcon(icon);
            }

            var frameColor = resolved.FrameColor;
            view.SetFrameColor(new Color(frameColor.R, frameColor.G, frameColor.B, frameColor.A));
        }

        private static void EnsureVisualsLoaded()
        {
            if (_visualsResolved)
            {
                return;
            }

            _visualsResolved = true;
            ContentVisualBootstrap.TryLoad(null, out _visualCatalog, out _frameStyleCatalog);

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
            };
#endif
        }

        private static CardPresentationKind ToPresentationKind(CardKind kind)
        {
            return (CardPresentationKind)(int)kind;
        }
    }
}

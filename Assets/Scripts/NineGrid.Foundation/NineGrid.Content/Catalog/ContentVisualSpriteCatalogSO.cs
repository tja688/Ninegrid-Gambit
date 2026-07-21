using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Content
{
    [Serializable]
    public sealed class ContentVisualSpriteEntry
    {
        [Tooltip("与 Core defId / content_visual.content_id 同值，例如 help.fireball。")]
        public string contentId;

        [Tooltip("主图标 Sprite（槽代号 Main_Icon）；留空则运行时/预览回退卡面模板兜底。")]
        public Sprite icon;

        [Tooltip("卡面背景 Sprite（槽代号 Face_Background）；留空则回退卡面模板兜底。")]
        public Sprite face;

        [Tooltip("卡背·背框 Sprite（槽代号 Back_Border）；留空则回退该卡面模板兜底，无全局卡背源。")]
        public Sprite backBorder;

        [Tooltip("卡背·背纹 Sprite（槽代号 Back_Shirt）；留空则回退该卡面模板兜底，无全局卡背源。")]
        public Sprite backShirt;

        [Tooltip("卡背·Logo Sprite（槽代号 Back_Logo）；留空则回退该卡面模板兜底，无全局卡背源。")]
        public Sprite backLogo;
    }

    public interface IContentVisualSpriteProvider
    {
        bool TryGet(ContentVisualKind kind, string contentId, out Sprite icon, out Sprite face);

        bool TryGetDirectSlots(
            ContentVisualKind kind,
            string contentId,
            out ContentVisualDirectSlotSprites slots);
    }

    /// <summary>
    /// 内容侧直接暴露装配项：主图标 / 卡面背景 / 卡背三件套（无全局卡背源）。
    /// </summary>
    public struct ContentVisualDirectSlotSprites
    {
        public Sprite MainIcon;
        public Sprite FaceBackground;
        public Sprite BackBorder;
        public Sprite BackShirt;
        public Sprite BackLogo;

        public bool HasAny =>
            MainIcon != null ||
            FaceBackground != null ||
            BackBorder != null ||
            BackShirt != null ||
            BackLogo != null;
    }

    public abstract class ContentVisualSpriteCatalogSO : ScriptableObject
    {
        [SerializeField]
        [Tooltip("按 contentId 索引的图标/卡面条目。")]
        private List<ContentVisualSpriteEntry> entries = new List<ContentVisualSpriteEntry>();

        [Header("类型默认设置")]
        [SerializeField]
        [Tooltip("本类型默认卡面；仅作编辑器「覆盖应用」的源，不会在运行时持续覆盖各条目 face。")]
        private Sprite defaultFace;

        [SerializeField]
        [Tooltip("本类型主图标缺失时的运行时回退 Sprite；条目已配置 icon 时不使用。")]
        private Sprite fallbackIcon;

        private Dictionary<string, ContentVisualSpriteEntry> mLookup;

        public abstract ContentVisualKind Kind { get; }

        public IReadOnlyList<ContentVisualSpriteEntry> Entries
        {
            get { return entries; }
        }

        public Sprite DefaultFace
        {
            get { return defaultFace; }
            set { defaultFace = value; }
        }

        public Sprite FallbackIcon
        {
            get { return fallbackIcon; }
            set { fallbackIcon = value; }
        }

        public bool TryGet(string contentId, out Sprite icon, out Sprite face)
        {
            ContentVisualDirectSlotSprites slots;
            if (!TryGetDirectSlots(contentId, out slots))
            {
                icon = null;
                face = null;
                return false;
            }

            icon = slots.MainIcon;
            face = slots.FaceBackground;
            return icon != null || face != null;
        }

        public bool TryGetDirectSlots(string contentId, out ContentVisualDirectSlotSprites slots)
        {
            slots = default;
            if (string.IsNullOrEmpty(contentId))
            {
                return false;
            }

            EnsureLookup();
            ContentVisualSpriteEntry entry;
            if (!mLookup.TryGetValue(contentId, out entry) || entry == null)
            {
                return false;
            }

            slots = new ContentVisualDirectSlotSprites
            {
                MainIcon = entry.icon,
                FaceBackground = entry.face,
                BackBorder = entry.backBorder,
                BackShirt = entry.backShirt,
                BackLogo = entry.backLogo
            };
            return slots.HasAny;
        }

        /// <summary>
        /// 解析条目图；icon 为空时回退到类型 FallbackIcon（face 不自动回退 DefaultFace）。
        /// 卡背三件套不做类型级全局回退（无全局卡背源；模板兜底在卡面侧）。
        /// </summary>
        public bool TryGetWithFallback(string contentId, out Sprite icon, out Sprite face)
        {
            ContentVisualDirectSlotSprites slots;
            var found = TryGetDirectSlotsWithFallback(contentId, out slots);
            icon = slots.MainIcon;
            face = slots.FaceBackground;
            return found && (icon != null || face != null);
        }

        public bool TryGetDirectSlotsWithFallback(string contentId, out ContentVisualDirectSlotSprites slots)
        {
            var found = TryGetDirectSlots(contentId, out slots);
            if (slots.MainIcon == null && fallbackIcon != null)
            {
                slots.MainIcon = fallbackIcon;
                found = true;
            }

            return found && slots.HasAny;
        }

        public void SetSprites(string contentId, Sprite icon, Sprite face)
        {
            SetDirectSlots(contentId, new ContentVisualDirectSlotSprites
            {
                MainIcon = icon,
                FaceBackground = face
            }, preserveUnspecifiedBack: true);
        }

        public void SetDirectSlots(string contentId, ContentVisualDirectSlotSprites slots)
        {
            SetDirectSlots(contentId, slots, preserveUnspecifiedBack: false);
        }

        public void SetDirectSlots(
            string contentId,
            ContentVisualDirectSlotSprites slots,
            bool preserveUnspecifiedBack)
        {
            if (string.IsNullOrEmpty(contentId))
            {
                return;
            }

            EnsureLookup();
            ContentVisualSpriteEntry entry;
            if (!mLookup.TryGetValue(contentId, out entry) || entry == null)
            {
                entry = new ContentVisualSpriteEntry { contentId = contentId };
                entries.Add(entry);
                mLookup[contentId] = entry;
            }

            entry.icon = slots.MainIcon;
            entry.face = slots.FaceBackground;
            if (!preserveUnspecifiedBack)
            {
                entry.backBorder = slots.BackBorder;
                entry.backShirt = slots.BackShirt;
                entry.backLogo = slots.BackLogo;
            }
        }

        public void ClearSprites(string contentId)
        {
            if (string.IsNullOrEmpty(contentId))
            {
                return;
            }

            EnsureLookup();
            ContentVisualSpriteEntry entry;
            if (!mLookup.TryGetValue(contentId, out entry) || entry == null)
            {
                return;
            }

            entry.icon = null;
            entry.face = null;
            entry.backBorder = null;
            entry.backShirt = null;
            entry.backLogo = null;
        }

        public bool HasIcon(string contentId)
        {
            Sprite icon;
            Sprite face;
            return TryGet(contentId, out icon, out face) && icon != null;
        }

        public bool HasFace(string contentId)
        {
            Sprite icon;
            Sprite face;
            return TryGet(contentId, out icon, out face) && face != null;
        }

        public void EnsureEntry(string contentId)
        {
            if (string.IsNullOrEmpty(contentId))
            {
                return;
            }

            EnsureLookup();
            if (mLookup.ContainsKey(contentId))
            {
                return;
            }

            var entry = new ContentVisualSpriteEntry { contentId = contentId };
            entries.Add(entry);
            mLookup[contentId] = entry;
        }

        public void InvalidateLookup()
        {
            mLookup = null;
        }

        /// <summary>
        /// 一次性把 DefaultFace 写入本 Catalog 全部条目的 face（初始化用，非持续覆盖）。
        /// </summary>
        public int ApplyDefaultFaceToAllEntries()
        {
            if (defaultFace == null || entries == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.contentId))
                {
                    continue;
                }

                entry.face = defaultFace;
                count++;
            }

            InvalidateLookup();
            return count;
        }

        private void OnEnable()
        {
            mLookup = null;
        }

        private void EnsureLookup()
        {
            if (mLookup != null)
            {
                return;
            }

            mLookup = new Dictionary<string, ContentVisualSpriteEntry>(StringComparer.Ordinal);
            if (entries == null)
            {
                entries = new List<ContentVisualSpriteEntry>();
                return;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.contentId))
                {
                    continue;
                }

                mLookup[entry.contentId] = entry;
            }
        }
    }

    [Serializable]
    public sealed class ContentVisualSpriteCatalogSet : IContentVisualSpriteProvider
    {
        [Tooltip("帮助卡图标/卡面 Catalog SO。")]
        public HelpCardVisualCatalogSO helpCards;

        [Tooltip("怪物卡图标/卡面 Catalog SO。")]
        public MonsterVisualCatalogSO monsters;

        [Tooltip("遗物图标/卡面 Catalog SO。")]
        public RelicVisualCatalogSO relics;

        [Tooltip("技能图标/卡面 Catalog SO。")]
        public SkillVisualCatalogSO skills;

        [Tooltip("Avatar/Room/MonsterDeck 等杂项 Catalog SO。")]
        public MiscVisualCatalogSO misc;

        [Tooltip("属性三选一等 ChoiceOption Catalog SO。")]
        public ChoiceOptionVisualCatalogSO choiceOptions;

        public bool TryGet(ContentVisualKind kind, string contentId, out Sprite icon, out Sprite face)
        {
            icon = null;
            face = null;
            var catalog = ResolveCatalog(kind);
            return catalog != null && catalog.TryGetWithFallback(contentId, out icon, out face);
        }

        public bool TryGetDirectSlots(
            ContentVisualKind kind,
            string contentId,
            out ContentVisualDirectSlotSprites slots)
        {
            // 直暴露槽：仅返回条目自定值；缺省回退卡面模板（非类型 FallbackIcon / 非全局卡背）。
            slots = default;
            var catalog = ResolveCatalog(kind);
            return catalog != null && catalog.TryGetDirectSlots(contentId, out slots);
        }

        public ContentVisualSpriteCatalogSO ResolveCatalog(ContentVisualKind kind)
        {
            switch (kind)
            {
                case ContentVisualKind.HelpCard:
                    return helpCards;
                case ContentVisualKind.Monster:
                    return monsters;
                case ContentVisualKind.Relic:
                    return relics;
                case ContentVisualKind.Skill:
                    return skills;
                case ContentVisualKind.ChoiceOption:
                    return choiceOptions;
                case ContentVisualKind.Avatar:
                case ContentVisualKind.Room:
                case ContentVisualKind.MonsterDeck:
                    return misc;
                default:
                    return null;
            }
        }
    }
}

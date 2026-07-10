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

        [Tooltip("主图标 Sprite；留空表示未配置。")]
        public Sprite icon;

        [Tooltip("卡面/立绘 Sprite；留空表示未配置或复用 icon。")]
        public Sprite face;
    }

    public interface IContentVisualSpriteProvider
    {
        bool TryGet(ContentVisualKind kind, string contentId, out Sprite icon, out Sprite face);
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
            icon = null;
            face = null;
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

            icon = entry.icon;
            face = entry.face;
            return icon != null || face != null;
        }

        /// <summary>
        /// 解析条目图；icon 为空时回退到类型 FallbackIcon（face 不自动回退 DefaultFace）。
        /// </summary>
        public bool TryGetWithFallback(string contentId, out Sprite icon, out Sprite face)
        {
            var found = TryGet(contentId, out icon, out face);
            if (icon == null && fallbackIcon != null)
            {
                icon = fallbackIcon;
                found = true;
            }

            return found && (icon != null || face != null);
        }

        public void SetSprites(string contentId, Sprite icon, Sprite face)
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

            entry.icon = icon;
            entry.face = face;
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

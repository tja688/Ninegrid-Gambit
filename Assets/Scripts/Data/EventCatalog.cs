using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Data
{
    /// <summary>事件池类型 —— 对应设计文档中的抽取权重表。</summary>
    public enum EventPoolKind
    {
        HighCommon,     // 高常见：90% / 10% / 0%
        MidRare,        // 中概率稀有：49% / 50% / 1%
        LowRare,        // 低概率稀有：80% / 20% / 0%
        LowLegendary    // 低概率传说：0% / 90% / 10%
    }

    /// <summary>单个事件池的 tier 权重（百分比，合计 100）。</summary>
    [Serializable]
    public sealed class EventPoolWeights
    {
        [SerializeField] EventPoolKind poolKind;
        [Range(0, 100)] [SerializeField] int commonPercent = 90;
        [Range(0, 100)] [SerializeField] int rarePercent = 10;
        [Range(0, 100)] [SerializeField] int legendaryPercent;

        public EventPoolKind PoolKind => poolKind;
        public int CommonPercent => commonPercent;
        public int RarePercent => rarePercent;
        public int LegendaryPercent => legendaryPercent;
    }

    /// <summary>航段事件节点与事件池的对应关系。</summary>
    [Serializable]
    public sealed class RouteEventPoolBinding
    {
        [SerializeField] string routeNode = "1-1";
        [SerializeField] EventPoolKind poolKind = EventPoolKind.HighCommon;

        public string RouteNode => routeNode;
        public EventPoolKind PoolKind => poolKind;
    }

    /// <summary>事件数据条目（嵌入 EventCatalog 使用）。</summary>
    [Serializable]
    public sealed class EventDataEntry
    {
        [Header("基础信息")]
        [SerializeField] string eventId;
        [SerializeField] string displayName;
        [SerializeField] EventTier tier;
        [SerializeField] EventChoiceKind choiceKind;

        [Header("文案")]
        [SerializeField, TextArea(2, 5)] string narrative;
        [SerializeField, TextArea(1, 4)] string effectDescription;

        [Header("表现")]
        [SerializeField] Sprite icon;

        public string EventId => eventId;
        public string DisplayName => displayName;
        public EventTier Tier => tier;
        public EventChoiceKind ChoiceKind => choiceKind;
        public string Narrative => narrative;
        public string EffectDescription => effectDescription;
        public Sprite Icon => icon;
    }

    /// <summary>航行事件目录 —— 持有全部事件数据、池权重与节点映射。</summary>
    [CreateAssetMenu(menuName = "NineGrid/Data/Event Catalog", fileName = "EventCatalog")]
    public sealed class EventCatalog : ScriptableObject
    {
        [SerializeField] Sprite defaultIcon;
        [SerializeField] List<EventDataEntry> entries = new();
        [SerializeField] List<EventPoolWeights> poolWeights = new();
        [SerializeField] List<RouteEventPoolBinding> routeBindings = new();

        public Sprite DefaultIcon => defaultIcon;
        public IReadOnlyList<EventDataEntry> Entries => entries;
        public IReadOnlyList<EventPoolWeights> PoolWeights => poolWeights;
        public IReadOnlyList<RouteEventPoolBinding> RouteBindings => routeBindings;

        public EventDataEntry Get(string eventId)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].EventId == eventId)
                {
                    return entries[i];
                }
            }

            return null;
        }

        public bool TryGet(string eventId, out EventDataEntry entry)
        {
            entry = Get(eventId);
            return entry != null;
        }

        public IReadOnlyList<EventDataEntry> GetByTier(EventTier tier)
        {
            var result = new List<EventDataEntry>();
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].Tier == tier)
                {
                    result.Add(entries[i]);
                }
            }

            return result;
        }

        public EventPoolWeights GetPoolWeights(EventPoolKind poolKind)
        {
            for (var i = 0; i < poolWeights.Count; i++)
            {
                if (poolWeights[i].PoolKind == poolKind)
                {
                    return poolWeights[i];
                }
            }

            return null;
        }

        public EventPoolKind GetPoolKindForRouteNode(string routeNode)
        {
            for (var i = 0; i < routeBindings.Count; i++)
            {
                if (routeBindings[i].RouteNode == routeNode)
                {
                    return routeBindings[i].PoolKind;
                }
            }

            return EventPoolKind.HighCommon;
        }

        public Sprite ResolveIcon(EventDataEntry entry)
        {
            if (entry != null && entry.Icon != null)
            {
                return entry.Icon;
            }

            return defaultIcon;
        }

        public Sprite ResolveIcon(EventDataSO data)
        {
            if (data != null && data.Icon != null)
            {
                return data.Icon;
            }

            return defaultIcon;
        }
    }
}

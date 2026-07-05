using UnityEngine;

namespace NineGrid.Data
{
    /// <summary>事件稀有度 —— 决定进入哪一档事件池。</summary>
    public enum EventTier
    {
        Common,    // 常见
        Rare,      // 稀有
        Legendary  // 传说
    }

    /// <summary>事件是否需要玩家做选择，以及选择类型。</summary>
    public enum EventChoiceKind
    {
        None,               // 无选项，直接生效
        PickOre,            // 从矿舱选择矿石
        PickForgingStation, // 选择铸造台
        PickVein            // 选择矿脉
    }

    /// <summary>
    /// 单个航行事件 ScriptableObject —— 可在 Inspector 中独立引用。
    /// 图标留空时由 <see cref="EventCatalog"/> 或 <see cref="DefaultIconPath"/> 回退。
    /// </summary>
    [CreateAssetMenu(menuName = "NineGrid/Data/Event Data", fileName = "EventData")]
    public sealed class EventDataSO : ScriptableObject
    {
        public const string DefaultIconPath = "Assets/Arts/Images/Png/Items/script.png";

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
}

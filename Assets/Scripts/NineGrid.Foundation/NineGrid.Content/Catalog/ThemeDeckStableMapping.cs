using System.Collections.Generic;

namespace NineGrid.Content
{
    /// <summary>
    /// 七套正式主题卡组的稳定槽位映射（#127 内容护栏 / ADR-0029）。
    /// 以策划七套机制表（Assets/Docs/九宫格登神/04-敌人侧卡牌信息/怪物卡.md）
    /// 与人工确认的当前内容数据为快照；deckId / contentId 是历史残留不透明主键
    /// （ADR-0014），不代表主题语义；displayName 可变，不属于本契约。
    /// 本契约被 <see cref="ThemeDeckMappingVerifier"/>（槽位映射正确性）与
    /// <see cref="ThemeDeckFormalReadiness"/>（正式可达性前置报告）消费。
    /// </summary>
    public static class ThemeDeckStableMapping
    {
        /// <summary>每套的序列槽位数（sequence 1–5）。</summary>
        public const int SequenceCount = 5;

        public static readonly IReadOnlyList<ThemeDeckStableEntry> Entries = new[]
        {
            new ThemeDeckStableEntry(
                "deck.dragon",
                "monster.melee_3",
                "monster.big_skeleton_reborn",
                "monster.headless_skeleton",
                "monster.skull_head",
                "monster.beggar"),
            new ThemeDeckStableEntry(
                "deck.orc_legion",
                "monster.big_stone",
                "monster.sky_eye",
                "monster.fire_dragon",
                "monster.skeleton_taunter",
                "monster.skeleton_king"),
            new ThemeDeckStableEntry(
                "deck.insect",
                "monster.wandering_child",
                "monster.smuggler",
                "monster.big_skeleton",
                "monster.world_turning_hand",
                "monster.rogue"),
            new ThemeDeckStableEntry(
                "deck.skeleton_legion",
                "monster.smart_orc",
                "monster.brainless_orc",
                "monster.young_orc",
                "monster.veteran_orc",
                "monster.orc_commander"),
            new ThemeDeckStableEntry(
                "deck.smallanimal",
                "monster.ringleader",
                "monster.pickpocket",
                "monster.hoodlum",
                "monster.thug",
                "monster.vagrant"),
            new ThemeDeckStableEntry(
                "deck.stone_legion",
                "monster.dragon_cult_leader",
                "monster.salamander",
                "monster.void_lost",
                "monster.dragon_follower",
                "monster.shelter_stone"),
            new ThemeDeckStableEntry(
                "deck.void",
                "monster.bone_club_skeleton",
                "monster.bone_courier",
                "monster.fire_priest",
                "monster.fire_swallower",
                "monster.fire_bather"),
        };

        public static bool TryGet(string deckId, out ThemeDeckStableEntry entry)
        {
            for (var i = 0; i < Entries.Count; i++)
            {
                if (string.Equals(Entries[i].DeckId, deckId, System.StringComparison.OrdinalIgnoreCase))
                {
                    entry = Entries[i];
                    return true;
                }
            }

            entry = null;
            return false;
        }
    }

    /// <summary>一套正式主题卡组的稳定槽位声明：deckId + sequence 1–5 的 contentId。</summary>
    public sealed class ThemeDeckStableEntry
    {
        private readonly string[] mSequenceContentIds;

        public ThemeDeckStableEntry(string deckId, params string[] sequenceContentIds)
        {
            DeckId = deckId ?? string.Empty;
            mSequenceContentIds = sequenceContentIds ?? new string[0];
        }

        public string DeckId { get; }

        /// <summary>长度恒为 <see cref="ThemeDeckStableMapping.SequenceCount"/>；下标 i 对应 sequence i+1。</summary>
        public IReadOnlyList<string> SequenceContentIds
        {
            get { return mSequenceContentIds; }
        }

        /// <summary>sequence 1–5 → contentId；越界返回空串。</summary>
        public string GetContentId(int sequence)
        {
            if (sequence < 1 || sequence > mSequenceContentIds.Length)
            {
                return string.Empty;
            }

            return mSequenceContentIds[sequence - 1];
        }
    }
}

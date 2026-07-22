using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 战斗编排路由表：(intent, playerId, monsterId) → Encounter Profile。
    /// </summary>
    [CreateAssetMenu(
        fileName = "Battle_Encounter_Catalog",
        menuName = "NineGrid/Cards/Battle/Encounter Catalog")]
    public sealed class BattleEncounterCatalogSO : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("本条目适用意图。")]
            public BattleIntent intent = BattleIntent.Attack;

            [Tooltip("玩家 DefId；通配 *。")]
            public string playerContentId = BattleParticipantIds.Wildcard;

            [Tooltip("怪物 DefId；通配 *。熊怪特殊手感填具体 ID。")]
            public string monsterContentId = BattleParticipantIds.Wildcard;

            [Tooltip("命中后使用的战斗编排预设；不可为空。")]
            public BattleEncounterProfileSO profile;
        }

        [TextArea(2, 6)]
        [Tooltip("Catalog 语义说明：默认覆盖范围、如何追加变体。供 AI 阅读。")]
        [SerializeField] private string description =
            "默认战斗编排路由表。新增玩家×怪物变体时追加 Entry，勿改场景 Rig 魔法数，勿把战斗 timing 塞进 CardEffect。";

        [Tooltip("路由条目列表。解析优先级：精确 > 玩家通配 > 怪物通配 > 双通配。")]
        [SerializeField] private List<Entry> entries = new();

        public string Description => description ?? string.Empty;

        public IReadOnlyList<Entry> Entries => entries;

        public bool HasDescription => !string.IsNullOrWhiteSpace(description);

        public BattleEncounterProfileSO Resolve(
            BattleIntent intent,
            string playerContentId,
            string monsterContentId)
        {
            return BattlePresentationRouter.Resolve(this, intent, playerContentId, monsterContentId);
        }

        public bool TryResolve(
            BattleIntent intent,
            string playerContentId,
            string monsterContentId,
            out BattleEncounterProfileSO profile,
            out BattlePresentationRouter.MatchKind matchKind)
        {
            return BattlePresentationRouter.TryResolve(
                this,
                intent,
                playerContentId,
                monsterContentId,
                out profile,
                out matchKind);
        }

#if UNITY_EDITOR
        public void EditorSetDescription(string value)
        {
            description = value;
        }

        public void EditorSetEntries(List<Entry> value)
        {
            entries = value ?? new List<Entry>();
        }

        public void EditorAddOrReplace(Entry entry)
        {
            if (entry == null)
            {
                return;
            }

            entries ??= new List<Entry>();
            for (var i = 0; i < entries.Count; i++)
            {
                var existing = entries[i];
                if (existing == null)
                {
                    continue;
                }

                if (existing.intent == entry.intent
                    && string.Equals(
                        BattleParticipantIds.Normalize(existing.playerContentId),
                        BattleParticipantIds.Normalize(entry.playerContentId),
                        StringComparison.Ordinal)
                    && string.Equals(
                        BattleParticipantIds.Normalize(existing.monsterContentId),
                        BattleParticipantIds.Normalize(entry.monsterContentId),
                        StringComparison.Ordinal))
                {
                    entries[i] = entry;
                    return;
                }
            }

            entries.Add(entry);
        }
#endif

        private void OnValidate()
        {
            if (entries == null)
            {
                return;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                entry.playerContentId = BattleParticipantIds.Normalize(entry.playerContentId);
                entry.monsterContentId = BattleParticipantIds.Normalize(entry.monsterContentId);
            }
        }
    }
}

using System;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 纯逻辑战斗编排路由器。可 EditMode 单测，无 MonoBehaviour 依赖。
    /// </summary>
    public static class BattlePresentationRouter
    {
        public enum MatchKind
        {
            None = 0,
            Exact = 1,
            PlayerWildcard = 2,
            MonsterWildcard = 3,
            BothWildcard = 4,
            SafeFallback = 5,
        }

        public static BattleEncounterProfileSO Resolve(
            BattleEncounterCatalogSO catalog,
            BattleIntent intent,
            string playerContentId,
            string monsterContentId)
        {
            TryResolve(catalog, intent, playerContentId, monsterContentId, out var profile, out _);
            return profile;
        }

        public static bool TryResolve(
            BattleEncounterCatalogSO catalog,
            BattleIntent intent,
            string playerContentId,
            string monsterContentId,
            out BattleEncounterProfileSO profile,
            out MatchKind matchKind)
        {
            profile = null;
            matchKind = MatchKind.None;

            var playerId = BattleParticipantIds.Normalize(playerContentId);
            var monsterId = BattleParticipantIds.Normalize(monsterContentId);

            if (catalog != null)
            {
                if (TryFind(catalog, intent, playerId, monsterId, requirePlayerExact: true, requireMonsterExact: true, out profile))
                {
                    matchKind = MatchKind.Exact;
                    return true;
                }

                if (TryFind(catalog, intent, playerId, monsterId, requirePlayerExact: true, requireMonsterExact: false, out profile))
                {
                    matchKind = MatchKind.PlayerWildcard;
                    return true;
                }

                if (TryFind(catalog, intent, playerId, monsterId, requirePlayerExact: false, requireMonsterExact: true, out profile))
                {
                    matchKind = MatchKind.MonsterWildcard;
                    return true;
                }

                if (TryFind(catalog, intent, playerId, monsterId, requirePlayerExact: false, requireMonsterExact: false, out profile))
                {
                    matchKind = MatchKind.BothWildcard;
                    return true;
                }
            }

            Debug.LogError(
                $"[BattlePresentationRouter] 未命中 Catalog：intent={intent}, player={playerId}, monster={monsterId}。使用 SafeFallback 绑参。");
            matchKind = MatchKind.SafeFallback;
            profile = null;
            return false;
        }

        /// <summary>
        /// 解析为可执行绑参；Catalog 未命中时返回 SafeFallback。
        /// </summary>
        public static BattleBindParams ResolveBindParams(
            BattleEncounterCatalogSO catalog,
            BattleIntent intent,
            string playerContentId,
            string monsterContentId,
            out BattleEncounterProfileSO profile,
            out MatchKind matchKind)
        {
            if (TryResolve(catalog, intent, playerContentId, monsterContentId, out profile, out matchKind)
                && profile != null)
            {
                var bind = profile.ToBindParams();
                if (bind.Intent != intent)
                {
                    Debug.LogWarning(
                        $"[BattlePresentationRouter] Profile '{profile.ProfileId}' intent={bind.Intent} 与请求 {intent} 不一致，仍按 Profile 绑参播放。");
                }

                return bind;
            }

            matchKind = MatchKind.SafeFallback;
            profile = null;
            return BattleBindParams.CreateSafeFallback(intent);
        }

        private static bool TryFind(
            BattleEncounterCatalogSO catalog,
            BattleIntent intent,
            string playerId,
            string monsterId,
            bool requirePlayerExact,
            bool requireMonsterExact,
            out BattleEncounterProfileSO profile)
        {
            profile = null;
            var entries = catalog.Entries;
            if (entries == null)
            {
                return false;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.profile == null || entry.intent != intent)
                {
                    continue;
                }

                var entryPlayer = BattleParticipantIds.Normalize(entry.playerContentId);
                var entryMonster = BattleParticipantIds.Normalize(entry.monsterContentId);

                var playerIsWildcard = entryPlayer == BattleParticipantIds.Wildcard;
                var monsterIsWildcard = entryMonster == BattleParticipantIds.Wildcard;

                if (requirePlayerExact == playerIsWildcard)
                {
                    continue;
                }

                if (requireMonsterExact == monsterIsWildcard)
                {
                    continue;
                }

                if (!BattleParticipantIds.Matches(entryPlayer, playerId))
                {
                    continue;
                }

                if (!BattleParticipantIds.Matches(entryMonster, monsterId))
                {
                    continue;
                }

                profile = entry.profile;
                return true;
            }

            return false;
        }
    }
}

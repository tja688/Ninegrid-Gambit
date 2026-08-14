using System.Collections.Generic;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Localization;
using NineGrid.Core.Systems;

namespace NineGrid.Flow.BattleLog
{
    /// <summary>
    /// 日志里出现的一切名字都从这里取：权威是表现层配置器（<c>Assets/Arts/ContentVisual/cards/*.json</c>
    /// 的 displayName，经 <see cref="CardPresentationAuthority"/> 读取），表现层没配才回退 Catalog
    /// 定义名，再没有才露出内部 defId。
    /// </summary>
    public static class BattleLogNaming
    {
        private static readonly Dictionary<string, string> sNameCache = new Dictionary<string, string>(64);
        private static int sCachedTablesVersion = -1;

        public static void InvalidateCache()
        {
            sNameCache.Clear();
        }

        /// <summary>内容主键 → 玩家可见名。解析不出时原样返回 defId，便于对账。</summary>
        public static string ResolveContentName(string defId)
        {
            if (string.IsNullOrEmpty(defId))
            {
                return string.Empty;
            }

            // 切语言后旧名失效（ADR-0046）。
            var version = LocalizationCatalog.TablesVersion;
            if (version != sCachedTablesVersion)
            {
                sCachedTablesVersion = version;
                sNameCache.Clear();
            }

            if (sNameCache.TryGetValue(defId, out var cached))
            {
                return cached;
            }

            var resolved = ResolveContentNameUncached(defId);
            sNameCache[defId] = resolved;
            return resolved;
        }

        private static string ResolveContentNameUncached(string defId)
        {
            if (CardPresentationAuthority.TryGetOwnedDisplayName(defId, out var owned) && !string.IsNullOrWhiteSpace(owned))
            {
                return owned;
            }

            var catalog = TryGetCatalog();
            if (catalog != null)
            {
                if (catalog.TryGetCard(defId, out var card) && !string.IsNullOrWhiteSpace(card.DisplayName))
                {
                    return card.DisplayName;
                }

                if (catalog.TryGetSkill(defId, out var skill) && !string.IsNullOrWhiteSpace(skill.DisplayName))
                {
                    return skill.DisplayName;
                }

                if (catalog.TryGetRelic(defId, out var relic) && !string.IsNullOrWhiteSpace(relic.DisplayName))
                {
                    return relic.DisplayName;
                }
            }

            return defId;
        }

        /// <summary>运行时卡 uid → 玩家可见名；Avatar 缺名时回退「玩家」。</summary>
        public static string ResolveCardName(int uid)
        {
            if (uid <= 0)
            {
                return string.Empty;
            }

            var card = TryGetCard(uid);
            if (card == null)
            {
                return "#" + uid;
            }

            var named = ResolveContentName(card.DefId);
            if (card.Kind == CardKind.Avatar && (string.IsNullOrEmpty(named) || named == card.DefId))
            {
                return L10n.Tr("battleLog.avatar", "玩家");
            }

            return string.IsNullOrEmpty(named) ? "#" + uid : named;
        }

        public static bool IsAvatar(int uid)
        {
            var card = TryGetCard(uid);
            return card != null && card.Kind == CardKind.Avatar;
        }

        /// <summary>
        /// 效果来源名：<c>SourceDefId</c> 是效果的容器主键（技能 / 遗物 / 卡自带效果），
        /// 语义聚合就落在这一层——同一技能由多少内核原子、多少条装配拼成，日志只认容器。
        /// </summary>
        public static string ResolveSourceName(string sourceDefId, int ownerUid)
        {
            if (!string.IsNullOrEmpty(sourceDefId))
            {
                var named = ResolveContentName(sourceDefId);
                if (!string.IsNullOrEmpty(named))
                {
                    return named;
                }
            }

            return ResolveCardName(ownerUid);
        }

        public static string ResolveRoomTitle(int floor, RoomKind room)
        {
            var head = string.Format(L10n.Tr("battleLog.floor", "第 {0} 层"), floor);
            var roomName = ResolveRoomName(room);
            return string.IsNullOrEmpty(roomName) ? head : head + " · " + roomName;
        }

        private static string ResolveRoomName(RoomKind room)
        {
            if (room == RoomKind.None)
            {
                return string.Empty;
            }

            var catalog = TryGetCatalog();
            if (catalog != null
                && catalog.Rewards != null
                && catalog.Rewards.TryGetRoom(room, out var def)
                && def != null
                && !string.IsNullOrWhiteSpace(def.DisplayName))
            {
                return def.DisplayName;
            }

            return room.ToString();
        }

        private static CardInstance TryGetCard(int uid)
        {
            try
            {
                var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
                if (arch == null)
                {
                    return null;
                }

                var registry = arch.GetModel<CardRegistry>();
                return registry != null && registry.TryGet(uid, out var card) ? card : null;
            }
            catch
            {
                return null;
            }
        }

        private static GameContentCatalog TryGetCatalog()
        {
            try
            {
                var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
                var content = arch?.GetSystem<IContentSystem>();
                return content != null && content.HasCatalog ? content.Catalog : null;
            }
            catch
            {
                return null;
            }
        }
    }
}

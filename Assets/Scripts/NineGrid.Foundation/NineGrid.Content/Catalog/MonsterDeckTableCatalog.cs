using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NineGrid.Content
{
    /// <summary>
    /// 遭遇牌组表（<c>monster_decks.json</c>）：玩法档位与登记，与表现层卡组 JSON 解耦。
    /// 成员列表由 <see cref="MonsterDeckCatalogBuilder"/> 按卡面 <c>deckId</c> 归属推导。
    /// </summary>
    public static class MonsterDeckTableCatalog
    {
        public const string FileName = "monster_decks.json";

        private static readonly List<MonsterDeckTableRow> sRows = new List<MonsterDeckTableRow>();
        private static bool sLoaded;

        public static IReadOnlyList<MonsterDeckTableRow> Rows
        {
            get
            {
                EnsureLoaded();
                return sRows;
            }
        }

        public static int Count
        {
            get
            {
                EnsureLoaded();
                return sRows.Count;
            }
        }

        public static void Invalidate()
        {
            sLoaded = false;
            sRows.Clear();
        }

        /// <summary>
        /// EditMode / 单测注入：不经磁盘加载，写入内存表（TearDown 须 <see cref="Invalidate"/>）。
        /// </summary>
        public static void UpsertForTests(MonsterDeckTableRow row)
        {
            EnsureLoaded();
            if (row == null || string.IsNullOrWhiteSpace(row.deck_id))
            {
                return;
            }

            for (var i = 0; i < sRows.Count; i++)
            {
                if (string.Equals(sRows[i].deck_id, row.deck_id, StringComparison.OrdinalIgnoreCase))
                {
                    sRows[i] = row;
                    return;
                }
            }

            sRows.Add(row);
        }

        public static bool TryGet(string deckId, out MonsterDeckTableRow row)
        {
            EnsureLoaded();
            row = null;
            if (string.IsNullOrWhiteSpace(deckId))
            {
                return false;
            }

            for (var i = 0; i < sRows.Count; i++)
            {
                if (string.Equals(sRows[i].deck_id, deckId.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    row = sRows[i];
                    return true;
                }
            }

            return false;
        }

        public static void EnsureLoaded()
        {
            if (sLoaded)
            {
                return;
            }

            sLoaded = true;
            sRows.Clear();
            var folder = ContentCatalogTableLoader.ResolveTablesDirectory();
            if (string.IsNullOrEmpty(folder))
            {
                return;
            }

            var path = Path.Combine(folder, FileName);
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                var raw = File.ReadAllText(path);
                var wrapped = "{\"items\":" + raw + "}";
                var list = JsonUtility.FromJson<MonsterDeckTableRowList>(wrapped);
                if (list?.items == null)
                {
                    return;
                }

                for (var i = 0; i < list.items.Length; i++)
                {
                    var row = list.items[i];
                    if (row == null || string.IsNullOrWhiteSpace(row.deck_id))
                    {
                        continue;
                    }

                    sRows.Add(row);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MonsterDeckTableCatalog] Failed reading " + path + ": " + ex.Message);
            }
        }

        [Serializable]
        private sealed class MonsterDeckTableRowList
        {
            public MonsterDeckTableRow[] items;
        }
    }

    [Serializable]
    public sealed class MonsterDeckTableRow
    {
        public string deck_id;
        public string deck_kind;
        public string display_name;
    }
}

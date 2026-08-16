using System;
using NineGrid.Core;

namespace NineGrid.Core.Content
{
    /// <summary>
    /// 当前地下城环境（楼层 × 层内房间 × 困难档）的权威解析。
    /// 表 JSON 优先，缺表或未命中行时回退烘焙默认；运行时读 <see cref="RunModel"/> 见 <see cref="GetCurrentDungeonEnvironmentQuery"/>。
    /// </summary>
    public static class DungeonEnvironmentCatalog
    {
        public const string FileName = "dungeon_environments.json";
        public const string FaceBackgroundResourceFolder = "Assets/Resources/ContentArt/Png/Other/";
        public const string GroundPanelResourceFolder = "Assets/Resources/ContentArt/Png/Venue/";
        public const string GroundPanelBloodSpriteName = "F_UI_Panel_H_血色";

        private static DungeonEnvironmentTableDto loadedTable;

        public static void Invalidate()
        {
            loadedTable = null;
        }

        public static void SetTable(DungeonEnvironmentTableDto table)
        {
            loadedTable = table;
        }

        public static DungeonEnvironmentTableDto CreateDefaultTable()
        {
            return new DungeonEnvironmentTableDto
            {
                roomSplit = 4,
                variants = new[]
                {
                    MakeVariant("forest.jade_mist", 1, 1, 4, "密林", "翡翠迷雾", false, "#172038", "#468232"),
                    MakeVariant("forest.lost_ruins", 1, 5, 8, "密林", "失落遗迹", false, "#1e1d39", "#577277"),
                    MakeVariant("forest.blood", 1, 1, 8, "密林", "血色", true, "#411d31", "#a53030"),
                    MakeVariant("rock.ossuary", 2, 1, 4, "岩层", "藏骨堂", false, "#151d28", "#4d2b32"),
                    MakeVariant("rock.magma", 2, 5, 8, "岩层", "熔岩之地", false, "#341c27", "#884b2b"),
                    MakeVariant("rock.blood", 2, 1, 8, "岩层", "血色", true, "#411d31", "#a53030"),
                    MakeVariant("cave.pale_road", 3, 1, 4, "溶洞", "苍白之路", false, "#202e37", "#577277"),
                    MakeVariant("cave.twilight_hall", 3, 5, 8, "溶洞", "黄昏礼堂", false, "#411d31", "#884b2b"),
                    MakeVariant("cave.blood", 3, 1, 8, "溶洞", "血色", true, "#411d31", "#a53030"),
                }
            };
        }

        public static bool IsHardDifficulty(string difficultyId)
        {
            return string.Equals(difficultyId, RunDifficultyIds.Hard, StringComparison.OrdinalIgnoreCase);
        }

        /// <param name="floor">1-based 层号。</param>
        /// <param name="room">层内房间序号 1–8（<see cref="MapNodeProgression.ToDisplayNode"/>）。</param>
        /// <param name="isHard">困难档：整层血色环境。</param>
        public static DungeonEnvironmentInfo Resolve(int floor, int room, bool isHard)
        {
            var safeFloor = floor < 1 ? 1 : floor;
            var safeRoom = room < 1 ? 1 : room > RunModel.NodesPerFloor ? RunModel.NodesPerFloor : room;
            if (TryResolveFromTable(safeFloor, safeRoom, isHard, out var fromTable))
            {
                return fromTable;
            }

            var displayName = ResolveDisplayName(safeFloor, safeRoom, isHard);
            var isBloodTheme = isHard || displayName.EndsWith("_血色", StringComparison.Ordinal);
            return new DungeonEnvironmentInfo(
                displayName,
                FaceBackgroundResourceFolder + displayName + ".png",
                ResolveGroundPanelResourcePath(displayName, isBloodTheme),
                ResolveMainBackgroundColorHex(displayName, isBloodTheme),
                ResolveSlotColorHex(displayName, isBloodTheme),
                isBloodTheme,
                safeFloor,
                safeRoom);
        }

        private static bool TryResolveFromTable(int floor, int room, bool isHard, out DungeonEnvironmentInfo info)
        {
            info = default;
            var variants = loadedTable?.variants;
            if (variants == null || variants.Length == 0)
            {
                return false;
            }

            DungeonEnvironmentVariantDto hit = null;
            for (var i = 0; i < variants.Length; i++)
            {
                var row = variants[i];
                if (row == null || row.floor != floor)
                {
                    continue;
                }

                if (isHard)
                {
                    if (!row.isBlood)
                    {
                        continue;
                    }

                    hit = row;
                    break;
                }

                if (row.isBlood)
                {
                    continue;
                }

                var min = row.roomMin < 1 ? 1 : row.roomMin;
                var max = row.roomMax < min ? min : row.roomMax;
                if (room < min || room > max)
                {
                    continue;
                }

                hit = row;
                break;
            }

            if (hit == null)
            {
                return false;
            }

            var displayName = string.IsNullOrWhiteSpace(hit.displayName)
                ? hit.layerName + "_" + hit.variantName
                : hit.displayName;
            var isBloodTheme = isHard || hit.isBlood;
            var face = string.IsNullOrWhiteSpace(hit.faceBackground)
                ? FaceBackgroundResourceFolder + displayName + ".png"
                : hit.faceBackground;
            var ground = string.IsNullOrWhiteSpace(hit.groundPanel)
                ? ResolveGroundPanelResourcePath(displayName, isBloodTheme)
                : hit.groundPanel;
            var hex = string.IsNullOrWhiteSpace(hit.mainBackgroundHex)
                ? ResolveMainBackgroundColorHex(displayName, isBloodTheme)
                : hit.mainBackgroundHex;
            var slotHex = string.IsNullOrWhiteSpace(hit.slotHex)
                ? ResolveSlotColorHex(displayName, isBloodTheme)
                : hit.slotHex;
            info = new DungeonEnvironmentInfo(
                displayName,
                face,
                ground,
                hex,
                slotHex,
                isBloodTheme,
                floor,
                room);
            return true;
        }

        private static DungeonEnvironmentVariantDto MakeVariant(
            string id,
            int floor,
            int roomMin,
            int roomMax,
            string layerName,
            string variantName,
            bool isBlood,
            string mainBackgroundHex,
            string slotHex)
        {
            var displayName = layerName + "_" + variantName;
            var groundName = isBlood ? GroundPanelBloodSpriteName : "F_UI_Panel_H_" + displayName;
            return new DungeonEnvironmentVariantDto
            {
                id = id,
                floor = floor,
                roomMin = roomMin,
                roomMax = roomMax,
                layerName = layerName,
                variantName = variantName,
                displayName = displayName,
                faceBackground = FaceBackgroundResourceFolder + displayName + ".png",
                groundPanel = GroundPanelResourceFolder + groundName + ".png",
                mainBackgroundHex = mainBackgroundHex,
                slotHex = slotHex,
                isBlood = isBlood,
                notes = isBlood ? "困难档整层使用" : string.Empty
            };
        }

        public static string ResolveGroundPanelResourcePath(string displayName, bool isBloodTheme)
        {
            var spriteName = isBloodTheme
                ? GroundPanelBloodSpriteName
                : "F_UI_Panel_H_" + displayName;
            return GroundPanelResourceFolder + spriteName + ".png";
        }

        public static string ResolveMainBackgroundColorHex(string displayName, bool isBloodTheme)
        {
            if (isBloodTheme)
            {
                return "#411d31";
            }

            switch (displayName)
            {
                case "密林_翡翠迷雾":
                    return "#172038";
                case "密林_失落遗迹":
                    return "#1e1d39";
                case "岩层_藏骨堂":
                    return "#151d28";
                case "岩层_熔岩之地":
                    return "#341c27";
                case "溶洞_苍白之路":
                    return "#202e37";
                case "溶洞_黄昏礼堂":
                    return "#411d31";
                default:
                    return "#411d31";
            }
        }

        /// <summary>
        /// GroundAnchors 格面 tint。与场地框内填充同族、比 MainBG 亮一档，让九宫格读得出来。
        /// </summary>
        public static string ResolveSlotColorHex(string displayName, bool isBloodTheme)
        {
            if (isBloodTheme)
            {
                return "#a53030";
            }

            switch (displayName)
            {
                case "密林_翡翠迷雾":
                    return "#468232";
                case "密林_失落遗迹":
                    return "#577277";
                case "岩层_藏骨堂":
                    return "#4d2b32";
                case "岩层_熔岩之地":
                    return "#884b2b";
                case "溶洞_苍白之路":
                    return "#577277";
                case "溶洞_黄昏礼堂":
                    return "#884b2b";
                default:
                    return "#a53030";
            }
        }

        public static DungeonEnvironmentInfo ResolveFromRun(int floor, int nodeIndex, string difficultyId)
        {
            var room = MapNodeProgression.ToDisplayNode(nodeIndex);
            return Resolve(floor, room, IsHardDifficulty(difficultyId));
        }

        private static string ResolveDisplayName(int floor, int room, bool isHard)
        {
            if (isHard)
            {
                switch (floor)
                {
                    case 1:
                        return "密林_血色";
                    case 2:
                        return "岩层_血色";
                    case 3:
                        return "溶洞_血色";
                    default:
                        return "密林_血色";
                }
            }

            var firstHalf = room >= 1 && room <= 4;
            switch (floor)
            {
                case 1:
                    return firstHalf ? "密林_翡翠迷雾" : "密林_失落遗迹";
                case 2:
                    return firstHalf ? "岩层_藏骨堂" : "岩层_熔岩之地";
                case 3:
                    return firstHalf ? "溶洞_苍白之路" : "溶洞_黄昏礼堂";
                default:
                    return firstHalf ? "密林_翡翠迷雾" : "密林_失落遗迹";
            }
        }
    }

    /// <summary>地下城虚构环境表（Arts/StreamingAssets <c>dungeon_environments.json</c>）。</summary>
    [Serializable]
    public sealed class DungeonEnvironmentTableDto
    {
        public int roomSplit = 4;
        public DungeonEnvironmentVariantDto[] variants;
    }

    [Serializable]
    public sealed class DungeonEnvironmentVariantDto
    {
        public string id = string.Empty;
        public int floor = 1;
        public int roomMin = 1;
        public int roomMax = 4;
        public string layerName = string.Empty;
        public string variantName = string.Empty;
        public string displayName = string.Empty;
        public string faceBackground = string.Empty;
        public string groundPanel = string.Empty;
        public string mainBackgroundHex = "#411d31";
        public string slotHex = "#a53030";
        public bool isBlood;
        public string notes = string.Empty;
    }

    public readonly struct DungeonEnvironmentInfo
    {
        public DungeonEnvironmentInfo(
            string displayName,
            string faceBackgroundResourcePath,
            string groundPanelResourcePath,
            string mainBackgroundColorHex,
            string slotColorHex,
            bool isBloodTheme,
            int floor,
            int room)
        {
            DisplayName = displayName ?? string.Empty;
            FaceBackgroundResourcePath = faceBackgroundResourcePath ?? string.Empty;
            GroundPanelResourcePath = groundPanelResourcePath ?? string.Empty;
            MainBackgroundColorHex = mainBackgroundColorHex ?? string.Empty;
            SlotColorHex = slotColorHex ?? string.Empty;
            IsBloodTheme = isBloodTheme;
            Floor = floor;
            Room = room;
        }

        public string DisplayName { get; }
        public string FaceBackgroundResourcePath { get; }
        public string GroundPanelResourcePath { get; }
        public string MainBackgroundColorHex { get; }
        public string SlotColorHex { get; }
        public bool IsBloodTheme { get; }
        public int Floor { get; }
        public int Room { get; }
    }
}

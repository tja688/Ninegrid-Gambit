using System;
using NineGrid.Core;

namespace NineGrid.Core.Content
{
    /// <summary>
    /// 当前地下城环境（楼层 × 层内房间 × 困难档）的权威解析。
    /// 纯函数，便于单测；运行时读 <see cref="RunModel"/> 见 <see cref="GetCurrentDungeonEnvironmentQuery"/>。
    /// </summary>
    public static class DungeonEnvironmentCatalog
    {
        public const string FaceBackgroundResourceFolder = "Assets/Resources/ContentArt/Png/Other/";
        public const string GroundPanelResourceFolder = "Assets/Resources/ContentArt/Png/Venue/";
        public const string GroundPanelBloodSpriteName = "F_UI_Panel_H_血色";

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
            var displayName = ResolveDisplayName(safeFloor, safeRoom, isHard);
            var isBloodTheme = isHard || displayName.EndsWith("_血色", StringComparison.Ordinal);
            return new DungeonEnvironmentInfo(
                displayName,
                FaceBackgroundResourceFolder + displayName + ".png",
                ResolveGroundPanelResourcePath(displayName, isBloodTheme),
                ResolveMainBackgroundColorHex(displayName, isBloodTheme),
                isBloodTheme,
                safeFloor,
                safeRoom);
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
                return "#241527";
            }

            switch (displayName)
            {
                case "密林_翡翠迷雾":
                    return "#151d28";
                case "岩层_藏骨堂":
                    return "#090a14";
                case "溶洞_苍白之路":
                    return "#202e37";
                case "密林_失落遗迹":
                case "岩层_熔岩之地":
                case "溶洞_黄昏礼堂":
                    return "#241527";
                default:
                    return "#241527";
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

    public readonly struct DungeonEnvironmentInfo
    {
        public DungeonEnvironmentInfo(
            string displayName,
            string faceBackgroundResourcePath,
            string groundPanelResourcePath,
            string mainBackgroundColorHex,
            bool isBloodTheme,
            int floor,
            int room)
        {
            DisplayName = displayName ?? string.Empty;
            FaceBackgroundResourcePath = faceBackgroundResourcePath ?? string.Empty;
            GroundPanelResourcePath = groundPanelResourcePath ?? string.Empty;
            MainBackgroundColorHex = mainBackgroundColorHex ?? string.Empty;
            IsBloodTheme = isBloodTheme;
            Floor = floor;
            Room = room;
        }

        public string DisplayName { get; }
        public string FaceBackgroundResourcePath { get; }
        public string GroundPanelResourcePath { get; }
        public string MainBackgroundColorHex { get; }
        public bool IsBloodTheme { get; }
        public int Floor { get; }
        public int Room { get; }
    }
}

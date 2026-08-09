using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Flow.BattleInfoPreview
{
    /// <summary>
    /// 战斗信息预览：按楼层 × 层内关卡配置房间信息文案模板。
    /// 占位符：{room} {floor} {progress} {displayNode} {nodesPerFloor}
    /// </summary>
    [CreateAssetMenu(
        fileName = "BattleInfoPreviewCopy",
        menuName = "NineGrid/Flow/Battle Info Preview Copy")]
    public sealed class BattleInfoPreviewCopySO : ScriptableObject
    {
        public const string ResourcePath = "Flow/BattleInfoPreviewCopy";

        [Serializable]
        public sealed class Entry
        {
            [Tooltip("楼层 1..FinalFloor；0 = 通配任意楼层。")]
            public int floor;

            [Tooltip("层内展示关卡 1..NodesPerFloor；0 = 通配该层任意关卡。")]
            public int displayNode;

            [TextArea(2, 8)]
            [Tooltip("整段房间信息 TMP。空 = 用 Presenter 默认三段文案。")]
            public string roomInfoTemplate =
                "房间类型：{room}\n楼层：{floor}\n进度：{progress}";
        }

        [SerializeField]
        private Entry[] entries = Array.Empty<Entry>();

        public IReadOnlyList<Entry> Entries => entries ?? Array.Empty<Entry>();

        /// <summary>
        /// 先精确匹配 floor+displayNode，再 floor+0，再 0+displayNode，再 0+0。
        /// </summary>
        public bool TryResolveTemplate(int floor, int displayNode, out string template)
        {
            template = null;
            var list = Entries;
            if (list.Count == 0)
            {
                return false;
            }

            if (TryFind(list, floor, displayNode, out template))
            {
                return !string.IsNullOrWhiteSpace(template);
            }

            if (TryFind(list, floor, 0, out template))
            {
                return !string.IsNullOrWhiteSpace(template);
            }

            if (TryFind(list, 0, displayNode, out template))
            {
                return !string.IsNullOrWhiteSpace(template);
            }

            if (TryFind(list, 0, 0, out template))
            {
                return !string.IsNullOrWhiteSpace(template);
            }

            return false;
        }

        private static bool TryFind(
            IReadOnlyList<Entry> list,
            int floor,
            int displayNode,
            out string template)
        {
            template = null;
            for (var i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e == null)
                {
                    continue;
                }

                if (e.floor == floor && e.displayNode == displayNode)
                {
                    template = e.roomInfoTemplate;
                    return true;
                }
            }

            return false;
        }
    }
}

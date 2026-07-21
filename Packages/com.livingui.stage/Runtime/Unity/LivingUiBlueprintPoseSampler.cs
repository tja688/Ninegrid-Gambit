using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.LivingUI.Unity
{
    /// <summary>
    /// 从构型蓝图读取定性信息：是否出现 + 本构型锚点。不再向 runtime 提供位姿。
    /// </summary>
    public static class LivingUiBlueprintPoseSampler
    {
        private static readonly (LivingUiLayoutId Id, string RootName)[] Roots =
        {
            (LivingUiLayoutId.MainMenu, "大盘构型0-主菜单"),
            (LivingUiLayoutId.CharacterChoice, "大盘构型1-人物选择"),
            (LivingUiLayoutId.Battle, "大盘构型2-核心战斗面板"),
            (LivingUiLayoutId.RewardChoice, "大盘构型3-选择奖励"),
            (LivingUiLayoutId.DeckPreview, "大盘构型4-打开卡组视图"),
            (LivingUiLayoutId.Room, "大盘构型5-房间基础面板"),
            (LivingUiLayoutId.Route, "大盘构型6-预备待定"),
        };

        private static readonly Dictionary<LivingUiLayoutId, Transform> Cache = new();
        private static bool _cacheBuilt;

        public static void InvalidateCache()
        {
            Cache.Clear();
            _cacheBuilt = false;
        }

        /// <summary>
        /// 定性采样：同面板同基础名内容是否存在，及其 LivingUiContentMarker.Anchor。
        /// </summary>
        public static bool TrySampleQualitative(
            LivingUiLayoutId layout,
            int carrierId,
            string contentName,
            out LivingUiContentAnchor anchor)
        {
            anchor = LivingUiContentAnchor.TopLeft;
            var content = FindContent(layout, carrierId, contentName);
            if (content == null) return false;

            var marker = content.GetComponent<LivingUiContentMarker>();
            if (marker != null) anchor = marker.Anchor;
            return true;
        }

        /// <summary>
        /// 编辑器/迁移用：读取蓝图局部位姿。运行时内容驱动不应调用。
        /// </summary>
        public static bool TrySamplePoseForEditor(
            LivingUiLayoutId layout,
            int carrierId,
            string contentName,
            out Vector3 localPosition,
            out Vector3 localScale,
            out Vector2 carrierSize)
        {
            localPosition = Vector3.zero;
            localScale = Vector3.one;
            carrierSize = Vector2.one;

            var content = FindContent(layout, carrierId, contentName);
            if (content == null) return false;

            localPosition = content.localPosition;
            localScale = content.localScale;
            if (localScale == Vector3.zero) localScale = Vector3.one;

            var carrier = content.parent != null ? content.parent.parent : null;
            if (carrier != null)
            {
                var skin = carrier.GetComponent<SpriteRenderer>();
                if (skin != null) carrierSize = skin.size;
            }

            return true;
        }

        private static Transform FindContent(LivingUiLayoutId layout, int carrierId, string contentName)
        {
            EnsureCache();
            if (!Cache.TryGetValue(layout, out var root) || root == null) return null;

            var carrier = root.Find(carrierId.ToString());
            if (carrier == null) return null;
            var attach = carrier.Find("ContentAttach");
            if (attach == null) return null;

            var want = LivingUiContentNames.BaseName(contentName);
            for (var i = 0; i < attach.childCount; i++)
            {
                var child = attach.GetChild(i);
                if (!LivingUiContentNames.BaseNameEquals(child.name, want)) continue;
                return child;
            }

            return null;
        }

        private static void EnsureCache()
        {
            if (_cacheBuilt) return;
            _cacheBuilt = true;
            Cache.Clear();

            var all = Resources.FindObjectsOfTypeAll<Transform>();
            for (var r = 0; r < Roots.Length; r++)
            {
                var want = Roots[r].RootName;
                for (var i = 0; i < all.Length; i++)
                {
                    var t = all[i];
                    if (!t.gameObject.scene.IsValid()) continue;
                    if (t.parent != null) continue;
                    if (t.name != want) continue;
                    Cache[Roots[r].Id] = t;
                    break;
                }
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.LivingUI.Unity
{
    /// <summary>
    /// 从构型蓝图读取同面板同名内容的局部位姿，供大盘 live 对齐。
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

        public static bool TrySample(
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

            EnsureCache();
            if (!Cache.TryGetValue(layout, out var root) || root == null) return false;

            var carrier = root.Find(carrierId.ToString());
            if (carrier == null) return false;
            var attach = carrier.Find("ContentAttach");
            if (attach == null) return false;

            Transform content = null;
            for (var i = 0; i < attach.childCount; i++)
            {
                if (attach.GetChild(i).name != contentName) continue;
                content = attach.GetChild(i);
                break;
            }

            if (content == null) return false;

            localPosition = content.localPosition;
            localScale = content.localScale;
            if (localScale == Vector3.zero) localScale = Vector3.one;

            var skin = carrier.GetComponent<SpriteRenderer>();
            if (skin != null) carrierSize = skin.size;
            return true;
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

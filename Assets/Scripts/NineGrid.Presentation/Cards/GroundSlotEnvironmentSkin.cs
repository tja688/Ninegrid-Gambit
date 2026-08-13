using System.Collections.Generic;
using NineGrid.Core;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 九宫格槽位环境皮肤：按本层绑定的主题卡组（<see cref="RunModel.FloorMonsterDeckId"/>）
    /// 把 GroundAnchors/slot1–9（含 slot5_Player）的底板换成对应环境地块，并把场景烘焙 tint 归白。
    /// 八张地块（岩层/密林）与 scripts/palette-map-apollo.py 的区域配色方案同源：
    /// 基础→浅岩层、旋转→地下丛林、召唤→藏骨堂、烈焰→熔岩之地、决斗→失落遗迹、
    /// 链接→岩洞区、翻面→密林血色；岩层_血色 作第 3 层未映射卡组的兜底变体。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GroundSlotEnvironmentSkin : MonoBehaviour
    {
        private const string ResourceFolder = "ContentArt/Png/Other/";

        /// <summary>教学关 / 尚未绑定本层卡组时的默认地块。</summary>
        private const string DefaultTile = "岩层_浅岩层";

        /// <summary>第 3 层绑定了未映射卡组时的兜底地块（血色变体）。</summary>
        private const string FinalFloorFallbackTile = "岩层_血色";

        // deckId 为历史残留不透明主键（ADR-0014）；主题语义见 ThemeDeckStableMapping 注释与
        // docs/feedback-analysis/content-audit-report.md 的 deck↔设计案表名对照。
        private static readonly Dictionary<string, string> DeckTiles =
            new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                { "deck.dragon", "岩层_浅岩层" },
                { "deck.orc_legion", "密林_地下丛林" },
                { "deck.insect", "岩层_藏骨堂" },
                { "deck.stone_legion", "岩层_熔岩之地" },
                { "deck.void", "密林_失落遗迹" },
                { "deck.smallanimal", "密林_岩洞区" },
                { "deck.skeleton_legion", "密林_血色" },
            };

        private static readonly Dictionary<string, Sprite> TileCache =
            new Dictionary<string, Sprite>();

        private readonly List<SpriteRenderer> mSlotRenderers = new List<SpriteRenderer>(9);
        private string mAppliedTile;

        /// <summary>由 <see cref="GroundFieldView"/> Awake 调用；重复调用只刷新缓存。</summary>
        public static void EnsureAttached(Transform groundAnchorsRoot)
        {
            if (groundAnchorsRoot == null)
            {
                return;
            }

            var skin = groundAnchorsRoot.GetComponent<GroundSlotEnvironmentSkin>();
            if (skin == null)
            {
                skin = groundAnchorsRoot.gameObject.AddComponent<GroundSlotEnvironmentSkin>();
            }

            skin.CacheRenderers();
            skin.mAppliedTile = null;
            skin.Refresh();
        }

        private void Update()
        {
            // 轮询一次字符串比较：对 DisableDomainReload / 架构重建 / 读档恢复均稳健，
            // 不依赖订阅时架构已就绪。
            Refresh();
        }

        private void CacheRenderers()
        {
            mSlotRenderers.Clear();
            for (var i = 0; i < transform.childCount; i++)
            {
                var renderer = transform.GetChild(i).GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    mSlotRenderers.Add(renderer);
                }
            }
        }

        private void Refresh()
        {
            var tile = ResolveTileName();
            if (string.Equals(tile, mAppliedTile, System.StringComparison.Ordinal))
            {
                return;
            }

            var sprite = LoadTile(tile);
            if (sprite == null)
            {
                // 资源缺失时保留场景烘焙底板，但记住结果避免每帧重试加载。
                mAppliedTile = tile;
                return;
            }

            for (var i = 0; i < mSlotRenderers.Count; i++)
            {
                var renderer = mSlotRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.sprite = sprite;
                renderer.color = Color.white;
            }

            mAppliedTile = tile;
        }

        private static Sprite LoadTile(string tileName)
        {
            if (TileCache.TryGetValue(tileName, out var cached) && cached != null)
            {
                return cached;
            }

            var sprite = Resources.Load<Sprite>(ResourceFolder + tileName);
            TileCache[tileName] = sprite;
            return sprite;
        }

        private string ResolveTileName()
        {
            var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            var run = arch?.GetModel<RunModel>();
            var deckId = run?.FloorMonsterDeckId?.Value;
            if (!string.IsNullOrEmpty(deckId))
            {
                if (DeckTiles.TryGetValue(deckId, out var tile))
                {
                    return tile;
                }

                if (run.Floor.Value >= RunModel.FinalFloor)
                {
                    return FinalFloorFallbackTile;
                }
            }

            return DefaultTile;
        }
    }
}

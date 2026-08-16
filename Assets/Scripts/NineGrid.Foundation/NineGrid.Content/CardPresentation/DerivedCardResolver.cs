using System;
using System.Collections.Generic;
using NineGrid.Core.Effects;

namespace NineGrid.Content.CardPresentation
{
    /// <summary>
    /// 衍生卡解析：卡 effect 装配中存在 <c>Spawn</c> 动作时，其 <c>defId</c> 即该卡的衍生卡
    /// （「这张卡会涉及到的另一张卡」，如 死亡召唤 → 复活石、复活石 → 巨剑骷髅、遗物 → 教学卡）。
    /// 数据源 = 卡 JSON <c>effectAssemblies[].templateId</c> → effect_templates.json 模板
    /// <c>body</c> → <c>action.atom == "Spawn"</c> 的 <c>defId</c>（递归收集，去重、排除自身）。
    /// 纯静态投影，不依赖运行时 GameContentCatalog 解析状态。
    /// </summary>
    public static class DerivedCardResolver
    {
        private static readonly Dictionary<string, string[]> Cache =
            new Dictionary<string, string[]>(StringComparer.Ordinal);

        /// <summary>内容重载（ADR-0008）后须失效；挂载点见 <see cref="ContentCatalogBootstrap.Load"/>。</summary>
        public static void Invalidate()
        {
            Cache.Clear();
        }

        public static IReadOnlyList<string> ResolveDerivedCardDefIds(string defId)
        {
            if (string.IsNullOrWhiteSpace(defId))
            {
                return Array.Empty<string>();
            }

            var key = defId.Trim();
            if (Cache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var result = ResolveUncached(key);
            Cache[key] = result;
            return result;
        }

        private static string[] ResolveUncached(string defId)
        {
            if (!CardPresentationConfigCatalog.TryGet(defId, out var dto)
                || dto == null
                || dto.effectAssemblies == null
                || dto.effectAssemblies.Length == 0)
            {
                return Array.Empty<string>();
            }

            var found = new List<string>(2);
            for (var i = 0; i < dto.effectAssemblies.Length; i++)
            {
                var assembly = dto.effectAssemblies[i];
                if (assembly == null || string.IsNullOrWhiteSpace(assembly.templateId))
                {
                    continue;
                }

                if (!EffectTemplateCatalog.TryGet(assembly.templateId.Trim(), out var template)
                    || template == null
                    || string.IsNullOrWhiteSpace(template.BodyJson))
                {
                    continue;
                }

                CollectSpawnDefIds(template.BodyJson, defId, found);
            }

            return found.ToArray();
        }

        private static void CollectSpawnDefIds(string bodyJson, string selfDefId, List<string> found)
        {
            EffectDslNode root;
            try
            {
                root = EffectJson.Parse(bodyJson);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning(
                    "[DerivedCardResolver] effect body parse failed: " + ex.Message);
                return;
            }

            WalkSpawn(root, selfDefId, found);
        }

        private static void WalkSpawn(EffectDslNode node, string selfDefId, List<string> found)
        {
            if (node == null || node.IsNull)
            {
                return;
            }

            if (node.IsObject)
            {
                if (string.Equals(node.Get("atom").AsString(string.Empty), "Spawn", StringComparison.Ordinal))
                {
                    var defId = node.Get("defId").AsString(string.Empty);
                    if (!string.IsNullOrWhiteSpace(defId)
                        && !string.Equals(defId.Trim(), selfDefId, StringComparison.Ordinal)
                        && !found.Contains(defId))
                    {
                        found.Add(defId.Trim());
                    }
                }

                // 动作可能嵌套（序列/条件内），整棵子树递归。
                if (node.RawValue is Dictionary<string, object> obj)
                {
                    foreach (var pair in obj)
                    {
                        WalkSpawn(new EffectDslNode(pair.Value), selfDefId, found);
                    }
                }
            }
            else if (node.IsArray)
            {
                var arr = node.AsArray();
                for (var i = 0; i < arr.Count; i++)
                {
                    WalkSpawn(arr[i], selfDefId, found);
                }
            }
        }
    }
}

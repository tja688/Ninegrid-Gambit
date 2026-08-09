#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using NineGrid.Content.Audio;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    /// <summary>#178 Catalog 卫生：重复键、覆盖冲突、路径、随机池、参数范围与孤儿绑定。</summary>
    public static class AudioBindingCatalogHygieneValidator
    {
        public sealed class Finding
        {
            public string Category;
            public string BindingKey;
            public string CueId;
            public string Detail;

            public override string ToString()
            {
                return "[" + Category + "] "
                    + (string.IsNullOrEmpty(CueId) ? string.Empty : CueId + " ")
                    + Detail;
            }
        }

        public static List<Finding> Validate(
            AudioBindingCatalogDto catalog,
            IReadOnlyCollection<string> formalClipKeys,
            IReadOnlyCollection<string> declaredCueIds)
        {
            var findings = new List<Finding>();
            if (catalog == null)
            {
                findings.Add(new Finding
                {
                    Category = "catalog-null",
                    Detail = "音频绑定 Catalog 为空。",
                });
                return findings;
            }

            var bindings = catalog.bindings ?? Array.Empty<AudioBindingDto>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var formal = ToClipKeySet(formalClipKeys);
            var declared = ToCueIdSet(declaredCueIds);
            var coverageCandidates = new List<AudioBindingDto>();

            for (var i = 0; i < bindings.Length; i++)
            {
                var row = bindings[i];
                if (row == null)
                {
                    findings.Add(new Finding
                    {
                        Category = "null-row",
                        Detail = "bindings[" + i + "] 为空。",
                    });
                    continue;
                }

                var key = AudioBindingEditorSession.ComputeBindingKey(row);
                if (string.IsNullOrWhiteSpace(row.cueId))
                {
                    findings.Add(new Finding
                    {
                        Category = "empty-cue",
                        BindingKey = key,
                        Detail = "cueId 为空。",
                    });
                }

                if (!keys.Add(key))
                {
                    findings.Add(new Finding
                    {
                        Category = "duplicate-binding-key",
                        BindingKey = key,
                        CueId = row.cueId,
                        Detail = "cue/内容选择器重复。",
                    });
                }

                var cueId = row.cueId == null ? string.Empty : row.cueId.Trim();
                if (declared.Count > 0
                    && !string.IsNullOrWhiteSpace(cueId)
                    && !declared.Contains(cueId))
                {
                    findings.Add(new Finding
                    {
                        Category = "orphan-binding",
                        BindingKey = key,
                        CueId = row.cueId,
                        Detail = "绑定没有对应声音提示声明。",
                    });
                }

                ValidateParams(row, key, findings);
                ValidateClips(row, key, formal, findings);
                coverageCandidates.Add(row);
            }

            ValidateCoverageConflicts(coverageCandidates, findings);
            return findings;
        }

        /// <summary>
        /// 同 cue、同 specificity、选择器可同时命中同一请求 → 运行时按后写入胜出，属覆盖冲突。
        /// 与 duplicate-binding-key（完全相同 binding key）分立。
        /// </summary>
        private static void ValidateCoverageConflicts(
            List<AudioBindingDto> rows,
            List<Finding> findings)
        {
            for (var i = 0; i < rows.Count; i++)
            {
                var a = rows[i];
                if (a == null || string.IsNullOrWhiteSpace(a.cueId))
                {
                    continue;
                }

                var aKey = AudioBindingEditorSession.ComputeBindingKey(a);
                var aSpec = CountSelectorSpecificity(a);
                for (var j = i + 1; j < rows.Count; j++)
                {
                    var b = rows[j];
                    if (b == null
                        || !string.Equals(a.cueId, b.cueId, StringComparison.Ordinal)
                        || CountSelectorSpecificity(b) != aSpec)
                    {
                        continue;
                    }

                    var bKey = AudioBindingEditorSession.ComputeBindingKey(b);
                    if (string.Equals(aKey, bKey, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!SelectorsCanBothMatch(a, b))
                    {
                        continue;
                    }

                    findings.Add(new Finding
                    {
                        Category = "coverage-conflict",
                        BindingKey = aKey,
                        CueId = a.cueId,
                        Detail = "与另一条同 cue/同 specificity 绑定可同时命中：" + bKey,
                    });
                }
            }
        }

        private static int CountSelectorSpecificity(AudioBindingDto row)
        {
            return CountNonEmpty(row.selectorCardDefId)
                + CountNonEmpty(row.selectorSkillId)
                + CountNonEmpty(row.selectorRoomId)
                + CountNonEmpty(row.selectorItemDefId)
                + CountNonEmpty(row.selectorContentId);
        }

        private static int CountNonEmpty(string value)
        {
            return string.IsNullOrEmpty(value) ? 0 : 1;
        }

        private static bool SelectorsCanBothMatch(AudioBindingDto a, AudioBindingDto b)
        {
            return SelectorPairCompatible(a.selectorCardDefId, b.selectorCardDefId)
                && SelectorPairCompatible(a.selectorSkillId, b.selectorSkillId)
                && SelectorPairCompatible(a.selectorRoomId, b.selectorRoomId)
                && SelectorPairCompatible(a.selectorItemDefId, b.selectorItemDefId)
                && SelectorPairCompatible(a.selectorContentId, b.selectorContentId);
        }

        private static bool SelectorPairCompatible(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            {
                return true;
            }

            return string.Equals(left, right, StringComparison.Ordinal);
        }

        private static void ValidateParams(AudioBindingDto row, string key, List<Finding> findings)
        {
            if (row.volumeDb < -80f || row.volumeDb > 24f)
            {
                findings.Add(new Finding
                {
                    Category = "param-range",
                    BindingKey = key,
                    CueId = row.cueId,
                    Detail = "volumeDb 超出 [-80, 24]：" + row.volumeDb,
                });
            }

            if (row.startOffsetSeconds < 0f
                || row.bindingDelaySeconds < 0f
                || row.minimumIntervalSeconds < 0f)
            {
                findings.Add(new Finding
                {
                    Category = "param-range",
                    BindingKey = key,
                    CueId = row.cueId,
                    Detail = "起播点/绑定延迟/最短间隔不得为负。",
                });
            }

            if (!string.IsNullOrWhiteSpace(row.authoringStatus)
                && !AudioBindingAuthoringStatuses.IsAiDraft(row.authoringStatus)
                && !AudioBindingAuthoringStatuses.IsHumanConfirmed(row.authoringStatus))
            {
                findings.Add(new Finding
                {
                    Category = "authoring-status",
                    BindingKey = key,
                    CueId = row.cueId,
                    Detail = "未知 authoringStatus：" + row.authoringStatus,
                });
            }

            var forbiddenRoots = new[]
            {
                AudioAssetPaths.LegacyFormalRoot,
                AudioAssetPaths.LegacyDuplicateRoot,
                AudioAssetPaths.QuarantineRoot,
                AudioAssetPaths.LegacyQuarantineRoot,
            };
            ValidatePathNotForbidden(row.clipKey, key, row.cueId, forbiddenRoots, findings);
            var variants = row.variants ?? Array.Empty<AudioVariantDto>();
            for (var i = 0; i < variants.Length; i++)
            {
                var variant = variants[i];
                if (variant == null)
                {
                    findings.Add(new Finding
                    {
                        Category = "pool-null",
                        BindingKey = key,
                        CueId = row.cueId,
                        Detail = "variants[" + i + "] 为空。",
                    });
                    continue;
                }

                if (variant.weight < 0f)
                {
                    findings.Add(new Finding
                    {
                        Category = "pool-weight",
                        BindingKey = key,
                        CueId = row.cueId,
                        Detail = "变体权重不得为负：" + variant.variantId,
                    });
                }

                if (variant.startOffsetSeconds < 0f)
                {
                    findings.Add(new Finding
                    {
                        Category = "param-range",
                        BindingKey = key,
                        CueId = row.cueId,
                        Detail = "变体起播点不得为负：" + variant.variantId,
                    });
                }

                ValidatePathNotForbidden(variant.clipKey, key, row.cueId, forbiddenRoots, findings);
            }

            if (variants.Length > 0)
            {
                var hasPositive = false;
                for (var i = 0; i < variants.Length; i++)
                {
                    var variant = variants[i];
                    if (variant != null
                        && variant.weight > 0f
                        && !string.IsNullOrWhiteSpace(variant.clipKey))
                    {
                        hasPositive = true;
                        break;
                    }
                }

                if (!hasPositive)
                {
                    findings.Add(new Finding
                    {
                        Category = "empty-pool",
                        BindingKey = key,
                        CueId = row.cueId,
                        Detail = "随机池没有任何有效变体。",
                    });
                }
            }
        }

        private static void ValidateClips(
            AudioBindingDto row,
            string key,
            HashSet<string> formal,
            List<Finding> findings)
        {
            if (formal.Count == 0)
            {
                return;
            }

            var variants = row.variants ?? Array.Empty<AudioVariantDto>();
            if (variants.Length == 0)
            {
                if (string.IsNullOrWhiteSpace(row.clipKey))
                {
                    findings.Add(new Finding
                    {
                        Category = "missing-clip",
                        BindingKey = key,
                        CueId = row.cueId,
                        Detail = "单素材绑定缺少 clipKey。",
                    });
                    return;
                }

                var normalized = AudioAssetManifestLoader.NormalizeKey(row.clipKey);
                if (!formal.Contains(normalized))
                {
                    findings.Add(new Finding
                    {
                        Category = "missing-clip",
                        BindingKey = key,
                        CueId = row.cueId,
                        Detail = "素材键不在正式 Resources 根：" + row.clipKey,
                    });
                }

                return;
            }

            for (var i = 0; i < variants.Length; i++)
            {
                var variant = variants[i];
                if (variant == null || string.IsNullOrWhiteSpace(variant.clipKey) || variant.weight <= 0f)
                {
                    continue;
                }

                var normalized = AudioAssetManifestLoader.NormalizeKey(variant.clipKey);
                if (!formal.Contains(normalized))
                {
                    findings.Add(new Finding
                    {
                        Category = "missing-clip",
                        BindingKey = key,
                        CueId = row.cueId,
                        Detail = "池变体素材键不在正式根：" + variant.clipKey,
                    });
                }
            }
        }

        private static void ValidatePathNotForbidden(
            string clipKey,
            string bindingKey,
            string cueId,
            string[] forbiddenRoots,
            List<Finding> findings)
        {
            if (string.IsNullOrWhiteSpace(clipKey))
            {
                return;
            }

            var normalized = clipKey.Replace('\\', '/');
            if (AudioAssetPaths.IsQuarantinePath(normalized)
                || normalized.IndexOf("拒绝用", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                findings.Add(new Finding
                {
                    Category = "forbidden-path",
                    BindingKey = bindingKey,
                    CueId = cueId,
                    Detail = "引用隔离/拒绝素材：" + clipKey,
                });
                return;
            }

            for (var i = 0; i < forbiddenRoots.Length; i++)
            {
                if (AudioAssetPaths.IsUnder(normalized, forbiddenRoots[i])
                    || normalized.StartsWith("Assets/Arts/", StringComparison.OrdinalIgnoreCase))
                {
                    findings.Add(new Finding
                    {
                        Category = "forbidden-path",
                        BindingKey = bindingKey,
                        CueId = cueId,
                        Detail = "引用旧根或非正式路径：" + clipKey,
                    });
                    return;
                }
            }
        }

        /// <summary>素材键：走 NormalizeKey（去扩展名 / Resources 前缀）。</summary>
        private static HashSet<string> ToClipKeySet(IReadOnlyCollection<string> values)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (values == null)
            {
                return set;
            }

            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    set.Add(AudioAssetManifestLoader.NormalizeKey(value));
                }
            }

            return set;
        }

        /// <summary>
        /// 声明 cueId 只 trim，禁止走 NormalizeKey：
        /// Path.GetExtension 会把 ui.action.press 裁成 ui.action，导致孤儿误报。
        /// </summary>
        private static HashSet<string> ToCueIdSet(IReadOnlyCollection<string> values)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (values == null)
            {
                return set;
            }

            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    set.Add(value.Trim());
                }
            }

            return set;
        }
    }
}
#endif

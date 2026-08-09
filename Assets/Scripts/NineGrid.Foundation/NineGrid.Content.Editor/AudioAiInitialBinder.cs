#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NineGrid.Content.Audio;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    /// <summary>
    /// #178 高权限 AI 初始绑定：扫描声明与正式素材，写入正式 Catalog（标记 aiDraft），
    /// 默认保留 humanConfirmed；可强制全量重绑。隔离区永不入候选。
    /// </summary>
    public static class AudioAiInitialBinder
    {
        public const string ReportAssetPath = AudioAssetPaths.AuditRoot + "/audio-ai-initial-bind-178.json";
        public const int CatalogSchemaVersion = 3;

        public sealed class CueHint
        {
            public string CueId;
            public string Note;
            public string Module;
            public string AuthoritativeEmitter;
        }

        public sealed class ClipCandidate
        {
            public string ResourcesKey;
            public string DisplayName;
            public string AssetPath;
        }

        public sealed class DomainSample
        {
            public string Domain;
            public string CueId;
            public string Note;
            public string ClipKey;
            public bool ClipExists;
        }

        public sealed class Report
        {
            public int schemaVersion = 1;
            public string ticket = "#178";
            public string generatedUtc;
            public bool forceRebindAll;
            public int declarationCount;
            public int formalClipCount;
            public int bindingCount;
            public int addedCount;
            public int updatedDraftCount;
            public int preservedHumanCount;
            public int poolAugmentedCount;
            public int unmatchedCueCount;
            public int unusedClipCount;
            public int abnormalClipCount;
            public string[] unmatchedCues = Array.Empty<string>();
            public string[] unusedClips = Array.Empty<string>();
            public string[] abnormalClips = Array.Empty<string>();
            public string[] hygieneFindings = Array.Empty<string>();
            public DomainSample[] domainSamples = Array.Empty<DomainSample>();
            public string summary;
        }

        public sealed class RunResult
        {
            public AudioBindingCatalogDto Catalog;
            public Report Report;
            public IReadOnlyList<AudioBindingCatalogHygieneValidator.Finding> HygieneFindings;
        }

        [MenuItem("NineGrid/音频/AI 全量初始绑定（保留人工确认）")]
        public static void RunPreserveMenu()
        {
            var result = RunAndWrite(forceRebindAll: false);
            LogResult(result);
        }

        [MenuItem("NineGrid/音频/AI 强制全量重绑（覆盖人工确认）")]
        public static void RunForceMenu()
        {
            if (!EditorUtility.DisplayDialog(
                    "强制全量重绑",
                    "将覆盖全部人工确认绑定，仅保留声明侧语义说明。确定继续？",
                    "强制重绑",
                    "取消"))
            {
                return;
            }

            var result = RunAndWrite(forceRebindAll: true);
            LogResult(result);
        }

        [MenuItem("NineGrid/音频/校验声音绑定 Catalog 卫生")]
        public static void ValidateCatalogMenu()
        {
            var declarations = CollectDeclarations();
            var clips = CollectFormalClips();
            var catalog = LoadCatalogFromDisk();
            var findings = AudioBindingCatalogHygieneValidator.Validate(
                catalog,
                clips.Select(c => c.ResourcesKey).ToArray(),
                declarations.Select(d => d.CueId).ToArray());
            if (findings.Count == 0)
            {
                Debug.Log("[AudioAiInitialBinder] Catalog 卫生 OK。bindings="
                    + (catalog.bindings?.Length ?? 0));
                return;
            }

            Debug.LogError("[AudioAiInitialBinder] Catalog 卫生失败 findings=" + findings.Count);
            var limit = Mathf.Min(findings.Count, 60);
            for (var i = 0; i < limit; i++)
            {
                Debug.LogError("[AudioAiInitialBinder] " + findings[i]);
            }
        }

        public static RunResult RunAndWrite(bool forceRebindAll)
        {
            var declarations = CollectDeclarations();
            var clips = CollectFormalClips();
            var existing = LoadCatalogFromDisk();
            var result = Run(declarations, clips, existing, forceRebindAll);
            WriteCatalog(result.Catalog);
            WriteReport(result.Report);
            AssetDatabase.ImportAsset(AudioBindingCatalogPaths.ManifestAssetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(ReportAssetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return result;
        }

        public static RunResult Run(
            IReadOnlyList<CueHint> declarations,
            IReadOnlyList<ClipCandidate> formalClips,
            AudioBindingCatalogDto existingCatalog,
            bool forceRebindAll)
        {
            declarations ??= Array.Empty<CueHint>();
            formalClips ??= Array.Empty<ClipCandidate>();
            existingCatalog ??= new AudioBindingCatalogDto
            {
                schemaVersion = CatalogSchemaVersion,
                ticket = "#178",
                bindings = Array.Empty<AudioBindingDto>(),
            };

            var existingRows = existingCatalog.bindings ?? Array.Empty<AudioBindingDto>();
            var existingByKey = new Dictionary<string, AudioBindingDto>(StringComparer.Ordinal);
            for (var i = 0; i < existingRows.Length; i++)
            {
                var row = existingRows[i];
                if (row == null || string.IsNullOrWhiteSpace(row.cueId))
                {
                    continue;
                }

                existingByKey[AudioBindingEditorSession.ComputeBindingKey(row)] = Clone(row);
            }

            var clipByKey = new Dictionary<string, ClipCandidate>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < formalClips.Count; i++)
            {
                var clip = formalClips[i];
                if (clip == null || string.IsNullOrWhiteSpace(clip.ResourcesKey))
                {
                    continue;
                }

                clipByKey[AudioAssetManifestLoader.NormalizeKey(clip.ResourcesKey)] = clip;
            }

            var usedClips = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var unmatched = new List<string>();
            var output = new List<AudioBindingDto>();
            var added = 0;
            var updatedDraft = 0;
            var preservedHuman = 0;
            var poolAugmented = 0;

            // Preserve content-specific overrides first (they are not base declarations).
            foreach (var pair in existingByKey)
            {
                var row = pair.Value;
                if (IsBaseBinding(row))
                {
                    continue;
                }

                if (!forceRebindAll && AudioBindingAuthoringStatuses.IsHumanConfirmed(row.authoringStatus))
                {
                    preservedHuman++;
                    MarkUsed(row, usedClips);
                    output.Add(row);
                    continue;
                }

                if (forceRebindAll || AudioBindingAuthoringStatuses.IsAiDraft(row.authoringStatus))
                {
                    if (string.IsNullOrWhiteSpace(row.authoringStatus)
                        || AudioBindingAuthoringStatuses.IsAiDraft(row.authoringStatus))
                    {
                        row.authoringStatus = AudioBindingAuthoringStatuses.AiDraft;
                    }

                    if (forceRebindAll)
                    {
                        row.authoringStatus = AudioBindingAuthoringStatuses.AiDraft;
                    }

                    MarkUsed(row, usedClips);
                    output.Add(row);
                    continue;
                }

                MarkUsed(row, usedClips);
                output.Add(row);
            }

            for (var i = 0; i < declarations.Count; i++)
            {
                var declaration = declarations[i];
                if (declaration == null || string.IsNullOrWhiteSpace(declaration.CueId))
                {
                    continue;
                }

                var key = AudioBindingEditorSession.ComputeBindingKey(new AudioBindingDto
                {
                    cueId = declaration.CueId,
                });
                existingByKey.TryGetValue(key, out var existing);

                if (existing != null
                    && !forceRebindAll
                    && AudioBindingAuthoringStatuses.IsHumanConfirmed(existing.authoringStatus))
                {
                    // humanConfirmed：默认不改写 clipKey/variants（含稀疏池增强）；仅补空 note/module。
                    var preserved = Clone(existing);
                    if (string.IsNullOrWhiteSpace(preserved.note))
                    {
                        preserved.note = declaration.Note;
                    }

                    if (string.IsNullOrWhiteSpace(preserved.module))
                    {
                        preserved.module = declaration.Module;
                    }

                    preservedHuman++;
                    MarkUsed(preserved, usedClips);
                    output.Add(preserved);
                    continue;
                }

                var matched = MatchClip(declaration, formalClips);
                if (matched == null)
                {
                    unmatched.Add(declaration.CueId + " · " + declaration.Note);
                    if (existing != null)
                    {
                        var keep = Clone(existing);
                        keep.authoringStatus = AudioBindingAuthoringStatuses.AiDraft;
                        keep.note = declaration.Note;
                        keep.module = declaration.Module;
                        MarkUsed(keep, usedClips);
                        output.Add(keep);
                        updatedDraft++;
                    }

                    continue;
                }

                AudioBindingDto draft;
                if (existing == null)
                {
                    draft = CreateDraft(declaration, matched.ResourcesKey);
                    added++;
                }
                else
                {
                    draft = Clone(existing);
                    draft.note = declaration.Note;
                    draft.module = declaration.Module;
                    draft.clipKey = matched.ResourcesKey;
                    draft.enabled = true;
                    draft.authoringStatus = AudioBindingAuthoringStatuses.AiDraft;
                    if (draft.variants != null && draft.variants.Length > 0)
                    {
                        // Keep intentional pools when refreshing drafts, but ensure primary key mirrors first variant.
                        draft.clipKey = string.Empty;
                    }
                    else
                    {
                        draft.variants = Array.Empty<AudioVariantDto>();
                    }

                    ApplyDefaultTuning(draft, declaration.CueId);
                    updatedDraft++;
                }

                if (TryAugmentSparsePool(draft, formalClips, clipByKey))
                {
                    poolAugmented++;
                }

                MarkUsed(draft, usedClips);
                output.Add(draft);
            }

            var abnormal = CollectAbnormalClips(formalClips);
            var unused = formalClips
                .Where(c => c != null
                    && !string.IsNullOrWhiteSpace(c.ResourcesKey)
                    && !usedClips.Contains(AudioAssetManifestLoader.NormalizeKey(c.ResourcesKey)))
                .Select(c => c.ResourcesKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v, StringComparer.Ordinal)
                .ToArray();

            var catalog = new AudioBindingCatalogDto
            {
                schemaVersion = CatalogSchemaVersion,
                ticket = "#178",
                bindings = output.ToArray(),
            };

            var hygiene = AudioBindingCatalogHygieneValidator.Validate(
                catalog,
                formalClips.Select(c => c.ResourcesKey).ToArray(),
                declarations.Select(d => d.CueId).ToArray());

            var report = new Report
            {
                generatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                forceRebindAll = forceRebindAll,
                declarationCount = declarations.Count,
                formalClipCount = formalClips.Count,
                bindingCount = catalog.bindings.Length,
                addedCount = added,
                updatedDraftCount = updatedDraft,
                preservedHumanCount = preservedHuman,
                poolAugmentedCount = poolAugmented,
                unmatchedCueCount = unmatched.Count,
                unusedClipCount = unused.Length,
                abnormalClipCount = abnormal.Length,
                unmatchedCues = unmatched.ToArray(),
                unusedClips = unused,
                abnormalClips = abnormal,
                hygieneFindings = hygiene.Select(f => f.ToString()).ToArray(),
                domainSamples = BuildDomainSamples(catalog.bindings, clipByKey),
                summary = "bindings=" + catalog.bindings.Length
                    + " added=" + added
                    + " updatedDraft=" + updatedDraft
                    + " preservedHuman=" + preservedHuman
                    + " pools=" + poolAugmented
                    + " unmatched=" + unmatched.Count
                    + " unused=" + unused.Length
                    + " hygiene=" + hygiene.Count,
            };

            return new RunResult
            {
                Catalog = catalog,
                Report = report,
                HygieneFindings = hygiene,
            };
        }

        public static List<CueHint> CollectDeclarations()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a =>
                {
                    var name = a.GetName().Name ?? string.Empty;
                    return name.StartsWith("NineGrid.", StringComparison.Ordinal)
                        || name == "Assembly-CSharp";
                })
                .ToArray();
            var scan = AudioCueDeclarationScanner.Scan(assemblies);
            var list = new List<CueHint>(scan.Declarations.Count);
            for (var i = 0; i < scan.Declarations.Count; i++)
            {
                var declaration = scan.Declarations[i];
                list.Add(new CueHint
                {
                    CueId = declaration.Attribute.CueId,
                    Note = declaration.Attribute.Note,
                    Module = declaration.Attribute.Module,
                    AuthoritativeEmitter = declaration.Attribute.AuthoritativeEmitter,
                });
            }

            return list
                .GroupBy(d => d.CueId, StringComparer.Ordinal)
                .Select(g => g.First())
                .OrderBy(d => d.CueId, StringComparer.Ordinal)
                .ToList();
        }

        public static List<ClipCandidate> CollectFormalClips()
        {
            var result = new List<ClipCandidate>();
            var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioAssetPaths.SfxRoot });
            for (var i = 0; i < guids.Length; i++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(assetPath)
                    || !AudioAssetPaths.IsFormalAudioPath(assetPath)
                    || AudioAssetPaths.IsQuarantinePath(assetPath))
                {
                    continue;
                }

                var resourcesKey = ToResourcesKey(assetPath);
                if (string.IsNullOrEmpty(resourcesKey))
                {
                    continue;
                }

                result.Add(new ClipCandidate
                {
                    AssetPath = assetPath.Replace('\\', '/'),
                    ResourcesKey = resourcesKey,
                    DisplayName = Path.GetFileNameWithoutExtension(assetPath),
                });
            }

            return result
                .OrderBy(c => c.ResourcesKey, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static ClipCandidate MatchClip(CueHint cue, IReadOnlyList<ClipCandidate> clips)
        {
            if (cue == null || clips == null || clips.Count == 0)
            {
                return null;
            }

            if (TryExactHint(cue.CueId, out var exactKey))
            {
                var exact = FindByKey(clips, exactKey);
                if (exact != null)
                {
                    return exact;
                }
            }

            var best = (ClipCandidate)null;
            var bestScore = 0;
            for (var i = 0; i < clips.Count; i++)
            {
                var clip = clips[i];
                var score = Score(cue, clip);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = clip;
                }
            }

            if (best != null && bestScore >= 8)
            {
                return best;
            }

            if (TryModuleFallback(cue, out var fallbackKey))
            {
                return FindByKey(clips, fallbackKey) ?? best;
            }

            return bestScore >= 4 ? best : null;
        }

        private static bool TryAugmentSparsePool(
            AudioBindingDto row,
            IReadOnlyList<ClipCandidate> formalClips,
            Dictionary<string, ClipCandidate> clipByKey)
        {
            if (row == null || !IsHighFrequencyCue(row.cueId))
            {
                return false;
            }

            var existingVariants = row.variants ?? Array.Empty<AudioVariantDto>();
            if (existingVariants.Length >= 2)
            {
                return false;
            }

            if (!TryPoolCandidates(row.cueId, out var poolKeys))
            {
                return false;
            }

            var primary = string.IsNullOrWhiteSpace(row.clipKey)
                ? (existingVariants.Length > 0 ? existingVariants[0].clipKey : null)
                : row.clipKey;
            var keys = new List<string>();
            var keySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(primary))
            {
                var normalizedPrimary = AudioAssetManifestLoader.NormalizeKey(primary);
                if (keySet.Add(normalizedPrimary))
                {
                    keys.Add(normalizedPrimary);
                }
            }

            for (var i = 0; i < poolKeys.Length; i++)
            {
                var key = AudioAssetManifestLoader.NormalizeKey(poolKeys[i]);
                if (clipByKey.ContainsKey(key) && keySet.Add(key))
                {
                    keys.Add(key);
                }

                if (keys.Count >= 3)
                {
                    break;
                }
            }

            if (keys.Count < 2)
            {
                return false;
            }

            var variants = new AudioVariantDto[keys.Count];
            for (var i = 0; i < keys.Count; i++)
            {
                variants[i] = new AudioVariantDto
                {
                    variantId = "v" + (i + 1),
                    clipKey = keys[i],
                    weight = 1f,
                    volumeTrimDb = i == 0 ? 0f : -1f,
                    startOffsetSeconds = 0f,
                };
            }

            row.variants = variants;
            row.clipKey = string.Empty;
            return true;
        }

        private static bool IsHighFrequencyCue(string cueId)
        {
            return cueId == "ui.action.hover"
                || cueId == "ui.main_menu.hover"
                || cueId == "ui.action.press"
                || cueId == "card.hand.hover"
                || cueId == "card.ground.hover"
                || cueId == "card.deck.hover"
                || cueId == "battle.attack.hit"
                || cueId == "battle.combat.hp_damage"
                || cueId == "economy.gold_gain"
                || cueId == "sfx.effect.trigger";
        }

        private static bool TryPoolCandidates(string cueId, out string[] keys)
        {
            switch (cueId)
            {
                case "card.hand.hover":
                case "card.ground.hover":
                case "card.deck.hover":
                    keys = new[]
                    {
                        "audio/SFX/卡牌聚焦",
                        "audio/SFX/界面聚焦",
                        "audio/SFX/点击1增强",
                    };
                    return true;
                case "ui.action.hover":
                case "ui.main_menu.hover":
                    keys = new[]
                    {
                        "audio/SFX/界面点击",
                        "audio/SFX/界面聚焦",
                        "audio/SFX/点击1增强",
                    };
                    return true;
                case "ui.action.press":
                    keys = new[]
                    {
                        "audio/SFX/按钮点击",
                        "audio/SFX/点击1增强",
                        "audio/SFX/点击2增强",
                    };
                    return true;
                case "battle.attack.hit":
                    keys = new[]
                    {
                        "audio/SFX/斩击",
                        "audio/SFX/挥动增强",
                        "audio/SFX/挥动2增强",
                    };
                    return true;
                case "battle.combat.hp_damage":
                    keys = new[]
                    {
                        "audio/SFX/物品击中",
                        "audio/SFX/剑受击",
                        "audio/SFX/用力增强",
                    };
                    return true;
                case "economy.gold_gain":
                    keys = new[]
                    {
                        "audio/SFX/硬币叮当",
                        "audio/SFX/小硬币掉落",
                        "audio/SFX/掉落硬币",
                    };
                    return true;
                case "sfx.effect.trigger":
                    keys = new[]
                    {
                        "audio/SFX/准备法术增强.FG",
                        "audio/SFX/准备法术2增强.FG",
                        "audio/SFX/施法增强",
                    };
                    return true;
                default:
                    keys = Array.Empty<string>();
                    return false;
            }
        }

        private static bool TryExactHint(string cueId, out string clipKey)
        {
            switch (cueId)
            {
                case "ui.main_menu.start":
                case "ui.main_menu.press":
                case "ui.action.press":
                    clipKey = "audio/SFX/按钮点击";
                    return true;
                case "ui.main_menu.hover":
                case "ui.action.hover":
                    clipKey = "audio/SFX/界面点击";
                    return true;
                case "ui.action.confirm":
                case "card.interaction.select":
                    clipKey = "audio/SFX/点击确认轻音";
                    return true;
                case "ui.action.cancel":
                case "ui.main_menu.cancel":
                case "ui.player_audio.escape":
                case "flow.room.leave":
                case "reward.abandon":
                    clipKey = "audio/SFX/关门";
                    return true;
                case "ui.action.reject":
                case "ui.main_menu.reject":
                case "shop.insufficient_gold":
                case "tavern.insufficient_gold":
                case "flow.defeat":
                    clipKey = "audio/SFX/失败铃声 00增强";
                    return true;
                case "card.hand.hover":
                case "card.ground.hover":
                case "card.deck.hover":
                    clipKey = "audio/SFX/卡牌聚焦";
                    return true;
                case "card.drag.pickup":
                case "card.lifecycle.draw":
                    clipKey = "audio/SFX/卡牌取出包装1增强";
                    return true;
                case "card.drag.drop":
                case "card.lifecycle.move":
                case "card.lifecycle.swap":
                    clipKey = "audio/SFX/卡牌滑动2增强";
                    return true;
                case "card.drag.valid":
                case "card.lifecycle.into_field":
                    clipKey = "audio/SFX/卡牌放置1增强";
                    return true;
                case "card.drag.return":
                case "card.lifecycle.into_hand":
                    clipKey = "audio/SFX/放置卡片";
                    return true;
                case "card.lifecycle.deal":
                    clipKey = "audio/SFX/添加卡牌";
                    return true;
                case "card.lifecycle.shuffle":
                    clipKey = "audio/SFX/洗牌_短版";
                    return true;
                case "card.lifecycle.flip":
                    clipKey = "audio/SFX/翻牌";
                    return true;
                case "card.lifecycle.rotate":
                    clipKey = "audio/SFX/复古嗖声";
                    return true;
                case "card.lifecycle.item_use":
                    clipKey = "audio/SFX/药水使用增强.FG";
                    return true;
                case "card.lifecycle.recycle":
                case "economy.gold_gain":
                    clipKey = "audio/SFX/硬币叮当";
                    return true;
                case "card.lifecycle.exit":
                    clipKey = "audio/SFX/撤退增强.FG";
                    return true;
                case "card.lifecycle.shatter":
                    clipKey = "audio/SFX/卡牌摧毁增强";
                    return true;
                case "battle.attack.prepare":
                case "battle.attack.charge":
                    clipKey = "audio/SFX/挥动增强";
                    return true;
                case "battle.attack.hit":
                    clipKey = "audio/SFX/斩击";
                    return true;
                case "battle.combat.block":
                    clipKey = "audio/SFX/护甲格挡4增强";
                    return true;
                case "battle.combat.armor_absorb":
                    clipKey = "audio/SFX/剑受击";
                    return true;
                case "battle.combat.hp_damage":
                    clipKey = "audio/SFX/物品击中";
                    return true;
                case "battle.combat.heal":
                    clipKey = "audio/SFX/短治疗";
                    return true;
                case "battle.combat.death":
                    clipKey = "audio/SFX/英雄死亡增强.FG";
                    return true;
                case "flow.room.enter":
                    clipKey = "audio/SFX/商店门打开";
                    return true;
                case "flow.run.floor_cross":
                case "flow.run.transition":
                    clipKey = "audio/SFX/房间过渡";
                    return true;
                case "shop.buy":
                    clipKey = "audio/SFX/购买物品";
                    return true;
                case "shop.refresh":
                case "tavern.refresh":
                    clipKey = "audio/SFX/按钮点击";
                    return true;
                case "shop.upgrade":
                    clipKey = "audio/SFX/卡牌升级";
                    return true;
                case "tavern.buy":
                    clipKey = "audio/SFX/商店物品";
                    return true;
                case "reward.claim":
                    clipKey = "audio/SFX/打开关卡解锁";
                    return true;
                case "attribute.pick":
                case "sfx.relic.trigger":
                    clipKey = "audio/SFX/解锁增强";
                    return true;
                case "economy.gold_spend":
                    clipKey = "audio/SFX/掉落硬币";
                    return true;
                case "flow.victory":
                    clipKey = "audio/SFX/解锁关卡秘密";
                    return true;
                case "flow.return_main_menu":
                    clipKey = "audio/SFX/打开菜单";
                    return true;
                case "sfx.effect.trigger":
                case "sfx.skill.trigger":
                    clipKey = "audio/SFX/准备法术增强.FG";
                    return true;
                case "sfx.trap.trigger":
                    clipKey = "audio/SFX/准备法术2增强.FG";
                    return true;
                default:
                    clipKey = null;
                    return false;
            }
        }

        private static bool TryModuleFallback(CueHint cue, out string clipKey)
        {
            var module = cue.Module ?? string.Empty;
            if (module.IndexOf("UI", StringComparison.OrdinalIgnoreCase) >= 0
                || module.IndexOf("MainMenu", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                clipKey = "audio/SFX/按钮点击";
                return true;
            }

            if (module.IndexOf("Card", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                clipKey = "audio/SFX/放置卡片";
                return true;
            }

            if (module.IndexOf("Battle", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                clipKey = "audio/SFX/斩击";
                return true;
            }

            clipKey = "audio/SFX/界面点击";
            return true;
        }

        private static int Score(CueHint cue, ClipCandidate clip)
        {
            if (cue == null || clip == null)
            {
                return 0;
            }

            var note = cue.Note ?? string.Empty;
            var name = clip.DisplayName ?? string.Empty;
            var score = 0;
            if (!string.IsNullOrEmpty(note) && name.IndexOf(note, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                score += 20;
            }

            foreach (var token in Tokenize(note))
            {
                if (token.Length >= 2 && name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    score += 4;
                }
            }

            foreach (var token in Tokenize(cue.CueId))
            {
                if (token.Length >= 3 && name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    score += 2;
                }
            }

            return score;
        }

        private static IEnumerable<string> Tokenize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            var buffer = new StringBuilder();
            for (var i = 0; i < value.Length; i++)
            {
                var ch = value[i];
                if (char.IsLetterOrDigit(ch) || ch > 127)
                {
                    buffer.Append(ch);
                    continue;
                }

                if (buffer.Length > 0)
                {
                    yield return buffer.ToString();
                    buffer.Length = 0;
                }
            }

            if (buffer.Length > 0)
            {
                yield return buffer.ToString();
            }
        }

        private static AudioBindingDto CreateDraft(CueHint declaration, string clipKey)
        {
            var dto = new AudioBindingDto
            {
                cueId = declaration.CueId,
                note = declaration.Note,
                module = declaration.Module,
                enabled = true,
                clipKey = clipKey,
                volumeDb = -4f,
                startOffsetSeconds = 0f,
                bindingDelaySeconds = 0f,
                minimumIntervalSeconds = 0.05f,
                variants = Array.Empty<AudioVariantDto>(),
                selectorCardDefId = string.Empty,
                selectorSkillId = string.Empty,
                selectorRoomId = string.Empty,
                selectorItemDefId = string.Empty,
                selectorContentId = string.Empty,
                authoringStatus = AudioBindingAuthoringStatuses.AiDraft,
            };
            ApplyDefaultTuning(dto, declaration.CueId);
            return dto;
        }

        private static void ApplyDefaultTuning(AudioBindingDto dto, string cueId)
        {
            if (dto == null)
            {
                return;
            }

            if (cueId != null && cueId.IndexOf("hover", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                dto.volumeDb = -8f;
                dto.minimumIntervalSeconds = 0.12f;
            }
            else if (cueId != null && cueId.IndexOf("press", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                dto.volumeDb = -4f;
                dto.minimumIntervalSeconds = 0.04f;
            }
            else if (cueId == "battle.attack.charge")
            {
                dto.volumeDb = -6f;
                dto.minimumIntervalSeconds = 0.05f;
            }
        }

        private static DomainSample[] BuildDomainSamples(
            AudioBindingDto[] bindings,
            Dictionary<string, ClipCandidate> clipByKey)
        {
            var domains = new[]
            {
                ("MainMenu/UI", new[] { "ui.main_menu.start", "ui.action.confirm", "ui.action.hover" }),
                ("CardsInteraction", new[] { "card.hand.hover", "card.drag.pickup", "card.drag.valid" }),
                ("CardLifecycle", new[] { "card.lifecycle.deal", "card.lifecycle.shuffle", "card.lifecycle.flip" }),
                ("Combat", new[] { "battle.attack.hit", "battle.combat.block", "battle.combat.heal" }),
                ("SkillEffectTrapRelic", new[] { "sfx.skill.trigger", "sfx.effect.trigger", "sfx.trap.trigger", "sfx.relic.trigger" }),
                ("RoomEconomyFlow", new[] { "flow.room.enter", "shop.buy", "economy.gold_gain", "flow.victory" }),
            };

            var byCue = new Dictionary<string, AudioBindingDto>(StringComparer.Ordinal);
            for (var i = 0; i < (bindings?.Length ?? 0); i++)
            {
                var row = bindings[i];
                if (row == null || string.IsNullOrWhiteSpace(row.cueId) || !IsBaseBinding(row))
                {
                    continue;
                }

                if (!byCue.ContainsKey(row.cueId))
                {
                    byCue[row.cueId] = row;
                }
            }

            var samples = new List<DomainSample>();
            for (var d = 0; d < domains.Length; d++)
            {
                var domain = domains[d];
                for (var i = 0; i < domain.Item2.Length; i++)
                {
                    var cueId = domain.Item2[i];
                    if (!byCue.TryGetValue(cueId, out var row))
                    {
                        continue;
                    }

                    var clipKey = ResolvePrimaryClip(row);
                    samples.Add(new DomainSample
                    {
                        Domain = domain.Item1,
                        CueId = cueId,
                        Note = row.note,
                        ClipKey = clipKey,
                        ClipExists = !string.IsNullOrEmpty(clipKey)
                            && clipByKey.ContainsKey(AudioAssetManifestLoader.NormalizeKey(clipKey)),
                    });
                    break;
                }
            }

            return samples.ToArray();
        }

        private static string ResolvePrimaryClip(AudioBindingDto row)
        {
            if (!string.IsNullOrWhiteSpace(row.clipKey))
            {
                return row.clipKey;
            }

            var variants = row.variants ?? Array.Empty<AudioVariantDto>();
            for (var i = 0; i < variants.Length; i++)
            {
                if (variants[i] != null && !string.IsNullOrWhiteSpace(variants[i].clipKey))
                {
                    return variants[i].clipKey;
                }
            }

            return string.Empty;
        }

        private static string[] CollectAbnormalClips(IReadOnlyList<ClipCandidate> clips)
        {
            var list = new List<string>();
            for (var i = 0; i < clips.Count; i++)
            {
                var clip = clips[i];
                if (clip == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(clip.DisplayName)
                    || clip.DisplayName.IndexOf("未知", StringComparison.Ordinal) >= 0
                    || clip.DisplayName.EndsWith("旧", StringComparison.Ordinal)
                    || clip.ResourcesKey.IndexOf("界面点击旧", StringComparison.Ordinal) >= 0)
                {
                    list.Add(clip.ResourcesKey + " · abnormal-or-legacy-name");
                }
            }

            return list.OrderBy(v => v, StringComparer.Ordinal).ToArray();
        }

        private static void MarkUsed(AudioBindingDto row, HashSet<string> used)
        {
            if (row == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(row.clipKey))
            {
                used.Add(AudioAssetManifestLoader.NormalizeKey(row.clipKey));
            }

            var variants = row.variants ?? Array.Empty<AudioVariantDto>();
            for (var i = 0; i < variants.Length; i++)
            {
                if (variants[i] != null && !string.IsNullOrWhiteSpace(variants[i].clipKey))
                {
                    used.Add(AudioAssetManifestLoader.NormalizeKey(variants[i].clipKey));
                }
            }
        }

        private static bool IsBaseBinding(AudioBindingDto row)
        {
            return row != null
                && string.IsNullOrEmpty(row.selectorCardDefId)
                && string.IsNullOrEmpty(row.selectorSkillId)
                && string.IsNullOrEmpty(row.selectorRoomId)
                && string.IsNullOrEmpty(row.selectorItemDefId)
                && string.IsNullOrEmpty(row.selectorContentId);
        }

        private static ClipCandidate FindByKey(IReadOnlyList<ClipCandidate> clips, string key)
        {
            var normalized = AudioAssetManifestLoader.NormalizeKey(key);
            for (var i = 0; i < clips.Count; i++)
            {
                if (string.Equals(
                        AudioAssetManifestLoader.NormalizeKey(clips[i].ResourcesKey),
                        normalized,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return clips[i];
                }
            }

            return null;
        }

        private static AudioBindingDto Clone(AudioBindingDto source)
        {
            return AudioBindingEditorSession.CloneDto(source);
        }

        private static AudioBindingCatalogDto LoadCatalogFromDisk()
        {
            var absolute = Path.GetFullPath(AudioBindingCatalogPaths.ManifestAssetPath);
            if (!Path.IsPathRooted(AudioBindingCatalogPaths.ManifestAssetPath))
            {
                absolute = Path.GetFullPath(
                    Path.Combine(Directory.GetCurrentDirectory(), AudioBindingCatalogPaths.ManifestAssetPath));
            }

            if (!File.Exists(absolute))
            {
                var projectRelative = Path.Combine(
                    Application.dataPath,
                    "..",
                    AudioBindingCatalogPaths.ManifestAssetPath);
                absolute = Path.GetFullPath(projectRelative);
            }

            if (!File.Exists(absolute))
            {
                return new AudioBindingCatalogDto
                {
                    schemaVersion = CatalogSchemaVersion,
                    ticket = "#178",
                    bindings = Array.Empty<AudioBindingDto>(),
                };
            }

            var json = File.ReadAllText(absolute, Encoding.UTF8);
            var catalog = JsonUtility.FromJson<AudioBindingCatalogDto>(json);
            catalog ??= new AudioBindingCatalogDto();
            catalog.bindings ??= Array.Empty<AudioBindingDto>();
            return catalog;
        }

        private static void WriteCatalog(AudioBindingCatalogDto catalog)
        {
            var absolute = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", AudioBindingCatalogPaths.ManifestAssetPath));
            Directory.CreateDirectory(Path.GetDirectoryName(absolute) ?? absolute);
            File.WriteAllText(absolute, FormatCatalogJson(catalog), new UTF8Encoding(false));
        }

        private static void WriteReport(Report report)
        {
            var absolute = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ReportAssetPath));
            Directory.CreateDirectory(Path.GetDirectoryName(absolute) ?? absolute);
            File.WriteAllText(absolute, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
        }

        /// <summary>保留 UTF-8 中文可读性，避免 JsonUtility 的 \\u 转义。</summary>
        public static string FormatCatalogJson(AudioBindingCatalogDto catalog)
        {
            var sb = new StringBuilder(64 * 1024);
            sb.Append("{\n");
            sb.Append("    \"schemaVersion\": ").Append(catalog.schemaVersion).Append(",\n");
            sb.Append("    \"ticket\": ").Append(JsonString(catalog.ticket)).Append(",\n");
            sb.Append("    \"bindings\": [\n");
            var bindings = catalog.bindings ?? Array.Empty<AudioBindingDto>();
            for (var i = 0; i < bindings.Length; i++)
            {
                AppendBinding(sb, bindings[i], "        ");
                if (i + 1 < bindings.Length)
                {
                    sb.Append(',');
                }

                sb.Append('\n');
            }

            sb.Append("    ]\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        private static void AppendBinding(StringBuilder sb, AudioBindingDto row, string indent)
        {
            sb.Append(indent).Append("{\n");
            AppendField(sb, indent, "cueId", row.cueId, true);
            AppendField(sb, indent, "note", row.note, true);
            AppendField(sb, indent, "module", row.module, true);
            sb.Append(indent).Append("    \"enabled\": ").Append(row.enabled ? "true" : "false").Append(",\n");
            AppendField(sb, indent, "clipKey", row.clipKey ?? string.Empty, true);
            sb.Append(indent).Append("    \"volumeDb\": ").Append(FormatFloat(row.volumeDb)).Append(",\n");
            sb.Append(indent).Append("    \"startOffsetSeconds\": ").Append(FormatFloat(row.startOffsetSeconds)).Append(",\n");
            sb.Append(indent).Append("    \"bindingDelaySeconds\": ").Append(FormatFloat(row.bindingDelaySeconds)).Append(",\n");
            sb.Append(indent).Append("    \"minimumIntervalSeconds\": ").Append(FormatFloat(row.minimumIntervalSeconds)).Append(",\n");
            sb.Append(indent).Append("    \"variants\": ");
            AppendVariants(sb, row.variants, indent);
            sb.Append(",\n");
            AppendField(sb, indent, "selectorCardDefId", row.selectorCardDefId ?? string.Empty, true);
            AppendField(sb, indent, "selectorSkillId", row.selectorSkillId ?? string.Empty, true);
            AppendField(sb, indent, "selectorRoomId", row.selectorRoomId ?? string.Empty, true);
            AppendField(sb, indent, "selectorItemDefId", row.selectorItemDefId ?? string.Empty, true);
            AppendField(sb, indent, "selectorContentId", row.selectorContentId ?? string.Empty, true);
            AppendField(sb, indent, "authoringStatus", row.authoringStatus ?? string.Empty, false);
            sb.Append(indent).Append('}');
        }

        private static void AppendVariants(StringBuilder sb, AudioVariantDto[] variants, string indent)
        {
            variants ??= Array.Empty<AudioVariantDto>();
            if (variants.Length == 0)
            {
                sb.Append("[]");
                return;
            }

            sb.Append("[\n");
            for (var i = 0; i < variants.Length; i++)
            {
                var variant = variants[i] ?? new AudioVariantDto();
                sb.Append(indent).Append("        {\n");
                sb.Append(indent).Append("            \"variantId\": ").Append(JsonString(variant.variantId)).Append(",\n");
                sb.Append(indent).Append("            \"clipKey\": ").Append(JsonString(variant.clipKey)).Append(",\n");
                sb.Append(indent).Append("            \"weight\": ").Append(FormatFloat(variant.weight)).Append(",\n");
                sb.Append(indent).Append("            \"volumeTrimDb\": ").Append(FormatFloat(variant.volumeTrimDb)).Append(",\n");
                sb.Append(indent).Append("            \"startOffsetSeconds\": ").Append(FormatFloat(variant.startOffsetSeconds)).Append('\n');
                sb.Append(indent).Append("        }");
                if (i + 1 < variants.Length)
                {
                    sb.Append(',');
                }

                sb.Append('\n');
            }

            sb.Append(indent).Append("    ]");
        }

        private static void AppendField(StringBuilder sb, string indent, string name, string value, bool trailingComma)
        {
            sb.Append(indent).Append("    \"").Append(name).Append("\": ").Append(JsonString(value));
            if (trailingComma)
            {
                sb.Append(',');
            }

            sb.Append('\n');
        }

        private static string JsonString(string value)
        {
            value ??= string.Empty;
            var sb = new StringBuilder(value.Length + 8);
            sb.Append('"');
            for (var i = 0; i < value.Length; i++)
            {
                var ch = value[i];
                switch (ch)
                {
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        sb.Append(ch);
                        break;
                }
            }

            sb.Append('"');
            return sb.ToString();
        }

        private static string FormatFloat(float value)
        {
            if (Mathf.Approximately(value, Mathf.Round(value)))
            {
                return ((int)Mathf.Round(value)).ToString(CultureInfo.InvariantCulture);
            }

            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string ToResourcesKey(string assetPath)
        {
            var normalized = (assetPath ?? string.Empty).Replace('\\', '/');
            const string prefix = "Assets/Resources/";
            if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var relative = normalized.Substring(prefix.Length);
            var dot = relative.LastIndexOf('.');
            if (dot > 0)
            {
                relative = relative.Substring(0, dot);
            }

            return relative;
        }

        private static void LogResult(RunResult result)
        {
            if (result?.Report == null)
            {
                Debug.LogError("[AudioAiInitialBinder] empty result");
                return;
            }

            Debug.Log("[AudioAiInitialBinder] " + result.Report.summary + " report=" + ReportAssetPath);
            if (result.HygieneFindings != null && result.HygieneFindings.Count > 0)
            {
                Debug.LogWarning("[AudioAiInitialBinder] hygiene findings=" + result.HygieneFindings.Count);
            }
        }
    }
}
#endif

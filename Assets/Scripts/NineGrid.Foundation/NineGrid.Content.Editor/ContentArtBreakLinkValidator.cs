using System;
using System.Collections.Generic;
using System.IO;
using NineGrid.Cards.Anim;
using NineGrid.Content.CardPresentation;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    /// <summary>
    /// 内容 JSON 资产路径断链校验（ADR-0008 强制代价对冲）。
    /// 扫描 Authoring / Streaming 卡牌 JSON，解析不到的路径记为失败项。
    /// </summary>
    public static class ContentArtBreakLinkValidator
    {
        public sealed class Finding
        {
            public string ContentId;
            public string Field;
            public string Kind;
            public string Path;
            public string Reason;
        }

        public static List<Finding> ValidateAuthoringCards()
        {
            return ValidateCardsFolder(CardPresentationJsonIO.AuthoringFolder);
        }

        public static List<Finding> ValidateStreamingCards()
        {
            return ValidateCardsFolder(CardPresentationJsonIO.StreamingFolder);
        }

        public static List<Finding> ValidateCardsFolder(string cardsFolderAssetPath)
        {
            var findings = new List<Finding>();
            if (string.IsNullOrWhiteSpace(cardsFolderAssetPath))
            {
                findings.Add(new Finding
                {
                    ContentId = string.Empty,
                    Field = "folder",
                    Kind = "folder",
                    Path = cardsFolderAssetPath ?? string.Empty,
                    Reason = "Cards folder path is empty."
                });
                return findings;
            }

            var absolute = ToAbsolute(cardsFolderAssetPath);
            if (!Directory.Exists(absolute))
            {
                findings.Add(new Finding
                {
                    ContentId = string.Empty,
                    Field = "folder",
                    Kind = "folder",
                    Path = cardsFolderAssetPath,
                    Reason = "Cards folder missing: " + absolute
                });
                return findings;
            }

            var files = Directory.GetFiles(absolute, "*.json", SearchOption.TopDirectoryOnly);
            var entries = new List<CardPresentationAssetPathEntry>(64);
            for (var i = 0; i < files.Length; i++)
            {
                var name = Path.GetFileName(files[i]);
                if (string.Equals(name, CardPresentationJsonIO.IndexFileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!CardPresentationJsonIO.TryLoad(files[i], out var dto, out var error) || dto == null)
                {
                    findings.Add(new Finding
                    {
                        ContentId = name,
                        Field = "json",
                        Kind = "json",
                        Path = files[i],
                        Reason = string.IsNullOrEmpty(error) ? "Failed to load JSON." : error
                    });
                    continue;
                }

                entries.Clear();
                CardPresentationContentArt.CollectAssetPathEntries(dto, entries);
                for (var e = 0; e < entries.Count; e++)
                {
                    if (TryResolve(entries[e], out var reason))
                    {
                        continue;
                    }

                    findings.Add(new Finding
                    {
                        ContentId = entries[e].ContentId,
                        Field = entries[e].Field,
                        Kind = entries[e].Kind,
                        Path = entries[e].Path,
                        Reason = reason
                    });
                }
            }

            return findings;
        }

        /// <summary>
        /// 解析单条路径条目。sprite → LoadSprite 非 null；folder/atlas → LoadFrames 非空。
        /// </summary>
        public static bool TryResolve(CardPresentationAssetPathEntry entry, out string reason)
        {
            reason = null;
            if (entry == null || string.IsNullOrWhiteSpace(entry.Path))
            {
                reason = "Empty path entry.";
                return false;
            }

            var kind = string.IsNullOrWhiteSpace(entry.Kind)
                ? "sprite"
                : entry.Kind.Trim().ToLowerInvariant();

            switch (kind)
            {
                case "sprite":
                {
                    var sprite = CardPresentationSpritePath.LoadSprite(entry.Path);
                    if (sprite == null)
                    {
                        reason = "Sprite unresolved.";
                        return false;
                    }

                    return true;
                }
                case "folder":
                case "atlas":
                {
                    var frames = CardAnimFrameSource.LoadFrames(kind, entry.Path);
                    if (frames == null || frames.Length == 0)
                    {
                        reason = kind + " frames unresolved.";
                        return false;
                    }

                    return true;
                }
                case "prefab":
                {
                    if (UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(entry.Path) == null)
                    {
                        reason = "Prefab unresolved.";
                        return false;
                    }

                    return true;
                }
                default:
                    reason = "Unknown kind: " + kind;
                    return false;
            }
        }

        [MenuItem("NineGrid/Tools/Validate Content Art Break-Links")]
        public static void ValidateMenu()
        {
            var findings = ValidateAuthoringCards();
            if (findings.Count == 0)
            {
                Debug.Log("[ContentArtBreakLink] OK — authoring cards all resolve.");
                return;
            }

            Debug.LogError("[ContentArtBreakLink] Failures=" + findings.Count);
            var limit = Mathf.Min(findings.Count, 40);
            for (var i = 0; i < limit; i++)
            {
                var f = findings[i];
                Debug.LogError(
                    "[ContentArtBreakLink] " + f.ContentId + " " + f.Field + " " + f.Kind + " → " + f.Path
                    + " | " + f.Reason);
            }
        }

        /// <summary>
        /// 内容卫生总校验（#140）：断链 + 索引↔磁盘 + Authoring/Streaming 双写 + skillIds→技能 +
        /// 装配→模板 + 模板 body 引用 + 空壳技能 + 归档可达性。Editor 汇总入口。
        /// </summary>
        [MenuItem("NineGrid/Tools/Validate Content Hygiene (Index/Mirror/Skill/Template)")]
        public static void ValidateHygieneMenu()
        {
            var failures = 0;
            var breakLinks = ValidateAuthoringCards();
            if (breakLinks.Count > 0)
            {
                failures += breakLinks.Count;
                Debug.LogError("[ContentHygiene] Break-links=" + breakLinks.Count);
                var limit = Mathf.Min(breakLinks.Count, 20);
                for (var i = 0; i < limit; i++)
                {
                    var f = breakLinks[i];
                    Debug.LogError(
                        "[ContentHygiene] " + f.ContentId + " " + f.Field + " " + f.Kind + " → " + f.Path
                        + " | " + f.Reason);
                }
            }

            var hygiene = ContentHygieneValidator.ValidateAll();
            if (hygiene.Count > 0)
            {
                failures += hygiene.Count;
                Debug.LogError("[ContentHygiene] Failures=" + hygiene.Count);
                var limit = Mathf.Min(hygiene.Count, 60);
                for (var i = 0; i < limit; i++)
                {
                    Debug.LogError("[ContentHygiene] " + hygiene[i]);
                }
            }

            if (failures == 0)
            {
                Debug.Log("[ContentHygiene] OK — authoring cards resolve; index/mirror/skills/templates consistent.");
            }
        }

        private static string ToAbsolute(string assetPath)
        {
            var normalized = assetPath.Replace('\\', '/');
            if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                var project = Directory.GetParent(Application.dataPath)?.FullName
                              ?? Application.dataPath;
                return Path.GetFullPath(Path.Combine(project, normalized));
            }

            return Path.GetFullPath(normalized);
        }
    }
}

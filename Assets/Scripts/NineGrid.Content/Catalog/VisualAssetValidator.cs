using System.Collections.Generic;
using System.Linq;
using NineGrid.Core.Content;

namespace NineGrid.Content
{
    public static class VisualAssetValidator
    {
        public sealed class ValidationIssue
        {
            public ValidationIssue(string severity, string message)
            {
                Severity = severity;
                Message = message;
            }

            public string Severity { get; }
            public string Message { get; }
        }

        public static List<ValidationIssue> Validate(
            cfg.Tables tables,
            GameContentCatalog coreCatalog = null)
        {
            var issues = new List<ValidationIssue>();
            if (tables == null)
            {
                issues.Add(new ValidationIssue("error", "Luban tables are null."));
                return issues;
            }

            var visualAssetIds = new HashSet<string>();
            var assetKeyOwners = new Dictionary<string, string>();
            foreach (var row in tables.TbVisualAsset.DataList)
            {
                if (!visualAssetIds.Add(row.VisualId))
                {
                    issues.Add(new ValidationIssue("error", "Duplicate visual_id: " + row.VisualId));
                }

                if (!string.IsNullOrEmpty(row.AssetKey))
                {
                    string owner;
                    if (assetKeyOwners.TryGetValue(row.AssetKey, out owner) && owner != row.VisualId)
                    {
                        issues.Add(new ValidationIssue(
                            "warn",
                            "asset_key reused: " + row.AssetKey + " by " + owner + " and " + row.VisualId));
                    }
                    else
                    {
                        assetKeyOwners[row.AssetKey] = row.VisualId;
                    }
                }
            }

            foreach (var styleId in CardFrameStyleCatalog.RequiredStyleIds)
            {
                if (tables.TbCardFrameStyle.GetOrDefault(styleId) == null)
                {
                    issues.Add(new ValidationIssue("error", "Missing card frame style: " + styleId));
                }
            }

            foreach (var row in tables.TbContentVisual.DataList)
            {
                if (!string.IsNullOrEmpty(row.FrameKey))
                {
                    issues.Add(new ValidationIssue(
                        "warn",
                        "Deprecated frame_key on " + row.ContentId + ": please clear this column."));
                }

                ValidateVisualReference(issues, row.ContentId, "icon_key", row.IconKey, visualAssetIds);
                ValidateVisualReference(issues, row.ContentId, "face_key", row.FaceKey, visualAssetIds);
            }

            if (HasFallbackCycle(tables))
            {
                issues.Add(new ValidationIssue("error", "Visual asset fallback cycle detected."));
            }

            return issues;
        }

        private static void ValidateVisualReference(
            List<ValidationIssue> issues,
            string contentId,
            string column,
            string key,
            HashSet<string> visualAssetIds)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            if (VisualIdNaming.IsVisualId(key) && !visualAssetIds.Contains(key))
            {
                issues.Add(new ValidationIssue(
                    "error",
                    "content_visual / " + contentId + " / " + column + " / " + key + " -> Visual asset not found"));
            }
        }

        private static bool HasFallbackCycle(cfg.Tables tables)
        {
            var fallbackMap = tables.TbVisualAsset.DataList
                .Where(row => !string.IsNullOrEmpty(row.FallbackId))
                .ToDictionary(row => row.VisualId, row => row.FallbackId);

            foreach (var start in fallbackMap.Keys)
            {
                var visited = new HashSet<string>();
                var current = start;
                while (!string.IsNullOrEmpty(current))
                {
                    if (!visited.Add(current))
                    {
                        return true;
                    }

                    string next;
                    if (!fallbackMap.TryGetValue(current, out next))
                    {
                        break;
                    }

                    current = next;
                }
            }

            return false;
        }
    }
}

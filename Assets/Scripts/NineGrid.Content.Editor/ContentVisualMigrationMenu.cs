using System;
using System.Linq;
using NineGrid.Content;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    public static class ContentVisualMigrationMenu
    {
        private const string MigrateMenu = "TableNine/Content/Migrate Legacy Path Keys";
        private const string ClearFrameMenu = "TableNine/Content/Clear Deprecated frame_key Column";

        [MenuItem(MigrateMenu)]
        public static void MigrateLegacyPathKeys()
        {
            var path = ContentVisualXlsxIO.ResolveAbsolutePath();
            if (!System.IO.File.Exists(path))
            {
                EditorUtility.DisplayDialog("迁移失败", "找不到 content_visual.xlsx", "确定");
                return;
            }

            var rows = ContentVisualXlsxIO.ReadAll(path);
            var upserts = new System.Collections.Generic.List<VisualAssetXlsxRow>();
            var patches = new System.Collections.Generic.List<ContentVisualXlsxRow>();
            var seenVisualIds = new System.Collections.Generic.HashSet<string>();

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                ContentVisualKind kind;
                if (!Enum.TryParse(row.ContentKind, true, out kind))
                {
                    kind = ContentVisualKind.Unknown;
                }

                var iconKey = MigrateKey(row.ContentId, kind, ContentVisualKeySlot.Icon, row.IconKey, upserts, seenVisualIds);
                var faceKey = MigrateKey(row.ContentId, kind, ContentVisualKeySlot.Face, row.FaceKey, upserts, seenVisualIds);
                if (iconKey != row.IconKey || faceKey != row.FaceKey)
                {
                    patches.Add(new ContentVisualXlsxRow
                    {
                        ContentId = row.ContentId,
                        SheetRowIndex = row.SheetRowIndex,
                        IconKey = iconKey,
                        FaceKey = faceKey
                    });
                }
            }

            if (patches.Count == 0)
            {
                EditorUtility.DisplayDialog("迁移完成", "没有需要迁移的 legacy path key。", "确定");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "迁移 Legacy Path Keys",
                    "将把 " + patches.Count + " 行的 icon/face key 迁移为 visual_id，并 upsert visual_asset.xlsx。继续？",
                    "继续",
                    "取消"))
            {
                return;
            }

            try
            {
                ContentVisualXlsxIO.PatchVisualKeys(path, patches);
                if (upserts.Count > 0)
                {
                    VisualAssetXlsxIO.UpsertRows(VisualAssetXlsxIO.ResolveAbsolutePath(), upserts);
                }

                EditorUtility.DisplayDialog("迁移完成", "已迁移 " + patches.Count + " 行。", "确定");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("迁移失败", ex.Message, "确定");
            }
        }

        [MenuItem(ClearFrameMenu)]
        public static void ClearDeprecatedFrameKeys()
        {
            var path = ContentVisualXlsxIO.ResolveAbsolutePath();
            if (!EditorUtility.DisplayDialog(
                    "清空 frame_key",
                    "将清空 content_visual.xlsx 中所有已废弃的 frame_key 列。继续？",
                    "继续",
                    "取消"))
            {
                return;
            }

            try
            {
                ContentVisualXlsxIO.ClearDeprecatedFrameKeys(path);
                EditorUtility.DisplayDialog("完成", "frame_key 列已清空。", "确定");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("失败", ex.Message, "确定");
            }
        }

        private static string MigrateKey(
            string contentId,
            ContentVisualKind kind,
            ContentVisualKeySlot slot,
            string currentKey,
            System.Collections.Generic.List<VisualAssetXlsxRow> upserts,
            System.Collections.Generic.HashSet<string> seenVisualIds)
        {
            if (string.IsNullOrEmpty(currentKey))
            {
                return currentKey;
            }

            if (VisualIdNaming.IsVisualId(currentKey))
            {
                return currentKey;
            }

            if (!VisualIdNaming.IsLegacyPathKey(currentKey))
            {
                return currentKey;
            }

            Sprite sprite;
            if (!ContentVisualSpriteKeyCodec.TryDecodeLegacy(currentKey, out sprite))
            {
                return currentKey;
            }

            var visualId = ContentVisualSpriteKeyCodec.Encode(slot, contentId, kind, sprite);
            if (seenVisualIds.Add(visualId))
            {
                var upsert = ContentVisualSpriteKeyCodec.BuildAssetUpsert(slot, contentId, kind, sprite);
                if (upsert != null)
                {
                    upserts.Add(upsert);
                }
            }

            return visualId;
        }
    }
}

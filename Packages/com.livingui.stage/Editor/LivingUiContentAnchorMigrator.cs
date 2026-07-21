using System.Collections.Generic;
using System.Text;
using NineGrid.LivingUI.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NineGrid.LivingUI.Editor
{
    /// <summary>
    /// 架构迁移：去掉旧 follow 字段语义；现有内容统一 Center 锚点（保持中心系局部位，视觉不变）。
    /// 新内容代码默认仍为 TopLeft。
    /// </summary>
    public static class LivingUiContentAnchorMigrator
    {
        [MenuItem("LivingUI/Migrate Markers → Anchor Architecture")]
        public static void MigrateToAnchorArchitecture()
        {
            var markers = Object.FindObjectsByType<LivingUiContentMarker>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var report = new StringBuilder();
            var converted = 0;

            for (var i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                if (marker == null) continue;

                Undo.RecordObject(marker, "LivingUI Anchor Migrate");

                // 旧 authored 若仍是中心系：用 Center 锚点直接等同，不改世界位
                var so = new SerializedObject(marker);
                var hasPose = so.FindProperty("hasAuthoredPose");
                var authoredPos = so.FindProperty("authoredLocalPosition");
                var authoredScale = so.FindProperty("authoredLocalScale");
                var anchorProp = so.FindProperty("anchor");
                var authoringSize = so.FindProperty("authoringSize");
                var envelope = so.FindProperty("envelopeSize");

                if (hasPose != null && !hasPose.boolValue)
                {
                    authoredPos.vector3Value = marker.transform.localPosition;
                    authoredScale.vector3Value = marker.transform.localScale == Vector3.zero
                        ? Vector3.one
                        : marker.transform.localScale;
                    hasPose.boolValue = true;
                }

                anchorProp.enumValueIndex = (int)LivingUiContentAnchor.Center;

                // 尽量保留已有 authoringSize / baseline 槽：若为 0，从 Face 终态补
                if (authoringSize.vector2Value.x <= 0f || authoringSize.vector2Value.y <= 0f)
                {
                    var baselineLegacy = so.FindProperty("baselineSize");
                    if (baselineLegacy != null
                        && baselineLegacy.vector2Value.x > 0f
                        && baselineLegacy.vector2Value.y > 0f)
                    {
                        authoringSize.vector2Value = baselineLegacy.vector2Value;
                    }
                }

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(marker);
                converted++;
                report.AppendLine(
                    $"  {marker.ContentId} C{marker.CarrierId} face={marker.FaceLayout} " +
                    $"anchor=Center env={envelope.vector2Value}");
            }

            // 确保 LivingUI 根上有 ContentController
            var drivers = Object.FindObjectsByType<LivingUiContentDriver>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < drivers.Length; i++)
            {
                var host = drivers[i].gameObject;
                if (host.GetComponent<LivingUiContentController>() == null)
                {
                    Undo.AddComponent<LivingUiContentController>(host);
                    report.AppendLine($"  + ContentController on {host.name}");
                }
            }

            if (markers.Length > 0)
            {
                EditorSceneManager.MarkSceneDirty(markers[0].gameObject.scene);
            }

            Debug.Log($"[LivingUI] Anchor migrate done. markers={converted}\n{report}");
        }
    }
}

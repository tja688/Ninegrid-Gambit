using UnityEditor;
using UnityEngine;

namespace NineGrid.LivingUI.Editor
{
    /// <summary>
    /// 已废弃：旧「按偏好序从蓝图覆写 live 位姿」与原初权威冲突。
    /// 请改用 Init Primordial Poses From First Blueprint Occurrence。
    /// </summary>
    public static class LivingUiLiveContentPoseResync
    {
        [MenuItem("LivingUI/Resync Live Content Poses From Blueprints")]
        public static void Resync()
        {
            Debug.LogWarning(
                "[LivingUI] Resync Live Content Poses From Blueprints 已废弃。" +
                "运行时不再从蓝图拉扯位姿。请改用：" +
                "LivingUI/Init Primordial Poses From First Blueprint Occurrence");
            LivingUiPrimordialPoseInit.Init();
        }
    }
}

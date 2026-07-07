using System.Reflection;
using Sirenix.OdinInspector;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 场景表演用 DOTween Timeline 播放器：拖入 Timeline 组件后，Inspector 按钮即可播放。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardPerformanceTimelinePlayer : MonoBehaviour
    {
        private const string TimelineTypeName = "Dott.DOTweenTimeline";

        [Tooltip("要播放的 DOTweenTimeline；留空时自动使用同物体上的 DOTweenTimeline。")]
        [SerializeField] private Component timeline;

        private void Reset()
        {
            timeline ??= FindTimelineOn(gameObject);
        }

        private void Awake()
        {
            timeline ??= FindTimelineOn(gameObject);
        }

        [Button("播放 Timeline", ButtonSizes.Medium)]
        [PropertyOrder(-10)]
        private void InspectorPlayTimeline()
        {
            PlayTimeline();
        }

        [Button("回到起点", ButtonSizes.Small)]
        [PropertyOrder(-9)]
        private void InspectorRestartTimeline()
        {
            RestartTimeline();
        }

        public void PlayTimeline()
        {
            InvokeTimelineMethod("DOPlay");
        }

        public void RestartTimeline()
        {
            InvokeTimelineMethod("Restart");
        }

        private void InvokeTimelineMethod(string methodName)
        {
            var target = timeline != null ? timeline : FindTimelineOn(gameObject);
            if (target == null)
            {
                Debug.LogWarning(
                    $"{nameof(CardPerformanceTimelinePlayer)} on {name} 未配置 DOTweenTimeline。",
                    this);
                return;
            }

            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
            {
                Debug.LogWarning(
                    $"{nameof(CardPerformanceTimelinePlayer)} 无法在 {target.GetType().Name} 上调用 {methodName}。",
                    this);
                return;
            }

            method.Invoke(target, null);
        }

        private static Component FindTimelineOn(GameObject gameObject)
        {
            var components = gameObject.GetComponents<Component>();
            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component != null && component.GetType().FullName == TimelineTypeName)
                {
                    return component;
                }
            }

            return null;
        }
    }
}

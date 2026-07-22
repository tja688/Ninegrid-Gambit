using System;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;
using NineGrid.Cards;

namespace NineGrid.Presentation.Tests
{
    public sealed class CardAttackBasicDirectionRigTests
    {
        private const string DotweenTimelineTypeName = "Dott.DOTweenTimeline";

        [Test]
        public void InvalidateCachedTimelineSequence_ClearsSequenceSoRestartRegenerates()
        {
            if (!TryCreateTimeline(out var timeline, out var timelineComponent))
            {
                Assert.Inconclusive("Dott.DOTweenTimeline unavailable in EditMode.");
                return;
            }

            InvokeTimelineMethod(timeline, "Restart");
            var before = ReadSequence(timeline);
            Assert.IsNotNull(before, "Sequence should exist after first Restart");

            CardAttackBasicDirectionRig.InvalidateCachedTimelineSequence(timelineComponent);
            Assert.IsNull(ReadSequence(timeline), "Invalidate should null out cached Sequence via OnKill");

            InvokeTimelineMethod(timeline, "Restart");
            var afterRestart = ReadSequence(timeline);
            Assert.IsNotNull(afterRestart, "Restart after invalidate should rebuild Sequence");
            Assert.AreNotSame(before, afterRestart, "Rebuilt sequence must be a new instance");

            UnityEngine.Object.DestroyImmediate(timelineComponent.gameObject);
        }

        [Test]
        public void InvalidateCachedTimelineSequence_AfterStaleCachedSequence_AllowsFreshRebuild()
        {
            if (!TryCreateTimeline(out var timeline, out var timelineComponent))
            {
                Assert.Inconclusive("Dott.DOTweenTimeline unavailable in EditMode.");
                return;
            }

            InvokeTimelineMethod(timeline, "Restart");
            Assert.IsNotNull(ReadSequence(timeline));

            // 模拟被 KillMotion 打断后 Sequence 仍残留非 null 的陈旧缓存（插件不会自动重建）。
            ForceSetCachedSequence(timeline, DOTween.Sequence());
            Assert.IsNotNull(ReadSequence(timeline), "Stale cached sequence should remain non-null before invalidate");

            CardAttackBasicDirectionRig.InvalidateCachedTimelineSequence(timelineComponent);
            Assert.IsNull(ReadSequence(timeline), "Invalidate must clear stale cached sequence");

            InvokeTimelineMethod(timeline, "Restart");
            Assert.IsNotNull(ReadSequence(timeline), "Restart after stale invalidate should rebuild");

            UnityEngine.Object.DestroyImmediate(timelineComponent.gameObject);
        }

        private static bool TryCreateTimeline(out object timeline, out Component timelineComponent)
        {
            timeline = null;
            timelineComponent = null;
            var timelineType = ResolveType(DotweenTimelineTypeName);
            if (timelineType == null)
            {
                return false;
            }

            var go = new GameObject("rig_timeline_test");
            timelineComponent = go.AddComponent(timelineType);
            timeline = timelineComponent;
            return timeline != null;
        }

        private static Sequence ReadSequence(object timeline)
        {
            var property = timeline.GetType().GetProperty(
                "Sequence",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return property?.GetValue(timeline) as Sequence;
        }

        private static void ForceSetCachedSequence(object timeline, Sequence sequence)
        {
            var property = timeline.GetType().GetProperty(
                "Sequence",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(property, "DOTweenTimeline.Sequence property should exist");
            property.SetValue(timeline, sequence);
        }

        private static void InvokeTimelineMethod(object timeline, string methodName)
        {
            var method = timeline.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(method, methodName + " should exist on DOTweenTimeline");
            method.Invoke(timeline, null);
        }

        private static Type ResolveType(string fullName)
        {
            var type = Type.GetType(fullName);
            if (type != null)
            {
                return type;
            }

            type = Type.GetType($"{fullName}, Assembly-CSharp-firstpass");
            if (type != null)
            {
                return type;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(fullName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}

using System;
using NineGrid.Cards;
using NineGrid.Presentation.Controllers;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.Output
{
    /// <summary>
    /// V9：诊断 Recorder 可整体拔除；旁路异常不阻塞主线。
    /// </summary>
    public sealed class DiagnosticOutputControllerTests
    {
        [TearDown]
        public void TearDown()
        {
            PerfTraceSink.ClearHandlers();
            FlowFieldTraceSink.ClearHandlers();
            ChoreoTraceSink.ClearHandlers();
            RegistryTraceSink.ClearHandlers();
        }

        [Test]
        public void Controller_Unbind_ClearsAllTraceSinkHandlers()
        {
            var go = new GameObject(nameof(DiagnosticOutputController));
            var controller = go.AddComponent<DiagnosticOutputController>();
            controller.AttachRecordersForTests(
                record: (_, __, ___, ____) => { },
                occupancyConflict: (_, __, ___, ____) => { },
                beginChoreo: (_, __) => 0,
                notifyUserInteraction: _ => { });

            Assert.IsNotNull(PerfTraceSink.Record);
            Assert.IsNotNull(FlowFieldTraceSink.OccupancyConflict);
            Assert.IsNotNull(ChoreoTraceSink.BeginChoreo);
            Assert.IsNotNull(RegistryTraceSink.NotifyUserInteraction);

            // EditMode 下 DestroyImmediate 不一定触发 OnDestroy/OnUnbind。
            controller.DetachRecorders();
            UnityEngine.Object.DestroyImmediate(go);

            Assert.IsNull(PerfTraceSink.Record);
            Assert.IsNull(FlowFieldTraceSink.OccupancyConflict);
            Assert.IsNull(ChoreoTraceSink.BeginChoreo);
            Assert.IsNull(RegistryTraceSink.NotifyUserInteraction);
        }

        [Test]
        public void TraceSinks_ThrowingHandler_DoesNotPropagate()
        {
            PerfTraceSink.Record = (_, __, ___, ____) => throw new InvalidOperationException("boom");
            FlowFieldTraceSink.OccupancyConflict = (_, __, ___, ____) =>
                throw new InvalidOperationException("boom");
            ChoreoTraceSink.BeginChoreo = (_, __) => throw new InvalidOperationException("boom");
            RegistryTraceSink.NotifyUserInteraction = _ => throw new InvalidOperationException("boom");

            Assert.DoesNotThrow(() =>
                PerfTraceSink.SafeRecord("kind", 1, "site", null));
            Assert.DoesNotThrow(() =>
                FlowFieldTraceSink.SafeOccupancyConflict(1, 2, 3, "caller"));
            Assert.DoesNotThrow(() =>
                ChoreoTraceSink.SafeBeginChoreo("kind", null));
            Assert.DoesNotThrow(() =>
                RegistryTraceSink.SafeNotifyUserInteraction("click"));
        }
    }
}

using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Systems;
using NineGrid.Flow.Diagnostics;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests.FlowShell
{
    /// <summary>
    /// #142：BattleTrace / FlowTrace 最低日志字段门禁——
    /// 会话共享 Run 关联（seed/sessionId/runTag）；事件/op 自动带 floor 与 nodeIndex；
    /// JSON 序列化输出关联字段，供 Run-Floor-Node-Battle 分析与 Error/Exception/Assert 扫描。
    /// </summary>
    public sealed class DiagTraceMinFieldsContractTests
    {
        [Test]
        public void FlowTraceEvent_StampsFloorAndNodeIndex_FromRunModel()
        {
            using (PresentationArchitectureFixture.CreateStartedGameWithCatalog(seed: 42UL))
            {
                var arch = NineGridArchitecture.Current;
                var run = arch.GetModel<RunModel>();
                run.Floor.Value = 2;
                run.NodeIndex.Value = 3;

                FlowTraceRecorder.Clear();
                FlowTraceRecorder.BeginSessionIfNeeded(run.Seed.Value);
                FlowTraceRecorder.Record(
                    FlowTraceCategory.CoreGate,
                    FlowTraceNames.StartNode,
                    new Dictionary<string, string> { { "probe", "1" } });

                var session = FlowTraceRecorder.CurrentSession;
                Assert.IsNotNull(session);
                Assert.Greater(session.events.Count, 0);
                var ev = session.events[session.events.Count - 1];
                Assert.AreEqual("2", ev.floor, "FlowTrace 事件应带 floor（RunModel.Floor）");
                Assert.AreEqual("3", ev.nodeIndex, "FlowTrace 事件应带 nodeIndex");
                Assert.AreEqual(run.Seed.Value.ToString(), session.seed, "会话应带 Run seed");
                Assert.IsNotEmpty(session.sessionId, "会话应带 sessionId");
                Assert.AreEqual(string.Empty, session.runTag, "正式 Run 不应带 QuickTest RunTag");

                var json = FlowTraceJson.Serialize(session);
                StringAssert.Contains("\"floor\":\"2\"", json);
                StringAssert.Contains("\"nodeIndex\":\"3\"", json);
            }
        }

        [Test]
        public void BattleTraceOp_StampsFloorAndNodeIndex_AndSerializes()
        {
            using (PresentationArchitectureFixture.CreateStartedGameWithCatalog(seed: 42UL))
            {
                var arch = NineGridArchitecture.Current;
                var run = arch.GetModel<RunModel>();
                run.Floor.Value = 3;
                run.NodeIndex.Value = 5;

                BattleTraceRecorder.Clear();
                BattleTraceRecorder.BeginSessionIfNeeded(run.Seed.Value);
                BattleTraceRecorder.RecordOp(new BattleTraceOp
                {
                    opKind = "Probe",
                    reason = "test",
                    presentation = new BattleTracePresentation { accepted = true },
                });

                var session = BattleTraceRecorder.CurrentSession;
                Assert.IsNotNull(session);
                Assert.Greater(session.ops.Count, 0);
                var op = session.ops[session.ops.Count - 1];
                Assert.AreEqual("3", op.floor, "BattleTrace op 应带 floor");
                Assert.AreEqual("5", op.nodeIndex, "BattleTrace op 应带 nodeIndex");
                Assert.AreEqual(run.Seed.Value.ToString(), session.seed, "会话应带 Run seed");

                var json = BattleTraceJson.Serialize(session);
                StringAssert.Contains("\"floor\":\"3\"", json);
                StringAssert.Contains("\"nodeIndex\":\"5\"", json);
            }
        }

        [Test]
        public void FlowTrace_RunTagSeparatesQuickTest_FromFormal()
        {
            using (PresentationArchitectureFixture.CreateBare())
            {
                DiagTraceShared.ClearRunTag();
                Assert.AreEqual(string.Empty, DiagTraceShared.RunTag);

                DiagTraceShared.SetRunTag(DiagTraceShared.QuickTestRunTag, "note");
                Assert.AreEqual(DiagTraceShared.QuickTestRunTag, DiagTraceShared.RunTag);
                DiagTraceShared.ClearRunTag();
            }
        }
    }
}

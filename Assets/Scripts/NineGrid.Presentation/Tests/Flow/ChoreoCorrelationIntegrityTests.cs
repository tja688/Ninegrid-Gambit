using System.Collections.Generic;
using NineGrid.Cards;
using NineGrid.Flow.Diagnostics;
using NineGrid.Flow.Presentation;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// #45：chainId + choreoSeqId 连锁诊断完整性（T3/T5/T6）。
    /// 缝：ChoreoTraceContext 作用域、ExploreTrace settle 配对、PresentStep Refill choreo。
    /// </summary>
    public sealed class ChoreoCorrelationIntegrityTests
    {
        private readonly List<CapturedTrace> _traces = new List<CapturedTrace>(32);

        [SetUp]
        public void SetUp()
        {
            ChoreoTraceContext.Reset();
            DirectorTrace.Reset();
            _traces.Clear();
            PerfTraceSink.Record = CapturePerf;
            ChoreoTraceSink.BeginChoreo = (kind, pairs) =>
                ChoreoTraceContext.BeginChoreo(kind, PairsToDict(pairs));
            ChoreoTraceSink.EndChoreo = (outcome, planned, actual, pairs) =>
                ChoreoTraceContext.EndChoreo(outcome, planned, actual, PairsToDict(pairs));
            ChoreoTraceSink.GetCurrentSeqId = () => ChoreoTraceContext.CurrentSeqId;
            ChoreoTraceSink.RecordExploreTrace = (uid, phase, birth, tracked, pairs) =>
                ChoreoTraceContext.RecordExploreTrace(uid, phase, birth, tracked, PairsToDict(pairs));
            ChoreoTraceSink.ForceCloseOpenChoreos = reason =>
                ChoreoTraceContext.ForceCloseOpenChoreos(reason);
        }

        [TearDown]
        public void TearDown()
        {
            ChoreoTraceSink.ClearHandlers();
            PerfTraceSink.ClearHandlers();
            ChoreoTraceContext.Reset();
            DirectorTrace.Reset();
        }

        [Test]
        public void SkeletonFusion_NestedExitToDeck_OpenSummaryShowsParentChild()
        {
            var root = ChoreoTraceSink.SafeBeginChoreo(
                "SkeletonFusion",
                "resultUid", "42",
                "participantCount", "2");
            Assert.Greater(root, 0);
            Assert.AreEqual(root, ChoreoTraceContext.CurrentSeqId);

            var child = ChoreoTraceSink.SafeBeginChoreo("ExitToDeck", "uid", "42");
            Assert.Greater(child, 0);
            Assert.AreEqual(child, ChoreoTraceContext.CurrentSeqId);

            var summary = ChoreoTraceContext.GetOpenChoreoSummary();
            StringAssert.Contains(root + ":SkeletonFusion", summary);
            StringAssert.Contains(child + ":ExitToDeck", summary);

            ChoreoTraceSink.SafeEndChoreo("ok");
            Assert.AreEqual(root, ChoreoTraceContext.CurrentSeqId);
            ChoreoTraceSink.SafeEndChoreo("ok");
            Assert.AreEqual(0, ChoreoTraceContext.CurrentSeqId);
            Assert.AreEqual(0, CountZeroChoreoSeqIds());
        }

        [Test]
        public void SkeletonFusion_ForceClose_ClearsNestedStack()
        {
            ChoreoTraceSink.SafeBeginChoreo("SkeletonFusion");
            ChoreoTraceSink.SafeBeginChoreo("ExitToDeck");
            Assert.AreEqual(2, ChoreoTraceContext.OpenChoreoCount);

            ChoreoTraceSink.SafeForceCloseOpenChoreos("SkeletonFusion");
            Assert.AreEqual(0, ChoreoTraceContext.OpenChoreoCount);
            Assert.AreEqual(0, ChoreoTraceContext.CurrentSeqId);
        }

        [Test]
        public void DealSettled_PairsWithSameChoreoSeqIdAsFlightBegin()
        {
            DirectorTrace.BeginChain();
            var chainId = DirectorTrace.CurrentChainId;
            var seq = ChoreoTraceSink.SafeBeginChoreo("dealFlight", "kind", "drain", "uid", "7");
            Assert.Greater(seq, 0);

            ChoreoTraceSink.SafeExploreTrace(7, "dealFlightBegin", birthSlot: 2, trackedSlot: 2);
            ChoreoTraceSink.SafeExploreTrace(7, "deal.settled", birthSlot: 2, trackedSlot: 2, "outcome", "ok");
            ChoreoTraceSink.SafeEndChoreo("ok");

            var begin = FindExplore("dealFlightBegin");
            var settled = FindExplore("deal.settled");
            Assert.IsNotNull(begin, "应有 dealFlightBegin");
            Assert.IsNotNull(settled, "应有 deal.settled");
            Assert.AreEqual(seq.ToString(), begin.Payload["choreoSeqId"]);
            Assert.AreEqual(seq.ToString(), settled.Payload["choreoSeqId"]);
            Assert.AreEqual(chainId.ToString(), begin.Payload["chainId"]);
            Assert.AreEqual(chainId.ToString(), settled.Payload["chainId"]);
            Assert.AreEqual(begin.Payload["choreoSeqId"], settled.Payload["choreoSeqId"]);
            DirectorTrace.ClearChain();
        }

        [Test]
        public void ChainLogIntegrity_SameChainId_NoZeroChoreo_BeginEndPaired()
        {
            DirectorTrace.BeginChain();
            var chainId = DirectorTrace.CurrentChainId;
            Assert.Greater(chainId, 0);

            var fusion = ChoreoTraceSink.SafeBeginChoreo("SkeletonFusion", "chainId", chainId.ToString());
            ChoreoTraceSink.SafeEndChoreo("ok");

            var refill = ChoreoTraceSink.SafeBeginChoreo("Refill", "chainId", chainId.ToString());
            ChoreoTraceSink.SafeEndChoreo("ok");

            var deal = ChoreoTraceSink.SafeBeginChoreo("dealFlight", "chainId", chainId.ToString());
            ChoreoTraceSink.SafeExploreTrace(9, "dealFlightBegin", 1, 1);
            ChoreoTraceSink.SafeExploreTrace(9, "deal.settled", 1, 1, "outcome", "ok");
            ChoreoTraceSink.SafeEndChoreo("ok");

            DirectorTrace.ClearChain();

            AssertChainLogIntegrity(chainId, fusion, refill, deal);
        }

        [Test]
        public void PresentStep_WithRefillChoreoKind_BeginEndAroundPresent()
        {
            var gate = new FakeBatchGate();
            var channel = new FakePresentChannel(ticksUntilComplete: 2);
            gate.Open(11);

            var step = new PresentStep(gate, channel, channelName: "FusionRefill", choreoKind: "Refill");
            Assert.AreEqual(0, ChoreoTraceContext.OpenChoreoCount);

            Assert.AreEqual(TimelineStepStatus.Continue, step.Tick(0.016f));
            Assert.AreEqual(1, ChoreoTraceContext.OpenChoreoCount);
            StringAssert.Contains("Refill", ChoreoTraceContext.GetOpenChoreoSummary());

            Assert.AreEqual(TimelineStepStatus.Finished, step.Tick(0.016f));
            Assert.AreEqual(0, ChoreoTraceContext.OpenChoreoCount);

            var begins = CountKind("ChoreoBegin", "Refill");
            var ends = CountKind("ChoreoEnd", "Refill");
            Assert.AreEqual(1, begins);
            Assert.AreEqual(1, ends);
            Assert.AreEqual(0, CountZeroChoreoSeqIds());
        }

        private void AssertChainLogIntegrity(int chainId, params int[] expectedSeqIds)
        {
            Assert.AreEqual(0, CountZeroChoreoSeqIds(), "连锁内不得出现 choreoSeqId=0");

            foreach (var seq in expectedSeqIds)
            {
                Assert.Greater(seq, 0);
                var begin = FindChoreo("ChoreoBegin", seq);
                var end = FindChoreo("ChoreoEnd", seq);
                Assert.IsNotNull(begin, "choreoSeqId=" + seq + " 应有 begin");
                Assert.IsNotNull(end, "choreoSeqId=" + seq + " 应有 end");
                Assert.IsTrue(end.Payload.ContainsKey("durationMs"), "end 应带 durationMs");
                Assert.IsTrue(begin.Payload.TryGetValue("chainId", out var beginChain),
                    "ChoreoBegin 须带 chainId");
                Assert.AreEqual(chainId.ToString(), beginChain);
            }

            var dealBegin = FindExplore("dealFlightBegin");
            var dealSettled = FindExplore("deal.settled");
            Assert.IsNotNull(dealBegin);
            Assert.IsNotNull(dealSettled);
            Assert.AreEqual(dealBegin.Payload["choreoSeqId"], dealSettled.Payload["choreoSeqId"]);
            Assert.AreEqual(chainId.ToString(), dealBegin.Payload["chainId"]);
            Assert.AreEqual(chainId.ToString(), dealSettled.Payload["chainId"]);
        }

        private int CountZeroChoreoSeqIds()
        {
            var n = 0;
            for (var i = 0; i < _traces.Count; i++)
            {
                if (_traces[i].Payload.TryGetValue("choreoSeqId", out var id) && id == "0")
                {
                    n++;
                }
            }

            return n;
        }

        private int CountKind(string kind, string choreoKind)
        {
            var n = 0;
            for (var i = 0; i < _traces.Count; i++)
            {
                var t = _traces[i];
                if (t.Kind != kind)
                {
                    continue;
                }

                if (t.Payload.TryGetValue("kind", out var k) && k == choreoKind)
                {
                    n++;
                }
            }

            return n;
        }

        private CapturedTrace FindExplore(string phase)
        {
            for (var i = 0; i < _traces.Count; i++)
            {
                var t = _traces[i];
                if (t.Kind == "ExploreTrace"
                    && t.Payload.TryGetValue("phase", out var p)
                    && p == phase)
                {
                    return t;
                }
            }

            return null;
        }

        private CapturedTrace FindChoreo(string kind, int seqId)
        {
            var want = seqId.ToString();
            for (var i = 0; i < _traces.Count; i++)
            {
                var t = _traces[i];
                if (t.Kind == kind
                    && t.Payload.TryGetValue("choreoSeqId", out var id)
                    && id == want)
                {
                    return t;
                }
            }

            return null;
        }

        private void CapturePerf(string kind, int uid, string site, string[] pairs)
        {
            _traces.Add(new CapturedTrace
            {
                Kind = kind ?? string.Empty,
                Uid = uid,
                Site = site ?? string.Empty,
                Payload = PairsToDict(pairs),
            });
        }

        private static Dictionary<string, string> PairsToDict(string[] pairs)
        {
            var dict = new Dictionary<string, string>();
            if (pairs == null)
            {
                return dict;
            }

            for (var i = 0; i + 1 < pairs.Length; i += 2)
            {
                var key = pairs[i] ?? string.Empty;
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                dict[key] = pairs[i + 1] ?? string.Empty;
            }

            return dict;
        }

        private sealed class CapturedTrace
        {
            public string Kind;
            public int Uid;
            public string Site;
            public Dictionary<string, string> Payload;
        }

        private sealed class FakeBatchGate : IPresentationBatchGate
        {
            private int _active;

            public bool HasOpenBatch => _active > 0;
            public int ActiveBatchId => _active;

            public void Open(int batchId) => _active = batchId;

            public BatchOpenResult TryOpenNextBatch(out int batchId)
            {
                batchId = _active;
                return _active > 0 ? BatchOpenResult.Opened : BatchOpenResult.Failed;
            }

            public bool TryAcknowledge(int batchId)
            {
                if (batchId != _active)
                {
                    return false;
                }

                _active = 0;
                return true;
            }
        }

        private sealed class FakePresentChannel : IPresentChannel
        {
            private readonly int _ticksUntilComplete;
            private int _ticks;
            private bool _begun;

            public FakePresentChannel(int ticksUntilComplete)
            {
                _ticksUntilComplete = ticksUntilComplete;
            }

            public int ActiveBatchId { get; private set; }
            public bool IsComplete => _begun && _ticks >= _ticksUntilComplete;

            public void Begin(int batchId)
            {
                ActiveBatchId = batchId;
                _begun = true;
                _ticks = 0;
            }

            public void Tick(float deltaTime)
            {
                if (_begun)
                {
                    _ticks++;
                }
            }
        }
    }
}

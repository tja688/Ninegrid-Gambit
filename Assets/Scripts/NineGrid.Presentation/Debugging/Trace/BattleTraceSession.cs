using System;
using System.Collections.Generic;
using System.Text;
using NineGrid.Core;
using NineGrid.Presentation.Orchestration;
using UnityEngine;

namespace NineGrid.Presentation.Debugging.Trace
{
    public sealed class BattleTraceSession : IDisposable
    {
        private static BattleTraceSession sCurrent;

        private readonly FileBattleTraceSink _Sink;
        private readonly List<BattleTraceViolationRecord> _Violations = new();
        private readonly List<BattleTraceSnapshotRecord> _Snapshots = new();
        private readonly List<BattleTraceTimelineRecord> _Timeline = new();
        private int _Seq;
        private float _SessionStartTime;

        private BattleTraceSession(FileBattleTraceSink sink, string sessionId, BattleTraceLevel level)
        {
            _Sink = sink;
            SessionId = sessionId;
            Level = level;
            IncludeInteraction = level >= BattleTraceLevel.Full;
        }

        public static BattleTraceSession Current => sCurrent;

        public static bool IsActive => sCurrent != null && sCurrent.Level != BattleTraceLevel.Off;

        public static BattleTraceLevel ActiveLevel => sCurrent?.Level ?? BattleTraceLevel.Off;

        public static bool ActiveIncludeInteraction => sCurrent != null && sCurrent.IncludeInteraction;

        public string SessionId { get; }
        public BattleTraceLevel Level { get; }
        public bool IncludeInteraction { get; private set; }
        public string JsonlPath => _Sink.JsonlPath;
        public string SummaryPath => _Sink.SummaryPath;

        public static BattleTraceSession Start(BattleTraceLevel level, bool includeInteraction)
        {
            End(flushSummary: false);

            if (level == BattleTraceLevel.Off)
            {
                return null;
            }

            string sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string directory = PathForDirectory();
            string jsonlPath = System.IO.Path.Combine(directory, "session_" + sessionId + ".jsonl");
            var sink = new FileBattleTraceSink(jsonlPath);
            var session = new BattleTraceSession(sink, sessionId, level)
            {
                IncludeInteraction = includeInteraction || level >= BattleTraceLevel.Full,
            };

            sCurrent = session;
            session._SessionStartTime = Time.realtimeSinceStartup;
            session.AppendRaw(session.BuildLine("SessionStart", builder =>
            {
                BattleTraceJsonWriter.AppendString(builder, "sessionId", sessionId);
                builder.Append(',');
                BattleTraceJsonWriter.AppendString(builder, "level", level.ToString());
                builder.Append(',');
                BattleTraceJsonWriter.AppendBool(builder, "includeInteraction", session.IncludeInteraction);
            }));

            Debug.Log("[BattleTrace] session started → " + jsonlPath);
            return session;
        }

        public static void End(bool flushSummary = true)
        {
            if (sCurrent == null)
            {
                return;
            }

            BattleTraceSession session = sCurrent;
            sCurrent = null;

            session.AppendRaw(session.BuildLine("SessionEnd", builder =>
            {
                BattleTraceJsonWriter.AppendString(builder, "sessionId", session.SessionId);
            }));

            if (flushSummary)
            {
                session._Sink.WriteSummary(BattleTraceSummaryGenerator.Generate(session));
            }

            session._Sink.Dispose();
        }

        public static void FlushSummary()
        {
            if (sCurrent == null)
            {
                return;
            }

            sCurrent._Sink.WriteSummary(BattleTraceSummaryGenerator.Generate(sCurrent));
            Debug.Log("[BattleTrace] summary flushed → " + sCurrent.SummaryPath);
        }

        public void SetIncludeInteraction(bool includeInteraction)
        {
            IncludeInteraction = includeInteraction || Level >= BattleTraceLevel.Full;
        }

        public int RecordCommand(string commandName, bool batchOpened, bool inputLockedBefore, bool inputLockedAfter)
        {
            int seq = NextSeq();
            AppendRaw(BuildLine(seq, "Command", builder =>
            {
                BattleTraceJsonWriter.AppendString(builder, "name", commandName);
                builder.Append(',');
                BattleTraceJsonWriter.AppendBool(builder, "batchOpened", batchOpened);
                builder.Append(',');
                BattleTraceJsonWriter.AppendBool(builder, "inputLockedBefore", inputLockedBefore);
                builder.Append(',');
                BattleTraceJsonWriter.AppendBool(builder, "inputLockedAfter", inputLockedAfter);
            }));

            _Timeline.Add(new BattleTraceTimelineRecord(seq, Time.realtimeSinceStartup, "Command " + commandName));
            return seq;
        }

        public void RecordSnapshot(string tag, ZoneSnapshotData data)
        {
            int seq = NextSeq();
            _Snapshots.Add(new BattleTraceSnapshotRecord(seq, tag, data));

            AppendRaw(BuildLine(seq, "Snapshot", builder =>
            {
                BattleTraceJsonWriter.AppendString(builder, "tag", tag);
                builder.Append(',');
                ZoneSnapshotCapture.AppendJson(builder, data);
            }));
        }

        public void RecordBatchStart(int batchId, long fromSeq, long toSeq, int instructionCount)
        {
            int seq = NextSeq();
            AppendRaw(BuildLine(seq, "BatchStart", builder =>
            {
                BattleTraceJsonWriter.AppendNumber(builder, "batchId", batchId);
                builder.Append(',');
                BattleTraceJsonWriter.AppendNumber(builder, "fromSeq", fromSeq);
                builder.Append(',');
                BattleTraceJsonWriter.AppendNumber(builder, "toSeq", toSeq);
                builder.Append(',');
                BattleTraceJsonWriter.AppendNumber(builder, "instructionCount", instructionCount);
            }));

            _Timeline.Add(new BattleTraceTimelineRecord(seq, Time.realtimeSinceStartup, "BatchStart #" + batchId));
        }

        public void RecordBatchEnd(int batchId)
        {
            int seq = NextSeq();
            AppendRaw(BuildLine(seq, "BatchEnd", builder =>
            {
                BattleTraceJsonWriter.AppendNumber(builder, "batchId", batchId);
            }));

            _Timeline.Add(new BattleTraceTimelineRecord(seq, Time.realtimeSinceStartup, "BatchEnd #" + batchId));
        }

        public void RecordPlanStep(
            int actionId,
            string stepId,
            FlowId flowId,
            FlowPayload payload,
            int parallelGroupSize)
        {
            int seq = NextSeq();
            AppendRaw(BuildLine(seq, "PlanStep", builder =>
            {
                BattleTraceJsonWriter.AppendNumber(builder, "actionId", actionId);
                builder.Append(',');
                BattleTraceJsonWriter.AppendString(builder, "stepId", stepId ?? string.Empty);
                builder.Append(',');
                BattleTraceJsonWriter.AppendString(builder, "flow", flowId.ToString());
                builder.Append(',');
                BattleTraceJsonWriter.AppendNumber(builder, "parallelGroupSize", parallelGroupSize);
                builder.Append(',');
                AppendPayload(builder, payload);
            }));
        }

        public void RecordFlowResolve(
            FlowId flowId,
            string status,
            string reason,
            FlowPayload payload,
            int actionId = 0)
        {
            int seq = NextSeq();
            AppendRaw(BuildLine(seq, "FlowResolve", builder =>
            {
                BattleTraceJsonWriter.AppendString(builder, "flow", flowId.ToString());
                builder.Append(',');
                BattleTraceJsonWriter.AppendString(builder, "status", status ?? string.Empty);
                builder.Append(',');
                BattleTraceJsonWriter.AppendString(builder, "reason", reason ?? string.Empty);
                builder.Append(',');
                BattleTraceJsonWriter.AppendNumber(builder, "actionId", actionId);
                builder.Append(',');
                AppendPayload(builder, payload);
            }));

            if (string.Equals(status, "fallback_noop", StringComparison.Ordinal))
            {
                RecordViolationInternal(
                    seq,
                    "FLOW_SILENT_NOOP",
                    "Flow " + flowId + " fell back to InstantAlign no-op",
                    "error",
                    flowId.ToString(),
                    payload);
            }
        }

        public void RecordFlowLifecycle(FlowId flowId, string phase, float wallTime)
        {
            int seq = NextSeq();
            AppendRaw(BuildLine(seq, "FlowLifecycle", builder =>
            {
                BattleTraceJsonWriter.AppendString(builder, "flow", flowId.ToString());
                builder.Append(',');
                BattleTraceJsonWriter.AppendString(builder, "phase", phase ?? string.Empty);
                builder.Append(',');
                BattleTraceJsonWriter.AppendFloat(builder, "wallTime", wallTime);
            }));
        }

        public void RecordWarning(string code, string message, int actionId = 0)
        {
            int seq = NextSeq();
            AppendRaw(BuildLine(seq, "Warning", builder =>
            {
                BattleTraceJsonWriter.AppendString(builder, "code", code ?? string.Empty);
                builder.Append(',');
                BattleTraceJsonWriter.AppendString(builder, "message", message ?? string.Empty);
                builder.Append(',');
                BattleTraceJsonWriter.AppendNumber(builder, "actionId", actionId);
            }));

            if (string.Equals(code, "PARALLEL_MOTION", StringComparison.Ordinal))
            {
                RecordViolationInternal(seq, code, message, "warning", null, null, actionId);
            }
        }

        public void RecordViolation(
            string code,
            string message,
            string severity,
            int? slot = null,
            int? expectedUid = null,
            int? actualUid = null)
        {
            RecordViolationInternal(NextSeq(), code, message, severity, null, null, 0, slot, expectedUid, actualUid);
        }

        public void RecordActorLifecycle(string kind, int uid, string defId, string detail)
        {
            int seq = NextSeq();
            AppendRaw(BuildLine(seq, kind, builder =>
            {
                BattleTraceJsonWriter.AppendNumber(builder, "uid", uid);
                builder.Append(',');
                BattleTraceJsonWriter.AppendString(builder, "defId", defId ?? string.Empty);
                builder.Append(',');
                BattleTraceJsonWriter.AppendString(builder, "detail", detail ?? string.Empty);
            }));
        }

        public void RecordLayout(
            string op,
            string trigger,
            IReadOnlyList<int> uids,
            float duration,
            bool draggingExcluded)
        {
            int seq = NextSeq();
            AppendRaw(BuildLine(seq, "Layout", builder =>
            {
                BattleTraceJsonWriter.AppendString(builder, "op", op ?? string.Empty);
                builder.Append(',');
                BattleTraceJsonWriter.AppendString(builder, "trigger", trigger ?? string.Empty);
                builder.Append(',');
                BattleTraceJsonWriter.AppendIntArray(builder, "uids", uids);
                builder.Append(',');
                BattleTraceJsonWriter.AppendFloat(builder, "duration", duration);
                builder.Append(',');
                BattleTraceJsonWriter.AppendBool(builder, "draggingExcluded", draggingExcluded);
            }));
        }

        public void RecordInteraction(
            string eventName,
            int uid,
            string fsmState,
            bool inputLocked,
            bool actorActive)
        {
            if (!IncludeInteraction)
            {
                return;
            }

            int seq = NextSeq();
            AppendRaw(BuildLine(seq, "Interaction", builder =>
            {
                BattleTraceJsonWriter.AppendString(builder, "event", eventName ?? string.Empty);
                builder.Append(',');
                BattleTraceJsonWriter.AppendNumber(builder, "uid", uid);
                builder.Append(',');
                BattleTraceJsonWriter.AppendString(builder, "fsm", fsmState ?? string.Empty);
                builder.Append(',');
                BattleTraceJsonWriter.AppendBool(builder, "inputLocked", inputLocked);
                builder.Append(',');
                BattleTraceJsonWriter.AppendBool(builder, "actorActive", actorActive);
            }));
        }

        internal IReadOnlyList<BattleTraceViolationRecord> Violations => _Violations;

        internal IReadOnlyList<BattleTraceSnapshotRecord> Snapshots => _Snapshots;

        internal IReadOnlyList<BattleTraceTimelineRecord> Timeline => _Timeline;

        public void Dispose()
        {
            if (ReferenceEquals(sCurrent, this))
            {
                End(flushSummary: true);
            }
            else
            {
                _Sink.Dispose();
            }
        }

        private void RecordViolationInternal(
            int seq,
            string code,
            string message,
            string severity,
            string flow = null,
            FlowPayload payload = null,
            int actionId = 0,
            int? slot = null,
            int? expectedUid = null,
            int? actualUid = null)
        {
            _Violations.Add(new BattleTraceViolationRecord(
                seq,
                code,
                message,
                severity,
                actionId,
                slot,
                expectedUid,
                actualUid));

            AppendRaw(BuildLine(seq, "Violation", builder =>
            {
                BattleTraceJsonWriter.AppendString(builder, "code", code ?? string.Empty);
                builder.Append(',');
                BattleTraceJsonWriter.AppendString(builder, "message", message ?? string.Empty);
                builder.Append(',');
                BattleTraceJsonWriter.AppendString(builder, "severity", severity ?? "error");
                builder.Append(',');
                BattleTraceJsonWriter.AppendNumber(builder, "actionId", actionId);
                builder.Append(',');
                BattleTraceJsonWriter.AppendNullableInt(builder, "slot", slot);
                builder.Append(',');
                BattleTraceJsonWriter.AppendNullableInt(builder, "expectedUid", expectedUid);
                builder.Append(',');
                BattleTraceJsonWriter.AppendNullableInt(builder, "actualUid", actualUid);
                if (!string.IsNullOrEmpty(flow))
                {
                    builder.Append(',');
                    BattleTraceJsonWriter.AppendString(builder, "flow", flow);
                }
            }));
        }

        private int NextSeq()
        {
            _Seq++;
            return _Seq;
        }

        private string BuildLine(string kind, Action<StringBuilder> appendFields)
        {
            return BuildLine(NextSeq(), kind, appendFields);
        }

        private string BuildLine(int seq, string kind, Action<StringBuilder> appendFields)
        {
            var builder = new StringBuilder(256);
            builder.Append('{');
            BattleTraceJsonWriter.AppendNumber(builder, "seq", seq);
            builder.Append(',');
            BattleTraceJsonWriter.AppendFloat(builder, "t", Time.realtimeSinceStartup - _SessionStartTime);
            builder.Append(',');
            BattleTraceJsonWriter.AppendString(builder, "kind", kind);
            builder.Append(',');
            appendFields?.Invoke(builder);
            builder.Append('}');
            return builder.ToString();
        }

        private void AppendRaw(string jsonLine)
        {
            _Sink.AppendLine(jsonLine);
        }

        private static void AppendPayload(StringBuilder builder, FlowPayload payload)
        {
            if (payload == null)
            {
                BattleTraceJsonWriter.AppendNumber(builder, "cardUid", 0);
                builder.Append(',');
                BattleTraceJsonWriter.AppendNumber(builder, "actorUid", 0);
                builder.Append(',');
                BattleTraceJsonWriter.AppendString(builder, "toSlot", "None");
                builder.Append(',');
                BattleTraceJsonWriter.AppendString(builder, "fromSlot", "None");
                return;
            }

            BattleTraceJsonWriter.AppendNumber(builder, "cardUid", payload.CardUid);
            builder.Append(',');
            BattleTraceJsonWriter.AppendNumber(builder, "actorUid", payload.ActorUid);
            builder.Append(',');
            BattleTraceJsonWriter.AppendString(builder, "toSlot", payload.ToSlot.ToString());
            builder.Append(',');
            BattleTraceJsonWriter.AppendString(builder, "fromSlot", payload.FromSlot.ToString());
        }

        private static string PathForDirectory()
        {
            return System.IO.Path.Combine(Application.persistentDataPath, "BattleTraces");
        }
    }

    internal sealed class BattleTraceViolationRecord
    {
        public BattleTraceViolationRecord(
            int seq,
            string code,
            string message,
            string severity,
            int actionId,
            int? slot,
            int? expectedUid,
            int? actualUid)
        {
            Seq = seq;
            Code = code;
            Message = message;
            Severity = severity;
            ActionId = actionId;
            Slot = slot;
            ExpectedUid = expectedUid;
            ActualUid = actualUid;
        }

        public int Seq { get; }
        public string Code { get; }
        public string Message { get; }
        public string Severity { get; }
        public int ActionId { get; }
        public int? Slot { get; }
        public int? ExpectedUid { get; }
        public int? ActualUid { get; }
    }

    public sealed class BattleTraceSnapshotRecord
    {
        public BattleTraceSnapshotRecord(int seq, string tag, ZoneSnapshotData data)
        {
            Seq = seq;
            Tag = tag;
            Data = data;
        }

        public int Seq { get; }
        public string Tag { get; }
        public ZoneSnapshotData Data { get; }
    }

    public sealed class BattleTraceTimelineRecord
    {
        public BattleTraceTimelineRecord(int seq, float time, string label)
        {
            Seq = seq;
            Time = time;
            Label = label;
        }

        public int Seq { get; }
        public float Time { get; }
        public string Label { get; }
    }
}

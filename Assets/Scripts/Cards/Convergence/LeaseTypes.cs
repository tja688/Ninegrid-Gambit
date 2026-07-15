using System;

namespace NineGrid.Cards.Convergence
{
    /// <summary>租约粒度键：按 (卡, 层) 独占，非整卡。</summary>
    public readonly struct LeaseKey : IEquatable<LeaseKey>
    {
        public int CardId { get; }
        public TowerLayer Layer { get; }

        public LeaseKey(int cardId, TowerLayer layer)
        {
            CardId = cardId;
            Layer = layer;
        }

        public bool Equals(LeaseKey other) => CardId == other.CardId && Layer == other.Layer;

        public override bool Equals(object obj) => obj is LeaseKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(CardId, (int)Layer);

        public override string ToString() => $"card={CardId}/L{(int)Layer}";
    }

    /// <summary>向 LeaseArbiter 申请写入/租约的请求。</summary>
    public readonly struct LeaseRequest
    {
        public LeaseKey Key { get; }
        public CommitmentKind Commitment { get; }
        public float WindowStart { get; }
        public float WindowEnd { get; }

        /// <summary>参与就位栅栏的必达收敛；被后发先至抢占且未兑现时触发纪律 B 报警。</summary>
        public bool Committed { get; }

        public LeaseRequest(
            LeaseKey key,
            CommitmentKind commitment,
            float windowStart,
            float windowEnd,
            bool committed = false)
        {
            Key = key;
            Commitment = commitment;
            WindowStart = windowStart;
            WindowEnd = windowEnd;
            Committed = committed;
        }

        public static LeaseRequest Async(
            LeaseKey key,
            float windowStart,
            float windowEnd,
            bool committed = false) =>
            new(key, CommitmentKind.Async, windowStart, windowEnd, committed);

        public static LeaseRequest Sync(
            LeaseKey key,
            float windowStart,
            float windowEnd,
            bool committed = true) =>
            new(key, CommitmentKind.Sync, windowStart, windowEnd, committed);
    }

    public enum LeaseVerdict
    {
        /// <summary>写入/租约生效（含异步后发先至替换）。</summary>
        Accepted = 0,

        /// <summary>被同步租约拒绝（异步撞同步锁）。</summary>
        Rejected = 1,

        /// <summary>同步征用异步通道，附带速度交接快照。</summary>
        Commandeered = 2,

        /// <summary>同步撞同步：拒绝 + 纪律 B 报警。</summary>
        SyncConflict = 3,
    }

    public readonly struct LeaseResult
    {
        public LeaseVerdict Verdict { get; }
        public int LeaseId { get; }
        public HandoffState CommandeerHandoff { get; }
        public bool HasCommandeerHandoff { get; }
        public bool RaisedDisciplineBAlarm { get; }
        public string AlarmReason { get; }

        public bool IsWriteAllowed =>
            Verdict == LeaseVerdict.Accepted || Verdict == LeaseVerdict.Commandeered;

        public LeaseResult(
            LeaseVerdict verdict,
            int leaseId = 0,
            HandoffState commandeerHandoff = default,
            bool hasCommandeerHandoff = false,
            bool raisedDisciplineBAlarm = false,
            string alarmReason = null)
        {
            Verdict = verdict;
            LeaseId = leaseId;
            CommandeerHandoff = commandeerHandoff;
            HasCommandeerHandoff = hasCommandeerHandoff;
            RaisedDisciplineBAlarm = raisedDisciplineBAlarm;
            AlarmReason = alarmReason ?? string.Empty;
        }

        public static LeaseResult Accepted(int leaseId = 0) =>
            new(LeaseVerdict.Accepted, leaseId);

        public static LeaseResult Rejected() =>
            new(LeaseVerdict.Rejected);

        public static LeaseResult Commandeered(int leaseId, in HandoffState handoff) =>
            new(LeaseVerdict.Commandeered, leaseId, handoff, hasCommandeerHandoff: true);

        public static LeaseResult SyncConflict(string reason) =>
            new(LeaseVerdict.SyncConflict, raisedDisciplineBAlarm: true, alarmReason: reason);

        public static LeaseResult AcceptedWithPreemptAlarm(string reason) =>
            new(LeaseVerdict.Accepted, raisedDisciplineBAlarm: true, alarmReason: reason);
    }
}

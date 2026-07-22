using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Cards.Convergence
{
    /// <summary>
    /// 租约仲裁：同步表演独占 (卡,层,时间窗)；异步无租约、可后发先至、可被同步带速度征用。
    /// 纪律 B：同步撞同步 / 抢占未兑现的 committed 目标 → 不静默，立刻报警回调。
    /// </summary>
    public sealed class LeaseArbiter
    {
        private readonly Dictionary<LeaseKey, ChannelState> _channels = new();
        private int _nextLeaseId = 1;
        private Action<string> _onDisciplineBAlarm;

        public LeaseArbiter(Action<string> onDisciplineBAlarm = null)
        {
            _onDisciplineBAlarm = onDisciplineBAlarm;
        }

        public void SetAlarmSink(Action<string> onDisciplineBAlarm)
        {
            _onDisciplineBAlarm = onDisciplineBAlarm;
        }

        public bool HasActiveSyncLease(LeaseKey key)
        {
            return _channels.TryGetValue(key, out var state)
                   && state.Kind == CommitmentKind.Sync
                   && state.LeaseId > 0;
        }

        public bool TryGetActiveLeaseId(LeaseKey key, out int leaseId)
        {
            if (_channels.TryGetValue(key, out var state)
                && state.Kind == CommitmentKind.Sync
                && state.LeaseId > 0)
            {
                leaseId = state.LeaseId;
                return true;
            }

            leaseId = 0;
            return false;
        }

        /// <summary>
        /// 申请写入/租约。征用异步时通过 <paramref name="sampleHandoff"/> 采样当前位姿速度（B 阶段吐真速度）。
        /// </summary>
        public LeaseResult TryAcquire(in LeaseRequest request, Func<HandoffState> sampleHandoff = null)
        {
            if (request.WindowEnd < request.WindowStart)
            {
                throw new ArgumentException(
                    $"Invalid lease window: end ({request.WindowEnd}) < start ({request.WindowStart}).");
            }

            _channels.TryGetValue(request.Key, out var existing);

            if (existing != null && existing.Kind == CommitmentKind.Sync && existing.LeaseId > 0)
            {
                if (WindowsOverlap(existing, request))
                {
                    if (request.Commitment == CommitmentKind.Sync)
                    {
                        var reason =
                            $"[DisciplineB] Sync vs Sync on {request.Key} " +
                            $"windows [{existing.WindowStart:F3},{existing.WindowEnd:F3}] vs " +
                            $"[{request.WindowStart:F3},{request.WindowEnd:F3}]";
                        RaiseAlarm(reason);
                        return LeaseResult.SyncConflict(reason);
                    }

                    // 同步持锁拒异步
                    return LeaseResult.Rejected();
                }

                // 窗口不重叠：旧租约已过期语义上可清；此处按"仍登记则挡重叠窗"处理，非重叠放行前先清
                ClearChannel(request.Key);
                existing = null;
            }

            if (request.Commitment == CommitmentKind.Sync)
            {
                HandoffState handoff = default;
                var commandeered = false;
                if (existing != null && existing.Kind == CommitmentKind.Async)
                {
                    handoff = sampleHandoff != null
                        ? sampleHandoff()
                        : HandoffState.AtRest(Vector3.zero);
                    commandeered = true;
                }

                var leaseId = _nextLeaseId++;
                _channels[request.Key] = new ChannelState
                {
                    Kind = CommitmentKind.Sync,
                    LeaseId = leaseId,
                    WindowStart = request.WindowStart,
                    WindowEnd = request.WindowEnd,
                    Committed = request.Committed,
                    Fulfilled = false,
                };

                if (commandeered)
                {
                    return LeaseResult.Commandeered(leaseId, handoff);
                }

                return LeaseResult.Accepted(leaseId);
            }

            // Async：后发先至
            var preemptAlarm = false;
            string alarmReason = null;
            if (existing != null
                && existing.Kind == CommitmentKind.Async
                && existing.Committed
                && !existing.Fulfilled)
            {
                preemptAlarm = true;
                alarmReason =
                    $"[DisciplineB] Preempted committed async on {request.Key} before fulfillment.";
                RaiseAlarm(alarmReason);
            }

            _channels[request.Key] = new ChannelState
            {
                Kind = CommitmentKind.Async,
                LeaseId = 0,
                WindowStart = request.WindowStart,
                WindowEnd = request.WindowEnd,
                Committed = request.Committed,
                Fulfilled = false,
            };

            return preemptAlarm
                ? LeaseResult.AcceptedWithPreemptAlarm(alarmReason)
                : LeaseResult.Accepted();
        }

        /// <summary>同步租约到期或承诺兑现后释放。</summary>
        public bool Release(LeaseKey key, int leaseId)
        {
            if (!_channels.TryGetValue(key, out var state))
            {
                return false;
            }

            if (state.Kind != CommitmentKind.Sync || state.LeaseId != leaseId)
            {
                return false;
            }

            _channels.Remove(key);
            return true;
        }

        /// <summary>标记当前通道承诺已兑现（异步 committed 被后发先至前若已兑现则不报警）。</summary>
        public void MarkFulfilled(LeaseKey key)
        {
            if (_channels.TryGetValue(key, out var state))
            {
                state.Fulfilled = true;
            }
        }

        public void Clear()
        {
            _channels.Clear();
            _nextLeaseId = 1;
        }

        private void ClearChannel(LeaseKey key) => _channels.Remove(key);

        private void RaiseAlarm(string reason)
        {
            _onDisciplineBAlarm?.Invoke(reason);
        }

        private static bool WindowsOverlap(ChannelState existing, in LeaseRequest request)
        {
            return request.WindowStart < existing.WindowEnd
                   && existing.WindowStart < request.WindowEnd;
        }

        private sealed class ChannelState
        {
            public CommitmentKind Kind;
            public int LeaseId;
            public float WindowStart;
            public float WindowEnd;
            public bool Committed;
            public bool Fulfilled;
        }
    }
}

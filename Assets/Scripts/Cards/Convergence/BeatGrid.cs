using System;
using System.Collections.Generic;

namespace NineGrid.Cards.Convergence
{
    /// <summary>
    /// 节拍栅格：指令挂 beat，同 beat 共享 sourceTime（整环旋转同曲线进度）。
    /// 就位栅栏：barrier 墙钟时刻上，所有收敛 sourceTime ≤ 栅栏相对时长，到点全员完成。
    /// </summary>
    public sealed class BeatGrid
    {
        private readonly PresentationClock _clock;
        private readonly Dictionary<int, BeatRecord> _beats = new();
        private int _nextBeatId = 1;

        public BeatGrid(PresentationClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public PresentationClock Clock => _clock;

        /// <summary>
        /// 打开新 beat，以当前墙钟为 startTime，同 beat 共享 <paramref name="sharedSourceTime"/>。
        /// </summary>
        public int OpenBeat(float sharedSourceTime)
        {
            if (sharedSourceTime < 0f)
            {
                sharedSourceTime = 0f;
            }

            var id = _nextBeatId++;
            _beats[id] = new BeatRecord
            {
                Id = id,
                StartWallTime = _clock.Now,
                SharedSourceTime = sharedSourceTime,
            };
            return id;
        }

        public bool TryGetBeat(int beatId, out BeatInfo info)
        {
            if (!_beats.TryGetValue(beatId, out var record))
            {
                info = default;
                return false;
            }

            info = new BeatInfo(
                record.Id,
                record.StartWallTime,
                record.SharedSourceTime,
                record.BarrierWallTime,
                record.Registrations.Count);
            return true;
        }

        public float GetSharedSourceTime(int beatId)
        {
            if (!_beats.TryGetValue(beatId, out var record))
            {
                throw new ArgumentException($"Unknown beatId={beatId}", nameof(beatId));
            }

            return record.SharedSourceTime;
        }

        /// <summary>
        /// 在绝对墙钟时刻放置就位栅栏。要求 start + sharedSourceTime ≤ barrierWallTime。
        /// </summary>
        public void PlaceBarrier(int beatId, float barrierWallTime)
        {
            var record = Require(beatId);
            if (barrierWallTime < record.StartWallTime)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(barrierWallTime),
                    barrierWallTime,
                    "Barrier wall time must be >= beat start.");
            }

            record.BarrierWallTime = barrierWallTime;
        }

        /// <summary>
        /// 注册一条同 beat 收敛。强制使用 beat 共享 sourceTime（刚体同步）。
        /// </summary>
        public BeatConvergenceSlot Register(int beatId, bool committed = true)
        {
            var record = Require(beatId);
            var slot = new BeatConvergenceSlot(record.SharedSourceTime, committed);
            record.Registrations.Add(slot);
            return slot;
        }

        /// <summary>
        /// 栅栏语义校验：已放置栅栏时，共享 sourceTime ≤ 栅栏相对时长；
        /// 所有注册槽的 sourceTime 亦同。
        /// </summary>
        public bool TryValidateBarrier(int beatId, out string error)
        {
            if (!_beats.TryGetValue(beatId, out var record))
            {
                error = $"Unknown beatId={beatId}";
                return false;
            }

            if (!record.BarrierWallTime.HasValue)
            {
                error = "Barrier not placed.";
                return false;
            }

            var budget = record.BarrierWallTime.Value - record.StartWallTime;
            if (record.SharedSourceTime > budget + 1e-5f)
            {
                error =
                    $"Shared sourceTime {record.SharedSourceTime:F4}s exceeds barrier budget {budget:F4}s.";
                return false;
            }

            for (var i = 0; i < record.Registrations.Count; i++)
            {
                var slot = record.Registrations[i];
                if (slot.SourceTime > budget + 1e-5f)
                {
                    error =
                        $"Slot[{i}] sourceTime {slot.SourceTime:F4}s exceeds barrier budget {budget:F4}s.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        /// <summary>
        /// 按当前时钟推进 beat 内所有槽；到栅栏时刻应全部 IsComplete。
        /// </summary>
        public void TickBeat(int beatId)
        {
            var record = Require(beatId);
            var elapsed = _clock.Now - record.StartWallTime;
            if (elapsed < 0f)
            {
                elapsed = 0f;
            }

            for (var i = 0; i < record.Registrations.Count; i++)
            {
                record.Registrations[i].Tick(elapsed);
            }
        }

        public bool AreAllComplete(int beatId)
        {
            var record = Require(beatId);
            if (record.Registrations.Count == 0)
            {
                return true;
            }

            for (var i = 0; i < record.Registrations.Count; i++)
            {
                if (!record.Registrations[i].IsComplete)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 到栅栏墙钟时：先 Tick，再报告是否全员就位。
        /// 未放置栅栏时返回 false。
        /// </summary>
        public bool IsBarrierSatisfied(int beatId)
        {
            if (!_beats.TryGetValue(beatId, out var record) || !record.BarrierWallTime.HasValue)
            {
                return false;
            }

            if (_clock.Now + 1e-5f < record.BarrierWallTime.Value)
            {
                return false;
            }

            TickBeat(beatId);
            return AreAllComplete(beatId);
        }

        public void Clear()
        {
            _beats.Clear();
            _nextBeatId = 1;
        }

        private BeatRecord Require(int beatId)
        {
            if (!_beats.TryGetValue(beatId, out var record))
            {
                throw new ArgumentException($"Unknown beatId={beatId}", nameof(beatId));
            }

            return record;
        }

        private sealed class BeatRecord
        {
            public int Id;
            public float StartWallTime;
            public float SharedSourceTime;
            public float? BarrierWallTime;
            public readonly List<BeatConvergenceSlot> Registrations = new();
        }
    }

    public readonly struct BeatInfo
    {
        public int BeatId { get; }
        public float StartWallTime { get; }
        public float SharedSourceTime { get; }
        public float? BarrierWallTime { get; }
        public int RegistrationCount { get; }

        public BeatInfo(
            int beatId,
            float startWallTime,
            float sharedSourceTime,
            float? barrierWallTime,
            int registrationCount)
        {
            BeatId = beatId;
            StartWallTime = startWallTime;
            SharedSourceTime = sharedSourceTime;
            BarrierWallTime = barrierWallTime;
            RegistrationCount = registrationCount;
        }
    }

    /// <summary>
    /// 挂在 beat 上的收敛槽（纯逻辑，不依赖 Transform）。
    /// 完成判定：elapsed ≥ sourceTime（与五次曲线到点语义对齐）。
    /// </summary>
    public sealed class BeatConvergenceSlot
    {
        public float SourceTime { get; }
        public bool IsCommitted { get; }
        public bool IsComplete { get; private set; }
        public float Elapsed { get; private set; }

        public BeatConvergenceSlot(float sourceTime, bool committed)
        {
            SourceTime = sourceTime < 0f ? 0f : sourceTime;
            IsCommitted = committed;
        }

        public void Tick(float elapsedSinceBeatStart)
        {
            Elapsed = elapsedSinceBeatStart < 0f ? 0f : elapsedSinceBeatStart;
            IsComplete = Elapsed + 1e-5f >= SourceTime;
        }
    }
}

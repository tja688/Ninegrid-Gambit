using UnityEngine;

namespace NineGrid.Presentation.Systems.Vfx
{
    /// <summary>精灵表帧推进逻辑；默认有限循环 1 次，scaled 时间基。</summary>
    public sealed class VfxSpriteSheetPlayback
    {
        public const int InfiniteLoops = 0;

        public Sprite[] Frames = System.Array.Empty<Sprite>();
        public int FrameIndex;
        public float Elapsed;
        public int CompletedLoops;
        public int LoopLimit = 1;
        public float Fps = 12f;
        public float Speed = 1f;
        public float StartOffsetSeconds;
        public bool UseUnscaledTime;
        public bool IsComplete;

        private float mStartOffsetRemaining;

        public void Configure(
            Sprite[] frames,
            float fps,
            float speed,
            int loopLimit,
            float startOffsetSeconds,
            bool useUnscaledTime)
        {
            Frames = frames ?? System.Array.Empty<Sprite>();
            Fps = Mathf.Max(0.01f, fps);
            Speed = Mathf.Max(0.01f, speed);
            LoopLimit = loopLimit <= 0 ? InfiniteLoops : Mathf.Max(1, loopLimit);
            StartOffsetSeconds = Mathf.Max(0f, startOffsetSeconds);
            UseUnscaledTime = useUnscaledTime;
            FrameIndex = 0;
            Elapsed = 0f;
            CompletedLoops = 0;
            IsComplete = false;
            mStartOffsetRemaining = StartOffsetSeconds;
        }

        public void Reset()
        {
            Frames = System.Array.Empty<Sprite>();
            FrameIndex = 0;
            Elapsed = 0f;
            CompletedLoops = 0;
            LoopLimit = 1;
            Fps = 12f;
            Speed = 1f;
            StartOffsetSeconds = 0f;
            UseUnscaledTime = false;
            IsComplete = false;
            mStartOffsetRemaining = 0f;
        }

        public bool Tick(float scaledDelta, float unscaledDelta)
        {
            if (IsComplete)
            {
                return true;
            }

            if (Frames.Length == 0)
            {
                IsComplete = true;
                return true;
            }

            var delta = UseUnscaledTime ? unscaledDelta : scaledDelta;
            if (delta < 0f)
            {
                delta = 0f;
            }

            if (mStartOffsetRemaining > 0f)
            {
                mStartOffsetRemaining -= delta;
                if (mStartOffsetRemaining > 0f)
                {
                    return false;
                }

                delta = -mStartOffsetRemaining;
                mStartOffsetRemaining = 0f;
                if (delta <= 0f)
                {
                    return false;
                }
            }

            if (Frames.Length == 1)
            {
                if (LoopLimit == InfiniteLoops)
                {
                    return false;
                }

                var singleDuration = 1f / (Fps * Speed);
                Elapsed += delta;
                if (Elapsed >= singleDuration)
                {
                    CompletedLoops = LoopLimit;
                    IsComplete = true;
                    return true;
                }

                return false;
            }

            var frameDuration = 1f / (Fps * Speed);
            Elapsed += delta;
            while (Elapsed >= frameDuration)
            {
                Elapsed -= frameDuration;
                FrameIndex++;
                if (FrameIndex >= Frames.Length)
                {
                    CompletedLoops++;
                    if (LoopLimit != InfiniteLoops && CompletedLoops >= LoopLimit)
                    {
                        FrameIndex = Frames.Length - 1;
                        IsComplete = true;
                        return true;
                    }

                    FrameIndex = 0;
                }
            }

            return false;
        }

        public Sprite CurrentFrame
        {
            get
            {
                if (Frames.Length == 0)
                {
                    return null;
                }

                if (FrameIndex < 0 || FrameIndex >= Frames.Length)
                {
                    return Frames[Frames.Length - 1];
                }

                return Frames[FrameIndex];
            }
        }
    }
}

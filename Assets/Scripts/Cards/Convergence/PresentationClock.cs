using UnityEngine;

namespace NineGrid.Cards.Convergence
{
    /// <summary>
    /// 表现层权威调度时钟。区别于仅做日志计数的 DiagBeatClock。
    /// 初版用变长帧推进（墙钟秒）；将来换固定步长只需换 Advance 的 dt 来源。
    /// </summary>
    public sealed class PresentationClock
    {
        private float _now;

        public float Now => _now;

        public void Reset()
        {
            _now = 0f;
        }

        /// <summary>推进墙钟。负 dt 视为 0；EditMode 测试可直接调用。</summary>
        public void Advance(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                deltaTime = 0f;
            }

            _now += deltaTime;
        }

        /// <summary>测试用：跳到绝对时刻（不可为负）。</summary>
        public void Seek(float absoluteTime)
        {
            _now = Mathf.Max(0f, absoluteTime);
        }
    }
}

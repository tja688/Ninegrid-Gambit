using UnityEngine;

namespace NineGrid.Presentation.Contracts
{
    /// <summary>
    /// Local Cue 调用上下文；本轮仅定形状，具体字段随 Feedback 池落地扩展。
    /// </summary>
    public readonly struct CueInvocation
    {
        public CueInvocation(Transform target)
        {
            Target = target;
        }

        public Transform Target { get; }
    }
}

using NineGrid.Presentation.Contracts;
using UnityEngine;

namespace NineGrid.Presentation.Feedback
{
    /// <summary>
    /// 攻击拖尾 Local Cue 占位；动效待后续批次补全。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AttackTrailCue : MonoBehaviour, ILocalCue
    {
        public void Play(CueInvocation invocation)
        {
            // TODO: 攻击拖尾动效（蓝图 AttackTrailCue）
        }

        public void StopAndRestore()
        {
        }
    }
}

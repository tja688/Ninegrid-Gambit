using NineGrid.Presentation.Contracts;
using UnityEngine;

namespace NineGrid.Presentation.Flow.Core
{
    /// <summary>
    /// 快照对齐型占位 Flow（no-op）：无时间线表演；终态由专属 Flow 或 <see cref="StatEventProjection"/> 承担。
    /// </summary>
    public sealed class SnapshotAlignFlow : MonoBehaviour, IDirectedFlow
    {
        public bool IsPlaying => false;
        public float ExpectedDuration => 0f;

        public void StopAndRestore()
        {
        }
    }
}

using NineGrid.Presentation.Contracts;
using UnityEngine;

namespace NineGrid.Presentation.Flow.Core
{
    /// <summary>
    /// 快照对齐型占位 Flow：无时间线表演，批末 Reconcile 负责终态对齐。
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

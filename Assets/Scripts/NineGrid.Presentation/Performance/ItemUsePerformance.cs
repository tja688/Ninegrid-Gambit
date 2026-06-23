using System;
using UnityEngine;

namespace NineGrid.Presentation.Performance
{
    /// <summary>
    /// 道具使用表演占位：无视觉效果，0 秒阻塞；后续替换为真实动效黑盒。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ItemUsePerformance : MonoBehaviour
    {
        public bool IsPlaying => false;

        public float TotalDuration => 0f;

        public void Play(int itemCardUid, Action onComplete = null)
        {
            onComplete?.Invoke();
        }
    }
}

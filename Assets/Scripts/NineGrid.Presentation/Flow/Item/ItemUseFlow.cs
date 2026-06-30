using System;
using NineGrid.Presentation.Contracts;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace NineGrid.Presentation.Flow.Item
{
    /// <summary>
    /// 道具使用表演占位：无视觉效果，0 秒阻塞；后续替换为真实动效黑盒。
    /// </summary>
    [DisallowMultipleComponent]
    [MovedFrom(true, "NineGrid.Presentation.Performance", null, "ItemUsePerformance")]
    public sealed class ItemUseFlow : MonoBehaviour, IDirectedFlow
    {
        public bool IsPlaying => false;

        public float ExpectedDuration => 0f;

        public void Play(int itemCardUid, Action onComplete = null)
        {
            onComplete?.Invoke();
        }

        public void StopAndRestore()
        {
        }
    }
}

using System;
using NineGrid.Core;
using NineGrid.Presentation.Contracts;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace NineGrid.Presentation.Reactions
{
    /// <summary>
    /// 修正应用表演占位：无视觉效果，0 秒阻塞；后续替换为真实动效黑盒。
    /// </summary>
    [DisallowMultipleComponent]
    [MovedFrom(true, "NineGrid.Presentation.Performance", null, "ModifierApplyPerformance")]
    public sealed class ModifierApplyReaction : MonoBehaviour, IPlannedReaction
    {
        public bool IsPlaying => false;

        public float TotalDuration => 0f;

        /// <param name="cardUid">目标卡 CardUid。</param>
        /// <param name="statOrRule">属性或规则枚举值（<see cref="CoreGameEvent.Amount"/>）。</param>
        /// <param name="delta">变化量。</param>
        /// <param name="source">修正来源描述（<see cref="CoreGameEvent.Message"/>）。</param>
        public void Play(
            int cardUid,
            int statOrRule,
            int delta,
            string source,
            Action onComplete = null)
        {
            onComplete?.Invoke();
        }

        public void StopAndRestore()
        {
        }
    }
}

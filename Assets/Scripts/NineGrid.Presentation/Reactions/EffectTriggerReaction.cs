using System;
using NineGrid.Core;
using NineGrid.Presentation.Contracts;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace NineGrid.Presentation.Reactions
{
    /// <summary>
    /// 效果触发表演占位：无视觉效果，0 秒阻塞；后续替换为真实动效黑盒。
    /// </summary>
    [DisallowMultipleComponent]
    [MovedFrom(true, "NineGrid.Presentation.Performance", null, "EffectTriggerPerformance")]
    public sealed class EffectTriggerReaction : MonoBehaviour, IPlannedReaction
    {
        public bool IsPlaying => false;

        public float TotalDuration => 0f;

        /// <param name="ownerCardUid">效果持有者 CardUid（<see cref="CoreGameEvent.CardUid"/>）。</param>
        /// <param name="effectId">效果定义 id（<see cref="CoreGameEvent.Message"/>）。</param>
        /// <param name="sourceDefId">来源定义 id。</param>
        /// <param name="cause">触发原因。</param>
        public void Play(
            int ownerCardUid,
            string effectId,
            string sourceDefId,
            string cause,
            Action onComplete = null)
        {
            onComplete?.Invoke();
        }

        public void StopAndRestore()
        {
        }
    }
}

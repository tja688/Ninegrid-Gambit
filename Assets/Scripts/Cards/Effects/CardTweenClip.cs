using System;
using DG.Tweening;
using UnityEngine;

namespace NineGrid.Cards
{
    [Serializable]
    public struct CardTweenClip
    {
        [Tooltip("片段起始延迟（秒），与 Dott Timeline Insert(0) 策略一致。")]
        public float delay;

        [Tooltip("片段时长（秒）。")]
        public float duration;

        [Tooltip("缓动类型。")]
        public Ease ease;

        [Tooltip("片段类型；本阶段仅 LocalMove。")]
        public CardTweenClipType type;

        [Tooltip("Right 朝向基准 endValue；Left 播放时对 X 取反。")]
        public Vector3 endValue;

        [Tooltip("是否为相对位移（对应 DOTweenAnimation.isRelative）。")]
        public bool isRelative;

        public float FullDuration => Mathf.Max(0f, delay) + Mathf.Max(0f, duration);
    }
}

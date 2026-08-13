using System.Collections.Generic;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 触发脉冲链级去重（ADR-0048）：同一条输入意图连锁（导演主线从接单到跑空）内，
    /// 同一持有卡的同一效果只演一次触发反馈（FX 缩放 + 类型化 VFX + 音频）。
    /// Core 对每个含 qualifying 事件的动作都会发独立 EffectTriggered（如光环 refresh 在
    /// 挂起位移落地批、补牌批、旋转批各发一条）——表现侧在此收敛为单次反馈。
    /// 复位时机：导演接受新意图（BeginChain）、主线跑空（ClearChain）、硬清场。
    /// </summary>
    public static class TriggerPulseChainDedup
    {
        private static readonly HashSet<string> sSeen = new HashSet<string>();

        /// <summary>首次见到返回 true（应演出）；链内重复返回 false（静默消费）。</summary>
        public static bool TryMarkFirst(int cardUid, string effectId)
        {
            return sSeen.Add(cardUid + "|" + (effectId ?? string.Empty));
        }

        public static void Reset()
        {
            if (sSeen.Count > 0)
            {
                sSeen.Clear();
            }
        }
    }
}

using System;

namespace NineGrid.Cards
{
    /// <summary>
    /// 骷髅合体表现请求（由 Flow 扫描 EventLog 后映射传入）。
    /// </summary>
    public readonly struct SkeletonFusionPresentationRequest
    {
        public SkeletonFusionPresentationRequest(
            int actionId,
            string skillId,
            int triggerCardUid,
            int[] participantUids,
            int resultUid,
            string resultDefId)
        {
            ActionId = actionId;
            SkillId = skillId ?? string.Empty;
            TriggerCardUid = triggerCardUid;
            ParticipantUids = participantUids ?? Array.Empty<int>();
            ResultUid = resultUid;
            ResultDefId = resultDefId ?? string.Empty;
        }

        public int ActionId { get; }
        public string SkillId { get; }
        public int TriggerCardUid { get; }
        public int[] ParticipantUids { get; }
        public int ResultUid { get; }
        public string ResultDefId { get; }
    }
}

using System;

namespace NineGrid.Content.Audio
{
    public readonly struct AudioCueRequest
    {
        public AudioCueRequest(
            string cueId,
            string diagnosticSource,
            string cardDefId,
            string skillId,
            string roomId,
            string itemDefId,
            string contentId,
            int diagnosticCardUid = 0)
        {
            CueId = cueId ?? string.Empty;
            DiagnosticSource = diagnosticSource ?? string.Empty;
            CardDefId = cardDefId ?? string.Empty;
            SkillId = skillId ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            ItemDefId = itemDefId ?? string.Empty;
            ContentId = contentId ?? string.Empty;
            // 运行时 UID 仅诊断上下文，不得进入绑定主键 / cueId。
            DiagnosticCardUid = diagnosticCardUid > 0 ? diagnosticCardUid : 0;
        }

        public string CueId { get; }
        public string DiagnosticSource { get; }
        public string CardDefId { get; }
        public string SkillId { get; }
        public string RoomId { get; }
        public string ItemDefId { get; }
        public string ContentId { get; }
        public int DiagnosticCardUid { get; }

        public static AudioCueRequest Simple(string cueId, string diagnosticSource)
        {
            return new AudioCueRequest(
                cueId,
                diagnosticSource,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);
        }
    }
}

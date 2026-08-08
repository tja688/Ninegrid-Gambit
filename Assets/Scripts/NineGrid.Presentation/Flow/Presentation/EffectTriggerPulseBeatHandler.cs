using NineGrid.Content.Audio;
using NineGrid.Core;
using NineGrid.Core.Systems;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 效果触发 FX/音效脉冲装饰处理器：在 Impact 消费 TriggerEffect，不占主线就位回执。
    /// 仅九宫格在场卡发脉冲；卡组/手牌/已移除跳过（仍认领指令以免 Settled 误诊）。
    /// </summary>
    public sealed class EffectTriggerPulseBeatHandler : IBattleBeatHandler
    {
        [AudioCue(
            "sfx.effect.trigger",
            "怪物技能触发",
            "Battle",
            "EffectTriggerPulseBeatHandler.TryApply",
            AudioCueContexts.CardDefId | AudioCueContexts.SkillId | AudioCueContexts.ContentId)]
        private const string EffectTriggerCueId = "sfx.effect.trigger";
        public bool TryApply(PresentationInstruction instruction)
        {
            if (instruction == null || instruction.Kind != PresentationInstructionKind.TriggerEffect)
            {
                return false;
            }

            var gameEvent = instruction.Event;
            if (gameEvent == null || gameEvent.CardUid <= 0)
            {
                return true;
            }

            if (!IsCoreCardOnBoard(gameEvent.CardUid))
            {
                return true;
            }

            var fxId = CardEffectTriggerPulseSink.IdForCard(gameEvent.CardUid);
            TriggerPulseHub.PulseFx(fxId);
            TriggerPulseHub.PulseAudio(new AudioCueRequest(
                EffectTriggerCueId,
                "EffectTriggerPulseBeatHandler.TryApply",
                gameEvent.SourceDefId,
                gameEvent.SourceDefId,
                string.Empty,
                string.Empty,
                gameEvent.Cause));
            return true;
        }

        private static bool IsCoreCardOnBoard(int uid)
        {
            var arch = NineGridArchitecture.Current;
            if (arch == null)
            {
                // 无架构时仍发脉冲，由 sink 按托管卡态降级；便于单测与装配前诊断。
                return true;
            }

            var registry = arch.GetModel<CardRegistry>();
            if (!registry.TryGet(uid, out var coreCard) || coreCard == null)
            {
                return false;
            }

            return coreCard.Zone.Value == ZoneId.Board;
        }
    }
}

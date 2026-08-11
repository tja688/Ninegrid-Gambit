using NineGrid.Content.Audio;
using NineGrid.Core;
using NineGrid.Core.Systems;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 效果触发 FX/音效脉冲装饰处理器：在 Impact 消费 TriggerEffect，不占主线就位回执。
    /// 已登记且不在场（非 Board）的卡跳过脉冲；无架构或卡尚未登记时仍发脉冲，便于单测与装配前诊断。
    /// 声音走稳定 cue + cardDefId/skillId 内容覆盖；运行时 UID 只进诊断，不进绑定主键。
    /// </summary>
    public sealed class EffectTriggerPulseBeatHandler : IBattleBeatHandler
    {
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

            // FX 脉冲仍要求卡在场（Board）；手牌/道具槽 HelpCard 的 OnSelfUsed 不在此列。
            if (IsCoreCardOnBoard(gameEvent.CardUid))
            {
                var fxId = CardEffectTriggerPulseSink.IdForCard(gameEvent.CardUid);
                TriggerPulseHub.PulseFx(fxId);
            }

            // 单一权威音频出口：按内容种类选一个稳定 cue，禁止再发 sfx.effect.<CardUid> 或第二条内容路径。
            // 音频不受棋盘门禁：爆弹等道具槽触发的效果仍需专属释放音。
            SkillEffectTrapRelicAudioCues.PulseTrigger(
                gameEvent.SourceDefId,
                gameEvent.Cause,
                "EffectTriggerPulseBeatHandler.TryApply",
                gameEvent.CardUid);
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
                // Architecture.Interface 会懒创建空架构；卡尚未登记时与无架构同，仍发脉冲。
                return true;
            }

            return coreCard.Zone.Value == ZoneId.Board;
        }
    }
}

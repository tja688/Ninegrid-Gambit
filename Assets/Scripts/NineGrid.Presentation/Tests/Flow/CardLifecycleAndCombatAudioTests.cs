using System.Collections.Generic;
using NineGrid.Cards;
using NineGrid.Content.Audio;
using NineGrid.Content.Editor;
using NineGrid.Core;
using NineGrid.Flow.Presentation;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// #176：从高层表演 seam 验证卡牌生命周期 / 基础战斗声音请求顺序与结果，不断言私有动画。
    /// </summary>
    public sealed class CardLifecycleAndCombatAudioTests
    {
        [TearDown]
        public void TearDown()
        {
            TriggerPulseHub.ResetToNull();
        }

        [Test]
        public void CombatOutcome_ShowDamage_EmitsHitThenSplitResultsWithoutDuplicateHit()
        {
            var sink = CaptureAudio();

            PresentationEventMap.TryGet(CoreEventType.DamageDealt, out var map);
            var gameEvent = new CoreGameEvent(CoreEventType.DamageDealt, 1, "CombatHit")
                .WithTarget(7)
                .WithAmount(5)
                .WithDamageSplit(armorDamage: 2, hpDamage: 3);
            var instruction = new PresentationInstruction(gameEvent, map);

            var handler = new DamageFloaterBeatHandler();
            Assert.IsTrue(handler.TryApply(instruction));

            CollectionAssert.AreEqual(
                new[]
                {
                    BattleCombatAudioCues.AttackHit,
                    BattleCombatAudioCues.ArmorAbsorb,
                    BattleCombatAudioCues.HpDamage,
                },
                sink.CueIds);
        }

        [Test]
        public void CombatOutcome_HelpCardDamage_SkipsMeleeHitButKeepsSplitResults()
        {
            var sink = CaptureAudio();

            PresentationEventMap.TryGet(CoreEventType.DamageDealt, out var map);
            var gameEvent = new CoreGameEvent(CoreEventType.DamageDealt, 1, "HelpBomb")
                .WithTarget(7)
                .WithAmount(4)
                .WithSource("help.bomb", "help.bomb.use")
                .WithDamageSplit(armorDamage: 1, hpDamage: 3);
            var instruction = new PresentationInstruction(gameEvent, map);

            Assert.IsTrue(new DamageFloaterBeatHandler().TryApply(instruction));

            CollectionAssert.AreEqual(
                new[]
                {
                    BattleCombatAudioCues.ArmorAbsorb,
                    BattleCombatAudioCues.HpDamage,
                },
                sink.CueIds);
            CollectionAssert.DoesNotContain(sink.CueIds, BattleCombatAudioCues.AttackHit);
        }

        [Test]
        public void CombatOutcome_FullArmorBlock_EmitsHitAndBlockNotHp()
        {
            var sink = CaptureAudio();

            PresentationEventMap.TryGet(CoreEventType.DamageDealt, out var map);
            var gameEvent = new CoreGameEvent(CoreEventType.DamageDealt, 1, "CombatHit")
                .WithTarget(7)
                .WithAmount(4)
                .WithDamageSplit(armorDamage: 4, hpDamage: 0);
            var instruction = new PresentationInstruction(gameEvent, map);

            Assert.IsTrue(new DamageFloaterBeatHandler().TryApply(instruction));

            CollectionAssert.AreEqual(
                new[]
                {
                    BattleCombatAudioCues.AttackHit,
                    BattleCombatAudioCues.Block,
                },
                sink.CueIds);
            CollectionAssert.DoesNotContain(sink.CueIds, BattleCombatAudioCues.HpDamage);
            CollectionAssert.DoesNotContain(sink.CueIds, BattleCombatAudioCues.ArmorAbsorb);
        }

        [Test]
        public void CombatOutcome_Heal_EmitsHealOnceFromImpactSeam()
        {
            var sink = CaptureAudio();

            PresentationEventMap.TryGet(CoreEventType.Healed, out var map);
            var gameEvent = new CoreGameEvent(CoreEventType.Healed, 1, "Heal")
                .WithTarget(7)
                .WithDelta(3);
            var instruction = new PresentationInstruction(gameEvent, map);

            // Healed UpdateHp 是旁路装饰：不认领指令。
            Assert.IsFalse(new DamageFloaterBeatHandler().TryApply(instruction));

            CollectionAssert.AreEqual(
                new[] { BattleCombatAudioCues.Heal },
                sink.CueIds);
        }

        [Test]
        public void MotionSeam_PulsesDistinctCuesForRotateMoveSwap()
        {
            var sink = CaptureAudio();

            CardLifecycleAudioCues.PulseMotion(
                BoardPresentationStepKind.Rotate,
                "test");
            CardLifecycleAudioCues.PulseMotion(
                BoardPresentationStepKind.Move,
                "test");
            CardLifecycleAudioCues.PulseMotion(
                BoardPresentationStepKind.Swap,
                "test");

            CollectionAssert.AreEqual(
                new[]
                {
                    CardLifecycleAudioCues.Rotate,
                    CardLifecycleAudioCues.Move,
                    CardLifecycleAudioCues.Swap,
                },
                sink.CueIds);
        }

        [Test]
        public void LifecycleAndCombatCueDeclarations_AreUniqueAndPresent()
        {
            var catalog = AudioBindingCatalog.FromJson(
                "{\"schemaVersion\":2,\"bindings\":["
                + "{\"cueId\":\"card.lifecycle.deal\",\"enabled\":true,\"clipKey\":\"audio/SFX/添加卡牌\"},"
                + "{\"cueId\":\"battle.attack.hit\",\"enabled\":true,\"clipKey\":\"audio/SFX/斩击\"}]}");

            var result = AudioCueDeclarationScanner.Scan(
                catalog,
                typeof(CardLifecycleAudioCues).Assembly);

            Assert.That(result.Findings, Has.None.Matches<AudioCueDeclarationFinding>(finding =>
                finding.CueId == CardLifecycleAudioCues.Deal
                && finding.Message.Contains("重复")));
            Assert.That(result.Findings, Has.None.Matches<AudioCueDeclarationFinding>(finding =>
                finding.CueId == BattleCombatAudioCues.AttackHit
                && finding.Message.Contains("重复")));
            Assert.That(result.Findings, Has.Some.Matches<AudioCueDeclarationFinding>(finding =>
                finding.CueId == CardLifecycleAudioCues.Flip
                && finding.Message.Contains("未绑定")));
        }

        private static CaptureSink CaptureAudio()
        {
            var sink = new CaptureSink();
            TriggerPulseHub.Configure(NullTriggerPulseSink.Instance, sink);
            return sink;
        }

        private sealed class CaptureSink : ITriggerPulseSink, IAudioCuePulseSink
        {
            public readonly List<string> CueIds = new List<string>();

            public void Pulse(string triggerId)
            {
                CueIds.Add(triggerId ?? string.Empty);
            }

            public void Pulse(AudioCueRequest request)
            {
                CueIds.Add(request.CueId ?? string.Empty);
            }
        }
    }
}

using System.Collections.Generic;
using NineGrid.Content.Audio;
using NineGrid.Content.Editor;
using NineGrid.Core;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// #175：技能/效果/机关/遗物声音——覆盖优先级、动态 UID 排除、延迟必播、显式取消、无重复发射。
    /// </summary>
    public sealed class SkillEffectTrapRelicAudioTests
    {
        private const string PriorityCatalogJson =
            "{\"schemaVersion\":2,\"ticket\":\"#175\",\"bindings\":["
            + "{\"cueId\":\"sfx.skill.trigger\",\"enabled\":true,\"clipKey\":\"audio/SFX/base\",\"note\":\"base\"},"
            + "{\"cueId\":\"sfx.skill.trigger\",\"enabled\":true,\"clipKey\":\"audio/SFX/card\",\"note\":\"card\","
            + "\"selectorCardDefId\":\"monster.demo\"},"
            + "{\"cueId\":\"sfx.skill.trigger\",\"enabled\":true,\"clipKey\":\"audio/SFX/skill\",\"note\":\"skill\","
            + "\"selectorSkillId\":\"skill.flame_boiling\"},"
            + "{\"cueId\":\"sfx.skill.trigger\",\"enabled\":true,\"clipKey\":\"audio/SFX/joint\",\"note\":\"joint\","
            + "\"selectorCardDefId\":\"monster.demo\",\"selectorSkillId\":\"skill.flame_boiling\","
            + "\"bindingDelaySeconds\":0.8}]}";

        [TearDown]
        public void TearDown()
        {
            TriggerPulseHub.ResetToNull();
        }

        [Test]
        public void BindingResolve_PrefersCardAndSkillThenSkillThenCardThenBase()
        {
            var catalog = AudioBindingCatalog.FromJson(PriorityCatalogJson);

            Assert.IsTrue(catalog.TryResolve(
                new AudioCueRequest(
                    SkillEffectTrapRelicAudioCues.SkillTrigger,
                    "test",
                    "monster.demo",
                    "skill.flame_boiling",
                    string.Empty,
                    string.Empty,
                    string.Empty),
                out var joint));
            Assert.AreEqual("joint", joint.Note);
            Assert.AreEqual(0.8f, joint.BindingDelaySeconds, 0.001f);

            Assert.IsTrue(catalog.TryResolve(
                new AudioCueRequest(
                    SkillEffectTrapRelicAudioCues.SkillTrigger,
                    "test",
                    "monster.other",
                    "skill.flame_boiling",
                    string.Empty,
                    string.Empty,
                    string.Empty),
                out var skillOnly));
            Assert.AreEqual("skill", skillOnly.Note);

            Assert.IsTrue(catalog.TryResolve(
                new AudioCueRequest(
                    SkillEffectTrapRelicAudioCues.SkillTrigger,
                    "test",
                    "monster.demo",
                    "skill.other",
                    string.Empty,
                    string.Empty,
                    string.Empty),
                out var cardOnly));
            Assert.AreEqual("card", cardOnly.Note);

            Assert.IsTrue(catalog.TryResolve(
                new AudioCueRequest(
                    SkillEffectTrapRelicAudioCues.SkillTrigger,
                    "test",
                    "monster.other",
                    "skill.other",
                    string.Empty,
                    string.Empty,
                    string.Empty),
                out var baser));
            Assert.AreEqual("base", baser.Note);
        }

        [Test]
        public void DynamicUidCueIds_AreRejectedAsFormalBindingKeys()
        {
            Assert.IsTrue(SkillEffectTrapRelicAudioCues.IsLegacyDynamicEffectCueId("sfx.effect.12"));
            Assert.IsTrue(SkillEffectTrapRelicAudioCues.IsLegacyDynamicEffectCueId("sfx.effect.7"));
            Assert.IsFalse(SkillEffectTrapRelicAudioCues.IsLegacyDynamicEffectCueId(
                SkillEffectTrapRelicAudioCues.EffectTrigger));
            Assert.IsFalse(SkillEffectTrapRelicAudioCues.IsLegacyDynamicEffectCueId(
                SkillEffectTrapRelicAudioCues.SkillTrigger));
            Assert.IsFalse(SkillEffectTrapRelicAudioCues.IsLegacyDynamicEffectCueId("sfx.effect.cast"));
        }

        [Test]
        public void EffectTrigger_UsesStableCueWithSkillContext_NotDynamicUid()
        {
            var sink = CaptureAudio();

            PresentationEventMap.TryGet(CoreEventType.EffectTriggered, out var map);
            var gameEvent = new CoreGameEvent(CoreEventType.EffectTriggered, 1, "Activate")
                .WithCard(42)
                .WithSource("monster.demo", "skill.flame_boiling");
            var instruction = new PresentationInstruction(gameEvent, map);

            Assert.IsTrue(new EffectTriggerPulseBeatHandler().TryApply(instruction));
            Assert.AreEqual(1, sink.Requests.Count);
            Assert.AreEqual(SkillEffectTrapRelicAudioCues.SkillTrigger, sink.Requests[0].CueId);
            Assert.AreEqual("monster.demo", sink.Requests[0].CardDefId);
            Assert.AreEqual("skill.flame_boiling", sink.Requests[0].SkillId);
            Assert.AreEqual(42, sink.Requests[0].DiagnosticCardUid);
            Assert.IsFalse(SkillEffectTrapRelicAudioCues.IsLegacyDynamicEffectCueId(sink.Requests[0].CueId));
        }

        [Test]
        public void EffectTrigger_RoutesTrapAndRelicWithoutSecondPulse()
        {
            var sink = CaptureAudio();

            PresentationEventMap.TryGet(CoreEventType.EffectTriggered, out var map);
            Assert.IsTrue(new EffectTriggerPulseBeatHandler().TryApply(
                new PresentationInstruction(
                    new CoreGameEvent(CoreEventType.EffectTriggered, 1, "Activate")
                        .WithCard(3)
                        .WithSource("trap.flame", "trap.flame.remove.every"),
                    map)));
            Assert.IsTrue(new EffectTriggerPulseBeatHandler().TryApply(
                new PresentationInstruction(
                    new CoreGameEvent(CoreEventType.EffectTriggered, 2, "Activate")
                        .WithCard(4)
                        .WithSource("relic.rotten_cleave_axe", "relic.rotten_cleave_axe.use"),
                    map)));

            CollectionAssert.AreEqual(
                new[]
                {
                    SkillEffectTrapRelicAudioCues.TrapTrigger,
                    SkillEffectTrapRelicAudioCues.RelicTrigger,
                },
                sink.CueIds);
            Assert.AreEqual(2, sink.Requests.Count);
        }

        [Test]
        public void BindingDelay_MustPlayViaAdapter_DoesNotUseCancellableSchedule()
        {
            var clock = new FakeClock();
            var playback = new FakePlayback();
            var scheduler = new FakeScheduler();
            var catalog = AudioBindingCatalog.FromJson(PriorityCatalogJson);
            var system = new AudioSystem(catalog, playback, clock, scheduler: scheduler, randomValue: () => 0d);

            var result = system.RequestCue(new AudioCueRequest(
                SkillEffectTrapRelicAudioCues.SkillTrigger,
                "delay-must-play",
                "monster.demo",
                "skill.flame_boiling",
                string.Empty,
                string.Empty,
                string.Empty,
                diagnosticCardUid: 99));

            Assert.AreEqual(AudioCueOutcome.Played, result.Outcome);
            Assert.AreEqual(1, playback.Requests.Count);
            Assert.AreEqual(0.8f, playback.Requests[0].BindingDelaySeconds, 0.001f);
            Assert.AreEqual(0, scheduler.PendingCount);
            Assert.AreEqual("audio/SFX/joint", playback.Requests[0].ClipKey);
        }

        [Test]
        public void AttackCharge_ExplicitCancelPreventsLatePlay()
        {
            var playback = new FakePlayback();
            var scheduler = new FakeScheduler();
            var catalog = AudioBindingCatalog.FromJson(
                "{\"schemaVersion\":2,\"bindings\":[{\"cueId\":\"battle.attack.charge\",\"enabled\":true,"
                + "\"clipKey\":\"audio/SFX/挥动增强\"}]}");
            var system = new AudioSystem(catalog, playback, clock: new FakeClock(), scheduler: scheduler);

            var kept = system.ScheduleCue(
                new AudioCueRequest(
                    SkillEffectTrapRelicAudioCues.AttackCharge,
                    "kept",
                    "monster.demo",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty),
                0.35f);
            var cancelled = system.ScheduleCue(
                new AudioCueRequest(
                    SkillEffectTrapRelicAudioCues.AttackCharge,
                    "cancelled",
                    "monster.demo",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty),
                0.35f);

            Assert.IsTrue(system.CancelScheduledCue(cancelled));
            scheduler.Fire(kept);
            Assert.AreEqual(1, playback.Requests.Count);
            Assert.AreEqual(SkillEffectTrapRelicAudioCues.AttackCharge, playback.Requests[0].CueId);
            Assert.IsFalse(system.CancelScheduledCue(cancelled));
        }

        [Test]
        public void UnboundContentOverride_FallsBackToBase_ThenSilentUnbound()
        {
            var catalog = AudioBindingCatalog.FromJson(
                "{\"schemaVersion\":2,\"bindings\":["
                + "{\"cueId\":\"sfx.effect.trigger\",\"enabled\":true,\"clipKey\":\"audio/SFX/base\"},"
                + "{\"cueId\":\"sfx.effect.trigger\",\"enabled\":true,\"clipKey\":\"audio/SFX/skill\","
                + "\"selectorSkillId\":\"skill.known\"}]}");
            var playback = new FakePlayback();
            var system = new AudioSystem(catalog, playback, new FakeClock(), scheduler: new FakeScheduler());

            // 无匹配内容覆盖 → 回退基础 Cue
            var fallback = system.RequestCue(new AudioCueRequest(
                SkillEffectTrapRelicAudioCues.EffectTrigger,
                "fallback",
                "monster.demo",
                "skill.unknown",
                string.Empty,
                string.Empty,
                string.Empty));
            Assert.AreEqual(AudioCueOutcome.Played, fallback.Outcome);
            Assert.AreEqual("audio/SFX/base", fallback.ActualClipKey);

            // 全部未绑定 → 静默 Unbound
            var unbound = system.RequestCue(AudioCueRequest.Simple("sfx.never.bound", "silent"));
            Assert.AreEqual(AudioCueOutcome.Unbound, unbound.Outcome);
            Assert.AreEqual(AudioHistoryOutcome.Unbound, system.History[system.History.Count - 1].Outcome);
        }

        [Test]
        public void SkillEffectCueDeclarations_AreUnique()
        {
            var catalog = AudioBindingCatalog.FromJson(
                "{\"schemaVersion\":2,\"bindings\":["
                + "{\"cueId\":\"sfx.skill.trigger\",\"enabled\":true,\"clipKey\":\"audio/SFX/准备法术增强.FG\"},"
                + "{\"cueId\":\"sfx.trap.trigger\",\"enabled\":true,\"clipKey\":\"audio/SFX/准备法术增强.FG\"}]}");
            var result = AudioCueDeclarationScanner.Scan(
                catalog,
                typeof(SkillEffectTrapRelicAudioCues).Assembly);

            Assert.That(result.Findings, Has.None.Matches<AudioCueDeclarationFinding>(finding =>
                finding.CueId == SkillEffectTrapRelicAudioCues.SkillTrigger
                && finding.Message.Contains("重复")));
            Assert.That(result.Findings, Has.None.Matches<AudioCueDeclarationFinding>(finding =>
                finding.CueId == SkillEffectTrapRelicAudioCues.TrapTrigger
                && finding.Message.Contains("重复")));
            Assert.That(result.Findings, Has.Some.Matches<AudioCueDeclarationFinding>(finding =>
                finding.CueId == SkillEffectTrapRelicAudioCues.RelicTrigger
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
            public readonly List<AudioCueRequest> Requests = new List<AudioCueRequest>();

            public void Pulse(string triggerId)
            {
                CueIds.Add(triggerId ?? string.Empty);
            }

            public void Pulse(AudioCueRequest request)
            {
                CueIds.Add(request.CueId ?? string.Empty);
                Requests.Add(request);
            }
        }

        private sealed class FakeClock : IAudioClock
        {
            public double UnscaledTime { get; set; } = 10d;
        }

        private sealed class FakePlayback : IAudioPlaybackAdapter
        {
            public readonly List<AudioPlaybackRequest> Requests = new List<AudioPlaybackRequest>();

            public AudioBackendResult Play(AudioPlaybackRequest request)
            {
                Requests.Add(request);
                return AudioBackendResult.Success(request.ClipKey);
            }
        }

        private sealed class FakeScheduler : IAudioCueScheduler
        {
            private readonly Dictionary<long, System.Action> pending = new Dictionary<long, System.Action>();
            private long nextKey;

            public int PendingCount => pending.Count;

            public AudioScheduleKey Schedule(float delaySeconds, System.Action callback)
            {
                var key = new AudioScheduleKey(++nextKey);
                pending[key.Value] = callback;
                return key;
            }

            public bool Cancel(AudioScheduleKey key)
            {
                return pending.Remove(key.Value);
            }

            public void Fire(AudioScheduleKey key)
            {
                if (pending.TryGetValue(key.Value, out var callback))
                {
                    pending.Remove(key.Value);
                    callback();
                }
            }
        }
    }
}

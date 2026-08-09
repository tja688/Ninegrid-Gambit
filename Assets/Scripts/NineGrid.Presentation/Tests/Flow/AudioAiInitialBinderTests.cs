using System;
using System.Collections.Generic;
using NineGrid.Content.Audio;
using NineGrid.Content.Editor;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    public sealed class AudioAiInitialBinderTests
    {
        [Test]
        public void Run_FillsUnboundAsAiDraft_AndPreservesHumanConfirmed()
        {
            var declarations = new List<AudioAiInitialBinder.CueHint>
            {
                new AudioAiInitialBinder.CueHint
                {
                    CueId = "ui.action.press",
                    Note = "通用界面按钮按下",
                    Module = "UI",
                },
                new AudioAiInitialBinder.CueHint
                {
                    CueId = "battle.attack.hit",
                    Note = "攻击命中",
                    Module = "Battle",
                },
            };
            var clips = new List<AudioAiInitialBinder.ClipCandidate>
            {
                Clip("audio/SFX/按钮点击"),
                Clip("audio/SFX/点击1增强"),
                Clip("audio/SFX/点击2增强"),
                Clip("audio/SFX/斩击"),
                Clip("audio/SFX/挥动增强"),
                Clip("audio/SFX/挥动2增强"),
            };
            var existing = new AudioBindingCatalogDto
            {
                schemaVersion = 2,
                ticket = "#test",
                bindings = new[]
                {
                    new AudioBindingDto
                    {
                        cueId = "ui.action.press",
                        note = "人工压低音量",
                        module = "UI",
                        enabled = true,
                        clipKey = "audio/SFX/按钮点击",
                        volumeDb = -12f,
                        authoringStatus = AudioBindingAuthoringStatuses.HumanConfirmed,
                    },
                },
            };

            var result = AudioAiInitialBinder.Run(declarations, clips, existing, forceRebindAll: false);

            Assert.AreEqual(2, result.Catalog.bindings.Length);
            var press = FindBase(result.Catalog.bindings, "ui.action.press");
            var hit = FindBase(result.Catalog.bindings, "battle.attack.hit");
            Assert.AreEqual(AudioBindingAuthoringStatuses.HumanConfirmed, press.authoringStatus);
            Assert.AreEqual(-12f, press.volumeDb, 0.001f);
            Assert.AreEqual("audio/SFX/按钮点击", press.clipKey);
            Assert.IsTrue(press.variants == null || press.variants.Length == 0);
            Assert.AreEqual(AudioBindingAuthoringStatuses.AiDraft, hit.authoringStatus);
            Assert.AreEqual("audio/SFX/斩击", hit.clipKey == string.Empty
                ? hit.variants[0].clipKey
                : hit.clipKey);
            Assert.AreEqual(0, result.Report.unmatchedCueCount);
            Assert.GreaterOrEqual(result.Report.poolAugmentedCount, 1);
            Assert.AreEqual(1, result.Report.preservedHumanCount);
        }

        [Test]
        public void Run_ForceRebindAll_OverwritesHumanConfirmedAsDraft()
        {
            var declarations = new List<AudioAiInitialBinder.CueHint>
            {
                new AudioAiInitialBinder.CueHint
                {
                    CueId = "ui.action.press",
                    Note = "通用界面按钮按下",
                    Module = "UI",
                },
            };
            var clips = new List<AudioAiInitialBinder.ClipCandidate>
            {
                Clip("audio/SFX/按钮点击"),
                Clip("audio/SFX/点击1增强"),
                Clip("audio/SFX/点击2增强"),
            };
            var existing = new AudioBindingCatalogDto
            {
                schemaVersion = 3,
                bindings = new[]
                {
                    new AudioBindingDto
                    {
                        cueId = "ui.action.press",
                        note = "旧人工",
                        module = "UI",
                        enabled = true,
                        clipKey = "audio/SFX/按钮点击",
                        volumeDb = -20f,
                        authoringStatus = AudioBindingAuthoringStatuses.HumanConfirmed,
                    },
                },
            };

            var result = AudioAiInitialBinder.Run(declarations, clips, existing, forceRebindAll: true);
            var press = FindBase(result.Catalog.bindings, "ui.action.press");
            Assert.AreEqual(AudioBindingAuthoringStatuses.AiDraft, press.authoringStatus);
        }

        [Test]
        public void Hygiene_ReportsDuplicateKeysMissingClipsAndForbiddenPaths()
        {
            var catalog = new AudioBindingCatalogDto
            {
                schemaVersion = 3,
                bindings = new[]
                {
                    new AudioBindingDto
                    {
                        cueId = "ui.action.press",
                        note = "a",
                        clipKey = "audio/SFX/按钮点击",
                        authoringStatus = AudioBindingAuthoringStatuses.AiDraft,
                    },
                    new AudioBindingDto
                    {
                        cueId = "ui.action.press",
                        note = "dup",
                        clipKey = "audio/SFX/按钮点击",
                        authoringStatus = AudioBindingAuthoringStatuses.AiDraft,
                    },
                    new AudioBindingDto
                    {
                        cueId = "battle.attack.hit",
                        note = "missing",
                        clipKey = "audio/SFX/不存在",
                        authoringStatus = AudioBindingAuthoringStatuses.AiDraft,
                    },
                    new AudioBindingDto
                    {
                        cueId = "orphan.cue",
                        note = "orphan",
                        clipKey = "audio/SFX/按钮点击",
                        authoringStatus = AudioBindingAuthoringStatuses.AiDraft,
                    },
                },
            };

            var findings = AudioBindingCatalogHygieneValidator.Validate(
                catalog,
                new[] { "audio/SFX/按钮点击" },
                new[] { "ui.action.press", "battle.attack.hit" });

            Assert.IsTrue(findings.Exists(f => f.Category == "duplicate-binding-key"));
            Assert.IsTrue(findings.Exists(f => f.Category == "missing-clip"));
            Assert.IsTrue(findings.Exists(f => f.Category == "orphan-binding" && f.CueId == "orphan.cue"));
            Assert.IsFalse(findings.Exists(f =>
                f.Category == "orphan-binding"
                && (f.CueId == "ui.action.press" || f.CueId == "battle.attack.hit")));
        }

        [Test]
        public void Hygiene_ReportsCoverageConflict_WhenSameCueEqualSpecificityOverlaps()
        {
            var catalog = new AudioBindingCatalogDto
            {
                schemaVersion = 3,
                bindings = new[]
                {
                    new AudioBindingDto
                    {
                        cueId = "ui.action.press",
                        note = "by-card",
                        clipKey = "audio/SFX/按钮点击",
                        selectorCardDefId = "card.a",
                        authoringStatus = AudioBindingAuthoringStatuses.AiDraft,
                    },
                    new AudioBindingDto
                    {
                        cueId = "ui.action.press",
                        note = "by-skill",
                        clipKey = "audio/SFX/按钮点击",
                        selectorSkillId = "skill.b",
                        authoringStatus = AudioBindingAuthoringStatuses.AiDraft,
                    },
                },
            };

            var findings = AudioBindingCatalogHygieneValidator.Validate(
                catalog,
                new[] { "audio/SFX/按钮点击" },
                new[] { "ui.action.press" });

            Assert.IsTrue(findings.Exists(f => f.Category == "coverage-conflict"));
        }

        private static AudioAiInitialBinder.ClipCandidate Clip(string key)
        {
            var name = key;
            var slash = key.LastIndexOf('/');
            if (slash >= 0 && slash + 1 < key.Length)
            {
                name = key.Substring(slash + 1);
            }

            return new AudioAiInitialBinder.ClipCandidate
            {
                ResourcesKey = key,
                DisplayName = name,
                AssetPath = "Assets/Resources/" + key + ".wav",
            };
        }

        private static AudioBindingDto FindBase(AudioBindingDto[] rows, string cueId)
        {
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row != null
                    && string.Equals(row.cueId, cueId, StringComparison.Ordinal)
                    && string.IsNullOrEmpty(row.selectorCardDefId)
                    && string.IsNullOrEmpty(row.selectorSkillId)
                    && string.IsNullOrEmpty(row.selectorRoomId)
                    && string.IsNullOrEmpty(row.selectorItemDefId)
                    && string.IsNullOrEmpty(row.selectorContentId))
                {
                    return row;
                }
            }

            throw new AssertionException("missing base binding " + cueId);
        }
    }
}

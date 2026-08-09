using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NineGrid.Content.Audio;
using NineGrid.Content.Editor;
using NineGrid.Flow.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>#179 Catalog / 声明清单卫生门禁：正式绑定不得有重复、断链、孤儿、空说明或隔离根引用。</summary>
    public sealed class AudioDeliveryHygieneTests
    {
        [Test]
        public void FormalCatalog_HasNoCriticalHygieneFindings()
        {
            var projectRoot = new DirectoryInfo(Application.dataPath).Parent.FullName;
            var catalogPath = Path.Combine(projectRoot, AudioBindingCatalogPaths.ManifestAssetPath.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(catalogPath), "缺少正式 audio_bindings.json");

            var json = File.ReadAllText(catalogPath, Encoding.UTF8);
            var catalog = JsonUtility.FromJson<AudioBindingCatalogDto>(json);
            Assert.IsNotNull(catalog);

            var formalClips = CollectFormalClipKeys(projectRoot);
            var declaredCueIds = CollectDeclaredCueIds();
            var findings = AudioBindingCatalogHygieneValidator.Validate(
                catalog,
                formalClips,
                declaredCueIds);

            var critical = findings
                .Where(f => IsCritical(f.Category))
                .Select(f => f.ToString())
                .ToList();

            Assert.IsEmpty(critical, "正式 Catalog 卫生失败：\n" + string.Join("\n", critical.Take(40)));
        }

        [Test]
        public void CueDeclarations_HaveUniqueIdsAndNonEmptyNotes()
        {
            var catalog = AudioBindingCatalog.LoadFromResources();
            var scan = AudioCueDeclarationScanner.Scan(
                catalog,
                typeof(SkillEffectTrapRelicAudioCues).Assembly);

            var critical = scan.Findings
                .Where(f =>
                    f.Message.IndexOf("重复", StringComparison.Ordinal) >= 0
                    || f.Message.IndexOf("不能为空", StringComparison.Ordinal) >= 0)
                .Select(f => f.CueId + ": " + f.Message)
                .ToList();

            Assert.IsEmpty(critical, "声音提示声明卫生失败：\n" + string.Join("\n", critical.Take(40)));
        }

        private static bool IsCritical(string category)
        {
            switch (category)
            {
                case "duplicate-binding-key":
                case "coverage-conflict":
                case "orphan-binding":
                case "missing-clip":
                case "empty-cue":
                case "empty-note":
                case "forbidden-path":
                case "pool-null":
                case "pool-weight":
                case "param-range":
                case "authoring-status":
                case "catalog-null":
                case "null-row":
                    return true;
                default:
                    return category != null
                        && (category.IndexOf("forbidden", StringComparison.OrdinalIgnoreCase) >= 0
                            || category.IndexOf("quarantine", StringComparison.OrdinalIgnoreCase) >= 0);
            }
        }

        private static HashSet<string> CollectFormalClipKeys(string projectRoot)
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var roots = new[]
            {
                Path.Combine(projectRoot, "Assets", "Resources", "audio", "SFX"),
                Path.Combine(projectRoot, "Assets", "Resources", "audio", "BGM"),
            };
            foreach (var absoluteRoot in roots)
            {
                if (!Directory.Exists(absoluteRoot))
                {
                    continue;
                }

                foreach (var file in Directory.EnumerateFiles(absoluteRoot, "*.*", SearchOption.AllDirectories))
                {
                    var ext = Path.GetExtension(file);
                    if (!string.Equals(ext, ".wav", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(ext, ".ogg", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(ext, ".mp3", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(ext, ".aif", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(ext, ".aiff", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var normalizedPath = file.Replace('\\', '/');
                    var marker = "/Resources/";
                    var idx = normalizedPath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                    if (idx < 0)
                    {
                        continue;
                    }

                    var relative = normalizedPath.Substring(idx + marker.Length);
                    var key = AudioAssetManifestLoader.NormalizeKey(relative);
                    if (!string.IsNullOrEmpty(key))
                    {
                        keys.Add(key);
                    }
                }
            }

            return keys;
        }

        private static HashSet<string> CollectDeclaredCueIds()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var scan = AudioCueDeclarationScanner.Scan(typeof(SkillEffectTrapRelicAudioCues).Assembly);
            foreach (var declaration in scan.Declarations)
            {
                var cueId = declaration?.Attribute?.CueId;
                if (!string.IsNullOrWhiteSpace(cueId))
                {
                    ids.Add(cueId.Trim());
                }
            }

            return ids;
        }
    }
}

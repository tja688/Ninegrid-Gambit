#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NineGrid.Content.Audio;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    /// <summary>
    /// #167 音频资产卫生：唯一 Resources 根、重复内容、隔离区、GUID/Resources 断链、导入设置与 LFS 属性。
    /// </summary>
    public static class AudioAssetHygieneValidator
    {
        private static readonly string[] SerializedTextExtensions =
        {
            ".asset", ".controller", ".json", ".mat", ".prefab", ".unity", ".anim"
        };

        public sealed class Finding
        {
            public string Category;
            public string AssetPath;
            public string Detail;

            public override string ToString()
            {
                return "[" + Category + "] "
                    + (string.IsNullOrEmpty(AssetPath) ? string.Empty : AssetPath + " ")
                    + Detail;
            }
        }

        public static List<Finding> ValidateAll()
        {
            var findings = new List<Finding>();
            ValidateRootLayout(findings);
            ValidateFormalAudioFiles(findings);
            ValidateLegacyRoots(findings);
            ValidateManifest(findings);
            ValidateImportSettings(findings);
            ValidateSerializedReferences(findings);
            return findings;
        }

        [MenuItem("NineGrid/Tools/Validate Audio Asset Hygiene")]
        public static void ValidateMenu()
        {
            var findings = ValidateAll();
            WriteAuditReport();
            if (findings.Count == 0)
            {
                Debug.Log("[AudioAssetHygiene] OK — formal audio roots, manifest, imports and serialized references are clean.");
                return;
            }

            Debug.LogError("[AudioAssetHygiene] Failures=" + findings.Count);
            var limit = Mathf.Min(findings.Count, 80);
            for (var i = 0; i < limit; i++)
            {
                Debug.LogError("[AudioAssetHygiene] " + findings[i]);
            }
        }

        public static List<Finding> ValidateRootLayout()
        {
            var findings = new List<Finding>();
            ValidateRootLayout(findings);
            return findings;
        }

        public static List<Finding> ValidateFormalAudioFiles()
        {
            var findings = new List<Finding>();
            ValidateFormalAudioFiles(findings);
            return findings;
        }

        public static List<Finding> ValidateManifest()
        {
            var findings = new List<Finding>();
            ValidateManifest(findings);
            return findings;
        }

        public static List<Finding> ValidateImportSettings()
        {
            var findings = new List<Finding>();
            ValidateImportSettings(findings);
            return findings;
        }

        public static List<Finding> ValidateSerializedReferences()
        {
            var findings = new List<Finding>();
            ValidateSerializedReferences(findings);
            return findings;
        }

        public static string ComputeSha256(string absolutePath)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(absolutePath))
            {
                var bytes = sha.ComputeHash(stream);
                var builder = new StringBuilder(bytes.Length * 2);
                for (var i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }

        public static bool IsLfsTrackedByAttributes(string assetPath)
        {
            var attributesPath = Path.Combine(ProjectRoot(), ".gitattributes");
            if (!File.Exists(attributesPath) || string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            var extension = Path.GetExtension(assetPath).ToLowerInvariant();
            var lines = File.ReadAllLines(attributesPath);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.StartsWith("*" + extension, StringComparison.OrdinalIgnoreCase)
                    && line.IndexOf("filter=lfs", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsLfsPointer(string absolutePath)
        {
            if (!File.Exists(absolutePath))
            {
                return false;
            }

            using (var stream = File.OpenRead(absolutePath))
            using (var reader = new StreamReader(stream, Encoding.UTF8, true, 256, true))
            {
                return string.Equals(
                    reader.ReadLine(),
                    "version https://git-lfs.github.com/spec/v1",
                    StringComparison.Ordinal);
            }
        }

        public static AudioAssetAuditReport BuildAuditReport()
        {
            var formalFiles = FindAudioFiles(AudioAssetPaths.FormalRoot);
            var quarantineFiles = FindAudioFiles(AudioAssetPaths.QuarantineRoot);
            var legacyQuarantineFiles = FindAudioFiles(AudioAssetPaths.LegacyQuarantineRoot);
            var duplicateFiles = FindAudioFiles(AudioAssetPaths.LegacyDuplicateRoot);
            var allAssets = new List<AudioAssetAuditAsset>();
            var hashToPaths = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            AddAuditAssets(formalFiles, "formal", allAssets, hashToPaths);
            AddAuditAssets(duplicateFiles, "duplicate", allAssets, hashToPaths);
            AddAuditAssets(quarantineFiles, "quarantine", allAssets, hashToPaths);
            AddAuditAssets(legacyQuarantineFiles, "legacy-quarantine", allAssets, hashToPaths);

            var duplicateGroups = 0;
            foreach (var pair in hashToPaths)
            {
                if (pair.Value.Count > 1)
                {
                    duplicateGroups++;
                }
            }

            var findings = ValidateAll();
            var findingText = new List<string>(findings.Count + 1);
            for (var i = 0; i < findings.Count; i++)
            {
                findingText.Add(findings[i].ToString());
            }

            if (formalFiles.Count > 0 && FindBgmFiles(formalFiles).Count == 0)
            {
                findingText.Add("[info] No BGM candidate is present in the migrated source set; formal assets are classified as SFX.");
            }

            return new AudioAssetAuditReport
            {
                schemaVersion = 1,
                ticket = "#167",
                generatedUtc = DateTime.UtcNow.ToString("O"),
                migrationCompleted = Directory.Exists(ToAbsolute(AudioAssetPaths.FormalRoot))
                    && findings.Count == 0,
                selectedSourceRoot = AudioAssetPaths.LegacyFormalRoot,
                duplicateSourceRoot = AudioAssetPaths.LegacyDuplicateRoot,
                quarantineSourceRoot = AudioAssetPaths.QuarantineRoot,
                formalRoot = AudioAssetPaths.FormalRoot,
                bgmRoot = AudioAssetPaths.BgmRoot,
                sfxRoot = AudioAssetPaths.SfxRoot,
                selectedAssetCount = formalFiles.Count + quarantineFiles.Count + legacyQuarantineFiles.Count,
                duplicateAssetCount = duplicateFiles.Count,
                quarantineAssetCount = quarantineFiles.Count + legacyQuarantineFiles.Count,
                finalFormalAssetCount = formalFiles.Count,
                duplicateHashGroupCount = duplicateGroups,
                duplicateReferenceCount = CountLegacyReferences(),
                assets = allAssets.ToArray(),
                references = Array.Empty<AudioAssetAuditReference>(),
                findings = findingText.ToArray(),
            };
        }

        public static void WriteAuditReport()
        {
            var report = BuildAuditReport();
            if (report.sourceAuditAssets == null || report.sourceAuditAssets.Length == 0)
            {
                var persisted = LoadPersistedSourceAuditAssets();
                report.sourceAuditAssets = persisted != null && persisted.Length > 0 ? persisted : report.assets;
            }
            var assetPath = AudioAssetPaths.AuditRoot + "/audio-asset-audit-167.json";
            var absolutePath = ToAbsolute(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            File.WriteAllText(absolutePath, JsonUtility.ToJson(report, true), Encoding.UTF8);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        private static AudioAssetAuditAsset[] LoadPersistedSourceAuditAssets()
        {
            var assetPath = AudioAssetPaths.AuditRoot + "/audio-asset-audit-167.json";
            var absolutePath = ToAbsolute(assetPath);
            if (!File.Exists(absolutePath))
            {
                return null;
            }

            try
            {
                var report = JsonUtility.FromJson<AudioAssetAuditReport>(
                    File.ReadAllText(absolutePath, Encoding.UTF8).TrimStart('\uFEFF'));
                return report?.sourceAuditAssets;
            }
            catch
            {
                return null;
            }
        }

        public static List<string> FindAudioFiles(string rootAssetPath)
        {
            var files = new List<string>();
            var absolute = ToAbsolute(rootAssetPath);
            if (!Directory.Exists(absolute))
            {
                return files;
            }

            var paths = Directory.GetFiles(absolute, "*", SearchOption.AllDirectories);
            for (var i = 0; i < paths.Length; i++)
            {
                var assetPath = ToAssetPath(paths[i]);
                if (AudioAssetPaths.IsAudioExtension(assetPath))
                {
                    files.Add(assetPath);
                }
            }

            files.Sort(StringComparer.OrdinalIgnoreCase);
            return files;
        }

        private static void ValidateRootLayout(List<Finding> findings)
        {
            if (!AssetDatabase.IsValidFolder(AudioAssetPaths.FormalRoot))
            {
                findings.Add(new Finding
                {
                    Category = "root",
                    AssetPath = AudioAssetPaths.FormalRoot,
                    Detail = "Formal Resources audio root is missing.",
                });
                return;
            }

            if (!AssetDatabase.IsValidFolder(AudioAssetPaths.BgmRoot))
            {
                findings.Add(new Finding
                {
                    Category = "root",
                    AssetPath = AudioAssetPaths.BgmRoot,
                    Detail = "BGM root is missing.",
                });
            }

            if (!AssetDatabase.IsValidFolder(AudioAssetPaths.SfxRoot))
            {
                findings.Add(new Finding
                {
                    Category = "root",
                    AssetPath = AudioAssetPaths.SfxRoot,
                    Detail = "SFX root is missing.",
                });
            }

            var files = Directory.GetFiles(ToAbsolute(AudioAssetPaths.FormalRoot), "*", SearchOption.AllDirectories);
            for (var i = 0; i < files.Length; i++)
            {
                var assetPath = ToAssetPath(files[i]);
                if (AudioAssetPaths.IsAudioExtension(assetPath)
                    && !AudioAssetPaths.IsFormalAudioPath(assetPath))
                {
                    findings.Add(new Finding
                    {
                        Category = "illegal-root",
                        AssetPath = assetPath,
                        Detail = "Formal audio is outside BGM/SFX.",
                    });
                }

                if (AudioAssetPaths.IsAudioExtension(assetPath)
                    && AudioAssetPaths.IsQuarantinePath(assetPath))
                {
                    findings.Add(new Finding
                    {
                        Category = "quarantine-in-formal",
                        AssetPath = assetPath,
                        Detail = "Quarantine material is inside the formal audio root.",
                    });
                }
            }
        }

        private static void ValidateFormalAudioFiles(List<Finding> findings)
        {
            var files = FindAudioFiles(AudioAssetPaths.FormalRoot);
            var hashes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < files.Count; i++)
            {
                var path = files[i];
                var absolute = ToAbsolute(path);
                if (AssetDatabase.LoadAssetAtPath<AudioClip>(path) == null)
                {
                    findings.Add(new Finding
                    {
                        Category = "broken-import",
                        AssetPath = path,
                        Detail = "AudioClip import is unresolved.",
                    });
                }

                var hash = ComputeSha256(absolute);
                if (!hashes.TryGetValue(hash, out var matches))
                {
                    matches = new List<string>();
                    hashes.Add(hash, matches);
                }

                matches.Add(path);
                if (!IsLfsTrackedByAttributes(path))
                {
                    findings.Add(new Finding
                    {
                        Category = "lfs",
                        AssetPath = path,
                        Detail = "Audio extension has no filter=lfs rule.",
                    });
                }

                if (IsLfsPointer(absolute))
                {
                    findings.Add(new Finding
                    {
                        Category = "lfs",
                        AssetPath = path,
                        Detail = "Audio file is an unmaterialized Git LFS pointer.",
                    });
                }
            }

            foreach (var pair in hashes)
            {
                if (pair.Value.Count > 1)
                {
                    findings.Add(new Finding
                    {
                        Category = "duplicate-content",
                        AssetPath = pair.Value[0],
                        Detail = "SHA-256 is shared by: " + string.Join(", ", pair.Value.ToArray()),
                    });
                }
            }
        }

        private static void ValidateLegacyRoots(List<Finding> findings)
        {
            if (AssetDatabase.IsValidFolder(AudioAssetPaths.LegacyDuplicateRoot))
            {
                findings.Add(new Finding
                {
                    Category = "legacy-duplicate-root",
                    AssetPath = AudioAssetPaths.LegacyDuplicateRoot,
                    Detail = "Duplicate formal audio root still exists.",
                });
            }

            if (AssetDatabase.IsValidFolder(AudioAssetPaths.LegacyFormalRoot)
                && FindAudioFiles(AudioAssetPaths.LegacyFormalRoot).Count > 0)
            {
                findings.Add(new Finding
                {
                    Category = "legacy-formal-root",
                    AssetPath = AudioAssetPaths.LegacyFormalRoot,
                    Detail = "Legacy formal audio root still contains audio files.",
                });
            }

            var quarantineFiles = FindAudioFiles(AudioAssetPaths.QuarantineRoot);
            for (var i = 0; i < quarantineFiles.Count; i++)
            {
                if (AudioAssetPaths.IsUnder(quarantineFiles[i], AudioAssetPaths.FormalRoot))
                {
                    findings.Add(new Finding
                    {
                        Category = "quarantine-in-formal",
                        AssetPath = quarantineFiles[i],
                        Detail = "Quarantine file is under Resources/audio.",
                    });
                }
            }
        }

        private static void ValidateManifest(List<Finding> findings)
        {
            var manifestPath = ToAbsolute(AudioAssetPaths.ManifestAssetPath);
            if (!File.Exists(manifestPath))
            {
                findings.Add(new Finding
                {
                    Category = "manifest",
                    AssetPath = AudioAssetPaths.ManifestAssetPath,
                    Detail = "Formal audio manifest is missing.",
                });
                return;
            }

            AudioAssetManifest manifest;
            try
            {
                manifest = JsonUtility.FromJson<AudioAssetManifest>(File.ReadAllText(manifestPath, Encoding.UTF8));
            }
            catch (Exception ex)
            {
                findings.Add(new Finding
                {
                    Category = "manifest",
                    AssetPath = AudioAssetPaths.ManifestAssetPath,
                    Detail = "Manifest JSON is invalid: " + ex.Message,
                });
                return;
            }

            if (manifest == null || manifest.entries == null)
            {
                findings.Add(new Finding
                {
                    Category = "manifest",
                    AssetPath = AudioAssetPaths.ManifestAssetPath,
                    Detail = "Manifest has no entries.",
                });
                return;
            }

            var expected = new HashSet<string>(FindAudioFiles(AudioAssetPaths.FormalRoot), StringComparer.OrdinalIgnoreCase);
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < manifest.entries.Length; i++)
            {
                var entry = manifest.entries[i];
                if (entry == null)
                {
                    findings.Add(new Finding
                    {
                        Category = "manifest",
                        AssetPath = AudioAssetPaths.ManifestAssetPath,
                        Detail = "Null manifest entry at index " + i + ".",
                    });
                    continue;
                }

                var path = entry.assetPath == null ? string.Empty : entry.assetPath.Replace('\\', '/');
                var key = AudioAssetManifestLoader.NormalizeKey(entry.resourcesKey);
                if (!expected.Contains(path))
                {
                    findings.Add(new Finding
                    {
                        Category = "manifest-orphan",
                        AssetPath = path,
                        Detail = "Manifest path is not a formal audio asset.",
                    });
                }

                if (!seenPaths.Add(path))
                {
                    findings.Add(new Finding
                    {
                        Category = "manifest-duplicate",
                        AssetPath = path,
                        Detail = "Manifest contains duplicate asset paths.",
                    });
                }

                if (!seenKeys.Add(key))
                {
                    findings.Add(new Finding
                    {
                        Category = "manifest-duplicate",
                        AssetPath = key,
                        Detail = "Manifest contains duplicate Resources keys.",
                    });
                }

                if (AssetDatabase.AssetPathToGUID(path) != entry.guid)
                {
                    findings.Add(new Finding
                    {
                        Category = "guid",
                        AssetPath = path,
                        Detail = "Manifest GUID does not match AssetDatabase.",
                    });
                }

                if (AssetDatabase.LoadAssetAtPath<AudioClip>(path) == null)
                {
                    findings.Add(new Finding
                    {
                        Category = "broken-import",
                        AssetPath = path,
                        Detail = "Manifest asset cannot be loaded as AudioClip.",
                    });
                }

                if (!path.StartsWith("Assets/Resources/", StringComparison.OrdinalIgnoreCase))
                {
                    findings.Add(new Finding
                    {
                        Category = "resources-link",
                        AssetPath = path,
                        Detail = "Manifest asset is outside a Resources directory.",
                    });
                }

                if (entry.kind == "BGM" && !AudioAssetPaths.IsUnder(path, AudioAssetPaths.BgmRoot))
                {
                    findings.Add(new Finding
                    {
                        Category = "manifest-kind",
                        AssetPath = path,
                        Detail = "BGM entry is not under BGM root.",
                    });
                }

                if (entry.kind == "SFX" && !AudioAssetPaths.IsUnder(path, AudioAssetPaths.SfxRoot))
                {
                    findings.Add(new Finding
                    {
                        Category = "manifest-kind",
                        AssetPath = path,
                        Detail = "SFX entry is not under SFX root.",
                    });
                }
            }

            foreach (var path in expected)
            {
                if (!seenPaths.Contains(path))
                {
                    findings.Add(new Finding
                    {
                        Category = "manifest-missing",
                        AssetPath = path,
                        Detail = "Formal audio is absent from manifest.",
                    });
                }
            }
        }

        private static void ValidateImportSettings(List<Finding> findings)
        {
            var files = FindAudioFiles(AudioAssetPaths.FormalRoot);
            for (var i = 0; i < files.Count; i++)
            {
                var path = files[i];
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null)
                {
                    findings.Add(new Finding
                    {
                        Category = "import-settings",
                        AssetPath = path,
                        Detail = "AudioImporter is unavailable.",
                    });
                    continue;
                }

                if (IsThreeD(importer))
                {
                    findings.Add(new Finding
                    {
                        Category = "import-settings",
                        AssetPath = path,
                        Detail = "Audio is configured as 3D; formal audio must be 2D.",
                    });
                }

                var settings = importer.defaultSampleSettings;
                var isBgm = AudioAssetPaths.IsUnder(path, AudioAssetPaths.BgmRoot);
                var expectedLoadType = isBgm ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                if (settings.loadType != expectedLoadType)
                {
                    findings.Add(new Finding
                    {
                        Category = "import-settings",
                        AssetPath = path,
                        Detail = "Unexpected load type. Expected " + expectedLoadType + " but was " + settings.loadType + ".",
                    });
                }

                if (settings.preloadAudioData != isBgm)
                {
                    findings.Add(new Finding
                    {
                        Category = "import-settings",
                        AssetPath = path,
                        Detail = "Unexpected preloadAudioData; BGM must preload and SFX must load on first use.",
                    });
                }
            }
        }

        private static void ValidateSerializedReferences(List<Finding> findings)
        {
            var files = FindSerializedTextFiles();
            for (var i = 0; i < files.Count; i++)
            {
                var path = files[i];
                string text;
                try
                {
                    text = File.ReadAllText(ToAbsolute(path), Encoding.UTF8);
                }
                catch
                {
                    continue;
                }

                if (text.IndexOf(AudioAssetPaths.LegacyDuplicateRoot, StringComparison.OrdinalIgnoreCase) >= 0
                    || text.IndexOf(AudioAssetPaths.LegacyFormalRoot, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    findings.Add(new Finding
                    {
                        Category = "dangling-reference",
                        AssetPath = path,
                        Detail = "Serialized asset still references a removed legacy audio root.",
                    });
                }

                if (text.IndexOf(AudioAssetPaths.QuarantineRoot, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    findings.Add(new Finding
                    {
                        Category = "quarantine-reference",
                        AssetPath = path,
                        Detail = "Serialized formal data references the isolated audio root.",
                    });
                }
            }
        }

        private static bool IsThreeD(AudioImporter importer)
        {
            var serialized = new SerializedObject(importer);
            var property = serialized.FindProperty("m_3D");
            return property != null && property.boolValue;
        }

        private static void AddAuditAssets(
            List<string> paths,
            string role,
            List<AudioAssetAuditAsset> into,
            Dictionary<string, List<string>> hashToPaths)
        {
            for (var i = 0; i < paths.Count; i++)
            {
                var path = paths[i];
                var hash = ComputeSha256(ToAbsolute(path));
                if (!hashToPaths.TryGetValue(hash, out var matches))
                {
                    matches = new List<string>();
                    hashToPaths.Add(hash, matches);
                }

                matches.Add(path);
                into.Add(new AudioAssetAuditAsset
                {
                    sourceRole = role,
                    originalAssetPath = path,
                    assetPath = path,
                    finalAssetPath = path,
                    guid = AssetDatabase.AssetPathToGUID(path),
                    sha256 = hash,
                    sizeBytes = new FileInfo(ToAbsolute(path)).Length,
                    lfsTracked = IsLfsTrackedByAttributes(path),
                    lfsPointer = IsLfsPointer(ToAbsolute(path)),
                    referenceCount = 0,
                    matchingPaths = Array.Empty<string>(),
                });
            }
        }

        private static List<string> FindBgmFiles(List<string> files)
        {
            var result = new List<string>();
            for (var i = 0; i < files.Count; i++)
            {
                if (AudioAssetPaths.IsUnder(files[i], AudioAssetPaths.BgmRoot))
                {
                    result.Add(files[i]);
                }
            }

            return result;
        }

        private static int CountLegacyReferences()
        {
            var count = 0;
            var files = FindSerializedTextFiles();
            for (var i = 0; i < files.Count; i++)
            {
                var text = File.ReadAllText(ToAbsolute(files[i]), Encoding.UTF8);
                if (text.IndexOf(AudioAssetPaths.LegacyDuplicateRoot, StringComparison.OrdinalIgnoreCase) >= 0
                    || text.IndexOf(AudioAssetPaths.LegacyFormalRoot, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    count++;
                }
            }

            return count;
        }

        private static List<string> FindSerializedTextFiles()
        {
            var result = new List<string>();
            var assetsRoot = ToAbsolute("Assets");
            if (!Directory.Exists(assetsRoot))
            {
                return result;
            }

            var files = Directory.GetFiles(assetsRoot, "*", SearchOption.AllDirectories);
            for (var i = 0; i < files.Length; i++)
            {
                var path = ToAssetPath(files[i]);
                if (path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)
                    || AudioAssetPaths.IsAudioExtension(path)
                    || AudioAssetPaths.IsUnder(path, AudioAssetPaths.FormalRoot)
                    || AudioAssetPaths.IsUnder(path, AudioAssetPaths.QuarantineRoot)
                    || AudioAssetPaths.IsUnder(path, AudioAssetPaths.AuditRoot))
                {
                    continue;
                }

                var extension = Path.GetExtension(path).ToLowerInvariant();
                for (var e = 0; e < SerializedTextExtensions.Length; e++)
                {
                    if (extension == SerializedTextExtensions[e])
                    {
                        result.Add(path);
                        break;
                    }
                }
            }

            return result;
        }

        private static string ProjectRoot()
        {
            return Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        }

        private static string ToAbsolute(string assetPath)
        {
            var normalized = assetPath.Replace('\\', '/');
            return Path.GetFullPath(Path.Combine(ProjectRoot(), normalized));
        }

        private static string ToAssetPath(string absolutePath)
        {
            var project = ProjectRoot().Replace('\\', '/') + "/";
            var normalized = absolutePath.Replace('\\', '/');
            return normalized.StartsWith(project, StringComparison.OrdinalIgnoreCase)
                ? normalized.Substring(project.Length)
                : normalized;
        }
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NineGrid.Content.Audio;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    /// <summary>
    /// #167：把选中的英文复数音频根迁到 Resources/audio，保留其 GUID；
    /// 把隔离素材移到 Resources 外；核对后删除中文重复根，并写固定 manifest 与审计报告。
    /// </summary>
    public static class AudioAssetMigrationRunner
    {
        private const string AuditFolder = AudioAssetPaths.AuditRoot;
        private const string PreflightAuditPath = AudioAssetPaths.AuditRoot + "/audio-asset-audit-167-preflight.json";
        private const string AuditPath = AudioAssetPaths.AuditRoot + "/audio-asset-audit-167.json";

        [MenuItem("NineGrid/Tools/Migrate Audio Assets To Resources")]
        public static void Run()
        {
            var report = Migrate();
            if (report.StartsWith("FAILED", StringComparison.Ordinal))
            {
                Debug.LogError("[AudioAssetMigration] " + report);
            }
            else
            {
                Debug.Log("[AudioAssetMigration] " + report);
            }
        }

        [MenuItem("NineGrid/Tools/Refresh Audio Manifest And Audit")]
        public static void RefreshManifestAndAudit()
        {
            EnsureFolders();
            WriteManifest();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            var report = AudioAssetHygieneValidator.BuildAuditReport();
            report.sourceAuditAssets = LoadSourceAuditAssets();
            if (report.sourceAuditAssets == null || report.sourceAuditAssets.Length == 0)
            {
                report.sourceAuditAssets = report.assets;
            }
            WriteAuditReport(report);
            Debug.Log("[AudioAssetMigration] manifest refreshed; formal=" + report.finalFormalAssetCount + " findings=" + report.findings.Length);
        }

        [MenuItem("NineGrid/Tools/Normalize Audio Imports")]
        public static void NormalizeAudioImportsMenu()
        {
            EnsureFolders();
            NormalizeFormalImportSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[AudioAssetMigration] formal audio imports normalized.");
        }

        public static string Migrate()
        {
            EnsureFolders();
            var selectedSources = AudioAssetHygieneValidator.FindAudioFiles(AudioAssetPaths.LegacyFormalRoot);
            selectedSources.RemoveAll(path => AudioAssetPaths.IsQuarantinePath(path));
            var duplicateSources = AudioAssetHygieneValidator.FindAudioFiles(AudioAssetPaths.LegacyDuplicateRoot);
            var quarantineSources = AudioAssetHygieneValidator.FindAudioFiles(AudioAssetPaths.LegacyQuarantineRoot);

            var preflightReport = AudioAssetHygieneValidator.BuildAuditReport();
            WriteReport(preflightReport, PreflightAuditPath);

            var quarantineHashes = HashesFor(quarantineSources);
            var duplicateFormalSources = new List<string>();
            for (var i = 0; i < duplicateSources.Count; i++)
            {
                var hash = AudioAssetHygieneValidator.ComputeSha256(ToAbsolute(duplicateSources[i]));
                if (!quarantineHashes.Contains(hash))
                {
                    duplicateFormalSources.Add(duplicateSources[i]);
                }
            }

            var preflightFindings = new List<string>();
            VerifyDuplicateSet(selectedSources, duplicateFormalSources, preflightFindings);
            CollectLegacyReferenceFindings(duplicateSources, preflightFindings);
            if (preflightFindings.Count > 0)
            {
                preflightReport.migrationCompleted = false;
                preflightReport.findings = preflightFindings.ToArray();
                WriteReport(preflightReport, PreflightAuditPath);
                return "FAILED preflight: " + string.Join("; ", preflightFindings.ToArray());
            }

            var sourceAuditAssets = preflightReport.assets;
            var movedFormal = MoveAssets(selectedSources, AudioAssetPaths.SfxRoot);
            var movedQuarantine = MoveAssets(quarantineSources, AudioAssetPaths.QuarantineRoot);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (AssetDatabase.IsValidFolder(AudioAssetPaths.LegacyDuplicateRoot)
                && !AssetDatabase.DeleteAsset(AudioAssetPaths.LegacyDuplicateRoot))
            {
                return Fail("cleanup", new List<string> { "Could not delete duplicate root " + AudioAssetPaths.LegacyDuplicateRoot });
            }

            DeleteEmptyLegacyFolders();
            NormalizeFormalImportSettings();
            WriteManifest();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var report = AudioAssetHygieneValidator.BuildAuditReport();
            report.sourceAuditAssets = sourceAuditAssets;
            report.migrationCompleted = report.findings.Length == 1
                && report.findings[0].StartsWith("[info]", StringComparison.Ordinal);
            WriteReport(report, AuditPath);
            AudioAssetManifestLoader.ClearCache();
            return "OK formal=" + report.finalFormalAssetCount
                + " moved=" + movedFormal
                + " quarantined=" + movedQuarantine
                + " duplicateRootDeleted=true"
                + " findings=" + report.findings.Length;
        }

        private static HashSet<string> HashesFor(List<string> paths)
        {
            var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < paths.Count; i++)
            {
                hashes.Add(AudioAssetHygieneValidator.ComputeSha256(ToAbsolute(paths[i])));
            }

            return hashes;
        }

        private static void VerifyDuplicateSet(List<string> selected, List<string> duplicate, List<string> findings)
        {
            if (selected.Count != duplicate.Count)
            {
                findings.Add("[hash] selected/duplicate audio counts differ: " + selected.Count + " vs " + duplicate.Count);
            }

            var selectedHashes = HashesFor(selected);
            var duplicateHashes = HashesFor(duplicate);
            foreach (var hash in selectedHashes)
            {
                if (!duplicateHashes.Contains(hash))
                {
                    findings.Add("[hash] selected audio has no duplicate match: " + hash);
                }
            }

            foreach (var hash in duplicateHashes)
            {
                if (!selectedHashes.Contains(hash))
                {
                    findings.Add("[hash] duplicate audio has no selected match: " + hash);
                }
            }
        }

        private static void CollectLegacyReferenceFindings(List<string> duplicateSources, List<string> findings)
        {
            var guids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < duplicateSources.Count; i++)
            {
                var guid = AssetDatabase.AssetPathToGUID(duplicateSources[i]);
                if (!string.IsNullOrEmpty(guid))
                {
                    guids.Add(guid);
                }
            }

            var files = Directory.GetFiles(ToAbsolute("Assets"), "*", SearchOption.AllDirectories);
            for (var i = 0; i < files.Length; i++)
            {
                var extension = Path.GetExtension(files[i]).ToLowerInvariant();
                if (extension != ".asset" && extension != ".controller" && extension != ".prefab"
                    && extension != ".unity" && extension != ".anim" && extension != ".mat")
                {
                    continue;
                }

                var text = File.ReadAllText(files[i], Encoding.UTF8);
                foreach (var guid in guids)
                {
                    if (text.IndexOf(guid, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        findings.Add("[reference] duplicate GUID " + guid + " is referenced by " + files[i]);
                    }
                }
            }
        }

        private static int MoveAssets(List<string> sources, string destinationRoot)
        {
            var moved = 0;
            for (var i = 0; i < sources.Count; i++)
            {
                var source = sources[i];
                var destination = destinationRoot + "/" + Path.GetFileName(source);
                if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                EnsureFolder(destinationRoot);
                if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(destination)))
                {
                    continue;
                }

                var sourceGuid = AssetDatabase.AssetPathToGUID(source);
                var error = AssetDatabase.MoveAsset(source, destination);
                if (!string.IsNullOrEmpty(error))
                {
                    throw new InvalidOperationException(source + " -> " + destination + ": " + error);
                }

                if (!string.Equals(sourceGuid, AssetDatabase.AssetPathToGUID(destination), StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("GUID changed during move: " + source + " -> " + destination);
                }

                moved++;
            }

            return moved;
        }

        private static string Fail(string stage, List<string> findings)
        {
            var report = AudioAssetHygieneValidator.BuildAuditReport();
            report.migrationCompleted = false;
            report.findings = findings.ToArray();
            WriteReport(report, AuditPath);
            return "FAILED " + stage + ": " + string.Join("; ", findings.ToArray());
        }

        private static void NormalizeFormalImportSettings()
        {
            var files = AudioAssetHygieneValidator.FindAudioFiles(AudioAssetPaths.FormalRoot);
            for (var i = 0; i < files.Count; i++)
            {
                var importer = AssetImporter.GetAtPath(files[i]) as AudioImporter;
                if (importer == null)
                {
                    continue;
                }

                var isBgm = AudioAssetPaths.IsUnder(files[i], AudioAssetPaths.BgmRoot);
                var settings = importer.defaultSampleSettings;
                settings.loadType = isBgm ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                settings.preloadAudioData = isBgm;
                importer.defaultSampleSettings = settings;
                SetTwoDimensional(importer);
                importer.SaveAndReimport();
            }
        }

        private static void SetTwoDimensional(AudioImporter importer)
        {
            var serialized = new SerializedObject(importer);
            var property = serialized.FindProperty("m_3D");
            if (property != null)
            {
                property.boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void WriteManifest()
        {
            var entries = new List<AudioAssetManifestEntry>();
            var files = AudioAssetHygieneValidator.FindAudioFiles(AudioAssetPaths.FormalRoot);
            for (var i = 0; i < files.Count; i++)
            {
                var path = files[i];
                var kind = AudioAssetPaths.IsUnder(path, AudioAssetPaths.BgmRoot) ? "BGM" : "SFX";
                entries.Add(new AudioAssetManifestEntry
                {
                    guid = AssetDatabase.AssetPathToGUID(path),
                    assetPath = path,
                    resourcesKey = AudioAssetManifestLoader.NormalizeKey(path),
                    sha256 = AudioAssetHygieneValidator.ComputeSha256(ToAbsolute(path)),
                    sizeBytes = new FileInfo(ToAbsolute(path)).Length,
                    kind = kind,
                    loadPolicy = kind == "BGM" ? "preload" : "first_use_cached",
                });
            }

            var manifest = new AudioAssetManifest
            {
                schemaVersion = 1,
                ticket = "#167",
                formalRoot = AudioAssetPaths.FormalRoot,
                bgmRoot = AudioAssetPaths.BgmRoot,
                sfxRoot = AudioAssetPaths.SfxRoot,
                entries = entries.ToArray(),
            };
            File.WriteAllText(ToAbsolute(AudioAssetPaths.ManifestAssetPath), JsonUtility.ToJson(manifest, true), Encoding.UTF8);
            AssetDatabase.ImportAsset(AudioAssetPaths.ManifestAssetPath, ImportAssetOptions.ForceUpdate);
        }

        private static void WriteAuditReport(AudioAssetAuditReport report)
        {
            WriteReport(report, AuditPath);
        }

        private static void WriteReport(AudioAssetAuditReport report, string assetPath)
        {
            File.WriteAllText(ToAbsolute(assetPath), JsonUtility.ToJson(report, true), Encoding.UTF8);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        private static AudioAssetAuditAsset[] LoadSourceAuditAssets()
        {
            var path = ToAbsolute(PreflightAuditPath);
            if (!File.Exists(path))
            {
                return Array.Empty<AudioAssetAuditAsset>();
            }

            try
            {
                var text = File.ReadAllText(path, Encoding.UTF8).TrimStart('\uFEFF');
                var report = JsonUtility.FromJson<AudioAssetAuditReport>(text);
                return report?.assets ?? Array.Empty<AudioAssetAuditAsset>();
            }
            catch
            {
                return Array.Empty<AudioAssetAuditAsset>();
            }
        }


        private static void DeleteEmptyLegacyFolders()
        {
            if (AssetDatabase.IsValidFolder(AudioAssetPaths.LegacyQuarantineRoot)
                && AudioAssetHygieneValidator.FindAudioFiles(AudioAssetPaths.LegacyQuarantineRoot).Count == 0)
            {
                AssetDatabase.DeleteAsset(AudioAssetPaths.LegacyQuarantineRoot);
            }

            if (AssetDatabase.IsValidFolder(AudioAssetPaths.LegacyFormalRoot)
                && AudioAssetHygieneValidator.FindAudioFiles(AudioAssetPaths.LegacyFormalRoot).Count == 0)
            {
                AssetDatabase.DeleteAsset(AudioAssetPaths.LegacyFormalRoot);
            }
        }

        private static void EnsureFolders()
        {
            EnsureFolder(AudioAssetPaths.FormalRoot);
            EnsureFolder(AudioAssetPaths.BgmRoot);
            EnsureFolder(AudioAssetPaths.SfxRoot);
            EnsureFolder(AudioAssetPaths.QuarantineRoot);
            EnsureFolder(AuditFolder);
        }

        private static void EnsureFolder(string folderAssetPath)
        {
            if (AssetDatabase.IsValidFolder(folderAssetPath))
            {
                return;
            }

            var parts = folderAssetPath.Replace('\\', '/').Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i]))
                {
                    continue;
                }

                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static string ToAbsolute(string assetPath)
        {
            var project = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.GetFullPath(Path.Combine(project, assetPath.Replace('\\', '/')));
        }
    }
}
#endif

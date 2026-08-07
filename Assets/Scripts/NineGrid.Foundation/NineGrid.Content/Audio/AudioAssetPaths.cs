using System;

namespace NineGrid.Content.Audio
{
    /// <summary>
    /// 唯一正式音频根与迁移边界（ADR-0036 / #167）。
    /// 这些是项目相对 AssetDatabase 路径，不是运行时磁盘路径。
    /// </summary>
    public static class AudioAssetPaths
    {
        public const string LegacyFormalRoot = "Assets/Arts/audios";
        public const string LegacyDuplicateRoot = "Assets/Arts/音频";
        public const string LegacyQuarantineRoot = LegacyFormalRoot + "/暂时不允许使用";
        public const string QuarantineRoot = "Assets/Arts/AudioQuarantine";
        public const string AuditRoot = "Assets/Notes/Logs/AudioAssetAudit";

        public const string FormalRoot = "Assets/Resources/audio";
        public const string BgmRoot = FormalRoot + "/BGM";
        public const string SfxRoot = FormalRoot + "/SFX";
        public const string ManifestAssetPath = FormalRoot + "/audio_manifest.json";
        public const string ResourcesRelativeRoot = "audio";
        public const string ManifestResourcesKey = ResourcesRelativeRoot + "/audio_manifest";

        public static bool IsAudioExtension(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            var dot = path.LastIndexOf('.');
            if (dot < 0 || dot == path.Length - 1)
            {
                return false;
            }

            var extension = path.Substring(dot).ToLowerInvariant();
            return extension == ".wav"
                || extension == ".mp3"
                || extension == ".ogg"
                || extension == ".aif"
                || extension == ".aiff"
                || extension == ".flac";
        }

        public static bool IsUnder(string path, string root)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(root))
            {
                return false;
            }

            var normalizedPath = path.Replace('\\', '/').TrimEnd('/');
            var normalizedRoot = root.Replace('\\', '/').TrimEnd('/');
            return string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase)
                || normalizedPath.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsFormalAudioPath(string path)
        {
            return IsUnder(path, BgmRoot) || IsUnder(path, SfxRoot);
        }

        public static bool IsQuarantinePath(string path)
        {
            return IsUnder(path, QuarantineRoot)
                || IsUnder(path, LegacyQuarantineRoot)
                || path.IndexOf("拒绝用", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}

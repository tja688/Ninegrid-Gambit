using System;

namespace NineGrid.Content.Audio
{
    /// <summary>
    /// 正式音频固定 manifest（#167）。条目与磁盘 1:1，运行时按 Resources 键取用。
    /// </summary>
    [Serializable]
    public sealed class AudioAssetManifest
    {
        public int schemaVersion;
        public string ticket;
        public string formalRoot;
        public string bgmRoot;
        public string sfxRoot;
        public AudioAssetManifestEntry[] entries;
    }

    [Serializable]
    public sealed class AudioAssetManifestEntry
    {
        public string guid;
        public string assetPath;
        public string resourcesKey;
        public string sha256;
        public long sizeBytes;
        public string kind;
        public string loadPolicy;
    }
}

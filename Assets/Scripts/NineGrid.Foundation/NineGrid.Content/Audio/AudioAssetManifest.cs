using System;

namespace NineGrid.Content.Audio
{
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

    [Serializable]
    public sealed class AudioAssetAuditReport
    {
        public int schemaVersion;
        public string ticket;
        public string generatedUtc;
        public bool migrationCompleted;
        public string selectedSourceRoot;
        public string duplicateSourceRoot;
        public string quarantineSourceRoot;
        public string formalRoot;
        public string bgmRoot;
        public string sfxRoot;
        public int selectedAssetCount;
        public int duplicateAssetCount;
        public int quarantineAssetCount;
        public int finalFormalAssetCount;
        public int duplicateHashGroupCount;
        public int duplicateReferenceCount;
        public AudioAssetAuditAsset[] assets;
        public AudioAssetAuditAsset[] sourceAuditAssets;
        public AudioAssetAuditReference[] references;
        public string[] findings;
    }

    [Serializable]
    public sealed class AudioAssetAuditAsset
    {
        public string sourceRole;
        public string originalAssetPath;
        public string assetPath;
        public string finalAssetPath;
        public string guid;
        public string sha256;
        public long sizeBytes;
        public bool lfsTracked;
        public bool lfsPointer;
        public int referenceCount;
        public string[] matchingPaths;
    }

    [Serializable]
    public sealed class AudioAssetAuditReference
    {
        public string assetPath;
        public string guid;
        public string referencePath;
        public string reason;
    }
}

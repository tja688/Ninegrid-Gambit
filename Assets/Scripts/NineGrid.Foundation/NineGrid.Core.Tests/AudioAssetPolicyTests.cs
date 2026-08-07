using NineGrid.Content.Audio;
using NUnit.Framework;

namespace NineGrid.Core.Tests
{
    public sealed class AudioAssetPolicyTests
    {
        [Test]
        public void FormalRoots_AreSingleResourcesAudioNamespace()
        {
            Assert.AreEqual("Assets/Resources/audio", AudioAssetPaths.FormalRoot);
            Assert.AreEqual("Assets/Resources/audio/BGM", AudioAssetPaths.BgmRoot);
            Assert.AreEqual("Assets/Resources/audio/SFX", AudioAssetPaths.SfxRoot);
            Assert.AreEqual("Assets/Arts/音频", AudioAssetPaths.LegacyDuplicateRoot);
        }

        [Test]
        public void QuarantinePaths_AreNeverFormalCandidates()
        {
            Assert.IsTrue(AudioAssetPaths.IsQuarantinePath("Assets/Arts/AudioQuarantine/拒绝用.wav"));
            Assert.IsTrue(AudioAssetPaths.IsQuarantinePath("Assets/Arts/audios/暂时不允许使用/charge.mp3"));
            Assert.IsFalse(AudioAssetPaths.IsFormalAudioPath("Assets/Arts/AudioQuarantine/charge.mp3"));
            Assert.IsFalse(AudioAssetPaths.IsFormalAudioPath("Assets/Arts/audios/暂时不允许使用/charge.mp3"));
        }

        [Test]
        public void ManifestLoader_NormalizesResourcesKeysWithoutScanningDirectories()
        {
            Assert.AreEqual("audio/SFX/click", AudioAssetManifestLoader.NormalizeKey("Assets/Resources/audio/SFX/click.mp3"));
            Assert.AreEqual("audio/SFX/click", AudioAssetManifestLoader.NormalizeKey("audio/SFX/click.wav"));
            Assert.AreEqual("SFX/click", AudioAssetManifestLoader.NormalizeKey("SFX/click"));
        }

        [Test]
        public void FormalAudioExtensionPolicy_RecognizesSupportedFormatsOnly()
        {
            Assert.IsTrue(AudioAssetPaths.IsAudioExtension("Assets/Resources/audio/SFX/click.wav"));
            Assert.IsTrue(AudioAssetPaths.IsAudioExtension("Assets/Resources/audio/BGM/theme.mp3"));
            Assert.IsFalse(AudioAssetPaths.IsAudioExtension("Assets/Resources/audio/audio_manifest.json"));
        }
    }
}

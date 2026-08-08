using System;
using System.Collections.Generic;
using NineGrid.Presentation.Systems;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    public sealed class PlayerAudioSettingsBehaviorTests
    {
        [Test]
        public void ThreeBusVolumeChanges_ApplyImmediatelyAndLeaveAuthorDefaultsUntouched()
        {
            var author = new PlayerAudioSettingsSnapshot(0.9f, false, 0.4f, false, 0.7f, false);
            var applier = new FakeApplier();
            var system = new PlayerAudioSettingsSystem(author, new FakeStore(), applier);

            system.SetVolume(PlayerAudioBus.Master, 0.25f);
            system.SetVolume(PlayerAudioBus.Bgm, 0.5f);
            system.SetVolume(PlayerAudioBus.Sfx, 0.75f);

            Assert.AreEqual(0.25f, applier.Volumes[PlayerAudioBus.Master], 0.001f);
            Assert.AreEqual(0.5f, applier.Volumes[PlayerAudioBus.Bgm], 0.001f);
            Assert.AreEqual(0.75f, applier.Volumes[PlayerAudioBus.Sfx], 0.001f);
            Assert.AreEqual(author, system.AuthorDefaults);
        }

        [Test]
        public void MutingEachBus_UsesSilenceWithoutDiscardingPreviousVolume()
        {
            var applier = new FakeApplier();
            var system = new PlayerAudioSettingsSystem(
                new PlayerAudioSettingsSnapshot(0.9f, false, 0.4f, false, 0.7f, false),
                new FakeStore(),
                applier);

            system.SetMuted(PlayerAudioBus.Master, true);
            system.SetMuted(PlayerAudioBus.Bgm, true);
            system.SetMuted(PlayerAudioBus.Sfx, true);

            Assert.AreEqual(0f, applier.Volumes[PlayerAudioBus.Master]);
            Assert.AreEqual(0f, applier.Volumes[PlayerAudioBus.Bgm]);
            Assert.AreEqual(0f, applier.Volumes[PlayerAudioBus.Sfx]);

            system.SetMuted(PlayerAudioBus.Master, false);
            system.SetMuted(PlayerAudioBus.Bgm, false);
            system.SetMuted(PlayerAudioBus.Sfx, false);

            Assert.AreEqual(0.9f, applier.Volumes[PlayerAudioBus.Master], 0.001f);
            Assert.AreEqual(0.4f, applier.Volumes[PlayerAudioBus.Bgm], 0.001f);
            Assert.AreEqual(0.7f, applier.Volumes[PlayerAudioBus.Sfx], 0.001f);
        }

        [Test]
        public void SavedPreferences_ReloadAndResetReturnsToPackageAuthorDefaults()
        {
            var store = new FakeStore();
            var author = new PlayerAudioSettingsSnapshot(0.9f, false, 0.4f, false, 0.7f, false);
            var first = new PlayerAudioSettingsSystem(author, store, new FakeApplier());
            first.SetVolume(PlayerAudioBus.Bgm, 0.1f);
            first.SetMuted(PlayerAudioBus.Sfx, true);

            var reloaded = new PlayerAudioSettingsSystem(author, store, new FakeApplier());
            Assert.AreEqual(0.1f, reloaded.Current.BgmVolume, 0.001f);
            Assert.IsTrue(reloaded.Current.SfxMuted);

            reloaded.ResetToAuthorDefaults();

            Assert.AreEqual(author, reloaded.Current);
            Assert.AreEqual(0, store.Count);
        }

        [Test]
        public void Changed_NotifiesStableSnapshotForUiBinding()
        {
            var system = new PlayerAudioSettingsSystem(
                new PlayerAudioSettingsSnapshot(1f, false, 1f, false, 1f, false),
                new FakeStore(),
                new FakeApplier());
            var observed = new List<PlayerAudioSettingsSnapshot>();
            system.Changed += observed.Add;

            system.SetMuted(PlayerAudioBus.Bgm, true);

            Assert.AreEqual(1, observed.Count);
            Assert.IsTrue(observed[0].BgmMuted);
        }

        private sealed class FakeApplier : IPlayerAudioBusApplier
        {
            public readonly Dictionary<PlayerAudioBus, float> Volumes = new Dictionary<PlayerAudioBus, float>();

            public void Apply(PlayerAudioSettingsSnapshot settings)
            {
                Volumes[PlayerAudioBus.Master] = settings.EffectiveVolume(PlayerAudioBus.Master);
                Volumes[PlayerAudioBus.Bgm] = settings.EffectiveVolume(PlayerAudioBus.Bgm);
                Volumes[PlayerAudioBus.Sfx] = settings.EffectiveVolume(PlayerAudioBus.Sfx);
            }
        }

        private sealed class FakeStore : IPlayerAudioSettingsStore
        {
            private readonly Dictionary<string, string> values = new Dictionary<string, string>();

            public int Count => values.Count;

            public bool TryGetString(string key, out string value) => values.TryGetValue(key, out value);
            public void SetString(string key, string value) => values[key] = value;
            public void DeleteKey(string key) => values.Remove(key);
            public void Save()
            {
            }
        }
    }
}

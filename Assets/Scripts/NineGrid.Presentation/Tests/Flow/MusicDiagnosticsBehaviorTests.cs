using System;
using System.Collections.Generic;
using NineGrid.Content.Audio;
using NineGrid.Presentation.Systems;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    public sealed class MusicDiagnosticsBehaviorTests
    {
        private const string CatalogJson =
            "{\"schemaVersion\":1,\"ticket\":\"#172\",\"bindings\":["
            + "{\"state\":\"MainMenu\",\"enabled\":true,\"clipKey\":\"audio/BGM/menu\",\"volumeDb\":-6,\"fadeInSeconds\":0.1,\"fadeOutSeconds\":0.2,\"loop\":true},"
            + "{\"state\":\"Battle\",\"enabled\":true,\"clipKey\":\"audio/BGM/battle\",\"volumeDb\":-6,\"fadeInSeconds\":0.1,\"fadeOutSeconds\":0.2,\"loop\":true},"
            + "{\"state\":\"BossBattle\",\"enabled\":true,\"clipKey\":\"audio/BGM/boss\",\"volumeDb\":-6,\"fadeInSeconds\":0.1,\"fadeOutSeconds\":0.2,\"loop\":true}]}";

        [Test]
        public void AuditMusicTrack_UnknownSource_ProducesCorrelatedAnomalyWithoutAutoStop()
        {
            var adapter = new FakeDiagnosticsAdapter();
            var system = CreateSystem(adapter);
            system.RequestState(new MusicStateRequest(DesiredMusicState.MainMenu, "main-menu"));

            adapter.MusicTrackSources.Add(new MusicTrackSourceSnapshot(
                "ghost-a",
                "audio/BGM/ghost",
                3.5d,
                true));

            var audit = system.AuditMusicTrack("TestAudit");

            Assert.IsTrue(audit.HasUnknownSources);
            Assert.AreEqual(1, audit.UnknownSources.Count);
            Assert.AreEqual("ghost-a", audit.UnknownSources[0].SourceId);
            Assert.IsNotNull(audit.Anomaly);
            Assert.AreEqual("TestAudit", audit.Anomaly.Trigger);
            Assert.AreEqual(1L, audit.Anomaly.MusicGeneration);
            Assert.AreEqual(DesiredMusicState.MainMenu.ToString(), audit.Anomaly.DesiredState);
            Assert.AreEqual("audio/BGM/menu", audit.Anomaly.FinalBindingClipKey);
            Assert.AreEqual("main-menu", audit.Anomaly.RequestSource);
            Assert.GreaterOrEqual(audit.Anomaly.ChainId, 0);
            Assert.AreEqual(1, system.OverlapAnomalies.Count);
            Assert.AreEqual(0, adapter.StoppedIds.Count, "审计不得自动停止未知来源。");
            Assert.IsTrue(HasHistory(system, MusicHistoryOutcome.OverlapAnomaly));
        }

        [Test]
        public void AuditMusicTrack_ClaimedSources_AreNotUnknown()
        {
            var adapter = new FakeDiagnosticsAdapter();
            var system = CreateSystem(adapter);
            system.RequestState(new MusicStateRequest(DesiredMusicState.MainMenu, "main-menu"));

            var audit = system.AuditMusicTrack("CleanAudit");

            Assert.IsFalse(audit.HasUnknownSources);
            Assert.AreEqual(0, audit.UnknownSources.Count);
            Assert.IsNull(audit.Anomaly);
            Assert.AreEqual(0, system.OverlapAnomalies.Count);
        }

        [Test]
        public void StopUnknownMusic_StopsOnlyUnknownSources_AndKeepsClaimedMusic()
        {
            var adapter = new FakeDiagnosticsAdapter();
            var system = CreateSystem(adapter);
            system.RequestState(new MusicStateRequest(DesiredMusicState.MainMenu, "main-menu"));
            system.RequestState(new MusicStateRequest(DesiredMusicState.Battle, "battle"));
            adapter.MusicTrackSources.Add(new MusicTrackSourceSnapshot(
                "ghost-a",
                "audio/BGM/ghost",
                1d,
                true));

            var audit = system.StopUnknownMusic("manual-stop");

            Assert.IsFalse(audit.HasUnknownSources);
            Assert.AreEqual(1, adapter.StoppedIds.Count);
            Assert.AreEqual("ghost-a", adapter.StoppedIds[0]);
            Assert.AreEqual(0, adapter.StoppedPlaybackHandles.Count, "已认领播放不得被 Stop Unknown Music 触碰。");
        }

        [Test]
        public void BeginPreview_ReplacesPreviousPreview_AndAuditTreatsPreviewAsClaimed()
        {
            var adapter = new FakeDiagnosticsAdapter();
            var system = CreateSystem(adapter);
            system.RequestState(new MusicStateRequest(DesiredMusicState.MainMenu, "main-menu"));

            var first = system.BeginPreview(new MusicPreviewRequest(
                "audio/BGM/previewA",
                -6f,
                0f,
                0.1f,
                true));
            var second = system.BeginPreview(new MusicPreviewRequest(
                "audio/BGM/previewB",
                -6f,
                0f,
                0.1f,
                true));

            Assert.IsTrue(first.Succeeded);
            Assert.IsTrue(second.Succeeded);
            Assert.AreEqual(1, adapter.StoppedPlaybackHandles.Count, "切换试听前必须先释放旧 Preview。");
            Assert.AreEqual(1, adapter.PausedHandles.Count, "游戏音乐应在 Preview 开始时暂停。");
            Assert.AreEqual(0, adapter.ResumedHandles.Count);

            adapter.MusicTrackSources.Add(new MusicTrackSourceSnapshot(
                adapter.HandleSourceId,
                "audio/BGM/previewB",
                0d,
                true));
            var audit = system.AuditMusicTrack("PreviewAudit");
            Assert.IsFalse(audit.HasUnknownSources, "Editor Preview 来源必须被认领。");

            system.EndPreview("test-end");
            Assert.AreEqual(1, adapter.ResumedHandles.Count, "Preview 结束后按原位置恢复游戏音乐。");
        }

        [Test]
        public void BeginPreview_SuspendsCurrentAndRetiringSources_AndRestoresBothPositions()
        {
            var adapter = new FakeDiagnosticsAdapter { DeferFades = true };
            var system = CreateSystem(adapter);
            system.RequestState(new MusicStateRequest(DesiredMusicState.MainMenu, "main-menu"));
            system.RequestState(new MusicStateRequest(DesiredMusicState.Battle, "battle"));

            var preview = system.BeginPreview(new MusicPreviewRequest(
                "audio/BGM/preview",
                -6f,
                0f,
                0.1f,
                true));

            Assert.IsTrue(preview.Succeeded);
            Assert.AreEqual(2, adapter.PausedHandles.Count, "试听必须同时暂停当前与淡出中的游戏音乐。");
            Assert.AreEqual(0, adapter.ResumedHandles.Count);

            system.EndPreview("test-end");

            Assert.AreEqual(2, adapter.ResumedHandles.Count, "试听结束后必须恢复当前与淡出来源。");
            Assert.AreEqual(DesiredMusicState.Battle, system.DesiredState.Value);
        }

        [Test]
        public void EndPreview_RestoresGameMusic_AtRecordedPosition_WithoutChangingDesiredState()
        {
            var adapter = new FakeDiagnosticsAdapter();
            var system = CreateSystem(adapter);
            system.RequestState(new MusicStateRequest(DesiredMusicState.MainMenu, "main-menu"));

            system.BeginPreview(new MusicPreviewRequest(
                "audio/BGM/previewA",
                -6f,
                0f,
                0.1f,
                true));
            system.EndPreview("test-end");

            Assert.AreEqual(DesiredMusicState.MainMenu, system.DesiredState.Value);
            Assert.AreEqual(1, adapter.ResumedHandles.Count);
            Assert.AreEqual(12.5d, adapter.ResumedPositions[0], 0.001d);
        }

        private static MusicSystem CreateSystem(FakeDiagnosticsAdapter adapter)
        {
            return new MusicSystem(
                MusicBindingCatalog.FromJson(CatalogJson),
                adapter,
                new FixedAudioClock());
        }

        private static bool HasHistory(MusicSystem system, MusicHistoryOutcome outcome)
        {
            for (var i = 0; i < system.History.Count; i++)
            {
                if (system.History[i].Outcome == outcome)
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class FixedAudioClock : IAudioClock
        {
            public double UnscaledTime => 1d;
        }

        private sealed class FakeDiagnosticsAdapter : IMusicPlaybackDiagnosticsAdapter
        {
            public readonly List<MusicPlaybackRequest> Played = new List<MusicPlaybackRequest>();
            public readonly List<MusicPlaybackHandle> StoppedPlaybackHandles = new List<MusicPlaybackHandle>();
            public readonly List<string> StoppedIds = new List<string>();
            public readonly List<MusicPlaybackHandle> PausedHandles = new List<MusicPlaybackHandle>();
            public readonly List<MusicPlaybackHandle> ResumedHandles = new List<MusicPlaybackHandle>();
            public readonly List<double> ResumedPositions = new List<double>();
            public readonly List<MusicTrackSourceSnapshot> MusicTrackSources = new List<MusicTrackSourceSnapshot>();

            private readonly List<Action> mFadeCallbacks = new List<Action>();
            private int mNextSourceId;

            public bool DeferFades { get; set; }

            public string HandleSourceId { get; private set; } = string.Empty;

            public MusicBackendResult Play(MusicPlaybackRequest request)
            {
                Played.Add(request);
                var sourceId = "claimed-" + (++mNextSourceId);
                var handle = new MusicPlaybackHandle(request.Binding.ClipKey, null, sourceId);
                return MusicBackendResult.Success(handle, request.Binding.ClipKey);
            }

            public void FadeOut(MusicPlaybackHandle handle, float durationSeconds, Action completed)
            {
                if (DeferFades)
                {
                    mFadeCallbacks.Add(completed);
                    return;
                }

                completed?.Invoke();
            }

            public void Stop(MusicPlaybackHandle handle)
            {
                StoppedPlaybackHandles.Add(handle);
            }

            public MusicBackendResult PlayPreview(MusicPreviewRequest request)
            {
                HandleSourceId = "preview-" + (++mNextSourceId);
                return MusicBackendResult.Success(
                    new MusicPlaybackHandle(request.ClipKey, null, HandleSourceId),
                    request.ClipKey);
            }

            public void InvokeFade(int index)
            {
                mFadeCallbacks[index]?.Invoke();
            }

            public IReadOnlyList<MusicTrackSourceSnapshot> GetPlayingMusicSources()
            {
                return MusicTrackSources.ToArray();
            }

            public double GetPlaybackPosition(MusicPlaybackHandle handle)
            {
                return 12.5d;
            }

            public void Pause(MusicPlaybackHandle handle)
            {
                PausedHandles.Add(handle);
            }

            public void Resume(MusicPlaybackHandle handle, double positionSeconds)
            {
                ResumedHandles.Add(handle);
                ResumedPositions.Add(positionSeconds);
            }

            public void StopMusicTrackSource(string sourceId)
            {
                StoppedIds.Add(sourceId);
                for (var i = MusicTrackSources.Count - 1; i >= 0; i--)
                {
                    if (string.Equals(MusicTrackSources[i].SourceId, sourceId, StringComparison.Ordinal))
                    {
                        MusicTrackSources.RemoveAt(i);
                    }
                }
            }
        }
    }
}

using System.Collections.Generic;
using NineGrid.Content.Vfx;
using NineGrid.Presentation.Systems.Vfx;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    public sealed class VfxSpriteSheetPlayerContractTests
    {
        [Test]
        public void FrameOrder_SpritesheetNumericSort_IsStable()
        {
            Assert.AreEqual(0, VfxSpriteSheetFrameOrder.ExtractSortIndex("spritesheet_0"));
            Assert.AreEqual(2, VfxSpriteSheetFrameOrder.ExtractSortIndex("spritesheet_2"));
            Assert.AreEqual(10, VfxSpriteSheetFrameOrder.ExtractSortIndex("spritesheet_10"));
            Assert.Less(
                VfxSpriteSheetFrameOrder.CompareNames("spritesheet_2", "spritesheet_10"),
                0);
            Assert.Less(
                VfxSpriteSheetFrameOrder.CompareNames("spritesheet_0", "spritesheet_1"),
                0);
        }

        [Test]
        public void Playback_FiniteLoop_EndsAfterConfiguredLoops()
        {
            var frames = new[] { CreateSprite("spritesheet_0"), CreateSprite("spritesheet_1") };
            var playback = new VfxSpriteSheetPlayback();
            playback.Configure(frames, fps: 10f, speed: 1f, loopLimit: 2, startOffsetSeconds: 0f, useUnscaledTime: false);

            var done = false;
            for (var i = 0; i < 16 && !done; i++)
            {
                done = playback.Tick(0.1f, 0.1f);
            }

            Assert.IsTrue(done);
            Assert.IsTrue(playback.IsComplete);
            Assert.AreEqual(2, playback.CompletedLoops);
        }

        [Test]
        public void Playback_UnscaledTimeBase_UsesUnscaledDelta()
        {
            var frames = new[] { CreateSprite("spritesheet_0"), CreateSprite("spritesheet_1") };
            var scaled = new VfxSpriteSheetPlayback();
            scaled.Configure(frames, 10f, 1f, 1, 0f, useUnscaledTime: false);
            scaled.Tick(0f, 0.2f);
            Assert.AreEqual(0, scaled.FrameIndex);

            var unscaled = new VfxSpriteSheetPlayback();
            unscaled.Configure(frames, 10f, 1f, 1, 0f, useUnscaledTime: true);
            unscaled.Tick(0f, 0.2f);
            Assert.Greater(unscaled.FrameIndex, 0);
        }

        [Test]
        public void Pool_ResetAfterRelease_ClearsRendererState()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            VfxSpriteSheetPoolDiagnostics.ResetForTests();
#endif
            var pool = new VfxSpriteSheetVisualPool();
            var visual = pool.Acquire();
            visual.Renderer.sprite = CreateSprite("spritesheet_0");
            visual.Renderer.color = Color.red;
            visual.Renderer.enabled = true;
            visual.transform.localScale = new Vector3(2f, 2f, 1f);

            pool.Release(visual);
            var reused = pool.Acquire();

            Assert.IsNull(reused.Renderer.sprite);
            Assert.AreEqual(Color.white, reused.Renderer.color);
            Assert.IsFalse(reused.Renderer.enabled);
            Assert.AreEqual(Vector3.one, reused.transform.localScale);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.Greater(VfxSpriteSheetPoolDiagnostics.ReuseCount, 0);
            Assert.Greater(VfxSpriteSheetPoolDiagnostics.ReleaseCount, 0);
#endif
        }

        [Test]
        public void AttachedPulse_EndsWhenDomainHostLost()
        {
            var host = new FakeVfxDomainHost { IsAvailable = true };
            var factory = new VfxSpriteSheetPlayerFactory(
                new VfxSpriteSheetVisualPool(),
                new FakeFrameLoader(CreateFrames(2)));
            factory.TryCreatePulsePlayer(VfxPlayerRegistry.SpriteSheet, out var player, out _);

            var binding = BuildBinding(VfxSpatialOwnership.Attached);
            var request = new VfxPulseStartRequest(
                VfxCueRequest.Simple("vfx.attached", "test"),
                binding,
                "default",
                "fx/test",
                10f,
                1f,
                1f,
                0f,
                Color.white,
                false,
                new VfxSpatialContext("slot", host, Vector3.zero));

            var start = player.StartPulse(request);
            Assert.IsTrue(start.Succeeded);
            host.IsAvailable = false;
            Assert.IsTrue(player.Tick(0.05f));
        }

        [Test]
        public void IndependentPulse_ContinuesAfterHostLost()
        {
            var host = new FakeVfxDomainHost { IsAvailable = true };
            var factory = new VfxSpriteSheetPlayerFactory(
                new VfxSpriteSheetVisualPool(),
                new FakeFrameLoader(CreateFrames(3)));
            factory.TryCreatePulsePlayer(VfxPlayerRegistry.SpriteSheet, out var player, out _);

            var binding = BuildBinding(VfxSpatialOwnership.Independent);
            var request = new VfxPulseStartRequest(
                VfxCueRequest.Simple("vfx.test", "test"),
                binding,
                "default",
                "fx/test",
                10f,
                1f,
                1f,
                0f,
                Color.white,
                false,
                new VfxSpatialContext("impact", host, new Vector3(1f, 2f, 0f)));

            Assert.IsTrue(player.StartPulse(request).Succeeded);
            host.IsAvailable = false;
            Assert.IsFalse(player.Tick(0.05f));
            Assert.IsFalse(player.Tick(0.05f));
            Assert.IsTrue(player.Tick(0.2f));
        }

        private static VfxCueBinding BuildBinding(VfxSpatialOwnership ownership)
        {
            var dto = new VfxCueBindingDto
            {
                cueId = "vfx.test",
                enabled = true,
                playerId = VfxPlayerRegistry.SpriteSheet,
                materialKey = "fx/test",
                spatialOwnership = ownership == VfxSpatialOwnership.Attached ? "attached" : "independent",
            };
            return new VfxCueBinding(dto);
        }

        private static Sprite[] CreateFrames(int count)
        {
            var frames = new Sprite[count];
            for (var i = 0; i < count; i++)
            {
                frames[i] = CreateSprite("spritesheet_" + i);
            }

            return frames;
        }

        private static Sprite CreateSprite(string name)
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            texture.SetPixels(new Color[16]);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            return sprite;
        }

        private sealed class FakeFrameLoader : IVfxMaterialFrameLoader
        {
            private readonly Sprite[] mFrames;

            public FakeFrameLoader(Sprite[] frames)
            {
                mFrames = frames ?? System.Array.Empty<Sprite>();
            }

            public bool TryLoadFrames(string materialKey, out Sprite[] frames, out string failureReason)
            {
                frames = mFrames;
                failureReason = string.Empty;
                return frames.Length > 0;
            }
        }

        private sealed class FakeVfxDomainHost : IVfxDomainHost
        {
            private readonly GameObject mRoot = new GameObject("FakeVfxDomainHost");
            private readonly Transform mAttachment = new GameObject("Attachment").transform;

            public FakeVfxDomainHost()
            {
                mAttachment.SetParent(mRoot.transform, false);
            }

            public bool IsAvailable { get; set; }
            public Transform AttachmentParent => mAttachment;
            public SpriteMask Mask => null;

            public bool TryWorldToLocal(Vector3 worldPosition, out Vector3 localPosition)
            {
                localPosition = mAttachment.InverseTransformPoint(worldPosition);
                return true;
            }

            public bool TryGetFollowTarget(out Transform followTarget)
            {
                followTarget = mAttachment;
                return true;
            }

            public bool TryGetSortingBounds(out VfxSortingBounds bounds)
            {
                bounds = new VfxSortingBounds("Main", 100, 200);
                return true;
            }
        }
    }
}

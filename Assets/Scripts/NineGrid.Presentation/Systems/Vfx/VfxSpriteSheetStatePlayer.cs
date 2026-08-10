using System;
using NineGrid.Content.Vfx;
using UnityEngine;

namespace NineGrid.Presentation.Systems.Vfx
{
    internal sealed class VfxSpriteSheetStatePlayer : IVfxStatePlayer
    {
        private static long sNextInstanceId = 1;

        private readonly VfxSpriteSheetVisualPool mPool;
        private readonly IVfxMaterialFrameLoader mFrameLoader;

        private VfxSpriteSheetVisual mVisual;
        private VfxSpriteSheetPlayback mPlayback;
        private IVfxDomainHost mDomainHost;
        private VfxSpatialOwnership mOwnership;
        private string mInstanceId;
        private Color mTint = Color.white;
        private bool mActive;
        private bool mExiting;

        public VfxSpriteSheetStatePlayer(
            VfxSpriteSheetVisualPool pool,
            IVfxMaterialFrameLoader frameLoader)
        {
            mPool = pool ?? throw new ArgumentNullException(nameof(pool));
            mFrameLoader = frameLoader ?? throw new ArgumentNullException(nameof(frameLoader));
            mPlayback = new VfxSpriteSheetPlayback();
        }

        public VfxPlayerStartResult StartState(VfxStateStartRequest request)
        {
            CancelInternal(releaseVisual: true);
            if (!mFrameLoader.TryLoadFrames(request.MaterialKey, out var frames, out var loadReason))
            {
                return VfxPlayerStartResult.Failure(loadReason);
            }

            if (frames == null || frames.Length == 0)
            {
                return VfxPlayerStartResult.Failure("精灵表帧为空。");
            }

            mVisual = mPool.Acquire();
            mOwnership = request.Binding.SpatialOwnership;
            mDomainHost = request.AcceptedSpatial.DomainHost;
            mTint = request.Tint;
            mInstanceId = "vfx-state-spritesheet-" + sNextInstanceId++;
            mExiting = false;

            if (!TryMountVisual(request, out var mountReason))
            {
                mPool.Release(mVisual);
                mVisual = null;
                return VfxPlayerStartResult.Failure(mountReason);
            }

            ApplyRendererState(request.Scale, frames[0]);
            mPlayback.Configure(
                frames,
                request.Fps,
                VfxBindingPlayback.ResolveSpeed(request.Speed),
                VfxSpriteSheetPlayback.InfiniteLoops,
                request.StartOffsetSeconds,
                request.UseUnscaledTime);
            mActive = true;
            ApplyCurrentFrame();
            return VfxPlayerStartResult.Success(mInstanceId);
        }

        public void BeginExit(bool immediate, int exitLoopLimit)
        {
            if (!mActive)
            {
                return;
            }

            if (immediate || mVisual == null)
            {
                CancelInternal(releaseVisual: true);
                return;
            }

            mExiting = true;
            mPlayback.Configure(
                mPlayback.Frames,
                mPlayback.Fps,
                mPlayback.Speed,
                Mathf.Max(1, exitLoopLimit),
                0f,
                mPlayback.UseUnscaledTime);
        }

        public void Cancel()
        {
            CancelInternal(releaseVisual: true);
        }

        public bool Tick(float deltaTime)
        {
            if (!mActive)
            {
                return true;
            }

            if (mOwnership == VfxSpatialOwnership.Attached)
            {
                if (mDomainHost == null || !mDomainHost.IsAvailable)
                {
                    FinishNaturally();
                    return true;
                }
            }

            var scaledDelta = deltaTime;
            var unscaledDelta = Time.unscaledDeltaTime;
            if (!mExiting)
            {
                mPlayback.Tick(scaledDelta, unscaledDelta);
                ApplyCurrentFrame();
                return false;
            }

            if (mPlayback.Tick(scaledDelta, unscaledDelta))
            {
                FinishNaturally();
                return true;
            }

            ApplyCurrentFrame();
            return false;
        }

        private void FinishNaturally()
        {
            CancelInternal(releaseVisual: true);
        }

        private void CancelInternal(bool releaseVisual)
        {
            mActive = false;
            mExiting = false;
            mDomainHost = null;
            mPlayback.Reset();
            if (releaseVisual && mVisual != null)
            {
                mPool.Release(mVisual);
                mVisual = null;
            }
        }

        private bool TryMountVisual(VfxStateStartRequest request, out string failureReason)
        {
            failureReason = string.Empty;
            var renderer = mVisual.Renderer;
            var worldPosition = ResolveWorldPosition(request.AcceptedSpatial);

            if (mOwnership == VfxSpatialOwnership.Attached && mDomainHost != null)
            {
                Transform parent = null;
                if (mDomainHost.TryGetFollowTarget(out var follow) && follow != null)
                {
                    parent = follow;
                }
                else if (mDomainHost.AttachmentParent != null)
                {
                    parent = mDomainHost.AttachmentParent;
                }

                if (parent == null)
                {
                    failureReason = "视觉域宿主缺少附着父级。";
                    return false;
                }

                mVisual.transform.SetParent(parent, false);
                if (mDomainHost.TryWorldToLocal(worldPosition, out var local))
                {
                    mVisual.transform.localPosition = local;
                }
                else
                {
                    mVisual.transform.position = worldPosition;
                }

                ApplySorting(renderer, mDomainHost);
                ApplyMask(renderer, mDomainHost.Mask);
                return true;
            }

            mVisual.transform.SetParent(VfxIndependentSpatialRoot.Root, false);
            mVisual.transform.position = worldPosition;
            renderer.sortingLayerName = "Main";
            renderer.sortingOrder = 5000;
            return true;
        }

        private static Vector3 ResolveWorldPosition(VfxSpatialContext spatial)
        {
            if (spatial.PositionSnapshot.HasValue)
            {
                return spatial.PositionSnapshot.Value;
            }

            return Vector3.zero;
        }

        private void ApplyRendererState(float scale, Sprite firstFrame)
        {
            var renderer = mVisual.Renderer;
            renderer.enabled = true;
            renderer.color = mTint;
            var uniform = Mathf.Max(0.01f, scale);
            mVisual.transform.localScale = new Vector3(uniform, uniform, 1f);
            if (firstFrame != null)
            {
                renderer.sprite = firstFrame;
            }
        }

        private void ApplyCurrentFrame()
        {
            if (mVisual == null)
            {
                return;
            }

            var frame = mPlayback.CurrentFrame;
            if (frame != null)
            {
                mVisual.Renderer.sprite = frame;
            }
        }

        private static void ApplySorting(SpriteRenderer renderer, IVfxDomainHost host)
        {
            if (renderer == null || host == null)
            {
                return;
            }

            if (!host.TryGetSortingBounds(out var bounds) || !bounds.HasLayer)
            {
                renderer.sortingLayerName = "Main";
                renderer.sortingOrder = 5000;
                return;
            }

            renderer.sortingLayerName = bounds.SortingLayerName;
            renderer.sortingOrder = bounds.ClampOrder(5000);
        }

        private static void ApplyMask(SpriteRenderer renderer, SpriteMask mask)
        {
            if (renderer == null || mask == null)
            {
                return;
            }

            renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        }
    }
}

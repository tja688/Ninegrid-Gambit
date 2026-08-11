using NineGrid.Content.Vfx;
using UnityEngine;

namespace NineGrid.Presentation.Systems.Vfx
{
    /// <summary>
    /// particle 程序化粒子 State 播放器：loop 预设持续发射；
    /// 退出契约——immediate 立刻拆除；segment 停止发射让在场粒子自然放完（不按 loop 次数计）。
    /// </summary>
    internal sealed class VfxParticleStatePlayer : IVfxStatePlayer
    {
        private static long sNextInstanceId = 1;

        private VfxParticleEmitterRig mRig;
        private IVfxDomainHost mDomainHost;
        private VfxSpatialOwnership mOwnership;
        private string mInstanceId;
        private bool mActive;
        private bool mExiting;

        public VfxPlayerStartResult StartState(VfxStateStartRequest request)
        {
            CancelInternal();
            if (!VfxParticlePresetLibrary.TryGet(request.MaterialKey, out var preset))
            {
                return VfxPlayerStartResult.Failure("未知粒子预设：" + request.MaterialKey);
            }

            if (!preset.Loop)
            {
                return VfxPlayerStartResult.Failure("一次性预设仅供 Pulse 绑定使用：" + preset.Id);
            }

            mOwnership = request.Binding.SpatialOwnership;
            mDomainHost = request.AcceptedSpatial.DomainHost;
            if (!VfxParticleEmitterRig.TryResolveMount(
                    mOwnership,
                    request.AcceptedSpatial,
                    out var parent,
                    out var worldPosition,
                    out var sortingLayer,
                    out var sortingOrder,
                    out var mountReason))
            {
                return VfxPlayerStartResult.Failure(mountReason);
            }

            var rigParams = new VfxParticleRigParams(
                countOverride: Mathf.RoundToInt(Mathf.Max(0f, request.Fps)),
                simulationSpeed: request.Speed,
                scale: request.Scale,
                tint: request.Tint,
                useUnscaledTime: request.UseUnscaledTime,
                startDelaySeconds: request.StartOffsetSeconds,
                sortingLayerName: sortingLayer,
                sortingOrder: sortingOrder);

            mRig = VfxParticleEmitterRig.Create(preset, rigParams);
            mRig.Mount(parent, worldPosition, preset.EmitOffsetY * (request.Scale <= 0f ? 1f : request.Scale));
            mRig.Play();

            mInstanceId = "vfx-state-particle-" + sNextInstanceId++;
            mActive = true;
            mExiting = false;
            return VfxPlayerStartResult.Success(mInstanceId);
        }

        public void BeginExit(bool immediate, int exitLoopLimit)
        {
            if (!mActive)
            {
                return;
            }

            if (immediate || mRig == null)
            {
                CancelInternal();
                return;
            }

            mExiting = true;
            mRig.StopEmitting();
        }

        public void Cancel()
        {
            CancelInternal();
        }

        public bool Tick(float deltaTime)
        {
            if (!mActive)
            {
                return true;
            }

            if (mOwnership == VfxSpatialOwnership.Attached
                && (mDomainHost == null || !mDomainHost.IsAvailable))
            {
                CancelInternal();
                return true;
            }

            if (mRig == null)
            {
                CancelInternal();
                return true;
            }

            if (mExiting && mRig.IsDrained)
            {
                CancelInternal();
                return true;
            }

            return false;
        }

        private void CancelInternal()
        {
            mActive = false;
            mExiting = false;
            mDomainHost = null;
            if (mRig != null)
            {
                mRig.Destroy();
                mRig = null;
            }
        }
    }
}

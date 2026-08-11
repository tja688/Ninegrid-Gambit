using NineGrid.Content.Vfx;
using UnityEngine;

namespace NineGrid.Presentation.Systems.Vfx
{
    /// <summary>
    /// particle 程序化粒子 Pulse 播放器：materialKey 携带 VfxParticlePresetIds 预设 ID，
    /// 运行时装配 ParticleSystem 一次性爆发，粒子放完自然结束。
    /// 绑定字段映射：fps→爆发数量覆盖、scale→整体缩放、speed→模拟速度、tint→整体着色、
    /// startOffsetSeconds→发射延迟、timeBase→unscaled 时钟。
    /// </summary>
    internal sealed class VfxParticlePulsePlayer : IVfxPulsePlayer
    {
        private const float FailsafeSeconds = 15f;

        private static long sNextInstanceId = 1;

        private VfxParticleEmitterRig mRig;
        private IVfxDomainHost mDomainHost;
        private VfxSpatialOwnership mOwnership;
        private string mInstanceId;
        private float mElapsed;
        private bool mUseUnscaledTime;
        private bool mActive;

        public VfxPlayerStartResult StartPulse(VfxPulseStartRequest request)
        {
            CancelInternal();
            if (!VfxParticlePresetLibrary.TryGet(request.MaterialKey, out var preset))
            {
                return VfxPlayerStartResult.Failure("未知粒子预设：" + request.MaterialKey);
            }

            if (preset.Loop)
            {
                return VfxPlayerStartResult.Failure("loop 预设仅供 State 绑定使用：" + preset.Id);
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

            mUseUnscaledTime = request.UseUnscaledTime;
            mElapsed = 0f;
            mInstanceId = "vfx-particle-" + sNextInstanceId++;
            mActive = true;
            return VfxPlayerStartResult.Success(mInstanceId);
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

            mElapsed += mUseUnscaledTime ? Time.unscaledDeltaTime : Mathf.Max(0f, deltaTime);
            if (mRig == null || mRig.IsFinished || mElapsed > FailsafeSeconds)
            {
                CancelInternal();
                return true;
            }

            return false;
        }

        private void CancelInternal()
        {
            mActive = false;
            mDomainHost = null;
            if (mRig != null)
            {
                mRig.Destroy();
                mRig = null;
            }
        }
    }
}

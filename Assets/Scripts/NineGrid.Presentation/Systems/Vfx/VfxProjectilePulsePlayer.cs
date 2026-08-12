using NineGrid.Content.Vfx;
using UnityEngine;

namespace NineGrid.Presentation.Systems.Vfx
{
    /// <summary>
    /// projectile 程序化弹道 Pulse 播放器：materialKey 携带 VfxProjectilePresetIds 预设 ID，
    /// 源坐标取 VfxSpatialContext.PositionSnapshot、靶坐标取 TargetPositionSnapshot，
    /// 播「A 打到 B」的弹头飞行 + 拖尾 + 命中爆点，全部粒子放完自然结束。
    /// 绑定字段映射：fps→弹道数量覆盖（1~8）、scale→整体缩放、speed→飞行与模拟时间倍率、
    /// tint→整体着色、startOffsetSeconds→起手延迟、timeBase→unscaled 时钟。
    /// 缺靶坐标时退化为向右前方的演示飞行（供工作台预览）。
    /// 命中时刻经 VfxPresentationPlan（首达/末达）回传给发射方，便于表演编排对齐伤害反馈。
    /// </summary>
    internal sealed class VfxProjectilePulsePlayer : IVfxPulsePlayer
    {
        private const float FailsafeSeconds = 15f;

        /// <summary>无靶坐标时的演示飞行向量（工作台预览/冒烟用）。</summary>
        private static readonly Vector3 PreviewFlightVector = new Vector3(3.6f, 0.6f, 0f);

        private static long sNextInstanceId = 1;

        private VfxProjectileFlightRig mRig;
        private IVfxDomainHost mDomainHost;
        private VfxSpatialOwnership mOwnership;
        private string mInstanceId;
        private float mElapsed;
        private bool mUseUnscaledTime;
        private bool mActive;

        public VfxPlayerStartResult StartPulse(VfxPulseStartRequest request)
        {
            CancelInternal();
            if (!VfxProjectilePresetLibrary.TryGet(request.MaterialKey, out var preset))
            {
                return VfxPlayerStartResult.Failure("未知弹道预设：" + request.MaterialKey);
            }

            mOwnership = request.Binding.SpatialOwnership;
            mDomainHost = request.AcceptedSpatial.DomainHost;
            if (!VfxParticleEmitterRig.TryResolveMount(
                    mOwnership,
                    request.AcceptedSpatial,
                    out var parent,
                    out var sourceWorld,
                    out var sortingLayer,
                    out var sortingOrder,
                    out var mountReason))
            {
                return VfxPlayerStartResult.Failure(mountReason);
            }

            var targetWorld = request.AcceptedSpatial.TargetPositionSnapshot
                ?? sourceWorld + PreviewFlightVector;

            var rigParams = new VfxProjectileRigParams(
                countOverride: Mathf.RoundToInt(Mathf.Max(0f, request.Fps)),
                speedMultiplier: request.Speed,
                scale: request.Scale,
                tint: request.Tint,
                useUnscaledTime: request.UseUnscaledTime,
                startDelaySeconds: request.StartOffsetSeconds,
                sortingLayerName: sortingLayer,
                sortingOrder: sortingOrder);

            mRig = VfxProjectileFlightRig.Create(preset, rigParams, parent, sourceWorld, targetWorld);

            mUseUnscaledTime = request.UseUnscaledTime;
            mElapsed = 0f;
            mInstanceId = "vfx-projectile-" + sNextInstanceId++;
            mActive = true;
            return VfxPlayerStartResult.Success(
                mInstanceId,
                new VfxPresentationPlan(mRig.FirstArrivalDelay, mRig.LastArrivalDelay));
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

            var effectiveDelta = mUseUnscaledTime ? Time.unscaledDeltaTime : Mathf.Max(0f, deltaTime);
            mElapsed += effectiveDelta;
            if (mRig == null || mRig.Advance(effectiveDelta) || mElapsed > FailsafeSeconds)
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

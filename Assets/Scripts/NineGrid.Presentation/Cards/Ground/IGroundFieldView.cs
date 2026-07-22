using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 场地场景视图适配：锚点、布局、HitProxy、骷髅 Fusion 与 Transform 操作。
    /// 不持有占格表 / busy / Clock / 飞牌。
    /// </summary>
    public interface IGroundFieldView
    {
        GroundFieldLayoutSettings LayoutSettings { get; }

        CancellationToken DestroyToken { get; }

        Transform GetGroundAnchor(int slot);

        bool TryGetAnchor(int slot, out Transform anchor);

        void EnsureHitProxies();

        void RefreshSlotHit(int slot, bool hitEnabled);

        void RefreshAllSlotHits(Func<int, bool> hitEnabledForSlot);

        UniTask PresentSkeletonFusionAsync(
            SkeletonFusionPresentationRequest request,
            Func<CancellationToken, UniTask> onFusionStarted,
            CancellationToken cancellationToken = default);
    }
}

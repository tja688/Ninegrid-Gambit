using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NineGrid.Cards.Convergence
{
    /// <summary>
    /// 复杂域特效位移约定：写 L3.localPosition，末态回零；与 L2 物理隔离。
    /// 世界视觉点 = L0/L1/L2 叠加后再加 L3；反算 local 时以 EffectFrame 父空间为准。
    /// </summary>
    public static class EffectFrameConvergence
    {
        public static bool TryGetTower(Transform root, out CardTransformTower tower)
        {
            tower = null;
            if (root == null)
            {
                return false;
            }

            tower = root.GetComponent<CardTransformTower>();
            if (tower == null)
            {
                return false;
            }

            tower.EnsureTower();
            return tower.EffectFrame != null;
        }

        public static bool TryGetTower(ManagedCard card, out CardTransformTower tower) =>
            TryGetTower(card?.Transform, out tower);

        public static bool TryGetDriver(Transform root, out LayerConvergenceDriver driver) =>
            LayerConvergenceDriver.TryGet(root, TowerLayer.EffectFrame, out driver);

        public static bool TryGetDriver(ManagedCard card, out LayerConvergenceDriver driver) =>
            TryGetDriver(card?.Transform, out driver);

        /// <summary>世界点 → L3 父空间（SlotFrame）下的 local，供 L3.localPosition 收敛目标。</summary>
        public static Vector3 WorldToEffectLocal(CardTransformTower tower, Vector3 worldPosition)
        {
            tower.EnsureTower();
            var parent = tower.EffectFrame != null ? tower.EffectFrame.parent : tower.SlotFrame;
            if (parent == null)
            {
                return worldPosition - tower.CardRoot.position;
            }

            return parent.InverseTransformPoint(worldPosition);
        }

        public static Vector3 WorldToEffectLocal(Transform root, Vector3 worldPosition)
        {
            if (!TryGetTower(root, out var tower))
            {
                return Vector3.zero;
            }

            return WorldToEffectLocal(tower, worldPosition);
        }

        /// <summary>离散归零：停止 L3 收敛并将 local 置 0。不写 L0。</summary>
        public static void SnapHome(Transform root, string reason = null, int uid = 0)
        {
            if (root == null)
            {
                return;
            }

            if (!TryGetTower(root, out var tower) || !TryGetDriver(root, out var driver))
            {
                return;
            }

            CardDeckTween.KillMotion(root, reason ?? "EffectFrame.SnapHome", uid);
            driver.Admit(HandoffState.AtRest(Vector3.zero));
            if (tower.EffectFrame != null)
            {
                tower.EffectFrame.localPosition = Vector3.zero;
            }
        }

        public static void SnapHome(ManagedCard card, string reason = null) =>
            SnapHome(card?.Transform, reason, card?.Uid ?? 0);

        /// <summary>
        /// 把 L3 收敛到目标世界点对应的 local（L0/L2 不动）。
        /// </summary>
        public static void ConvergeVisualToWorld(Transform root, Vector3 targetWorld, float sourceTime)
        {
            if (root == null || !TryGetTower(root, out var tower) || !TryGetDriver(root, out var driver))
            {
                return;
            }

            CardDeckTween.KillMotion(root, "EffectFrame.Converge", 0);
            var targetLocal = WorldToEffectLocal(tower, targetWorld);
            driver.ConvergeTo(targetLocal, sourceTime);
        }

        public static void ConvergeVisualToWorld(ManagedCard card, Vector3 targetWorld, float sourceTime) =>
            ConvergeVisualToWorld(card?.Transform, targetWorld, sourceTime);

        /// <summary>L3 收敛回零（末态回零）。</summary>
        public static void ConvergeHome(Transform root, float sourceTime)
        {
            if (root == null || !TryGetDriver(root, out var driver))
            {
                return;
            }

            CardDeckTween.KillMotion(root, "EffectFrame.ConvergeHome", 0);
            driver.ConvergeTo(Vector3.zero, sourceTime);
        }

        public static void ConvergeHome(ManagedCard card, float sourceTime) =>
            ConvergeHome(card?.Transform, sourceTime);

        /// <summary>
        /// 就位后释放：把 L0 停到目标世界点，L3 归零。用于融合撞点后把视觉位固化到根。
        /// </summary>
        public static void ParkRootAtWorld(Transform root, Vector3 worldPosition, string reason = null, int uid = 0)
        {
            if (root == null || !TryGetTower(root, out var tower) || !TryGetDriver(root, out var driver))
            {
                if (root != null)
                {
                    root.position = worldPosition;
                }

                return;
            }

            CardDeckTween.KillMotion(root, reason ?? "EffectFrame.ParkRoot", uid);
            driver.Admit(HandoffState.AtRest(Vector3.zero));
            tower.CardRoot.position = worldPosition;
            if (tower.EffectFrame != null)
            {
                tower.EffectFrame.localPosition = Vector3.zero;
            }
        }

        public static void ParkRootAtWorld(ManagedCard card, Vector3 worldPosition, string reason = null) =>
            ParkRootAtWorld(card?.Transform, worldPosition, reason, card?.Uid ?? 0);

        public static async UniTask AwaitDriverAsync(
            LayerConvergenceDriver driver,
            CancellationToken cancellationToken,
            Func<bool> stillValid = null)
        {
            if (driver == null)
            {
                return;
            }

            while (driver.IsActive)
            {
                if (stillValid != null && !stillValid())
                {
                    return;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        public static async UniTask ConvergeVisualToWorldAsync(
            Transform root,
            Vector3 targetWorld,
            float sourceTime,
            CancellationToken cancellationToken,
            bool parkRootOnComplete = false)
        {
            if (root == null || !TryGetDriver(root, out var driver))
            {
                return;
            }

            ConvergeVisualToWorld(root, targetWorld, sourceTime);
            await AwaitDriverAsync(driver, cancellationToken);

            if (parkRootOnComplete && !cancellationToken.IsCancellationRequested)
            {
                ParkRootAtWorld(root, targetWorld, "EffectFrame.ParkAfterConverge");
            }
        }

        public static async UniTask ConvergeVisualToWorldAsync(
            ManagedCard card,
            Vector3 targetWorld,
            float sourceTime,
            CancellationToken cancellationToken,
            bool parkRootOnComplete = false) =>
            await ConvergeVisualToWorldAsync(
                card?.Transform,
                targetWorld,
                sourceTime,
                cancellationToken,
                parkRootOnComplete);

        /// <summary>
        /// 冲刺往返：L3 先到偏移世界点再回零。无塔时返回 false，由调用方走旧路径。
        /// </summary>
        public static async UniTask<bool> TryPlayOutAndBackAsync(
            Transform root,
            Vector3 worldOffset,
            float halfDuration,
            CancellationToken cancellationToken)
        {
            if (root == null || !TryGetTower(root, out var tower) || !TryGetDriver(root, out var driver))
            {
                return false;
            }

            var duration = Mathf.Max(0.01f, halfDuration);
            var peakWorld = tower.EffectFrame.position + worldOffset;

            ConvergeVisualToWorld(root, peakWorld, duration);
            await AwaitDriverAsync(driver, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return true;
            }

            ConvergeHome(root, duration);
            await AwaitDriverAsync(driver, cancellationToken);
            return true;
        }
    }
}

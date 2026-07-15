using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NineGrid.Cards.Convergence
{
    /// <summary>
    /// 复杂域格位收敛约定：就位时 L0 停在格锚、L2.local = 0；换格/补牌只改 L2 目标重收敛，零 SetParent。
    /// L0 在收敛过程中不写 world position；仅在 SnapHome / ParkRoot 离散落锚。
    /// </summary>
    public static class SlotFrameConvergence
    {
        public static bool TryGetTower(ManagedCard card, out CardTransformTower tower)
        {
            tower = null;
            if (card?.Transform == null)
            {
                return false;
            }

            tower = card.Transform.GetComponent<CardTransformTower>();
            if (tower == null)
            {
                return false;
            }

            tower.EnsureTower();
            return tower.SlotFrame != null;
        }

        public static bool TryGetDriver(ManagedCard card, out LayerConvergenceDriver driver) =>
            LayerConvergenceDriver.TryGet(card?.Transform, TowerLayer.SlotFrame, out driver);

        /// <summary>世界点 → L2 父空间（BoardFrame）下的 local，供 L2.localPosition 收敛目标。</summary>
        public static Vector3 WorldToSlotLocal(CardTransformTower tower, Vector3 worldPosition)
        {
            tower.EnsureTower();
            var parent = tower.SlotFrame != null ? tower.SlotFrame.parent : tower.BoardFrame;
            if (parent == null)
            {
                return worldPosition - tower.CardRoot.position;
            }

            return parent.InverseTransformPoint(worldPosition);
        }

        public static Vector3 WorldToSlotLocal(ManagedCard card, Vector3 worldPosition)
        {
            if (!TryGetTower(card, out var tower))
            {
                return Vector3.zero;
            }

            return WorldToSlotLocal(tower, worldPosition);
        }

        /// <summary>
        /// 离散落锚：L0 → 世界锚点，L2 归零并停止收敛。不播动画。
        /// </summary>
        public static void SnapHome(ManagedCard card, Vector3 anchorWorld, string reason = null, int uid = 0)
        {
            if (card?.Transform == null)
            {
                return;
            }

            if (!TryGetTower(card, out var tower) || !TryGetDriver(card, out var driver))
            {
                CardDeckTween.KillMotion(card.Transform, reason ?? "SlotFrame.SnapHome", uid);
                card.Transform.position = anchorWorld;
                return;
            }

            CardDeckTween.KillMotion(card.Transform, reason ?? "SlotFrame.SnapHome", uid > 0 ? uid : card.Uid);
            driver.Admit(HandoffState.AtRest(Vector3.zero));
            tower.CardRoot.position = anchorWorld;
            if (tower.SlotFrame != null)
            {
                tower.SlotFrame.localPosition = Vector3.zero;
            }

            FlightSortingChannel.Restore(card);
        }

        /// <summary>
        /// 补牌起飞：L0 先落目标格锚，L2 设为起飞点相对 local，再收敛回 0。
        /// </summary>
        public static void BeginDealFromLaunch(
            ManagedCard card,
            Vector3 launchWorld,
            Vector3 slotAnchorWorld,
            float sourceTime)
        {
            if (card?.Transform == null || !TryGetTower(card, out var tower) || !TryGetDriver(card, out var driver))
            {
                return;
            }

            CardDeckTween.KillMotion(card.Transform, "SlotFrame.BeginDeal", card.Uid);
            tower.CardRoot.position = slotAnchorWorld;
            var launchLocal = WorldToSlotLocal(tower, launchWorld);
            driver.Admit(HandoffState.AtRest(launchLocal));
            FlightSortingChannel.ArmForSlotConvergence(card, driver);
            driver.ConvergeTo(Vector3.zero, sourceTime);
        }

        /// <summary>
        /// 换格/旋转：在保持 L0 不动的前提下，把 L2 收敛到目标格世界点对应的 local。
        /// </summary>
        public static void ConvergeVisualToWorld(
            ManagedCard card,
            Vector3 targetWorld,
            float sourceTime)
        {
            if (card?.Transform == null || !TryGetTower(card, out var tower) || !TryGetDriver(card, out var driver))
            {
                return;
            }

            CardDeckTween.KillMotion(card.Transform, "SlotFrame.Converge", card.Uid);
            var targetLocal = WorldToSlotLocal(tower, targetWorld);
            FlightSortingChannel.ArmForSlotConvergence(card, driver);
            driver.ConvergeTo(targetLocal, sourceTime);
        }

        public static void RedirectVisualToWorld(
            ManagedCard card,
            Vector3 targetWorld,
            float sourceTime)
        {
            ConvergeVisualToWorld(card, targetWorld, sourceTime);
        }

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
            ManagedCard card,
            Vector3 targetWorld,
            float sourceTime,
            CancellationToken cancellationToken,
            bool snapHomeOnComplete = true)
        {
            if (card?.Transform == null || !TryGetDriver(card, out var driver))
            {
                return;
            }

            ConvergeVisualToWorld(card, targetWorld, sourceTime);
            await AwaitDriverAsync(driver, cancellationToken);

            if (snapHomeOnComplete && !cancellationToken.IsCancellationRequested)
            {
                SnapHome(card, targetWorld, "SlotFrame.HopComplete", card.Uid);
            }
        }
    }
}

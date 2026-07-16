using System;
using System.Collections.Generic;
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
        private static readonly Dictionary<int, int> sMotionByDriver = new();

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

        /// <summary>
        /// 无塔/无 driver 时补齐 L2 基础设施；成功则可继续走曲线。仍失败才返回 false。
        /// </summary>
        public static bool TryEnsureInfrastructure(
            ManagedCard card,
            out CardTransformTower tower,
            out LayerConvergenceDriver driver,
            string site = null)
        {
            tower = null;
            driver = null;
            if (card?.Transform == null)
            {
                return false;
            }

            var root = card.Transform;
            var hadTower = root.GetComponent<CardTransformTower>() != null;
            var hadDriver = LayerConvergenceDriver.TryGet(root, TowerLayer.SlotFrame, out _);

            tower = root.GetComponent<CardTransformTower>();
            if (tower == null)
            {
                tower = root.gameObject.AddComponent<CardTransformTower>();
            }

            tower.EnsureTower();
            driver = LayerConvergenceDriver.Ensure(root, TowerLayer.SlotFrame);
            LayerConvergenceDriver.Ensure(root, TowerLayer.EffectFrame);

            if (tower.SlotFrame == null || driver == null)
            {
                var reason = tower.SlotFrame == null ? "noTower" : "noDriver";
                CardPresentationProbe.Anomaly(
                    card.Uid,
                    "SilentFail",
                    reason,
                    site ?? "SlotFrame.Ensure",
                    layer: "L2",
                    verdict: "abort");
                tower = null;
                driver = null;
                return false;
            }

            if (!hadTower || !hadDriver)
            {
                var detail = !hadTower && !hadDriver
                    ? "noTower+noDriver"
                    : (!hadTower ? "noTower" : "noDriver");
                CardPresentationProbe.Anomaly(
                    card.Uid,
                    "EnsureRepaired",
                    detail,
                    site ?? "SlotFrame.Ensure",
                    layer: "L2",
                    verdict: "repaired");
            }

            return true;
        }

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
            if (!TryGetTower(card, out var tower)
                && !TryEnsureInfrastructure(card, out tower, out _, "SlotFrame.WorldToLocal"))
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

            var probeUid = uid > 0 ? uid : card.Uid;
            if (!TryGetTower(card, out var tower) || !TryGetDriver(card, out var driver))
            {
                if (!TryEnsureInfrastructure(card, out tower, out driver, reason ?? "SlotFrame.SnapHome"))
                {
                    CardPresentationProbe.Anomaly(
                        probeUid,
                        "forceSnap",
                        "noTower/noDriver",
                        reason ?? "SlotFrame.SnapHome",
                        layer: "L2",
                        verdict: "hardSet");
                    CardDeckTween.KillMotion(card.Transform, reason ?? "SlotFrame.SnapHome", probeUid);
                    card.Transform.position = anchorWorld;
                    return;
                }
            }

            CardDeckTween.KillMotion(card.Transform, reason ?? "SlotFrame.SnapHome", probeUid);
            EndMotionDiag(driver, card, "snap");
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
            if (card?.Transform == null)
            {
                return;
            }

            if (!TryGetTower(card, out var tower) || !TryGetDriver(card, out var driver))
            {
                if (!TryEnsureInfrastructure(card, out tower, out driver, "SlotFrame.BeginDeal"))
                {
                    return;
                }
            }

            CardDeckTween.KillMotion(card.Transform, "SlotFrame.BeginDeal", card.Uid);
            tower.CardRoot.position = slotAnchorWorld;
            var launchLocal = WorldToSlotLocal(tower, launchWorld);
            driver.Admit(HandoffState.AtRest(launchLocal));
            FlightSortingChannel.ArmForSlotConvergence(card, driver);
            StartConvergenceDiag(
                card,
                driver,
                launchWorld,
                slotAnchorWorld,
                Vector3.zero,
                sourceTime,
                "SlotFrame.BeginDeal",
                CommitmentKind.Async.ToString());
        }

        /// <summary>
        /// 换格/旋转：在保持 L0 不动的前提下，把 L2 收敛到目标格世界点对应的 local。
        /// </summary>
        public static void ConvergeVisualToWorld(
            ManagedCard card,
            Vector3 targetWorld,
            float sourceTime,
            CommitmentKind commitment = CommitmentKind.Sync)
        {
            if (card?.Transform == null)
            {
                return;
            }

            if (!TryGetTower(card, out var tower) || !TryGetDriver(card, out var driver))
            {
                if (!TryEnsureInfrastructure(card, out tower, out driver, "SlotFrame.Converge"))
                {
                    return;
                }
            }

            CardDeckTween.KillMotion(card.Transform, "SlotFrame.Converge", card.Uid);
            var fromWorld = card.Transform.position;
            var targetLocal = WorldToSlotLocal(tower, targetWorld);
            FlightSortingChannel.ArmForSlotConvergence(card, driver);
            StartConvergenceDiag(
                card,
                driver,
                fromWorld,
                targetWorld,
                targetLocal,
                sourceTime,
                "SlotFrame.Converge",
                commitment.ToString());
        }

        public static void RedirectVisualToWorld(
            ManagedCard card,
            Vector3 targetWorld,
            float sourceTime,
            CommitmentKind commitment = CommitmentKind.Sync)
        {
            ConvergeVisualToWorld(card, targetWorld, sourceTime, commitment);
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
            bool snapHomeOnComplete = true,
            CommitmentKind commitment = CommitmentKind.Sync)
        {
            if (card?.Transform == null)
            {
                return;
            }

            if (!TryGetDriver(card, out var driver)
                && !TryEnsureInfrastructure(card, out _, out driver, "SlotFrame.ConvergeAsync"))
            {
                return;
            }

            ConvergeVisualToWorld(card, targetWorld, sourceTime, commitment);
            try
            {
                await AwaitDriverAsync(driver, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                EndMotionDiag(driver, card, "cancel");
                throw;
            }

            if (snapHomeOnComplete && !cancellationToken.IsCancellationRequested)
            {
                SnapHome(card, targetWorld, "SlotFrame.HopComplete", card.Uid);
            }
        }

        private static void StartConvergenceDiag(
            ManagedCard card,
            LayerConvergenceDriver driver,
            Vector3 fromWorld,
            Vector3 toWorld,
            Vector3 targetLocal,
            float sourceTime,
            string site,
            string commitment)
        {
            if (driver == null || card == null)
            {
                return;
            }

            var driverId = driver.GetInstanceID();
            if (sMotionByDriver.TryGetValue(driverId, out var prevMotionId))
            {
                CardPresentationProbe.MotionEnd(
                    card.Uid,
                    prevMotionId,
                    card.Transform != null ? card.Transform.position : fromWorld,
                    "redirected",
                    site);
                sMotionByDriver.Remove(driverId);
            }

            var motionId = CardPresentationProbe.NextMotionId();
            sMotionByDriver[driverId] = motionId;
            CardPresentationProbe.MotionBegin(
                card.Uid,
                motionId,
                fromWorld,
                toWorld,
                site,
                reason: commitment,
                expectMs: sourceTime,
                choreoSeqId: ChoreoTraceSink.SafeCurrentSeqId());

            void OnCompleted()
            {
                driver.Completed -= OnCompleted;
                if (!sMotionByDriver.TryGetValue(driverId, out var mid) || mid != motionId)
                {
                    return;
                }

                sMotionByDriver.Remove(driverId);
                var at = card.Transform != null ? card.Transform.position : toWorld;
                CardPresentationProbe.MotionEnd(
                    card.Uid,
                    motionId,
                    at,
                    "complete",
                    site,
                    choreoSeqId: ChoreoTraceSink.SafeCurrentSeqId());
            }

            driver.Completed += OnCompleted;
            driver.ConvergeTo(targetLocal, sourceTime);
        }

        private static void EndMotionDiag(LayerConvergenceDriver driver, ManagedCard card, string endHow)
        {
            if (driver == null)
            {
                return;
            }

            var driverId = driver.GetInstanceID();
            if (!sMotionByDriver.TryGetValue(driverId, out var motionId))
            {
                return;
            }

            sMotionByDriver.Remove(driverId);
            var uid = card?.Uid ?? 0;
            var at = card?.Transform != null ? card.Transform.position : Vector3.zero;
            CardPresentationProbe.MotionEnd(
                uid,
                motionId,
                at,
                endHow,
                "SlotFrame.Converge",
                choreoSeqId: ChoreoTraceSink.SafeCurrentSeqId());
        }
    }
}

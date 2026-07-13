using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 发牌补位替身：瞬间落锚点占表现目标，逐帧跟随大盘 hop/旋转曲线；真牌追踪替身世界坐标。
    /// </summary>
    internal sealed class GroundSlotDealDecoyTracker
    {
        private sealed class Entry
        {
            public int Uid;
            public int TrackedSlot;
            public Transform Transform;
            public int ActiveHopCount;
            public CancellationTokenSource HopCts;
        }

        private readonly GroundFieldManagerSingleton _field;
        private readonly CancellationToken _destroyToken;
        private readonly Dictionary<int, Entry> _byUid = new();
        private Transform _root;

        public GroundSlotDealDecoyTracker(
            GroundFieldManagerSingleton field,
            CancellationToken destroyToken)
        {
            _field = field;
            _destroyToken = destroyToken;
        }

        public bool HasDecoy(int uid) => uid > 0 && _byUid.ContainsKey(uid);

        public bool IsHopping(int uid) =>
            uid > 0 && _byUid.TryGetValue(uid, out var entry) && entry.ActiveHopCount > 0;

        public bool TrySpawn(int uid, int slot)
        {
            if (uid <= 0 || _field == null || !_field.TryGetExploreAnchorPosition(slot, out var position))
            {
                return false;
            }

            if (_byUid.ContainsKey(uid))
            {
                Release(uid);
            }

            var go = new GameObject($"DealDecoy_u{uid}_s{slot}");
            go.transform.SetParent(EnsureRoot(), worldPositionStays: true);
            go.transform.position = position;

            _byUid[uid] = new Entry
            {
                Uid = uid,
                TrackedSlot = slot,
                Transform = go.transform,
            };

            ChoreoTraceSink.SafeExploreTrace(
                uid,
                "decoySpawn",
                slot,
                slot,
                "x", position.x.ToString("F3"),
                "y", position.y.ToString("F3"));
            return true;
        }

        public bool TryGetPosition(int uid, out Vector3 position)
        {
            position = default;
            if (!_byUid.TryGetValue(uid, out var entry) || entry.Transform == null)
            {
                return false;
            }

            position = entry.Transform.position;
            return true;
        }

        public void ShiftRingSlots(bool clockwise)
        {
            foreach (var entry in _byUid.Values)
            {
                if (!GroundSlotTopology.IsOuterRing(entry.TrackedSlot))
                {
                    continue;
                }

                var fromSlot = entry.TrackedSlot;
                var toSlot = GroundSlotTopology.GetClockwiseRingTargetSlot(fromSlot, clockwise);
                entry.TrackedSlot = toSlot;
                StartHopAsync(entry, fromSlot, toSlot).Forget();
            }
        }

        public void StartHopForUid(int uid, int fromSlot, int toSlot)
        {
            if (!_byUid.TryGetValue(uid, out var entry))
            {
                return;
            }

            entry.TrackedSlot = toSlot;
            StartHopAsync(entry, fromSlot, toSlot).Forget();
        }

        public void StartLinearMoveForUid(int uid, int toSlot, float duration)
        {
            if (!_byUid.TryGetValue(uid, out var entry)
                || entry.Transform == null
                || !_field.TryGetExploreAnchorPosition(toSlot, out var target))
            {
                return;
            }

            entry.TrackedSlot = toSlot;
            entry.HopCts?.Cancel();
            entry.HopCts?.Dispose();
            entry.HopCts = CancellationTokenSource.CreateLinkedTokenSource(_destroyToken);
            RunLinearMoveAsync(entry, target, duration, entry.HopCts.Token).Forget();
        }

        public void Release(int uid)
        {
            if (!_byUid.TryGetValue(uid, out var entry))
            {
                return;
            }

            entry.HopCts?.Cancel();
            entry.HopCts?.Dispose();
            entry.HopCts = null;

            if (entry.Transform != null)
            {
                UnityEngine.Object.Destroy(entry.Transform.gameObject);
            }

            _byUid.Remove(uid);
            ChoreoTraceSink.SafeExploreTrace(uid, "decoyRelease", entry.TrackedSlot, entry.TrackedSlot);
        }

        public void ReleaseAll()
        {
            var uids = new List<int>(_byUid.Keys);
            for (var i = 0; i < uids.Count; i++)
            {
                Release(uids[i]);
            }
        }

        private Transform EnsureRoot()
        {
            if (_root != null)
            {
                return _root;
            }

            var go = new GameObject("DealFlightDecoys");
            go.transform.SetParent(_field.transform, worldPositionStays: true);
            _root = go.transform;
            return _root;
        }

        private async UniTaskVoid StartHopAsync(Entry entry, int fromSlot, int toSlot)
        {
            entry.HopCts?.Cancel();
            entry.HopCts?.Dispose();
            entry.HopCts = CancellationTokenSource.CreateLinkedTokenSource(_destroyToken);
            var token = entry.HopCts.Token;

            if (entry.Transform == null)
            {
                return;
            }

            if (!_field.TryGetExploreAnchorPosition(toSlot, out var end))
            {
                return;
            }

            if (!_field.TryGetExploreAnchorPosition(fromSlot, out _))
            {
                entry.Transform.position = end;
                return;
            }

            var start = entry.Transform.position;
            var layout = _field.LayoutSettings;
            var mid = DealFlightMath.ComputeHopMidpoint(
                start,
                end,
                layout != null ? layout.hopArcHeight : 0f);

            entry.ActiveHopCount++;
            ChoreoTraceSink.SafeExploreTrace(
                entry.Uid,
                "decoyHop",
                fromSlot,
                toSlot);

            try
            {
                await CardDeckTween.MoveHopToWorldAsync(
                    entry.Transform,
                    start,
                    mid,
                    end,
                    layout != null ? layout.moveDuration : 0.28f,
                    layout != null ? layout.hopPeakScaleIntensity : 0f,
                    layout != null ? layout.hopLandScaleIntensity : 0f,
                    token);
            }
            catch (OperationCanceledException)
            {
                // 被更新的 hop 取代。
            }
            finally
            {
                entry.ActiveHopCount = Mathf.Max(0, entry.ActiveHopCount - 1);
            }
        }

        private async UniTaskVoid RunLinearMoveAsync(
            Entry entry,
            Vector3 target,
            float duration,
            CancellationToken token)
        {
            entry.ActiveHopCount++;
            try
            {
                var tween = CardDeckTween.MoveToWorld(
                    entry.Transform,
                    target,
                    duration,
                    reason: "decoySwap");
                if (tween == null)
                {
                    return;
                }

                var tcs = new UniTaskCompletionSource();
                tween.OnComplete(() => tcs.TrySetResult());
                tween.OnKill(() => tcs.TrySetResult());
                if (token.CanBeCanceled)
                {
                    token.Register(() =>
                    {
                        if (tween.IsActive())
                        {
                            tween.Kill(complete: false);
                        }

                        tcs.TrySetCanceled(token);
                    });
                }

                await tcs.Task;
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            finally
            {
                entry.ActiveHopCount = Mathf.Max(0, entry.ActiveHopCount - 1);
            }
        }
    }
}

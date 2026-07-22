using System.Threading;
using Cysharp.Threading.Tasks;

namespace NineGrid.Cards
{
    /// <summary>
    /// 单张飞牌的句柄，用于 Drain 批量聚合等待。
    /// </summary>
    public sealed class DealFlightHandle
    {
        private readonly UniTaskCompletionSource<bool> _settleTcs = new();

        internal DealFlightHandle(int uid, int trackedSlot, DealFlightKind kind)
        {
            Uid = uid;
            TrackedSlot = trackedSlot;
            Kind = kind;
        }

        public int Uid { get; }
        public int TrackedSlot { get; internal set; }
        public DealFlightKind Kind { get; }
        public bool IsSettled { get; private set; }

        public UniTask WaitSettleAsync(CancellationToken cancellationToken)
        {
            return _settleTcs.Task.AttachExternalCancellation(cancellationToken);
        }

        internal void Complete(bool ok)
        {
            if (IsSettled)
            {
                return;
            }

            IsSettled = true;
            _settleTcs.TrySetResult(ok);
        }
    }
}

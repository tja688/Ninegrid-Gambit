using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Systems;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// Avatar 连跳执行器：SetDestination → 逐跳 Core MoveAvatar + hop 动画；
    /// Hopping 中改目标等落地后重规划。
    /// </summary>
    public sealed class AvatarWalkRunner
    {
        private readonly IArchitecture mArch;
        private int mDesiredSlot;
        private bool mHasDesire;
        private bool mHopping;
        private bool mLoopRunning;
        private CancellationTokenSource mLoopCts;

        public AvatarWalkRunner(IArchitecture architecture)
        {
            mArch = architecture ?? throw new ArgumentNullException(nameof(architecture));
        }

        public bool IsHopping => mHopping;

        public bool HasDesire => mHasDesire;

        public int DesiredSlot => mDesiredSlot;

        public void SetEnabled(bool enabled)
        {
            if (!enabled)
            {
                Cancel();
            }
        }

        public void Cancel()
        {
            mHasDesire = false;
            mDesiredSlot = 0;
            mLoopCts?.Cancel();
            mLoopCts?.Dispose();
            mLoopCts = null;
            mLoopRunning = false;
            mHopping = false;
        }

        /// <summary>设定或覆盖目标格（1..9）。同格则清除欲望。</summary>
        public void SetDestination(int groundSlot)
        {
            if (groundSlot < SlotId.MinBoardIndex || groundSlot > SlotId.MaxBoardIndex)
            {
                return;
            }

            var board = mArch.GetModel<BoardModel>();
            if (board == null || board.AvatarSlot.Value.Index == groundSlot)
            {
                mHasDesire = false;
                mDesiredSlot = 0;
                return;
            }

            mDesiredSlot = groundSlot;
            mHasDesire = true;
            EnsureLoop();
        }

        private void EnsureLoop()
        {
            if (mLoopRunning)
            {
                return;
            }

            mLoopCts?.Cancel();
            mLoopCts?.Dispose();
            mLoopCts = new CancellationTokenSource();
            mLoopRunning = true;
            RunLoopAsync(mLoopCts.Token).Forget();
        }

        private async UniTaskVoid RunLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && mHasDesire)
                {
                    var board = mArch.GetModel<BoardModel>();
                    if (board == null)
                    {
                        break;
                    }

                    var from = board.AvatarSlot.Value;
                    if (!from.IsBoardSlot)
                    {
                        break;
                    }

                    if (!mHasDesire || from.Index == mDesiredSlot)
                    {
                        mHasDesire = false;
                        break;
                    }

                    var to = SlotId.Board(mDesiredSlot);
                    if (!AvatarWalkPathfinder.TryGetNextStep(board, from, to, out var next)
                        || !next.IsBoardSlot)
                    {
                        Debug.LogWarning(
                            $"[AvatarWalk] 无路径 from={from.Index} to={mDesiredSlot}，取消欲望。");
                        mHasDesire = false;
                        break;
                    }

                    var move = mArch.SendCommand(new MoveAvatarCommand(next));
                    if (move == null || !move.Accepted)
                    {
                        Debug.LogWarning(
                            $"[AvatarWalk] MoveAvatar 被拒 from={from.Index} next={next.Index}"
                            + $" reason={move?.Reason}");
                        mHasDesire = false;
                        break;
                    }

                    mHopping = true;
                    try
                    {
                        var geometry = mArch.GetSystem<IGroundFieldGeometrySystem>();
                        if (geometry != null)
                        {
                            await geometry.HopAvatarToSlotAsync(from.Index, next.Index, ct);
                        }
                    }
                    finally
                    {
                        mHopping = false;
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                mHopping = false;
                mLoopRunning = false;
            }
        }
    }

    /// <summary>QF System 壳：持有 <see cref="AvatarWalkRunner"/> 与跳格门禁。</summary>
    public interface IAvatarWalkSystem : ISystem
    {
        bool IsEnabled { get; }

        bool IsHopping { get; }

        void SetEnabled(bool enabled);

        void SetDestination(int groundSlot);

        void Cancel();
    }

    public sealed class AvatarWalkSystem : AbstractSystem, IAvatarWalkSystem
    {
        private AvatarWalkRunner mRunner;
        private bool mEnabled;

        public bool IsEnabled => mEnabled;

        public bool IsHopping => mRunner != null && mRunner.IsHopping;

        protected override void OnInit()
        {
            mRunner = new AvatarWalkRunner(NineGridArchitecture.Interface ?? NineGridArchitecture.Current);
        }

        public void SetEnabled(bool enabled)
        {
            mEnabled = enabled;
            mRunner?.SetEnabled(enabled);
            if (!enabled)
            {
                mRunner?.Cancel();
            }

            // 跳格开/关切换 Hit 策略：任意空格 ↔ 中心正交邻格。
            var geometry = this.GetSystem<IGroundFieldGeometrySystem>()
                           ?? NineGridArchitecture.Interface?.GetSystem<IGroundFieldGeometrySystem>();
            geometry?.RefreshSlotHitColliders();
        }

        public void SetDestination(int groundSlot)
        {
            if (!mEnabled || mRunner == null)
            {
                return;
            }

            mRunner.SetDestination(groundSlot);
        }

        public void Cancel()
        {
            mRunner?.Cancel();
        }

        public static IAvatarWalkSystem EnsureRegistered(IArchitecture architecture = null)
        {
            var arch = architecture ?? NineGridArchitecture.Interface;
            if (arch == null)
            {
                return null;
            }

            var existing = arch.GetSystem<IAvatarWalkSystem>();
            if (existing != null)
            {
                return existing;
            }

            var created = new AvatarWalkSystem();
            arch.RegisterSystem<IAvatarWalkSystem>(created);
            return created;
        }
    }
}

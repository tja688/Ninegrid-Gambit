using System;
using NineGrid.Flow.Presentation;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Commands
{
    /// <summary>
    /// 扫描 EventLog 金币变更并广播单向表现事件（不改 PlayerModel）。
    /// </summary>
    public sealed class PresentGoldGainsFromEventLogCommand : AbstractCommand<int>
    {
        private readonly int mStartIndex;
        private readonly Vector3? mOriginWorld;
        private readonly string mSkipReason;
        private readonly Func<int, Vector3?> mResolveCardWorldPosition;
        private readonly GoldGainPresentationScheduler mScheduler;

        public PresentGoldGainsFromEventLogCommand(
            int startIndex,
            Vector3? originWorld = null,
            string skipReason = null,
            Func<int, Vector3?> resolveCardWorldPosition = null,
            GoldGainPresentationScheduler scheduler = null)
        {
            mStartIndex = startIndex;
            mOriginWorld = originWorld;
            mSkipReason = skipReason;
            mResolveCardWorldPosition = resolveCardWorldPosition;
            mScheduler = scheduler ?? new GoldGainPresentationScheduler();
        }

        protected override int OnExecute()
        {
            return mScheduler.PresentFromEventLog(
                this,
                mStartIndex,
                mOriginWorld,
                mSkipReason,
                mResolveCardWorldPosition);
        }
    }
}

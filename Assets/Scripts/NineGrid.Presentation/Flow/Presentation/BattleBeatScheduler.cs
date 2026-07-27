using System.Collections.Generic;
using NineGrid.Core;
using UnityEngine;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 战斗锚点排期器：持有当批未消费结算指令，按报点归属交给卡面数值处理器。
    /// Settled 后仍有卡面归属未消费：只报不改（无强制对账）。
    /// Avatar 数值指令在同一拍旁路刷新 PlayerInfo HUD（仍 SyncFromCore；非卡面第二条路径）。
    /// </summary>
    public sealed class BattleBeatScheduler
    {
        private readonly CardFaceStatHandler mHandler;
        private readonly List<PresentationInstruction> mPending = new List<PresentationInstruction>(16);
        private int mActiveBatchId;

        public BattleBeatScheduler(CardFaceStatHandler handler)
        {
            mHandler = handler ?? new CardFaceStatHandler();
        }

        public void OnBatchOpened(PresentationBatch batch)
        {
            mPending.Clear();
            mActiveBatchId = batch != null ? batch.BatchId : 0;
            if (batch?.Instructions == null)
            {
                return;
            }

            for (var i = 0; i < batch.Instructions.Count; i++)
            {
                var instruction = batch.Instructions[i];
                if (instruction?.MapEntry == null)
                {
                    continue;
                }

                if (instruction.MapEntry.Beat == PresentationBeat.None)
                {
                    continue;
                }

                mPending.Add(instruction);
            }
        }

        public void ReportBeat(PresentationBeat beat)
        {
            if (beat == PresentationBeat.None)
            {
                return;
            }

            var avatarHudDirty = false;
            for (var i = 0; i < mPending.Count;)
            {
                var instruction = mPending[i];
                if (instruction.MapEntry.Beat != beat)
                {
                    i++;
                    continue;
                }

                mHandler.Apply(instruction);
                if (AffectsAvatarHud(instruction))
                {
                    avatarHudDirty = true;
                }

                mPending.RemoveAt(i);
            }

            if (avatarHudDirty)
            {
                // 与旧命中帧 SyncManagedCardPresentation(avatar) 同拍；HUD 仍直读内核（ADR-0005 后续迁移）。
                PlayerInfoHudPresenter.TryGetInstance()?.SyncFromCore(animate: true);
            }

            if (beat == PresentationBeat.Settled)
            {
                DiagnoseUnconsumed();
            }
        }

        private static bool AffectsAvatarHud(PresentationInstruction instruction)
        {
            var gameEvent = instruction?.Event;
            if (gameEvent == null)
            {
                return false;
            }

            var uid = gameEvent.CardUid > 0 ? gameEvent.CardUid : gameEvent.TargetUid;
            if (uid <= 0)
            {
                return false;
            }

            var arch = NineGridArchitecture.Current;
            if (arch == null)
            {
                return false;
            }

            var avatarUid = arch.GetModel<BoardModel>().AvatarUid != null
                ? arch.GetModel<BoardModel>().AvatarUid.Value
                : 0;
            return avatarUid > 0 && uid == avatarUid;
        }

        private void DiagnoseUnconsumed()
        {
            if (mPending.Count == 0)
            {
                return;
            }

            for (var i = 0; i < mPending.Count; i++)
            {
                var instruction = mPending[i];
                var type = instruction.Event != null ? instruction.Event.Type.ToString() : "?";
                var message =
                    "[BattleBeatScheduler] Unconsumed card-face instruction after Settled"
                    + " batchId=" + mActiveBatchId
                    + " type=" + type
                    + " beat=" + instruction.MapEntry.Beat
                    + " seq=" + instruction.Sequence;
                Debug.LogError(message);
                Debug.Assert(false, message);
            }
        }
    }
}

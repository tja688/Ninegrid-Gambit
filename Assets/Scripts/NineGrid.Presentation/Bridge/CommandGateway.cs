using System;
using System.Collections;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Presentation.Orchestration;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Bridge
{
    /// <summary>
    /// 表现层 Command 唯一出口：Dispatcher → BatchPlayer → PresentationFinished。
    /// </summary>
    public sealed class CommandGateway
    {
        private readonly MonoBehaviour mCoroutineHost;
        private readonly CoreCommandDispatcher mDispatcher;
        private readonly PresentationBatchPlayer mBatchPlayer;
        private readonly IPresentationSyncSystem mSync;
        private readonly Action<PresentationBatch> mOnPlaybackComplete;
        private Coroutine mActivePlayback;

        public CommandGateway(
            MonoBehaviour coroutineHost,
            IArchitecture architecture,
            CoreCommandDispatcher dispatcher,
            PresentationBatchPlayer batchPlayer,
            Action<PresentationBatch> onPlaybackComplete = null)
        {
            mCoroutineHost = coroutineHost;
            mDispatcher = dispatcher;
            mBatchPlayer = batchPlayer;
            mSync = architecture.GetSystem<IPresentationSyncSystem>();
            mOnPlaybackComplete = onPlaybackComplete;
        }

        public IInputLockGate InputLockGate => mBatchPlayer?.InputLockGate;

        public bool IsInputLocked => mSync != null && mSync.IsInputLocked;

        public int ActiveBatchId => mSync != null ? mSync.ActiveBatchId : 0;

        public CoreCommandDispatchResult Send(ICommand<CoreCommandResult> command)
        {
            var result = mDispatcher.Send(command);
            if (result.BatchOpened && result.Batch != null)
            {
                StartPlayback(result.Batch);
            }

            return result;
        }

        private void StartPlayback(PresentationBatch batch)
        {
            if (mCoroutineHost == null || mBatchPlayer == null || batch == null)
            {
                return;
            }

            if (mActivePlayback != null)
            {
                mCoroutineHost.StopCoroutine(mActivePlayback);
            }

            mActivePlayback = mCoroutineHost.StartCoroutine(PlayBatchAndFinish(batch));
        }

        private IEnumerator PlayBatchAndFinish(PresentationBatch batch)
        {
            yield return mBatchPlayer.PlayCoroutine(batch);
            mOnPlaybackComplete?.Invoke(batch);
            mDispatcher.Send(new PresentationFinishedCommand(batch.BatchId));
            mActivePlayback = null;
        }
    }
}

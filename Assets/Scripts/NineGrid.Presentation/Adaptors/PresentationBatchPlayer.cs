using System.Collections;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Presentation.Registry;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Adaptors
{
    /// <summary>
    /// 中心批次播放器：订阅 <see cref="IPresentationSyncSystem"/> 活跃批次，
    /// 路由至域适配器，批末对齐快照并发送 <see cref="PresentationFinishedCommand"/>。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PresentationBatchPlayer : MonoBehaviour, IController
    {
        [SerializeField] private TableNineViewRegistry viewRegistry;
        [SerializeField] private TableNineBoardAdaptor boardAdaptor;
        [SerializeField] private TableNineCardDeckAdaptor deckAdaptor;
        [SerializeField] private TableNineItemAdaptor itemAdaptor;
        [SerializeField] private TableNineEffectAdaptor effectAdaptor;

        private Coroutine mPlaybackCoroutine;
        private int mLastPlayedBatchId;

        public IArchitecture GetArchitecture()
        {
            return NineGridArchitecture.Interface;
        }

        public void PlayDispatchResult(CoreCommandDispatchResult result)
        {
            if (result == null || !result.BatchOpened || result.Batch == null)
            {
                return;
            }

            EnsureReferences();
            if (mPlaybackCoroutine != null)
            {
                StopCoroutine(mPlaybackCoroutine);
            }

            mPlaybackCoroutine = StartCoroutine(PlayBatchCoroutine(result.Batch));
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            var sync = this.GetSystem<IPresentationSyncSystem>();
            if (!sync.IsInputLocked || sync.ActiveBatch == null)
            {
                return;
            }

            if (sync.ActiveBatchId == mLastPlayedBatchId || mPlaybackCoroutine != null)
            {
                return;
            }

            EnsureReferences();
            mPlaybackCoroutine = StartCoroutine(PlayBatchCoroutine(sync.ActiveBatch));
        }

        private IEnumerator PlayBatchCoroutine(PresentationBatch batch)
        {
            if (batch == null)
            {
                yield break;
            }

            mLastPlayedBatchId = batch.BatchId;
            BoardBatchPlan boardPlan = BoardBatchPlan.Build(batch.Instructions);
            DeckBatchPlan deckPlan = DeckBatchPlan.Build(batch.Instructions);

            for (var i = 0; i < batch.Instructions.Count; i++)
            {
                PresentationInstruction instruction = batch.Instructions[i];
                if (deckAdaptor != null && deckAdaptor.CanHandle(instruction))
                {
                    yield return deckAdaptor.PlayInstruction(
                        instruction,
                        batch.Instructions,
                        deckPlan,
                        i,
                        batch.Snapshot);
                }
                else if (itemAdaptor != null && itemAdaptor.CanHandle(instruction))
                {
                    yield return itemAdaptor.PlayInstruction(
                        instruction,
                        batch.Instructions,
                        batch.Snapshot);
                }
                else if (effectAdaptor != null && effectAdaptor.CanHandle(instruction))
                {
                    yield return effectAdaptor.PlayInstruction(
                        instruction,
                        batch.Instructions,
                        batch.Snapshot);
                }
                else if (boardAdaptor != null && boardAdaptor.CanHandle(instruction))
                {
                    yield return boardAdaptor.PlayInstruction(
                        instruction,
                        batch.Instructions,
                        boardPlan,
                        batch.Snapshot);
                }
            }

            if (viewRegistry != null && batch.Snapshot != null)
            {
                if (deckAdaptor != null)
                {
                    deckAdaptor.AlignDeckFromSnapshot(batch.Snapshot);
                }

                viewRegistry.AlignBoardFromSnapshot(batch.Snapshot);
            }

            if (batch.RequiresAcknowledgement)
            {
                this.SendCommand(new PresentationFinishedCommand(batch.BatchId));
            }

            mPlaybackCoroutine = null;
        }

        private void EnsureReferences()
        {
            if (viewRegistry == null)
            {
                viewRegistry = GetComponent<TableNineViewRegistry>();
            }

            if (boardAdaptor == null)
            {
                boardAdaptor = GetComponent<TableNineBoardAdaptor>();
            }

            if (deckAdaptor == null)
            {
                deckAdaptor = GetComponent<TableNineCardDeckAdaptor>();
            }

            if (itemAdaptor == null)
            {
                itemAdaptor = GetComponent<TableNineItemAdaptor>();
            }

            if (effectAdaptor == null)
            {
                effectAdaptor = GetComponent<TableNineEffectAdaptor>();
            }

            if (boardAdaptor != null && viewRegistry != null && boardAdaptor.ViewRegistry == null)
            {
                // Serialized wiring is preferred; runtime fallback only for empty scenes.
            }
        }

        private void OnDisable()
        {
            if (mPlaybackCoroutine != null)
            {
                StopCoroutine(mPlaybackCoroutine);
                mPlaybackCoroutine = null;
            }
        }
    }
}

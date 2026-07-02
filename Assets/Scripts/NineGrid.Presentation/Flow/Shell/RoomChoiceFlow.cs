using System;
using System.Collections.Generic;
using NineGrid.Presentation.Contracts;
using NineGrid.Presentation.Shell;
using UnityEngine;

namespace NineGrid.Presentation.Flow.Shell
{
    /// <summary>
    /// 房间二选一入离场：委托 <see cref="SelectionPresentation"/>，由 Batch 或 Shell 共用。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomChoiceFlow : MonoBehaviour, IDirectedFlow
    {
        [SerializeField] private SelectionPresentation selectionPresentation;

        private bool mEntrancePlayed;

        public bool IsPlaying => selectionPresentation != null && selectionPresentation.IsPlaying;
        public float ExpectedDuration => selectionPresentation != null ? selectionPresentation.ExpectedDuration : 0f;
        public bool EntrancePlayed => mEntrancePlayed;

        public void Configure(SelectionPresentation presentation)
        {
            selectionPresentation = presentation;
        }

        public void PlayEntrance(IReadOnlyList<SelectionPresentation.RoomMove> moves, Action onComplete = null)
        {
            if (selectionPresentation == null || moves == null || moves.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            mEntrancePlayed = true;
            selectionPresentation.PlayRoomEntrance(moves, onComplete);
        }

        public void PlayExit(
            Transform selectedCard,
            Transform selectedEnd,
            Transform unselectedCard,
            Action onComplete = null)
        {
            if (selectionPresentation == null)
            {
                onComplete?.Invoke();
                return;
            }

            selectionPresentation.PlayRoomExit(selectedCard, selectedEnd, unselectedCard, onComplete);
        }

        public void StopAndRestore()
        {
            selectionPresentation?.StopAllPlayback();
            mEntrancePlayed = false;
        }

        public void MarkEntrancePlayed()
        {
            mEntrancePlayed = true;
        }
    }
}

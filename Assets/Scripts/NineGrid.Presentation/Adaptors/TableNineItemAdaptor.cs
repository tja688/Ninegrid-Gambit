using System;
using System.Collections;
using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Presentation.Diagnostics;
using NineGrid.Presentation.FSM;
using NineGrid.Presentation.Performance;
using NineGrid.Presentation.Registry;
using UnityEngine;

namespace NineGrid.Presentation.Adaptors
{
    /// <summary>
    /// 道具适配器：认领 PickItem / UseItem，调度拾取获取与道具使用表演黑盒。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TableNineItemAdaptor : MonoBehaviour
    {
        private static readonly HashSet<PresentationInstructionKind> HandledKinds = new()
        {
            PresentationInstructionKind.PickItem,
            PresentationInstructionKind.UseItem,
        };

        [Header("Registry")]
        [SerializeField] private TableNineViewRegistry viewRegistry;

        [Header("Performances")]
        [SerializeField] private CardAcquisitionPerformance cardAcquisitionPerformance;
        [SerializeField] private ItemUsePerformance itemUsePerformance;

        private readonly List<Transform> mHandActorBuffer = new();
        private readonly List<HandCardLayoutTarget> mHandLayoutBuffer = new();

        public TableNineViewRegistry ViewRegistry => viewRegistry;

        public bool CanHandle(PresentationInstruction instruction)
        {
            return instruction != null && HandledKinds.Contains(instruction.Kind);
        }

        public IEnumerator PlayInstruction(
            PresentationInstruction instruction,
            IReadOnlyList<PresentationInstruction> batchInstructions,
            CoreViewSnapshot snapshot)
        {
            if (!CanHandle(instruction))
            {
                yield break;
            }

            EnsureReferences();
            CoreGameEvent evt = instruction.Event;
            if (evt == null)
            {
                yield break;
            }

            switch (instruction.Kind)
            {
                case PresentationInstructionKind.PickItem:
                    yield return PlayPickItem(evt, snapshot);
                    break;
                case PresentationInstructionKind.UseItem:
                    yield return PlayUseItem(evt);
                    break;
            }
        }

        private IEnumerator PlayPickItem(CoreGameEvent evt, CoreViewSnapshot snapshot)
        {
            Transform acquiredCard;
            if (!viewRegistry.TryGetActor(evt.CardUid, out acquiredCard) || acquiredCard == null)
            {
                yield break;
            }

            HandCardLayoutSolver layoutSolver = viewRegistry.HandLayoutSolver;
            layoutSolver.ResolveFromReferenceAnchors();

            viewRegistry.CollectHandActors(mHandActorBuffer);
            int totalCount = mHandActorBuffer.Count + 1;
            layoutSolver.BuildLayout(totalCount, mHandLayoutBuffer);

            if (mHandLayoutBuffer.Count == 0)
            {
                yield break;
            }

            HandCardLayoutTarget acquiredTarget = mHandLayoutBuffer[mHandLayoutBuffer.Count - 1];
            var existingTargets = new List<HandCardLayoutTarget>(Mathf.Max(0, mHandLayoutBuffer.Count - 1));
            for (var i = 0; i < mHandLayoutBuffer.Count - 1; i++)
            {
                existingTargets.Add(mHandLayoutBuffer[i]);
            }

            bool completed = false;
            cardAcquisitionPerformance.Play(
                acquiredCard,
                acquiredTarget.LocalPosition,
                acquiredTarget.SortingOrder,
                mHandActorBuffer,
                existingTargets,
                viewRegistry.HandRoot,
                () => completed = true);

            yield return WaitUntilOrTimeout(
                () => completed || !cardAcquisitionPerformance.IsPlaying,
                cardAcquisitionPerformance.TotalDuration + 0.25f);

            viewRegistry.RegisterActor(evt.CardUid, acquiredCard, ViewActorZone.Hand);
            viewRegistry.AppendHandCard(evt.CardUid);
        }

        private IEnumerator PlayUseItem(CoreGameEvent evt)
        {
            bool completed = false;
            itemUsePerformance.Play(evt.CardUid, () => completed = true);
            yield return WaitUntilOrTimeout(
                () => completed || !itemUsePerformance.IsPlaying,
                itemUsePerformance.TotalDuration + 0.01f);
        }

        private void EnsureReferences()
        {
            if (viewRegistry == null)
            {
                viewRegistry = GetComponentInParent<TableNineViewRegistry>();
            }

            if (cardAcquisitionPerformance == null)
            {
                cardAcquisitionPerformance = GetComponent<CardAcquisitionPerformance>();
            }

            if (itemUsePerformance == null)
            {
                itemUsePerformance = GetComponent<ItemUsePerformance>();
                if (itemUsePerformance == null)
                {
                    itemUsePerformance = gameObject.AddComponent<ItemUsePerformance>();
                }
            }
        }

        private static IEnumerator WaitUntilOrTimeout(Func<bool> condition, float timeoutSeconds, string performanceName = "Unknown")
        {
            return AdaptorPlaybackTrace.WaitUntilOrTimeout(
                nameof(TableNineItemAdaptor),
                performanceName,
                condition,
                timeoutSeconds);
        }
    }
}

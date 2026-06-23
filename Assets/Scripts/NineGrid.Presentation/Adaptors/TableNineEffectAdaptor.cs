using System;
using System.Collections;
using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Presentation.Performance;
using NineGrid.Presentation.Registry;
using UnityEngine;

namespace NineGrid.Presentation.Adaptors
{
    /// <summary>
    /// 效果适配器：认领 TriggerEffect / ApplyModifier，调度效果触发与修正应用表演黑盒。
    /// 当前为占位壳，表演器 0s 直通；后续补全动效与演员解析。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TableNineEffectAdaptor : MonoBehaviour
    {
        private static readonly HashSet<PresentationInstructionKind> HandledKinds = new()
        {
            PresentationInstructionKind.TriggerEffect,
            PresentationInstructionKind.ApplyModifier,
        };

        [Header("Registry")]
        [SerializeField] private TableNineViewRegistry viewRegistry;

        [Header("Performances")]
        [SerializeField] private EffectTriggerPerformance effectTriggerPerformance;
        [SerializeField] private ModifierApplyPerformance modifierApplyPerformance;

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
                case PresentationInstructionKind.TriggerEffect:
                    yield return PlayTriggerEffect(evt);
                    break;
                case PresentationInstructionKind.ApplyModifier:
                    yield return PlayApplyModifier(evt);
                    break;
            }
        }

        private IEnumerator PlayTriggerEffect(CoreGameEvent evt)
        {
            bool completed = false;
            effectTriggerPerformance.Play(
                evt.CardUid,
                evt.Message,
                evt.SourceDefId,
                evt.Cause,
                () => completed = true);

            yield return WaitUntilOrTimeout(
                () => completed || !effectTriggerPerformance.IsPlaying,
                effectTriggerPerformance.TotalDuration + 0.01f);
        }

        private IEnumerator PlayApplyModifier(CoreGameEvent evt)
        {
            bool completed = false;
            modifierApplyPerformance.Play(
                evt.CardUid,
                evt.Amount,
                evt.Delta,
                evt.Message,
                () => completed = true);

            yield return WaitUntilOrTimeout(
                () => completed || !modifierApplyPerformance.IsPlaying,
                modifierApplyPerformance.TotalDuration + 0.01f);
        }

        private void EnsureReferences()
        {
            if (viewRegistry == null)
            {
                viewRegistry = GetComponentInParent<TableNineViewRegistry>();
            }

            if (effectTriggerPerformance == null)
            {
                effectTriggerPerformance = GetComponent<EffectTriggerPerformance>();
                if (effectTriggerPerformance == null)
                {
                    effectTriggerPerformance = gameObject.AddComponent<EffectTriggerPerformance>();
                }
            }

            if (modifierApplyPerformance == null)
            {
                modifierApplyPerformance = GetComponent<ModifierApplyPerformance>();
                if (modifierApplyPerformance == null)
                {
                    modifierApplyPerformance = gameObject.AddComponent<ModifierApplyPerformance>();
                }
            }
        }

        private static IEnumerator WaitUntilOrTimeout(Func<bool> condition, float timeoutSeconds)
        {
            float elapsed = 0f;
            while (!condition() && elapsed < timeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }
}

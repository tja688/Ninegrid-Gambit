using System;
using System.Collections;
using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Presentation.Performance;
using NineGrid.Presentation.Registry;
using NineGrid.Presentation.Visuals;
using UnityEngine;

namespace NineGrid.Presentation.Adaptors
{
  /// <summary>
  /// 玩家状态适配器：认领 ShowDamage / UpdateHp / UpdateArmor / UpdateGold / UpdateInteractionCount，
  /// 调度飘字与 HUD 面板更新表演黑盒。
  /// </summary>
  [DisallowMultipleComponent]
  public sealed class TableNineStatusAdaptor : MonoBehaviour
  {
    private static readonly HashSet<PresentationInstructionKind> HandledKinds = new()
    {
      PresentationInstructionKind.ShowDamage,
      PresentationInstructionKind.UpdateHp,
      PresentationInstructionKind.UpdateArmor,
      PresentationInstructionKind.UpdateGold,
      PresentationInstructionKind.UpdateInteractionCount,
    };

    [Header("Registry")]
    [SerializeField] private TableNineViewRegistry viewRegistry;

    [Header("Performances")]
    [SerializeField] private DamagePopupPerformance damagePopupPerformance;
    [SerializeField] private StatusPanelUpdatePerformance statusPanelUpdatePerformance;

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
        case PresentationInstructionKind.ShowDamage:
          PlayDamagePopup(evt, snapshot);
          yield break;
        case PresentationInstructionKind.UpdateHp:
          yield return PlayUpdateHp(evt, snapshot);
          break;
        case PresentationInstructionKind.UpdateArmor:
          yield return PlayUpdateArmor(evt, snapshot);
          break;
        case PresentationInstructionKind.UpdateGold:
          yield return PlayUpdateGold(evt, snapshot);
          break;
        case PresentationInstructionKind.UpdateInteractionCount:
          yield return PlayUpdateInteractionCount(evt, snapshot);
          break;
      }
    }

    public void AlignFromSnapshot(CoreViewSnapshot snapshot)
    {
      EnsureReferences();
      statusPanelUpdatePerformance.AlignFromSnapshot(snapshot);
    }

    private IEnumerator PlayUpdateHp(CoreGameEvent evt, CoreViewSnapshot snapshot)
    {
      if (evt.Type == CoreEventType.Healed && evt.Delta > 0)
      {
        PlayHealPopup(evt, snapshot);
      }

      yield return PlayPanelUpdate(evt, snapshot);
    }

    private IEnumerator PlayUpdateArmor(CoreGameEvent evt, CoreViewSnapshot snapshot)
    {
      yield return PlayPanelUpdate(evt, snapshot);
    }

    private IEnumerator PlayUpdateGold(CoreGameEvent evt, CoreViewSnapshot snapshot)
    {
      if (evt.Delta != 0)
      {
        PlayGoldPopup(evt, snapshot);
      }

      yield return PlayPanelUpdate(evt, snapshot);
    }

    private IEnumerator PlayUpdateInteractionCount(CoreGameEvent evt, CoreViewSnapshot snapshot)
    {
      yield return PlayPanelUpdate(evt, snapshot);
    }

    private IEnumerator PlayPanelUpdate(CoreGameEvent evt, CoreViewSnapshot snapshot)
    {
      bool completed = false;
      statusPanelUpdatePerformance.Play(evt, snapshot, () => completed = true);
      yield return WaitUntilOrTimeout(
        () => completed || !statusPanelUpdatePerformance.IsPlaying,
        statusPanelUpdatePerformance.TotalDuration + 0.01f);
    }

    private void PlayDamagePopup(CoreGameEvent evt, CoreViewSnapshot snapshot)
    {
      Transform target = ResolveTargetTransform(evt, snapshot);
      if (target == null)
      {
        return;
      }

      float amount = evt.Delta > 0 ? evt.Delta : evt.Amount;
      damagePopupPerformance.Play(target, amount, DamagePopupKind.Damage);
    }

    private void PlayHealPopup(CoreGameEvent evt, CoreViewSnapshot snapshot)
    {
      Transform target = ResolveTargetTransform(evt, snapshot);
      if (target == null)
      {
        return;
      }

      damagePopupPerformance.Play(target, evt.Delta, DamagePopupKind.Heal);
    }

    private void PlayGoldPopup(CoreGameEvent evt, CoreViewSnapshot snapshot)
    {
      Transform target = ResolveAvatarTransform(snapshot);
      if (target == null)
      {
        return;
      }

      float amount = Mathf.Abs(evt.Delta);
      damagePopupPerformance.Play(target, amount, DamagePopupKind.Gold);
    }

    private Transform ResolveTargetTransform(CoreGameEvent evt, CoreViewSnapshot snapshot)
    {
      int targetUid = evt.TargetUid > 0 ? evt.TargetUid : evt.CardUid;
      Transform actor;
      if (viewRegistry != null && viewRegistry.TryGetActor(targetUid, out actor) && actor != null)
      {
        return actor;
      }

      if (snapshot != null && targetUid == snapshot.AvatarUid && viewRegistry != null)
      {
        Transform anchor;
        if (viewRegistry.TryGetSlotAnchor(snapshot.AvatarSlot, out anchor) && anchor != null)
        {
          return anchor;
        }
      }

      return null;
    }

    private Transform ResolveAvatarTransform(CoreViewSnapshot snapshot)
    {
      if (snapshot == null || viewRegistry == null)
      {
        return null;
      }

      Transform actor;
      if (viewRegistry.TryGetAvatarActor(snapshot, out actor) && actor != null)
      {
        return actor;
      }

      Transform anchor;
      if (viewRegistry.TryGetSlotAnchor(snapshot.AvatarSlot, out anchor) && anchor != null)
      {
        return anchor;
      }

      return null;
    }

    private void EnsureReferences()
    {
      if (viewRegistry == null)
      {
        viewRegistry = GetComponentInParent<TableNineViewRegistry>();
      }

      if (damagePopupPerformance == null)
      {
        damagePopupPerformance = GetComponent<DamagePopupPerformance>();
        if (damagePopupPerformance == null)
        {
          damagePopupPerformance = gameObject.AddComponent<DamagePopupPerformance>();
        }
      }

      if (statusPanelUpdatePerformance == null)
      {
        statusPanelUpdatePerformance = GetComponent<StatusPanelUpdatePerformance>();
        if (statusPanelUpdatePerformance == null)
        {
          statusPanelUpdatePerformance = gameObject.AddComponent<StatusPanelUpdatePerformance>();
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

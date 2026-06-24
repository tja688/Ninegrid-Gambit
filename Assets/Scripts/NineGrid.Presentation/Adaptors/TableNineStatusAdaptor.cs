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
  /// 玩家状态适配器：认领 ShowDamage / UpdateHp / UpdateArmor / UpdateGold / UpdateInteractionCount / ModifyBaseStat，
  /// 调度飘字、HUD 与场地 Card 状态面板表演黑盒。
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
      PresentationInstructionKind.ModifyBaseStat,
    };

    [Header("Registry")]
    [SerializeField] private TableNineViewRegistry viewRegistry;

    [Header("HUD")]
    [SerializeField] private TableNineStatusPanelView panelView;

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
          if (evt.Type == CoreEventType.Healed && evt.Delta > 0)
          {
            PlayHealPopup(evt, snapshot);
          }

          ApplyHudEvent(evt, snapshot);
          yield return PlayCardStatusUpdate(evt, snapshot);
          break;
        case PresentationInstructionKind.UpdateArmor:
          ApplyHudEvent(evt, snapshot);
          yield return PlayCardStatusUpdate(evt, snapshot);
          break;
        case PresentationInstructionKind.UpdateGold:
          if (evt.Delta != 0)
          {
            PlayGoldPopup(evt, snapshot);
          }

          ApplyHudEvent(evt, snapshot);
          yield break;
        case PresentationInstructionKind.UpdateInteractionCount:
          ApplyHudEvent(evt, snapshot);
          yield break;
        case PresentationInstructionKind.ModifyBaseStat:
          yield return PlayCardStatusUpdate(evt, snapshot);
          break;
      }
    }

    public void AlignFromSnapshot(CoreViewSnapshot snapshot)
    {
      EnsureReferences();
      if (panelView != null)
      {
        panelView.ApplySnapshot(snapshot);
      }

      AlignBoardCardStatuses(snapshot);
    }

    private void ApplyHudEvent(CoreGameEvent evt, CoreViewSnapshot snapshot)
    {
      if (panelView == null)
      {
        return;
      }

      panelView.ApplyEvent(evt, snapshot);
    }

    private IEnumerator PlayCardStatusUpdate(CoreGameEvent evt, CoreViewSnapshot snapshot)
    {
      if (!ShouldAnimateCardStatus(evt, snapshot))
      {
        yield break;
      }

      Transform cardActor = ResolveCardActor(evt.CardUid);
      if (cardActor == null)
      {
        yield break;
      }

      bool completed = false;
      statusPanelUpdatePerformance.Play(cardActor, evt, snapshot, () => completed = true);
      yield return WaitUntilOrTimeout(
        () => completed || !statusPanelUpdatePerformance.IsPlaying,
        statusPanelUpdatePerformance.TotalDuration + 0.05f);
    }

    private void AlignBoardCardStatuses(CoreViewSnapshot snapshot)
    {
      if (snapshot == null || viewRegistry == null || statusPanelUpdatePerformance == null)
      {
        return;
      }

      for (var i = 0; i < snapshot.BoardSlots.Count; i++)
      {
        BoardSlotView slot = snapshot.BoardSlots[i];
        if (slot.CardUid <= 0)
        {
          continue;
        }

        Transform actor;
        if (!viewRegistry.TryGetActor(slot.CardUid, out actor) || actor == null)
        {
          continue;
        }

        statusPanelUpdatePerformance.AlignCard(actor, slot, instant: true);
      }
    }

    private static bool ShouldAnimateCardStatus(CoreGameEvent evt, CoreViewSnapshot snapshot)
    {
      if (evt == null || snapshot == null || evt.CardUid <= 0)
      {
        return false;
      }

      if (evt.CardUid == snapshot.AvatarUid)
      {
        return false;
      }

      for (var i = 0; i < snapshot.BoardSlots.Count; i++)
      {
        if (snapshot.BoardSlots[i].CardUid == evt.CardUid)
        {
          return true;
        }
      }

      return false;
    }

    private Transform ResolveCardActor(int cardUid)
    {
      if (viewRegistry == null || cardUid <= 0)
      {
        return null;
      }

      Transform actor;
      return viewRegistry.TryGetActor(cardUid, out actor) ? actor : null;
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

      if (panelView == null)
      {
        panelView = GetComponentInChildren<TableNineStatusPanelView>(true);
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
